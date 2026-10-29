"""Teszt SMTP szerver STARTTLS-sel és AUTH LOGIN-nal (felhasználó: mailuser / mailpass).
A leveleket a tests/_mail mappába menti. Indítás: python tests/test_smtpd.py
Kell hozzá: pip install aiosmtpd   és egy self-signed tanúsítvány:
  openssl req -x509 -newkey rsa:2048 -nodes -keyout tests/_smtp.key -out tests/_smtp.crt -days 30 -subj "/CN=localhost"
"""
import os
HERE = os.path.dirname(os.path.abspath(__file__))
MAILDIR = os.environ.get('SL_MAILDIR', os.path.join(HERE, '_mail'))
os.makedirs(MAILDIR, exist_ok=True)
import ssl, asyncio, json, time, sys, email
from aiosmtpd.controller import Controller
from aiosmtpd.smtp import AuthResult, LoginPassword
class H:
    async def handle_DATA(self, server, session, envelope):
        fn=os.path.join(MAILDIR, f"{time.time_ns()}.eml")
        open(fn,'wb').write(envelope.original_content or envelope.content)
        json.dump({'from':envelope.mail_from,'to':envelope.rcpt_tos,'tls':session.ssl is not None,'auth':str(session.authenticated)}, open(fn+'.json','w'))
        return '250 OK'
def auth(server, session, envelope, mechanism, auth_data):
    ok = isinstance(auth_data, LoginPassword) and auth_data.login==b'mailuser' and auth_data.password==b'mailpass'
    return AuthResult(success=ok, handled=False)
ctx=ssl.create_default_context(ssl.Purpose.CLIENT_AUTH); ctx.load_cert_chain(os.path.join(HERE, '_smtp.crt'), os.path.join(HERE, '_smtp.key'))
c=Controller(H(), hostname='127.0.0.1', port=2587, tls_context=ctx, require_starttls=True, authenticator=auth, auth_required=True, auth_require_tls=True)
c.start(); print('smtp up', flush=True)
while True: time.sleep(3600)
