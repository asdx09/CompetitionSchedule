"""End-to-end API teszt a PHP backendhez (valódi PostgreSQL, valódi SMTP STARTTLS, mock solver).

Futtatás: lásd tests/README.md. A teszt a C# backend viselkedését (válaszkódok,
JSON formátum, hibaüzenetek) és a PHP-ban elvégzett biztonsági javításokat ellenőrzi.
"""
import base64, hashlib, email, glob, io, json, os, re, subprocess, sys, time, uuid
from email import policy
import requests, openpyxl

HERE = os.path.dirname(os.path.abspath(__file__))
BASE = os.environ.get('SL_BASE', 'http://127.0.0.1:7099')          # a PHP backend (közvetlenül, Caddy nélkül)
SOLVER = os.environ.get('SL_MOCK_SOLVER', 'http://127.0.0.1:8000')  # mock_solver.py
MAILDIR = os.environ.get('SL_MAILDIR', os.path.join(HERE, '_mail'))  # test_smtpd.py ide menti a leveleket
FRONTEND = os.environ.get('SL_FRONTEND', 'https://localhost:58921/')  # URLS:Frontend
PUBLIC_URL = os.environ.get('SL_PUBLIC_URL', 'https://localhost:58921')  # AppSettings:PublicUrl
ADMIN_USER = os.environ.get('SL_ADMIN_USER', 'admin')
ADMIN_PASSWORD = os.environ.get('SL_ADMIN_PASSWORD', 'AdminPass123!')
FAILS = []
PASSES = 0


def check(cond, msg):
    global PASSES
    if cond:
        PASSES += 1
    else:
        FAILS.append(msg)
        print('  FAIL:', msg)


def psql(sql):
    # a PGHOST / PGUSER / PGPASSWORD / PGDATABASE környezeti változókat használja
    out = subprocess.run(['psql', '-tA', '-c', sql], capture_output=True, text=True)
    if out.returncode:
        raise RuntimeError(out.stderr)
    return out.stdout.strip()


class Client:
    """Kézi cookie-kezelés (a Secure cookie-kat http-n is visszaküldi, mint ha https lenne)."""
    def __init__(self):
        self.cookies = {}
        self.last_set_cookies = []

    def req(self, method, path, **kw):
        headers = kw.pop('headers', {})
        if self.cookies:
            headers['Cookie'] = '; '.join(f'{k}={v}' for k, v in self.cookies.items())
        r = requests.request(method, BASE + path, headers=headers, allow_redirects=False, **kw)
        self.last_set_cookies = r.raw.headers.getlist('Set-Cookie') if hasattr(r.raw.headers, 'getlist') else []
        for sc in self.last_set_cookies:
            name, _, rest = sc.partition('=')
            value = rest.split(';')[0]
            if 'expires=Thu, 01 Jan 1970' in sc or value == '':
                self.cookies.pop(name, None)
            else:
                self.cookies[name] = value
        return r


def mails_to(addr):
    out = []
    for f in sorted(glob.glob(MAILDIR + '/*.eml')):
        msg = email.message_from_bytes(open(f, 'rb').read(), policy=policy.default)
        if addr in msg['To']:
            meta = json.load(open(f + '.json'))
            out.append((msg, meta))
    return out


def body_of(msg):
    return msg.get_body().get_content()


uid = uuid.uuid4().hex[:6]
USER, EMAIL, PW = f'Tester_{uid}', f'tester_{uid}@example.com', 'Secret123!'

print('== Regisztráció / validáció')
c = Client()
r = c.req('POST', '/api/Authentication/register', json={'Username': '', 'Password': 'x', 'Email': ''})
check(r.status_code == 400 and r.headers['content-type'].startswith('application/problem+json')
      and 'Username' in r.json()['errors'] and 'Email' in r.json()['errors'], f'empty register -> validation problem: {r.status_code} {r.text}')
r = c.req('POST', '/api/Authentication/register', json={'Username': USER, 'Password': 'short', 'Email': EMAIL})
check(r.status_code == 400 and r.text == 'Password must be at least 8 characters.', f'short pw: {r.status_code} {r.text}')
r = c.req('POST', '/api/Authentication/register', json={'Username': USER, 'Password': PW, 'Email': 'not-an-email'})
check(r.status_code == 400 and r.text == 'Invalid email address!', f'bad email: {r.text}')
r = c.req('POST', '/api/Authentication/register', json={'Username': USER, 'Password': PW, 'Email': EMAIL})
check(r.status_code == 200 and r.json() == {'emailSent': True}, f'register ok: {r.status_code} {r.text}')
r = c.req('POST', '/api/Authentication/register', json={'username': USER.upper(), 'password': PW, 'email': 'x' + EMAIL})
check(r.status_code == 400 and r.text == 'Username already taken!', f'dup username (case-insens, lowercase keys): {r.text}')
r = c.req('POST', '/api/Authentication/register', json={'Username': USER + 'x', 'Password': PW, 'Email': EMAIL.upper()})
check(r.status_code == 400 and r.text == 'Email already taken!', f'dup email: {r.text}')
r = c.req('POST', '/api/Authentication/register', data='notjson', headers={'Content-Type': 'text/plain'})
check(r.status_code == 415, f'non-json -> 415: {r.status_code}')

stored = psql(f"select password, validated, registrationdate from users where username='{USER}'").split('|')
raw = base64.b64decode(stored[0])
check(raw[0] == 1 and int.from_bytes(raw[1:5], 'big') == 2 and int.from_bytes(raw[5:9], 'big') == 100000
      and int.from_bytes(raw[9:13], 'big') == 16 and len(raw) == 13 + 16 + 32, 'hash is Identity V3 SHA512/100k/16/32')
# független Python ellenőrzés: a PHP által készített hash a .NET algoritmusával ellenőrizhető
check(hashlib.pbkdf2_hmac('sha512', PW.encode(), raw[13:29], 100000, 32) == raw[29:], 'hash verifies with independent PBKDF2')
check(re.fullmatch(r'[0-9A-F]{64}', stored[1]) is not None, f'validation token format {stored[1]}')

mails = mails_to(EMAIL)
check(len(mails) == 1, f'1 confirmation mail, got {len(mails)}')
msg, meta = mails[-1]
check(meta['tls'] is True and meta['auth'] == 'True', f'mail via STARTTLS+AUTH {meta}')
check(msg['Subject'] == 'Email confirmation required - ScheduleLogic', f"subject {msg['Subject']}")
html = body_of(msg)
m = re.search(r'href="([^"]+)"', html)
link = m.group(1).replace('&amp;', '&')
check(link.startswith(PUBLIC_URL + '/api/authentication/confirm?token='), f'confirm link {link}')
token = link.split('token=')[1]

print('== Belépés megerősítés előtt / megerősítés')
r = c.req('POST', '/api/Authentication/login', json={'Username': USER, 'Password': PW})
check(r.status_code == 401 and r.text == 'Invalid username/email and password or not verified email!', f'login unconfirmed {r.status_code} {r.text}')
r = c.req('POST', '/api/Authentication/resend-confirmation', json={'UsernameOrEmail': USER})
check(r.status_code == 200 and r.text == '', 'resend ok')
check(len(mails_to(EMAIL)) == 2 and token in body_of(mails_to(EMAIL)[-1][0]), 'resend uses same token')
r = c.req('POST', '/api/Authentication/resend-confirmation', json={'UsernameOrEmail': 'nobody-here'})
check(r.status_code == 200, 'resend unknown still 200')
r = c.req('GET', '/api/authentication/confirm?token=WRONG')
check(r.status_code == 302 and r.headers['Location'] == FRONTEND + '?confirm=failed', f'bad confirm {r.status_code} {r.headers.get("Location")}')
r = c.req('GET', '/api/Authentication/confirm')
check(r.status_code == 400 and r.text == 'Invalid confirmation request.', 'confirm without token')
r = c.req('GET', '/api/authentication/confirm?token=' + token)
check(r.status_code == 302 and r.headers['Location'] == FRONTEND, f'confirm ok {r.headers.get("Location")}')
check(psql(f"select validated from users where username='{USER}'") == '', 'validated cleared')

print('== Belépés')
r = c.req('POST', '/api/Authentication/login', json={'Username': USER, 'Password': 'wrong-pass'})
check(r.status_code == 401, 'wrong pw 401')
r = c.req('POST', '/api/Authentication/login', json={'Username': '', 'Password': ''})
check(r.status_code == 400 and 'errors' in r.json(), 'empty login -> validation 400')
r = c.req('POST', '/api/Authentication/login', json={'Username': EMAIL.upper(), 'Password': PW})
check(r.status_code == 200 and r.text == '', f'login by email (case-insens) {r.status_code} {r.text}')
sc = [s for s in c.last_set_cookies if s.startswith('sl_auth=')]
check(len(sc) == 1 and 'httponly' in sc[0].lower() and 'samesite=lax' in sc[0].lower() and 'expires=' in sc[0].lower() and 'path=/' in sc[0], f'cookie attrs {sc}')
r = c.req('GET', '/api/Authentication/check-token')
check(r.status_code == 200 and r.json() == {'name': USER}, f'check-token {r.text}')

# Régi (.NET Core 3-6 féle SHA256/10k) hash -> sikeres belépés + automatikus frissítés
# .NET Core 3-6 formátum: V3, HMAC-SHA256, 10 000 iteráció - független Python implementációval
import struct
_salt = os.urandom(16)
legacy_hash = base64.b64encode(b'\x01' + struct.pack('>III', 1, 10000, 16) + _salt
                               + hashlib.pbkdf2_hmac('sha256', b'OldUser123!', _salt, 10000, 32)).decode()
legacy_user = f'legacy_{uid}'
psql(f"insert into users(username,email,password,validated) values ('{legacy_user}','{legacy_user}@example.com','{legacy_hash}','')")
lc = Client()
r = lc.req('POST', '/api/Authentication/login', json={'Username': legacy_user, 'Password': 'OldUser123!'})
check(r.status_code == 200, f'legacy SHA256 hash login {r.status_code}')
newhash = base64.b64decode(psql(f"select password from users where username='{legacy_user}'"))
check(int.from_bytes(newhash[1:5], 'big') == 2 and int.from_bytes(newhash[5:9], 'big') == 100000, 'legacy hash rehashed to SHA512/100k')
r = lc.req('POST', '/api/Authentication/login', json={'Username': legacy_user, 'Password': 'OldUser123!'})
check(r.status_code == 200, 'login again after rehash')

print('== Események')
r = c.req('GET', '/api/Event/get-events')
check(r.status_code == 200 and r.json() == [], f'no events {r.text}')
r = c.req('POST', '/api/Event/new-event')
check(r.status_code == 200 and r.text == '', 'new event')
events = c.req('GET', '/api/Event/get-events').json()
check(len(events) == 1 and events[0]['eventName'] == 'New Event' and isinstance(events[0]['eventId'], int), f'events list {events}')
eid = events[0]['eventId']
ev = c.req('GET', f'/api/Event/get-event?id={eid}').json()
ed = ev['eventData']
check(ed['eventId'] == str(eid) and ed['isPrivate'] is True and ed['basePauseTime'] == 5 and ed['locWeight'] == 1, f'event defaults {ed}')
check(re.fullmatch(r'\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(\.\d+)?Z', ed['startDate']) is not None, f'date format {ed["startDate"]}')
check(set(ev.keys()) == {'eventData', 'eventTypes', 'groups', 'locations', 'participants', 'registrations', 'pauseTable', 'locationTable', 'constraints', 'timeZones'}, f'keys {ev.keys()}')

# A frontend által küldött formátum: ideiglenes UUID azonosítók, eventId "0", "HH:mm" és "HH:mm:ss" idők
T1, T2, G1, L1, L2, P1, P2, P3 = (str(uuid.uuid4()) for _ in range(8))
payload = {
    'eventData': {'eventId': str(eid), 'eventName': 'Úszóverseny 2026', 'startDate': '2026-03-01T07:00:00.000Z',
                  'endDate': '2026-03-02T16:00:00.000Z', 'isPrivate': False, 'basePauseTime': 10, 'locationPauseTime': 7,
                  'locWeight': 3, 'groupWeight': 2, 'typeWeight': 1, 'compWeight': 450},
    'eventTypes': [{'eventTypeId': T1, 'eventId': '0', 'typeName': '100m gyors', 'timeRange': '00:05:00'},
                   {'eventTypeId': T2, 'eventId': '0', 'typeName': '200m hát', 'timeRange': '01:30'}],
    'groups': [{'groupId': G1, 'eventId': '0', 'groupName': 'Junior'}],
    'locations': [{'locationId': L1, 'eventId': '0', 'locationName': 'Medence A', 'slots': 2},
                  {'locationId': L2, 'eventId': '0', 'locationName': 'Medence B', 'slots': 1}],
    'participants': [{'participantId': P1, 'eventId': '0', 'competitorNumber': 1, 'participantName': 'Kiss Anna', 'groupId': G1},
                     {'participantId': P2, 'eventId': '0', 'competitorNumber': 2, 'participantName': 'Nagy Béla', 'groupId': ''},
                     {'participantId': P3, 'eventId': '0', 'competitorNumber': '3', 'participantName': 'Tóth Cili', 'groupId': None}],
    'registrations': [{'registrationId': 'r1', 'eventId': '0', 'participantId': P1, 'eventTypeId': T1},
                      {'registrationId': 'r2', 'eventId': '0', 'participantId': P2, 'eventTypeId': T1},
                      {'registrationId': 'r3', 'eventId': '0', 'participantId': P3, 'eventTypeId': T2}],
    'pauseTable': [{'pauseId': 'p1', 'eventId': '0', 'locationId1': L1, 'locationId2': L2, 'pause': '00:10'}],
    'locationTable': [{'locationTableId': '1', 'eventId': '0', 'eventTypeId': T1, 'locationId': L1},
                      {'locationTableId': '2', 'eventId': '0', 'eventTypeId': T2, 'locationId': L2}],
    'constraints': [{'constraintId': 'c1', 'eventId': '0', 'objectId': L2, 'constraintType': 'L',
                     'startTime': '2026-03-01T11:00:00.000Z', 'endTime': '2026-03-01T12:30:00.000Z'},
                    {'constraintId': 'c2', 'eventId': '0', 'objectId': G1, 'constraintType': 'G',
                     'startTime': '2026-03-01T08:00:00', 'endTime': '2026-03-01T09:00:00+01:00'}],
    'timeZones': [],
}
r = c.req('POST', '/api/Event/save-event', json=payload)
check(r.status_code == 200, f'save-event {r.status_code} {r.text}')
ev = c.req('GET', f'/api/Event/get-event?id={eid}').json()
check(ev['eventData']['eventName'] == 'Úszóverseny 2026' and ev['eventData']['compWeight'] == 450, f'saved eventdata {ev["eventData"]}')
check(ev['eventData']['startDate'] == '2026-03-01T07:00:00Z', f'startDate roundtrip {ev["eventData"]["startDate"]}')
check([t['timeRange'] for t in ev['eventTypes']] == ['00:05:00', '01:30:00'], f'timeRange {ev["eventTypes"]}')
check(ev['pauseTable'][0]['pause'] == '00:10:00', 'pause normalized')
db_group = ev['groups'][0]['groupId']
parts = {p['participantName']: p for p in ev['participants']}
check(parts['Kiss Anna']['groupId'] == db_group, 'participant group mapped to new id')
check(parts['Nagy Béla']['groupId'] is None and parts['Tóth Cili']['groupId'] is None, f'no group -> null {parts}')
check(parts['Tóth Cili']['competitorNumber'] == 3, 'number from string accepted')
locs = {l['locationName']: l['locationId'] for l in ev['locations']}
cons = {c_['constraintType']: c_ for c_ in ev['constraints']}
check(cons['L']['objectId'] == locs['Medence B'], 'constraint L mapped')
check(cons['G']['objectId'] == db_group, 'constraint G mapped')
# helyi idő (Europe/Budapest, télen +01:00): "08:00:00" zóna nélkül = 07:00Z
check(cons['G']['startTime'] == '2026-03-01T07:00:00Z' and cons['G']['endTime'] == '2026-03-01T08:00:00Z', f'local time conversion {cons["G"]}')
check(all(isinstance(x['eventId'], str) for x in ev['registrations']), 'ids are strings')

# Hibás hivatkozás -> 400 és változatlan adatok (rollback)
bad = json.loads(json.dumps(payload))
bad['eventData']['eventName'] = 'SHOULD NOT SAVE'
bad['registrations'].append({'registrationId': 'rx', 'eventId': '0', 'participantId': 'nope', 'eventTypeId': T1})
r = c.req('POST', '/api/Event/save-event', json=bad)
check(r.status_code == 400 and r.json()['status'] == 400, f'bad ref -> 400 {r.status_code}')
ev2 = c.req('GET', f'/api/Event/get-event?id={eid}').json()
check(ev2['eventData']['eventName'] == 'Úszóverseny 2026' and len(ev2['registrations']) == 3, 'rollback kept old data')
bad = json.loads(json.dumps(payload)); bad['locations'][0]['locationName'] = ''
r = c.req('POST', '/api/Event/save-event', json=bad)
check(r.status_code == 400 and 'Locations[0].LocationName' in r.json().get('errors', {}), f'empty name -> validation {r.text[:200]}')
bad = json.loads(json.dumps(payload)); bad['eventData']['startDate'] = 'garbage'
r = c.req('POST', '/api/Event/save-event', json=bad)
check(r.status_code == 400 and 'EventData.StartDate' in r.json().get('errors', {}), 'bad date -> validation')

# Más felhasználó nem fér hozzá
c2 = Client()
u2 = f'other_{uid}'
c2.req('POST', '/api/Authentication/register', json={'Username': u2, 'Password': PW, 'Email': u2 + '@example.com'})
psql(f"update users set validated='' where username='{u2}'")
c2.req('POST', '/api/Authentication/login', json={'Username': u2, 'Password': PW})
check(c2.req('GET', f'/api/Event/get-event?id={eid}').status_code == 404, 'foreign get-event 404')
check(c2.req('DELETE', f'/api/Event/delete-event?id={eid}').status_code == 404, 'foreign delete 404')
check(c2.req('POST', '/api/Event/save-event', json=payload).status_code == 400, 'foreign save 400')
check(c2.req('GET', f'/api/Schedule/{eid}').status_code == 404, 'foreign schedule 404')
check(c2.req('GET', f'/api/Schedule/export?id={eid}').status_code == 404, 'foreign export 404')
check(Client().req('GET', '/api/Event/get-events').status_code == 401, 'anonymous 401')
check(c.req('GET', '/api/Event/get-event?id=abc').status_code == 400, 'non-numeric id 400')

print('== Varázsló')
wiz = json.loads(json.dumps(payload)); wiz['eventData']['eventId'] = '0'; wiz['eventData']['eventName'] = 'Wizard event'
r = c.req('POST', '/api/Event/new-wizard', json=wiz)
check(r.status_code == 200, f'wizard ok {r.status_code} {r.text}')
evs = c.req('GET', '/api/Event/get-events').json()
check(len(evs) == 2 and evs[1]['eventName'] == 'Wizard event', f'wizard event listed {evs}')
wiz_ev = c.req('GET', f'/api/Event/get-event?id={evs[1]["eventId"]}').json()
check(wiz_ev['eventData']['locWeight'] == 1 and len(wiz_ev['registrations']) == 3, 'wizard: weights default, children inserted')
badwiz = json.loads(json.dumps(wiz)); badwiz['constraints'][0]['objectId'] = 'missing'
r = c.req('POST', '/api/Event/new-wizard', json=badwiz)
check(r.status_code == 400 and len(c.req('GET', '/api/Event/get-events').json()) == 2, 'failed wizard leaves no orphan event')

print('== Ütemezés / solver')
r = c.req('POST', f'/api/Schedule/{eid}')
check(r.status_code == 200 and r.json()['returnURL'] == '', f'generate -> returnURL hidden {r.status_code}')
sreq = requests.get(SOLVER + '/_last').json()
exp_keys = {'returnURL', 'eventId', 'locations', 'events', 'competitors', 'entries', 'travel', 'constraints', 'dayLength',
            'maxDays', 'breakTimeLoc', 'basePauseTime', 'locWeight', 'groupWeight', 'typeWeight', 'compWeight'}
check(set(sreq.keys()) == exp_keys, f'solver request keys {set(sreq.keys()) ^ exp_keys}')
check('/api/Schedule/answer?key=' in sreq['returnURL'], f'returnURL {sreq["returnURL"]}')
check(sreq['eventId'] == eid and sreq['dayLength'] == 1439 and sreq['maxDays'] == 2 and sreq['compWeight'] == 450
      and sreq['breakTimeLoc'] == 7 and sreq['basePauseTime'] == 10, f'solver scalars {sreq}')
ev = c.req('GET', f'/api/Event/get-event?id={eid}').json()
tids = {t['typeName']: int(t['eventTypeId']) for t in ev['eventTypes']}
lids = {l['locationName']: int(l['locationId']) for l in ev['locations']}
pids = {p['participantName']: int(p['participantId']) for p in ev['participants']}
gid = int(ev['groups'][0]['groupId'])
check(sorted(sreq['events'], key=lambda e: e['id']) == sorted([
    {'id': tids['100m gyors'], 'name': '100m gyors', 'duration': 5, 'possibleLocations': [lids['Medence A']]},
    {'id': tids['200m hát'], 'name': '200m hát', 'duration': 90, 'possibleLocations': [lids['Medence B']]}], key=lambda e: e['id']), f'events {sreq["events"]}')
check({(x['name'], x['groupId']) for x in sreq['competitors']} == {('Kiss Anna', gid), ('Nagy Béla', -1), ('Tóth Cili', -1)}, 'competitors groupId -1')
check(sreq['travel'] == [{'id': sreq['travel'][0]['id'], 'locationID1': lids['Medence A'], 'locationID2': lids['Medence B'], 'pause': 10}], f'travel {sreq["travel"]}')
cL = [x for x in sreq['constraints'] if x['constraintType'] == 'L'][0]
check(cL['startTime'] == 240 and cL['endTime'] == 330 and cL['objectId'] == lids['Medence B'], f'constraint minutes {cL}')
check(sorted((e['competitorId'], e['eventId']) for e in sreq['entries']) ==
      sorted([(pids['Kiss Anna'], tids['100m gyors']), (pids['Nagy Béla'], tids['100m gyors']), (pids['Tóth Cili'], tids['200m hát'])]), 'entries')

r = c.req('GET', f'/api/Schedule/isRunning?id={eid}')
check(r.status_code == 200 and r.json() is True, f'isRunning true {r.text}')
r = c.req('POST', f'/api/Schedule/stop?id={eid}')
check(r.status_code == 200 and r.json() is True, f'stop -> true (fixed case-insensitive) {r.text}')
r = c.req('GET', f'/api/Schedule/isRunning?id={eid}')
check(r.json() is False, 'isRunning false after stop')

answer_url = '/api/Schedule/answer?' + sreq['returnURL'].split('?', 1)[1]
sol = {'Status': 'PARTIAL_SOLUTION', 'EventId': eid, 'SolutionNumber': '3', 'Schedule': [
    {'ParticipantId': pids['Kiss Anna'], 'EventTypeId': tids['100m gyors'], 'Start': 60, 'End': 65, 'LocationId': lids['Medence A'], 'Slot': 0},
    {'ParticipantId': pids['Nagy Béla'], 'EventTypeId': tids['100m gyors'], 'Start': 60, 'End': 65, 'LocationId': lids['Medence A'], 'Slot': 0},
    {'ParticipantId': pids['Tóth Cili'], 'EventTypeId': tids['200m hát'], 'Start': 1500, 'End': 1590, 'LocationId': lids['Medence B'], 'Slot': 0},
]}
anon = Client()
check(anon.req('POST', '/api/Schedule/answer', json=sol).status_code == 403, 'answer without key 403')
check(anon.req('POST', '/api/Schedule/answer?key=deadbeef', json=sol).status_code == 403, 'answer wrong key 403')
wrong_event = dict(sol, EventId=evs[1]['eventId'])
check(anon.req('POST', answer_url, json=wrong_event).status_code == 403, 'key bound to event id')
r = anon.req('POST', answer_url, json=sol)
check(r.status_code == 200 and r.text == '', f'answer ok {r.status_code} {r.text}')
# idegen objektum -> 400, régi eredmény marad
foreign = json.loads(json.dumps(sol)); foreign['Schedule'][0]['ParticipantId'] = int(wiz_ev['participants'][0]['participantId'])
check(anon.req('POST', answer_url, json=foreign).status_code == 400, 'foreign ids rejected')
infeasible = {'Status': 'INFEASIBLE', 'EventId': eid, 'Schedule': []}
check(anon.req('POST', answer_url, json=infeasible).status_code == 200, 'infeasible accepted (no-op)')

sd = c.req('GET', f'/api/Schedule/{eid}').json()
tz = sorted(sd['timeZones'], key=lambda t: (t['startTime'], t['participantId']))
check(len(tz) == 3, f'timeZones {sd["timeZones"]}')
# 07:00Z = 08:00 helyi -> +60 perc = 09:00 helyi = 540 perc éjféltől
check([(t['startTime'], t['endTime']) for t in tz] == [(540, 545), (540, 545), (1980, 2070)], f'tz minutes {tz}')
check(sorted(t['slot'] for t in tz[:2]) == [1, 2] and tz[2]['slot'] == 1, f'slot assignment {tz}')
check(all(isinstance(t['scheduleId'], int) for t in tz), 'tz ints')
check([p['groupId'] for p in sd['participants'] if p['participantName'] == 'Nagy Béla'] == [None], 'schedule participants groupId null')

r = c.req('GET', f'/api/Schedule/export?id={eid}')
check(r.status_code == 200 and r.headers['content-type'] == 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
      and 'schedule.xlsx' in r.headers.get('content-disposition', ''), f'export headers {r.headers}')
wb = openpyxl.load_workbook(io.BytesIO(r.content))
ws = wb['Schedule']
rows = list(ws.iter_rows(values_only=True))
check(rows[0] == ('Competitor', 'Group', 'Event Type', 'Location', 'Slot', 'Start', 'End'), f'header {rows[0]}')
check(ws.freeze_panes == 'A2', f'freeze {ws.freeze_panes}')
check(ws['A1'].fill.fgColor.rgb == 'FFADD8E6' and ws['A2'].fill.fgColor.rgb == 'FFADD8E6' and ws['A3'].fill.fgColor.rgb == 'FFE0FFFF', 'fills')
check([r_[0] for r_ in rows[1:]] == ['Kiss Anna (1)', 'Nagy Béla (2)', 'Tóth Cili (3)'], f'sorted by group+name {[r_[0] for r_ in rows[1:]]}')
check(rows[1][1] == 'Junior' and rows[3][1] == '' and rows[1][5] == '2026-03-01 09:00:00' and isinstance(rows[1][4], int), f'row content {rows[1]} {rows[3]}')

print('== CORS')
r = requests.options(BASE + '/api/Event/get-events', headers={'Origin': 'https://localhost:58921', 'Access-Control-Request-Method': 'GET', 'Access-Control-Request-Headers': 'content-type'})
check(r.status_code == 204 and r.headers.get('Access-Control-Allow-Origin') == 'https://localhost:58921'
      and r.headers.get('Access-Control-Allow-Credentials') == 'true', f'preflight {r.status_code} {r.headers}')
r = requests.options(BASE + '/api/Event/get-events', headers={'Origin': 'https://evil.example', 'Access-Control-Request-Method': 'GET'})
check('Access-Control-Allow-Origin' not in r.headers, 'evil origin denied')
r = c.req('GET', '/api/Event/get-events', headers={'Origin': 'https://localhost:58921'})
check(r.headers.get('Access-Control-Allow-Origin') == 'https://localhost:58921', 'simple request CORS')

print('== Elfelejtett jelszó')
r = c.req('POST', '/api/Authentication/forgot-password', json={'UsernameOrEmail': USER})
check(r.status_code == 200, 'forgot ok')
msg = mails_to(EMAIL)[-1][0]
check(msg['Subject'] == 'Password reset - ScheduleLogic', 'reset mail subject')
rlink = re.search(r'href="([^"]+)"', body_of(msg)).group(1).replace('&amp;', '&')
check(rlink.startswith(PUBLIC_URL + '/reset-password?token='), f'reset link {rlink}')
rtoken = rlink.split('token=')[1]
check(c.req('POST', '/api/Authentication/forgot-password', json={'UsernameOrEmail': 'nobody'}).status_code == 200, 'forgot unknown 200')
r = c.req('POST', '/api/Authentication/reset-password', json={'Token': rtoken, 'NewPassword': 'short'})
check(r.status_code == 400 and r.text == 'Password must be at least 8 characters.', 'reset short')
r = c.req('POST', '/api/Authentication/reset-password', json={'Token': 'bad', 'NewPassword': 'NewSecret456!'})
check(r.status_code == 400 and r.text == 'Invalid or expired reset link.', 'reset bad token')
r = c.req('POST', '/api/Authentication/reset-password', json={'Token': rtoken, 'NewPassword': 'NewSecret456!'})
check(r.status_code == 200, f'reset ok {r.status_code} {r.text}')
r = c.req('POST', '/api/Authentication/reset-password', json={'Token': rtoken, 'NewPassword': 'Another789!'})
check(r.status_code == 400, 'reset token single-use')
check(Client().req('POST', '/api/Authentication/login', json={'Username': USER, 'Password': 'NewSecret456!'}).status_code == 200, 'login with new pw')
check(Client().req('POST', '/api/Authentication/login', json={'Username': USER, 'Password': PW}).status_code == 401, 'old pw rejected')
# lejárt token
c.req('POST', '/api/Authentication/forgot-password', json={'UsernameOrEmail': USER})
etok = re.search(r'token=([0-9A-F]+)', body_of(mails_to(EMAIL)[-1][0])).group(1)
psql(f"update users set passwordresetexpires = now() - interval '1 minute' where username='{USER}'")
check(c.req('POST', '/api/Authentication/reset-password', json={'Token': etok, 'NewPassword': 'Expired123!'}).status_code == 400, 'expired token rejected')

print('== Kijelentkezés / fióktörlés')
old_cookie = dict(c.cookies)
r = c.req('POST', '/api/Authentication/logout')
check(r.status_code == 200 and any('sl_auth=;' in s and '1970' in s for s in c.last_set_cookies), 'logout clears cookie')
check(c.req('GET', '/api/Authentication/check-token').status_code == 401, 'after logout 401')
replay = Client(); replay.cookies = old_cookie
check(replay.req('GET', '/api/Authentication/check-token').status_code == 401, 'old session id is dead server-side')
check(Client().req('POST', '/api/Authentication/logout').status_code == 401, 'logout anonymous 401')
c.req('POST', '/api/Authentication/login', json={'Username': USER, 'Password': 'NewSecret456!'})
r = c.req('DELETE', '/api/Authentication/delete')
check(r.status_code == 200, 'delete account')
check(psql(f"select count(*) from users where username='{USER}'") == '0' and psql(f'select count(*) from event where event_id={eid}') == '0'
      and psql(f"select count(*) from schedule where eventtype_id in ({','.join(map(str, tids.values()))})") == '0', 'cascade delete')
check(c.req('GET', '/api/Authentication/check-token').status_code == 401, 'session gone after delete')

print('== Admin')
a = Client()
r = a.req('GET', '/admin')
check(r.status_code == 302 and r.headers['Location'] == '/admin/login?ReturnUrl=%2Fadmin', f'admin redirect {r.headers.get("Location")}')
check(a.req('GET', '/admin/api/stats').status_code == 401, 'admin api 401')
r = a.req('GET', '/admin/login')
check(r.status_code == 200 and 'ScheduleLogic - Admin Login' in r.text, 'login page')
csrf = re.search(r'name="__RequestVerificationToken" type="hidden" value="([a-f0-9]+)"', r.text).group(1)
check('sl_admin_csrf' in a.cookies and a.cookies['sl_admin_csrf'] == csrf, 'csrf cookie set')
r = a.req('POST', '/admin/login', data={'username': ADMIN_USER, 'password': ADMIN_PASSWORD})
check(r.status_code == 400, 'admin login without csrf 400')
r = a.req('POST', '/admin/login', data={'username': ADMIN_USER, 'password': 'nope', '__RequestVerificationToken': csrf})
check(r.status_code == 200 and 'Hibás felhasználónév vagy jelszó.' in r.text, 'admin wrong pw')
r = a.req('POST', '/admin/login', data={'username': ADMIN_USER, 'password': ADMIN_PASSWORD, '__RequestVerificationToken': csrf})
check(r.status_code == 302 and r.headers['Location'] == '/admin', f'admin login ok {r.status_code} {r.text[:200]}')
asc = [s for s in a.last_set_cookies if s.startswith('sl_admin_auth=')][0].lower()
check('secure' in asc and 'samesite=strict' in asc and 'httponly' in asc and 'expires' not in asc, f'admin cookie attrs {asc}')
r = a.req('GET', '/admin')
check(r.status_code == 200 and 'ScheduleLogic - Admin' in r.text and '__RequestVerificationToken' in r.text, 'admin index')
st = a.req('GET', '/admin/api/stats').json()
check(st['databaseAvailable'] is True and st['solverAvailable'] is True and st['userCount'] >= 2, f'stats {st}')
users = a.req('GET', '/admin/api/users').json()
check(len(users) >= 2 and set(users[0].keys()) == {'userId', 'username', 'email', 'registrationDate', 'emailConfirmed'}, f'users {users[:1]}')
check(re.fullmatch(r'\d{4}-\d\d-\d\d', users[0]['registrationDate']) is not None, 'DateOnly format')
found = a.req('GET', '/admin/api/users?q=' + u2.upper()).json()
check(len(found) == 1 and found[0]['username'] == u2, f'search case-insens {found}')
check(a.req('GET', '/admin/api/users?q=%25').json() == [], 'percent is not a wildcard')
uid2 = found[0]['userId']
check(len(a.req('GET', f'/admin/api/users?q={uid2}').json()) >= 1, 'search by id')
pend = f'pending_{uid}'
c.req('POST', '/api/Authentication/register', json={'Username': pend, 'Password': PW, 'Email': pend + '@example.com'})
pid_ = a.req('GET', '/admin/api/users?q=' + pend).json()[0]
check(pid_['emailConfirmed'] is False, 'pending user')
check(a.req('POST', f'/admin/api/users/{pid_["userId"]}/confirm').status_code == 200, 'admin confirm')
check(a.req('GET', '/admin/api/users?q=' + pend).json()[0]['emailConfirmed'] is True, 'confirmed')
check(a.req('POST', '/admin/api/users/999999999/confirm').status_code == 404, 'confirm missing 404')
check(a.req('DELETE', f'/admin/api/users/{pid_["userId"]}').status_code == 200, 'admin delete')
check(a.req('GET', '/admin/api/users?q=' + pend).json() == [], 'deleted')
r = a.req('POST', '/admin/logout', data={'__RequestVerificationToken': 'x'})
check(r.status_code == 400, 'logout bad csrf')
r = a.req('POST', '/admin/logout', data={'__RequestVerificationToken': a.cookies['sl_admin_csrf']})
check(r.status_code == 302 and r.headers['Location'] == '/admin/login', 'admin logout')
check(a.req('GET', '/admin/api/stats').status_code == 401, 'after admin logout 401')
# a felhasználói session nem nyit admin felületet
check(c2.req('GET', '/admin/api/stats').status_code == 401, 'user cookie != admin')

print('== Rate limit')
rl = Client(); codes = []
for i in range(22):
    codes.append(rl.req('POST', '/api/Authentication/login', json={'Username': 'x', 'Password': 'y'}).status_code)
check(codes.count(429) >= 1 and codes[-1] == 429, f'login rate limited {codes}')
r = rl.req('POST', '/api/Authentication/login', json={'Username': 'x', 'Password': 'y'})
check(r.text == 'Too many requests. Please try again later.' and int(r.headers['Retry-After']) > 0, 'retry-after')

print('== Egyéb')
r = requests.get(BASE + '/api/Schedule/answer')
check(r.status_code == 405, f'wrong method 405 {r.status_code}')
r = requests.get(BASE + '/API/EVENT/GET-EVENTS')
check(r.status_code == 401, 'case-insensitive routing')

print(f'\nPASSED: {PASSES}  FAILED: {len(FAILS)}')
sys.exit(1 if FAILS else 0)
