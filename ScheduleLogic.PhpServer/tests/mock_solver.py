"""A Python solver (ScheduleSolver/Main.py) API-jának egyszerű utánzata a gyors tesztekhez.
POST /schedule, GET /is_solver_running, GET /stop_solver - ugyanazokkal a válaszokkal.
GET /_last: az utoljára kapott ütemezési kérés (a teszt ebből ellenőrzi a JSON-t)."""
import json, http.server, threading
STATE={'requests':[], 'running':set()}
class H(http.server.BaseHTTPRequestHandler):
    def log_message(self,*a): pass
    def _send(self, code, obj):
        b=json.dumps(obj).encode(); self.send_response(code); self.send_header('Content-Type','application/json'); self.send_header('Content-Length',str(len(b))); self.end_headers(); self.wfile.write(b)
    def do_POST(self):
        n=int(self.headers.get('Content-Length',0)); body=json.loads(self.rfile.read(n) or b'{}')
        if self.path=='/schedule':
            STATE['requests'].append(body); STATE['running'].add(body['eventId'])
            return self._send(200, {"Status":"QUEUED_OR_STARTED","EventId":body['eventId']})
        self._send(404,{"detail":"Not Found"})
    def do_GET(self):
        from urllib.parse import urlparse, parse_qs
        u=urlparse(self.path); q=parse_qs(u.query)
        if u.path=='/is_solver_running':
            e=int(q['EventId'][0]); return self._send(200, {"Running": e in STATE['running'], "EventId": e})
        if u.path=='/stop_solver':
            e=int(q['EventId'][0])
            if e in STATE['running']:
                STATE['running'].discard(e); return self._send(200, {"status":"STOPPED","eventId":e})
            return self._send(200, {"status":"NOT_RUNNING","eventId":e})
        if u.path=='/_last':
            return self._send(200, STATE['requests'][-1] if STATE['requests'] else {})
        self._send(404,{"detail":"Not Found"})
http.server.ThreadingHTTPServer(('127.0.0.1',8000),H).serve_forever()
