// Valódi Angular kliens + PHP backend + valódi solver, headless Chromiumban.
const { chromium } = require('playwright');
const fs = require('fs');
const BASE = process.env.SL_BASE || 'http://127.0.0.1:7099';
const path = require('path');
const MAILDIR = process.env.SL_MAILDIR || path.join(__dirname, '_mail');
const results = []; const ok = (c, m) => { results.push([c, m]); console.log((c ? '  ok   ' : '  FAIL ') + m); };

(async () => {
  const browser = await chromium.launch();
  const ctx = await browser.newContext({ acceptDownloads: true, viewport: { width: 1400, height: 900 } });
  const page = await ctx.newPage();
  const apiErrors = []; const consoleErrors = [];
  page.on('response', r => { if (r.url().includes('/api/') && r.status() >= 400) apiErrors.push(r.status() + ' ' + r.request().method() + ' ' + r.url()); });
  page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
  page.on('dialog', d => d.accept());

  const user = 'ui_' + Date.now().toString(36);
  const email = user + '@example.com';

  // --- Regisztráció a felületen ---
  await page.goto(BASE + '/login');
  await page.getByText('Register here').click();
  await page.fill('input[name=username]', user);
  await page.fill('input[name=email]', email);
  await page.fill('input[name=password]', 'Secret123!');
  await page.fill('input[name=passwordAgain]', 'Secret123!');
  const regResp = page.waitForResponse(r => r.url().endsWith('/api/Authentication/register'));
  await page.locator('button[type=submit]').click();
  const rr = await regResp;
  ok(rr.status() === 200, 'UI register -> 200 ' + rr.status());
  await page.waitForTimeout(500);
  ok(await page.getByText('confirmation email').isVisible(), 'UI shows "confirmation email" alert');

  // --- E-mail megerősítés: a levélben kapott link ---
  await page.waitForTimeout(500);
  const files = fs.readdirSync(MAILDIR).filter(f => f.endsWith('.eml')).sort();
  let link = null;
  for (const f of files.reverse()) {
    const raw = fs.readFileSync(path.join(MAILDIR, f), 'utf8');
    if (!raw.includes(email)) continue;
    const b64 = raw.split('\r\n\r\n').slice(1).join('').replace(/\s+/g, '');
    const html = Buffer.from(b64, 'base64').toString('utf8');
    link = html.match(/href="([^"]+)"/)[1].replace(/&amp;/g, '&');
    break;
  }
  ok(!!link, 'confirmation link found in mail');
  await page.goto(link);
  ok(page.url().startsWith(BASE + '/'), 'confirm redirects to frontend: ' + page.url());

  // --- Belépés a felületen ---
  await page.goto(BASE + '/login');
  await page.fill('input[name=username]', user);
  await page.fill('input[name=password]', 'Secret123!');
  await page.locator('button[type=submit]').click();
  await page.waitForURL(/\/home$/, { timeout: 10000 });
  ok(page.url().endsWith('/home'), 'UI login navigates to /home');
  await page.waitForTimeout(800);
  ok(await page.getByText(user, { exact: true }).first().isVisible(), 'username shown in header (check-token)');

  // --- Új esemény ---
  await page.getByText('Add New Event').click();
  await page.waitForTimeout(800);
  ok(await page.getByText('New Event').first().isVisible(), 'new event listed on home');

  // Az eseményt az API-n keresztül töltjük fel (ugyanaz a cookie), majd a felületen mentjük újra
  const events = await page.evaluate(async () => (await fetch('/api/Event/get-events', { credentials: 'include' })).json());
  const eid = events[0].eventId;
  const data = await page.evaluate(async (id) => (await fetch('/api/Event/get-event?id=' + id, { credentials: 'include' })).json(), eid);
  const u = () => crypto.randomUUID ? crypto.randomUUID() : String(Math.random());
  data.eventData.eventName = 'UI Teszt Verseny';
  data.eventData.startDate = '2026-06-01T06:00:00.000Z'; data.eventData.endDate = '2026-06-01T16:00:00.000Z';
  data.eventTypes = [{ eventTypeId: 't1', eventId: '0', typeName: 'Sprint', timeRange: '00:10:00' }];
  data.locations = [{ locationId: 'l1', eventId: '0', locationName: 'Stadion', slots: 2 }];
  data.locationTable = [{ locationTableId: '1', eventId: '0', eventTypeId: 't1', locationId: 'l1' }];
  data.participants = [1, 2, 3].map(i => ({ participantId: 'p' + i, eventId: '0', competitorNumber: i, participantName: 'Futó ' + i, groupId: '' }));
  data.registrations = [1, 2, 3].map(i => ({ registrationId: 'r' + i, eventId: '0', participantId: 'p' + i, eventTypeId: 't1' }));
  const st = await page.evaluate(async (d) => (await fetch('/api/Event/save-event', { method: 'POST', credentials: 'include', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(d) })).status, data);
  ok(st === 200, 'API save from browser context ' + st);

  // --- Esemény oldal: betölt, a felületen MENTÉS gomb ---
  await page.goto(BASE + '/home');
  await page.waitForTimeout(800);
  await page.getByText('UI Teszt Verseny').first().click();
  await page.waitForURL(/\/event/, { timeout: 10000 });
  await page.waitForTimeout(1500);
  ok(await page.locator('h2', { hasText: 'UI Teszt Verseny' }).isVisible(), 'event page renders loaded event');
  await page.locator('.FirstListItem input[type=text]').first().fill('UI Teszt Verseny 2');
  const saveResp = page.waitForResponse(r => r.url().endsWith('/api/Event/save-event'));
  await page.getByRole('button', { name: /SAVE/ }).click();
  const sr = await saveResp;
  ok(sr.status() === 200, 'UI SAVE button round-trip (frontend-built DataDTO accepted) ' + sr.status());
  await page.waitForTimeout(1000);

  // --- Megoldás indítása a felületről (valódi solver) ---
  await page.waitForTimeout(1500);
  const solveBtn = page.getByRole('button', { name: /SOLVE SOLUTION/ });
  ok(await solveBtn.isEnabled(), 'SOLVE enabled (isRunning reachable)');
  const genResp = page.waitForResponse(r => /\/api\/Schedule\/\d+$/.test(r.url()) && r.request().method() === 'POST');
  await solveBtn.click();
  ok((await genResp).status() === 200, 'generate from UI');
  // várunk, hogy a solver visszahívjon
  let tzCount = 0;
  for (let i = 0; i < 40 && tzCount < 3; i++) {
    await page.waitForTimeout(1000);
    tzCount = await page.evaluate(async (id) => (await (await fetch('/api/Schedule/' + id, { credentials: 'include' })).json()).timeZones.length, eid);
  }
  ok(tzCount === 3, 'solver results stored: ' + tzCount);
  // leállítás a felületről (CANCEL gomb), hogy a solver ne optimalizáljon tovább
  const stopResp = page.waitForResponse(r => r.url().includes('/api/Schedule/stop'));
  await page.getByRole('button', { name: /CANCEL/ }).click();
  const sresp = await stopResp;
  const sj = await sresp.json(); ok(sresp.status() === 200 && typeof sj === 'boolean', 'CANCEL from UI -> 200 bool (' + sj + '; false = solver had already finished)');

  // --- Megoldás nézet + export ---
  await page.getByRole('button', { name: 'VIEW SOLUTION' }).click();
  await page.waitForURL(/\/schedule/, { timeout: 10000 });
  await page.waitForTimeout(2000);
  const sprintCells = await page.getByText('Sprint').count();
  ok(sprintCells >= 3, 'schedule view renders timezones (' + sprintCells + ')');
  const [download] = await Promise.all([page.waitForEvent('download'), page.getByRole('button', { name: 'DOWNLOAD DATA' }).click()]);
  const dl = await download.path();
  ok(fs.statSync(dl).size > 1000, 'export downloaded via UI (' + download.suggestedFilename() + ')');

  // --- Kijelentkezés a profil ablakból ---
  await page.goto(BASE + '/home');
  await page.waitForTimeout(800);
  await page.locator('.upperbox .right').click();
  await page.waitForTimeout(500);
  const logoutResp = page.waitForResponse(r => r.url().endsWith('/api/Authentication/logout'));
  await page.getByText(/log ?out/i).first().click();
  ok((await logoutResp).status() === 200, 'logout from UI');
  await page.waitForTimeout(800);
  ok(page.url().includes('/login'), 'back on login page');
  await page.goto(BASE + '/home');
  await page.waitForTimeout(1000);
  ok(page.url().includes('/login'), 'guard redirects to login after logout');

  const unexpected = apiErrors.filter(e => !e.startsWith('401 GET') );
  ok(unexpected.length === 0, 'no unexpected API errors: ' + JSON.stringify(apiErrors));
  ok(consoleErrors.filter(e => !e.includes('401') && !e.includes('ERR_TUNNEL_CONNECTION_FAILED')).length === 0, 'no console errors: ' + JSON.stringify(consoleErrors.slice(0, 3)));
  await browser.close();
  const failed = results.filter(r => !r[0]).length;
  console.log('\nFAILED: ' + failed);
  process.exit(failed ? 1 : 0);
})().catch(e => { console.error(e); process.exit(2); });
