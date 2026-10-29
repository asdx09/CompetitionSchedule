"""PHP backend + a VALÓDI Python solver (OR-Tools, ScheduleSolver/Main.py) együttműködésének tesztje."""
import os, subprocess, time, uuid, sys, io
import requests, openpyxl

BASE = os.environ.get('SL_BASE', 'http://127.0.0.1:7099')
FAILS = []


def check(cond, msg):
    print(('  ok   ' if cond else '  FAIL ') + msg)
    if not cond:
        FAILS.append(msg)


def psql(sql):
    return subprocess.run(['psql', '-tA', '-c', sql], capture_output=True, text=True, check=True).stdout.strip()


s = requests.Session()
u = 'solver_' + uuid.uuid4().hex[:6]
s.post(BASE + '/api/Authentication/register', json={'Username': u, 'Password': 'Secret123!', 'Email': u + '@example.com'})
psql(f"update users set validated='' where username='{u}'")
r = s.post(BASE + '/api/Authentication/login', json={'Username': u, 'Password': 'Secret123!'})
check(r.status_code == 200, 'login')

s.post(BASE + '/api/Event/new-event')
eid = s.get(BASE + '/api/Event/get-events').json()[-1]['eventId']

# 2 versenyszám, 2 helyszín, 6 versenyző, 2 csoport, egy helyszín-tiltás
T = [str(uuid.uuid4()) for _ in range(2)]
L = [str(uuid.uuid4()) for _ in range(2)]
G = [str(uuid.uuid4()) for _ in range(2)]
P = [str(uuid.uuid4()) for _ in range(6)]
payload = {
    'eventData': {'eventId': str(eid), 'eventName': 'Solver teszt', 'startDate': '2026-05-10T06:00:00.000Z',
                  'endDate': '2026-05-10T18:00:00.000Z', 'isPrivate': True, 'basePauseTime': 5, 'locationPauseTime': 5,
                  'locWeight': 1, 'groupWeight': 1, 'typeWeight': 1, 'compWeight': 1},
    'eventTypes': [{'eventTypeId': T[0], 'eventId': '0', 'typeName': 'Futás', 'timeRange': '00:20:00'},
                   {'eventTypeId': T[1], 'eventId': '0', 'typeName': 'Ugrás', 'timeRange': '00:15:00'}],
    'groups': [{'groupId': G[0], 'eventId': '0', 'groupName': 'A'}, {'groupId': G[1], 'eventId': '0', 'groupName': 'B'}],
    'locations': [{'locationId': L[0], 'eventId': '0', 'locationName': 'Pálya', 'slots': 2},
                  {'locationId': L[1], 'eventId': '0', 'locationName': 'Gödör', 'slots': 1}],
    'participants': [{'participantId': P[i], 'eventId': '0', 'competitorNumber': i + 1, 'participantName': f'Versenyző {i+1}',
                      'groupId': G[i % 2]} for i in range(6)],
    'registrations': [{'registrationId': f'r{i}{t}', 'eventId': '0', 'participantId': P[i], 'eventTypeId': T[t]}
                      for i in range(6) for t in range(2)],
    'pauseTable': [{'pauseId': 'p', 'eventId': '0', 'locationId1': L[0], 'locationId2': L[1], 'pause': '00:10'}],
    'locationTable': [{'locationTableId': '1', 'eventId': '0', 'eventTypeId': T[0], 'locationId': L[0]},
                      {'locationTableId': '2', 'eventId': '0', 'eventTypeId': T[1], 'locationId': L[1]}],
    'constraints': [{'constraintId': 'c', 'eventId': '0', 'objectId': L[1], 'constraintType': 'L',
                     'startTime': '2026-05-10T06:00:00.000Z', 'endTime': '2026-05-10T07:00:00.000Z'}],
    'timeZones': [],
}
r = s.post(BASE + '/api/Event/save-event', json=payload)
check(r.status_code == 200, f'save {r.status_code}')

r = s.post(BASE + f'/api/Schedule/{eid}')
check(r.status_code == 200, f'generate {r.status_code} {r.text[:200]}')

running, waited = True, 0
seen_running = False
while waited < 90:
    running = s.get(BASE + f'/api/Schedule/isRunning?id={eid}').json()
    seen_running |= running
    if not running and waited > 2:
        break
    time.sleep(1); waited += 1
check(seen_running, 'solver reported running at least once')
time.sleep(2)  # végső callback
sd = s.get(BASE + f'/api/Schedule/{eid}').json()
tz = sd['timeZones']
check(len(tz) == 12, f'12 scheduled entries from real solver (got {len(tz)})')

types = {int(t['eventTypeId']): t['typeName'] for t in sd['eventTypes']}
locs = {int(l['locationId']): l['locationName'] for l in sd['locations']}
dur = {'Futás': 20, 'Ugrás': 15}
check(all(t['endTime'] - t['startTime'] == dur[types[t['eventTypeId']]] for t in tz), 'durations match event types')
check(all(locs[t['locationId']] == ('Pálya' if types[t['eventTypeId']] == 'Futás' else 'Gödör') for t in tz), 'possibleLocations respected')
# 06:00Z = 08:00 helyi (CEST, május) -> 480 perc; a gödör 07:00Z-ig (540) tiltva
check(all(t['startTime'] >= 480 for t in tz), 'nothing before event start (480 = 08:00 local)')
check(all(t['startTime'] >= 540 for t in tz if locs[t['locationId']] == 'Gödör'), 'location constraint respected')
by_p = {}
for t in tz:
    by_p.setdefault(t['participantId'], []).append((t['startTime'], t['endTime']))
ok = all(a[1] <= b[0] for v in by_p.values() for a, b in zip(sorted(v), sorted(v)[1:]))
check(ok, 'no overlapping events per participant')
gp = [t for t in tz if locs[t['locationId']] == 'Gödör']
gp.sort(key=lambda t: t['startTime'])
check(all(a['endTime'] <= b['startTime'] for a, b in zip(gp, gp[1:])) and all(t['slot'] == 1 for t in gp), 'single-slot location sequential, slot 1')
check(all(t['slot'] in (1, 2) for t in tz if locs[t['locationId']] == 'Pálya'), 'two-slot location uses slots 1..2')

r = s.get(BASE + f'/api/Schedule/export?id={eid}')
wb = openpyxl.load_workbook(io.BytesIO(r.content))
check(r.status_code == 200 and wb['Schedule'].max_row == 13, 'export 12 rows + header')

print('\nFAILED:', len(FAILS))
sys.exit(1 if FAILS else 0)
