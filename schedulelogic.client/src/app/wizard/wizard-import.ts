import * as ExcelJS from 'exceljs';
import { Constraint, data, EventType, Group, Location, LocationTable, Participant, PauseTable, Registration } from '../events.service';

export interface ImportIssue {
  sheet: string;
  row?: number;
  message: string;
}

export interface ImportResult {
  data: data;
  errors: ImportIssue[];
  warnings: ImportIssue[];
}

interface ColumnSpec {
  key: string;
  title: string;
  aliases: string[];
  required: boolean;
  hint: string;
}

interface SheetSpec {
  key: string;
  title: string;
  aliases: string[];
  required: boolean;
  columns: ColumnSpec[];
  example: (string | number | Date)[][];
}

const LIST_SEPARATOR = /[;,\n]/;

export const SHEETS: SheetSpec[] = [
  {
    key: 'locations', title: 'Locations', aliases: ['location', 'venues', 'venue', 'ranges', 'range'], required: false,
    columns: [
      { key: 'name', title: 'Location', aliases: ['name', 'location name', 'venue', 'range'], required: true, hint: 'Name of the location, e.g. Range 1' },
      { key: 'lanes', title: 'Lanes', aliases: ['slots', 'slot', 'capacity', 'lane count', 'places'], required: false, hint: 'How many competitors can start there at the same time (default: 1)' },
    ],
    example: [['Range 1', 10], ['Range 2', 5], ['Pistol hall', 8]],
  },
  {
    key: 'events', title: 'Events', aliases: ['event', 'event types', 'event type', 'disciplines', 'discipline'], required: true,
    columns: [
      { key: 'name', title: 'Event', aliases: ['name', 'event name', 'discipline'], required: true, hint: 'Name of the event, e.g. Air rifle 40 shots' },
      { key: 'duration', title: 'Duration (min)', aliases: ['duration', 'minutes', 'length', 'time', 'duration minutes'], required: true, hint: 'Length of one start in minutes (e.g. 45) or as hours:minutes (e.g. 1:15)' },
      { key: 'locations', title: 'Locations', aliases: ['location', 'venues', 'venue', 'ranges', 'range'], required: true, hint: 'Where the event can be held. Several locations separated by ;' },
    ],
    example: [['Air rifle 40 shots', 50, 'Range 1; Range 2'], ['Air pistol 40 shots', 50, 'Pistol hall'], ['Sport rifle 3x20', 90, 'Range 1']],
  },
  {
    key: 'competitors', title: 'Competitors', aliases: ['competitor', 'participants', 'participant', 'athletes', 'athlete', 'shooters', 'entries'], required: true,
    columns: [
      { key: 'number', title: 'Number', aliases: ['start number', 'bib', 'bib number', 'competitor number', 'no', 'nr'], required: true, hint: 'Start number. Every competitor needs a different number' },
      { key: 'name', title: 'Name', aliases: ['competitor', 'competitor name', 'full name', 'athlete'], required: true, hint: 'Name of the competitor' },
      { key: 'group', title: 'Group', aliases: ['category', 'class', 'team', 'club', 'age group'], required: false, hint: 'Optional, e.g. Junior. Competitors of the same group are kept together' },
      { key: 'events', title: 'Events', aliases: ['event', 'entries', 'disciplines', 'registrations'], required: true, hint: 'The events the competitor enters. Several events separated by ;' },
    ],
    example: [[1, 'Anna Smith', 'Junior', 'Air rifle 40 shots; Sport rifle 3x20'], [2, 'Ben Taylor', 'Senior', 'Air pistol 40 shots'], [3, 'Cecil Brown', '', 'Air rifle 40 shots']],
  },
  {
    key: 'unavailable', title: 'Unavailable times', aliases: ['unavailable', 'constraints', 'constraint', 'blocked times', 'blocked', 'not available'], required: false,
    columns: [
      { key: 'type', title: 'Type', aliases: ['kind', 'applies to'], required: true, hint: 'Location, Event, Competitor or Group' },
      { key: 'what', title: 'Name or number', aliases: ['name', 'number', 'what', 'who', 'object'], required: true, hint: 'Name of the location/event/group, or the start number of the competitor' },
      { key: 'from', title: 'From', aliases: ['start', 'begin', 'from time'], required: true, hint: 'Date and time, e.g. 2026-03-01 12:00' },
      { key: 'to', title: 'To', aliases: ['end', 'until', 'to time'], required: true, hint: 'Date and time, e.g. 2026-03-01 13:00' },
    ],
    example: [['Location', 'Range 2', '2026-03-01 12:00', '2026-03-01 13:00'], ['Competitor', 2, '2026-03-01 08:00', '2026-03-01 10:00']],
  },
  {
    key: 'travel', title: 'Travel times', aliases: ['travel', 'travel time', 'walking times', 'pauses'], required: false,
    columns: [
      { key: 'from', title: 'From', aliases: ['location 1', 'from location', 'start'], required: true, hint: 'A location' },
      { key: 'to', title: 'To', aliases: ['location 2', 'to location', 'end'], required: true, hint: 'Another location' },
      { key: 'minutes', title: 'Minutes', aliases: ['time', 'duration', 'travel time', 'min'], required: true, hint: 'Minutes needed to get from one to the other' },
    ],
    example: [['Range 1', 'Pistol hall', 10]],
  },
];

const CONSTRAINT_TYPES: { code: string; words: string[] }[] = [
  { code: 'L', words: ['location', 'venue', 'range', 'l'] },
  { code: 'T', words: ['event', 'event type', 'discipline', 't', 'e'] },
  { code: 'C', words: ['competitor', 'participant', 'athlete', 'shooter', 'c', 'p'] },
  { code: 'G', words: ['group', 'category', 'class', 'g'] },
];

const normalize = (s: string) => s.toLowerCase().replace(/\(.*?\)/g, '').replace(/[^a-z0-9]/g, '');
const nameKey = (s: string) => s.trim().replace(/\s+/g, ' ').toLowerCase();

function cellValue(cell: ExcelJS.Cell): string | number | Date | null {
  let v: any = cell.value;
  if (v && typeof v === 'object' && !(v instanceof Date)) {
    if ('result' in v) v = v.result;
    else if ('richText' in v) v = v.richText.map((t: any) => t.text).join('');
    else if ('text' in v) v = v.text;
    else if ('error' in v) return null;
  }
  if (v === null || v === undefined) return null;
  if (typeof v === 'string') {
    const t = v.trim();
    return t === '' ? null : t;
  }
  if (typeof v === 'number' || v instanceof Date) return v;
  return String(v);
}

const asText = (v: string | number | Date | null) =>
  v === null ? '' : v instanceof Date ? v.toISOString() : String(v).trim();

function parseMinutes(v: string | number | Date | null): number | null {
  if (v === null) return null;
  if (v instanceof Date) return v.getUTCHours() * 60 + v.getUTCMinutes();
  if (typeof v === 'number') {
    if (Number.isInteger(v)) return v;
    return v > 0 && v < 1 ? Math.round(v * 24 * 60) : null;
  }
  const s = v.trim();
  let m = /^(\d{1,2}):(\d{2})(?::\d{2})?$/.exec(s);
  if (m) return Number(m[1]) * 60 + Number(m[2]);
  m = /^(\d+)\s*[^\d:]*$/.exec(s);
  if (m) return Number(m[1]);
  return null;
}

function toTimeOnly(minutes: number): string {
  const h = Math.floor(minutes / 60).toString().padStart(2, '0');
  const m = (minutes % 60).toString().padStart(2, '0');
  return `${h}:${m}:00`;
}

function parseDateTime(v: string | number | Date | null): Date | null {
  if (v === null) return null;
  if (v instanceof Date) {
    return new Date(v.getUTCFullYear(), v.getUTCMonth(), v.getUTCDate(), v.getUTCHours(), v.getUTCMinutes());
  }
  if (typeof v === 'number') {
    const ms = Math.round((v - 25569) * 86400 * 1000);
    const d = new Date(ms);
    return new Date(d.getUTCFullYear(), d.getUTCMonth(), d.getUTCDate(), d.getUTCHours(), d.getUTCMinutes());
  }
  const m = /^(\d{4})\D(\d{1,2})\D(\d{1,2})\.?(?:\D+(\d{1,2})[:.](\d{2}))?/.exec(v.trim());
  if (!m) return null;
  const d = new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]), Number(m[4] ?? 0), Number(m[5] ?? 0));
  return isNaN(d.getTime()) || d.getMonth() !== Number(m[2]) - 1 ? null : d;
}

function splitList(text: string): string[] {
  return text.split(LIST_SEPARATOR).map(s => s.trim()).filter(s => s !== '');
}

interface SheetRows {
  title: string;
  rows: { row: number; get: (key: string) => string | number | Date | null }[];
}

export function parseWorkbook(workbook: ExcelJS.Workbook): ImportResult {
  const errors: ImportIssue[] = [];
  const warnings: ImportIssue[] = [];
  const result = new data();
  const err = (sheet: string, message: string, row?: number) => errors.push({ sheet, row, message });
  const warn = (sheet: string, message: string, row?: number) => warnings.push({ sheet, row, message });

  const worksheets = workbook.worksheets.filter(ws => ws.state === 'visible' || ws.state === undefined);
  const sheets: { [key: string]: SheetRows | null } = {};

  for (const spec of SHEETS) {
    const wanted = [spec.title, ...spec.aliases].map(normalize);
    const ws = worksheets.find(w => wanted.includes(normalize(w.name)));
    if (!ws) {
      sheets[spec.key] = null;
      if (spec.required) {
        err(spec.title, `The workbook has no "${spec.title}" sheet. Sheets found: ${worksheets.map(w => `"${w.name}"`).join(', ') || 'none'}. Rename the right sheet to "${spec.title}" or start from the template.`);
      }
      continue;
    }

    const header = ws.getRow(1);
    const colIndex: { [key: string]: number } = {};
    const headersFound: string[] = [];
    header.eachCell((cell, col) => {
      const text = asText(cellValue(cell));
      if (text === '') return;
      headersFound.push(text);
      const n = normalize(text);
      const column = spec.columns.find(c => [c.title, ...c.aliases].map(normalize).includes(n));
      if (column) {
        if (colIndex[column.key] !== undefined) err(ws.name, `The column "${column.title}" appears twice in the first row.`, 1);
        else colIndex[column.key] = col;
      }
    });

    const missing = spec.columns.filter(c => c.required && colIndex[c.key] === undefined);
    for (const c of missing) {
      err(ws.name, `Missing column "${c.title}" (${c.hint}). The first row must contain the column titles; found: ${headersFound.map(h => `"${h}"`).join(', ') || 'nothing'}.`, 1);
    }
    if (missing.length > 0) {
      sheets[spec.key] = null;
      continue;
    }

    const rows: SheetRows['rows'] = [];
    ws.eachRow((row, rowNumber) => {
      if (rowNumber === 1) return;
      const get = (key: string) => colIndex[key] === undefined ? null : cellValue(row.getCell(colIndex[key]));
      if (spec.columns.every(c => get(c.key) === null)) return;
      rows.push({ row: rowNumber, get });
    });
    sheets[spec.key] = { title: ws.name, rows };
  }

  const locations = new Map<string, Location>();
  const locationRow = new Map<string, number>();
  const ls = sheets['locations'];
  if (ls) {
    for (const r of ls.rows) {
      const name = asText(r.get('name'));
      if (name === '') { err(ls.title, 'The location name is empty.', r.row); continue; }
      const key = nameKey(name);
      if (locations.has(key)) { err(ls.title, `"${name}" is listed twice (also in row ${locationRow.get(key)}).`, r.row); continue; }
      let lanes = 1;
      const raw = r.get('lanes');
      if (raw !== null) {
        lanes = typeof raw === 'number' ? raw : Number(asText(raw));
        if (!Number.isInteger(lanes) || lanes < 1 || lanes > 1000) { err(ls.title, `"${asText(raw)}" is not a valid number of lanes for "${name}". Use a whole number from 1 to 1000.`, r.row); continue; }
      }
      const loc = new Location();
      loc.locationId = `L${locations.size + 1}`;
      loc.locationName = name;
      loc.slots = lanes;
      locations.set(key, loc);
      locationRow.set(key, r.row);
    }
    if (ls.rows.length === 0) warn(ls.title, 'The sheet is empty; locations will be created from the Events sheet with 1 lane each.');
  }
  const hasLocationSheet = ls !== null && ls.rows.length > 0;
  const autoLocations: string[] = [];

  const resolveLocation = (sheet: string, row: number, name: string): Location | null => {
    const key = nameKey(name);
    const found = locations.get(key);
    if (found) return found;
    if (hasLocationSheet) {
      err(sheet, `Unknown location "${name}". It is not on the Locations sheet (check the spelling).`, row);
      return null;
    }
    const loc = new Location();
    loc.locationId = `L${locations.size + 1}`;
    loc.locationName = name;
    loc.slots = 1;
    locations.set(key, loc);
    autoLocations.push(name);
    return loc;
  };

  const eventTypes = new Map<string, EventType>();
  const eventRow = new Map<string, number>();
  const es = sheets['events'];
  if (es) {
    if (es.rows.length === 0) err(es.title, 'The sheet has no events. Add at least one event below the title row.');
    for (const r of es.rows) {
      const name = asText(r.get('name'));
      if (name === '') { err(es.title, 'The event name is empty.', r.row); continue; }
      const key = nameKey(name);
      if (eventTypes.has(key)) { err(es.title, `"${name}" is listed twice (also in row ${eventRow.get(key)}).`, r.row); continue; }

      const rawDuration = r.get('duration');
      const minutes = parseMinutes(rawDuration);
      if (rawDuration === null) { err(es.title, `The duration of "${name}" is empty.`, r.row); }
      else if (minutes === null || minutes < 1 || minutes >= 24 * 60) { err(es.title, `"${asText(rawDuration)}" is not a valid duration for "${name}". Give the minutes (e.g. 45) or hours:minutes (e.g. 1:15), less than 24 hours.`, r.row); }
      else if (minutes > 8 * 60) { warn(es.title, `"${name}" lasts ${Math.floor(minutes / 60)} hours ${minutes % 60} minutes per start. Check that the duration is really meant in minutes.`, r.row); }

      const et = new EventType();
      et.eventTypeId = `E${eventTypes.size + 1}`;
      et.typeName = name;
      et.timeRange = minutes !== null && minutes >= 1 && minutes < 24 * 60 ? toTimeOnly(minutes) : '';
      eventTypes.set(key, et);
      eventRow.set(key, r.row);

      const locText = asText(r.get('locations'));
      if (locText === '') { err(es.title, `No location is given for "${name}".`, r.row); continue; }
      const seen = new Set<string>();
      for (const locName of splitList(locText)) {
        const loc = resolveLocation(es.title, r.row, locName);
        if (!loc || seen.has(loc.locationId)) continue;
        seen.add(loc.locationId);
        const lt = new LocationTable();
        lt.locationTableId = `LT${result.locationTable.length + 1}`;
        lt.eventTypeId = et.eventTypeId;
        lt.locationId = loc.locationId;
        result.locationTable.push(lt);
      }
    }
  }
  if (autoLocations.length > 0) {
    warn('Locations', `There is no Locations sheet, so these locations were created with 1 lane each: ${autoLocations.join(', ')}.`);
  }

  const groups = new Map<string, Group>();
  const competitors = new Map<number, Participant>();
  const competitorRow = new Map<number, number>();
  const entered = new Set<string>();
  const cs = sheets['competitors'];
  if (cs) {
    if (cs.rows.length === 0) err(cs.title, 'The sheet has no competitors. Add at least one competitor below the title row.');
    for (const r of cs.rows) {
      const rawNumber = r.get('number');
      const name = asText(r.get('name'));
      const number = typeof rawNumber === 'number' ? rawNumber : Number(asText(rawNumber));
      if (rawNumber === null) { err(cs.title, `The start number of "${name || 'this competitor'}" is empty.`, r.row); continue; }
      if (!Number.isInteger(number) || number < 1) { err(cs.title, `"${asText(rawNumber)}" is not a valid start number. Use a whole number greater than 0.`, r.row); continue; }
      if (competitors.has(number)) {
        err(cs.title, `Start number ${number} is used twice: here and in row ${competitorRow.get(number)}. Every competitor needs a different number.`, r.row);
        continue;
      }
      if (name === '') { err(cs.title, `The name of competitor ${number} is empty.`, r.row); continue; }

      const p = new Participant();
      p.participantId = `C${competitors.size + 1}`;
      p.competitorNumber = number;
      p.participantName = name;
      const groupName = asText(r.get('group'));
      if (groupName !== '') {
        const gk = nameKey(groupName);
        let g = groups.get(gk);
        if (!g) {
          g = new Group();
          g.groupId = `G${groups.size + 1}`;
          g.groupName = groupName;
          groups.set(gk, g);
        }
        p.groupId = g.groupId;
      }
      competitors.set(number, p);
      competitorRow.set(number, r.row);

      const eventText = asText(r.get('events'));
      if (eventText === '') { warn(cs.title, `${name} (${number}) has not entered any event.`, r.row); continue; }
      const mine = new Set<string>();
      const wholeCell = eventTypes.get(nameKey(eventText));
      const names = wholeCell ? [eventText] : splitList(eventText);
      for (const en of names) {
        const et = eventTypes.get(nameKey(en));
        if (!et) {
          if (es) err(cs.title, `${name} (${number}) entered "${en}", but there is no such event on the Events sheet (check the spelling).`, r.row);
          continue;
        }
        if (mine.has(et.eventTypeId)) { warn(cs.title, `${name} (${number}) entered "${en}" twice; it is counted once.`, r.row); continue; }
        mine.add(et.eventTypeId);
        entered.add(et.eventTypeId);
        const reg = new Registration();
        reg.registrationId = `R${result.registrations.length + 1}`;
        reg.participantId = p.participantId;
        reg.eventTypeId = et.eventTypeId;
        result.registrations.push(reg);
      }
    }
  }
  for (const et of eventTypes.values()) {
    if (cs && !entered.has(et.eventTypeId)) warn(es!.title, `Nobody entered "${et.typeName}".`, eventRow.get(nameKey(et.typeName)));
  }

  const us = sheets['unavailable'];
  if (us) {
    for (const r of us.rows) {
      const typeText = asText(r.get('type'));
      const type = CONSTRAINT_TYPES.find(t => t.words.includes(nameKey(typeText)));
      if (!type) { err(us.title, `"${typeText}" is not a valid type. Use Location, Event, Competitor or Group.`, r.row); continue; }
      const whatRaw = r.get('what');
      const what = asText(whatRaw);
      let objectId: string | undefined;
      if (type.code === 'L') objectId = locations.get(nameKey(what))?.locationId;
      if (type.code === 'T') objectId = eventTypes.get(nameKey(what))?.eventTypeId;
      if (type.code === 'G') objectId = groups.get(nameKey(what))?.groupId;
      if (type.code === 'C') objectId = competitors.get(typeof whatRaw === 'number' ? whatRaw : Number(what))?.participantId;
      if (!objectId) {
        const kind = { L: 'location', T: 'event', G: 'group', C: 'competitor with start number' }[type.code];
        err(us.title, `There is no ${kind} "${what}".`, r.row);
        continue;
      }
      const from = parseDateTime(r.get('from'));
      const to = parseDateTime(r.get('to'));
      if (!from) { err(us.title, `"${asText(r.get('from'))}" is not a valid date and time. Use e.g. 2026-03-01 12:00.`, r.row); continue; }
      if (!to) { err(us.title, `"${asText(r.get('to'))}" is not a valid date and time. Use e.g. 2026-03-01 13:00.`, r.row); continue; }
      if (to <= from) { err(us.title, 'The end must be later than the start.', r.row); continue; }
      const c = new Constraint();
      c.constraintId = `U${result.constraints.length + 1}`;
      c.constraintType = type.code;
      c.objectId = objectId;
      c.startTime = from.toISOString();
      c.endTime = to.toISOString();
      result.constraints.push(c);
    }
  }

  const ts = sheets['travel'];
  if (ts) {
    const pairs = new Map<string, { minutes: number; row: number }>();
    for (const r of ts.rows) {
      const fromName = asText(r.get('from'));
      const toName = asText(r.get('to'));
      const a = locations.get(nameKey(fromName));
      const b = locations.get(nameKey(toName));
      if (!a) { err(ts.title, `Unknown location "${fromName}".`, r.row); continue; }
      if (!b) { err(ts.title, `Unknown location "${toName}".`, r.row); continue; }
      if (a === b) { err(ts.title, `"${fromName}" is given as both start and destination.`, r.row); continue; }
      const raw = r.get('minutes');
      const minutes = parseMinutes(raw);
      if (minutes === null || minutes < 0 || minutes >= 24 * 60) { err(ts.title, `"${asText(raw)}" is not a valid travel time. Give the minutes, e.g. 15.`, r.row); continue; }
      const key = [a.locationId, b.locationId].sort().join('|');
      const previous = pairs.get(key);
      if (previous) {
        if (previous.minutes !== minutes) err(ts.title, `The travel time between "${fromName}" and "${toName}" is given twice with different values (here ${minutes} min, in row ${previous.row} ${previous.minutes} min). One value is used for both directions.`, r.row);
        else warn(ts.title, `The travel time between "${fromName}" and "${toName}" is given twice (also in row ${previous.row}); it is used once.`, r.row);
        continue;
      }
      pairs.set(key, { minutes, row: r.row });
      const p = new PauseTable();
      p.pauseId = `P${result.pauseTable.length + 1}`;
      p.locationId1 = a.locationId;
      p.locationId2 = b.locationId;
      p.pause = toTimeOnly(minutes);
      result.pauseTable.push(p);
    }
  }

  result.locations = [...locations.values()];
  result.eventTypes = [...eventTypes.values()];
  result.groups = [...groups.values()];
  result.participants = [...competitors.values()];

  const usedLocations = new Set(result.locationTable.map(l => l.locationId));
  for (const loc of result.locations) {
    if (!usedLocations.has(loc.locationId) && ls) warn(ls.title, `No event uses "${loc.locationName}".`, locationRow.get(nameKey(loc.locationName)));
  }

  return { data: result, errors, warnings };
}

export function buildTemplate(): ExcelJS.Workbook {
  const wb = new ExcelJS.Workbook();
  const help = wb.addWorksheet('How to fill in');
  help.columns = [{ width: 22 }, { width: 22 }, { width: 90 }];
  help.addRow(['Fill in the sheets of this workbook, then upload it in the wizard.']).font = { bold: true, size: 13 };
  help.addRow(['Keep the sheet names and the titles in the first row. You can add or delete rows freely. Lists go into one cell, separated by ;']);
  help.addRow([]);
  help.addRow(['Sheet', 'Column', 'What to write']).font = { bold: true };
  for (const s of SHEETS) {
    s.columns.forEach((c, i) => help.addRow([i === 0 ? `${s.title}${s.required ? '' : ' (optional)'}` : '', `${c.title}${c.required ? '' : ' (optional)'}`, c.hint]));
  }

  for (const s of SHEETS) {
    const ws = wb.addWorksheet(s.title);
    ws.columns = s.columns.map(c => ({ header: c.title, key: c.key, width: Math.max(18, c.title.length + 6) }));
    const head = ws.getRow(1);
    head.font = { bold: true, color: { argb: 'FFFFFFFF' } };
    head.eachCell(cell => {
      cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF5D3DF5' } };
      const spec = s.columns.find(c => c.title === cell.text);
      if (spec) cell.note = spec.hint;
    });
    ws.views = [{ state: 'frozen', ySplit: 1 }];
    s.example.forEach(r => ws.addRow(r));
    if (s.key === 'unavailable') ws.columns.forEach((col, i) => { if (i >= 2) col.width = 20; });
  }
  return wb;
}
