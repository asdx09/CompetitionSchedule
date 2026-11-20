import { Component, HostListener, OnDestroy } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { ActivatedRoute, Router } from '@angular/router';
import { data, TimeZone } from '../events.service';
import { isSolverActive, ScheduleService, solverStatusText } from '../schedule.service';
import { TimezoneInfoComponent } from '../timezone-info/timezone-info.component';
import { AlertService } from '../alert.service';
import { environment } from '../../environments/environment';
import { AuthGuardService } from '../auth-guard.service';
import { TimelineBlock, TimelineRow } from '../schedule-timeline/schedule-timeline.component';

const PALETTE = ['#7b5cff', '#22c3a6', '#f59e0b', '#ef4f6b', '#3b9dff', '#c084fc', '#84cc16', '#f97316', '#2dd4bf', '#ec4899', '#facc15', '#6366f1'];
const MIN_ZOOM = 0.2;
const MAX_ZOOM = 12;

interface LegendItem {
  id: string;
  name: string;
  color: string;
  count: number;
}

@Component({
  selector: 'app-schedule',
  standalone: false,
  templateUrl: './schedule.component.html',
  styleUrl: './schedule.component.css'
})
export class ScheduleComponent implements OnDestroy {
  constructor(private auth: AuthGuardService, private route: ActivatedRoute, private router: Router, public dialog: MatDialog, public scheduleService: ScheduleService, private alertService: AlertService) { }

  isMobile = false;
  loaded = false;
  id: string = "";
  data: data = new data();

  zoom = 2;
  showUnavailable = true;
  isExporting = false;

  startDate = new Date();
  totalMinutes = 1440;
  activeStart = 0;
  activeEnd = 1440;

  solverStatus: string | null = null;
  solverStatusAt: string | null = null;
  private statusTimer: ReturnType<typeof setInterval> | null = null;

  legend: LegendItem[] = [];
  participantRows: TimelineRow[] = [];
  locationRows: TimelineRow[] = [];

  ngOnInit() {
    this.auth.checkToken().subscribe({
      next: (res) => {
        this.auth.usernameSubject.next(res.name);
        this.route.queryParamMap.subscribe(params => {
          this.id = params.get('id') ?? "";
        });
        this.RefreshData();
      },
      error: () => {
        this.router.navigate(['login']);
      }
    });
    this.isMobile = window.innerWidth <= 768;
  }

  ngOnDestroy() {
    this.stopPolling();
  }

  @HostListener('window:resize')
  onResize() {
    this.isMobile = window.innerWidth <= 768;
  }

  get statusText(): string {
    return solverStatusText(this.solverStatus);
  }

  get hasSchedule(): boolean {
    return this.data.timeZones.length > 0;
  }

  get competitorCount(): number {
    return this.participantRows.length;
  }

  RefreshData() {
    this.refreshStatus();
    this.scheduleService.scheduleGet(this.id).subscribe({
      next: (res) => {
        if (res == null) {
          this.router.navigate(['home']);
          return;
        }
        this.data = res;
        this.buildTimeline();
        if (!this.loaded) this.zoom = this.fitZoom();
        this.loaded = true;
        const splash = document.getElementById('splash');
        if (splash) {
          splash.style.opacity = '0';
          splash.style.transition = '0.5s';
        }
      },
      error: (err) => {
        this.alertService.error("Something went wrong!");
        if (environment.production == false) { console.error('Get schedule error! ', err); }
        this.router.navigate(['home']);
      }
    });
  }

  private refreshStatus() {
    this.scheduleService.getStatus(this.id).subscribe({
      next: (res) => {
        const previous = this.solverStatus;
        this.solverStatus = res?.status ?? null;
        this.solverStatusAt = res?.updatedAt ?? null;
        if (isSolverActive(this.solverStatus)) {
          if (this.statusTimer === null) this.statusTimer = setInterval(() => this.refreshStatus(), 10000);
        } else {
          this.stopPolling();
        }
        if (this.loaded && previous !== this.solverStatus) this.RefreshData();
      }
    });
  }

  private stopPolling() {
    if (this.statusTimer === null) return;
    clearInterval(this.statusTimer);
    this.statusTimer = null;
  }

  private buildTimeline() {
    const start = new Date(this.data.eventData.startDate);
    const end = new Date(this.data.eventData.endDate);
    const midnight = new Date(start);
    midnight.setHours(0, 0, 0, 0);
    this.startDate = midnight;

    this.activeStart = Math.round((start.getTime() - midnight.getTime()) / 60000);
    this.activeEnd = Math.round((end.getTime() - midnight.getTime()) / 60000);
    const lastBlockEnd = Math.max(0, ...this.data.timeZones.map(t => t.endTime));
    const days = Math.max(1, Math.ceil(Math.max(this.activeEnd, lastBlockEnd) / 1440));
    this.totalMinutes = days * 1440;

    const typeColor = new Map<string, string>();
    this.data.eventTypes.forEach((t, i) => typeColor.set(String(t.eventTypeId), PALETTE[i % PALETTE.length]));
    const typeName = new Map(this.data.eventTypes.map(t => [String(t.eventTypeId), t.typeName]));
    const participants = new Map(this.data.participants.map(p => [String(p.participantId), p]));
    const groupName = new Map(this.data.groups.map(g => [String(g.groupId), g.groupName]));
    const locationName = new Map(this.data.locations.map(l => [String(l.locationId), l.locationName]));

    this.legend = this.data.eventTypes.map(t => ({
      id: String(t.eventTypeId),
      name: t.typeName,
      color: typeColor.get(String(t.eventTypeId))!,
      count: this.data.timeZones.filter(z => String(z.eventTypeId) === String(t.eventTypeId)).length
    })).filter(l => l.count > 0);

    const block = (tz: TimeZone, label: string): TimelineBlock => {
      const color = typeColor.get(String(tz.eventTypeId)) ?? '#94a3b8';
      const p = participants.get(String(tz.participantId));
      const type = typeName.get(String(tz.eventTypeId)) ?? '';
      const tooltip = [
        type,
        p ? `${p.participantName} (#${p.competitorNumber})` : '',
        `${locationName.get(String(tz.locationId)) ?? ''}, lane ${tz.slot}`,
        `${this.formatMinute(tz.startTime)} – ${this.formatMinute(tz.endTime, false)}`
      ].filter(x => x).join('\n');
      return { id: String(tz.scheduleId), start: tz.startTime, end: tz.endTime, label, color, textColor: this.textColorFor(color), tooltip };
    };

    const byParticipant = new Map<string, TimeZone[]>();
    const byLane = new Map<string, TimeZone[]>();
    for (const tz of this.data.timeZones) {
      const pk = String(tz.participantId);
      const lk = `${tz.locationId}|${tz.slot}`;
      if (!byParticipant.has(pk)) byParticipant.set(pk, []);
      if (!byLane.has(lk)) byLane.set(lk, []);
      byParticipant.get(pk)!.push(tz);
      byLane.get(lk)!.push(tz);
    }

    const constraintRange = (c: { startTime: any; endTime: any }) => ({
      start: this.minutesFromStart(c.startTime),
      end: this.minutesFromStart(c.endTime)
    });

    this.participantRows = this.data.participants
      .filter(p => byParticipant.has(String(p.participantId)))
      .sort((a, b) => a.competitorNumber - b.competitorNumber)
      .map(p => {
        const group = p.groupId ? groupName.get(String(p.groupId)) ?? '' : '';
        const sub = `#${p.competitorNumber}${group ? ' · ' + group : ''}`;
        return {
          key: String(p.participantId),
          name: p.participantName,
          sub,
          search: `${p.participantName} ${p.competitorNumber} ${group}`.toUpperCase(),
          blocks: byParticipant.get(String(p.participantId))!.map(tz => block(tz, typeName.get(String(tz.eventTypeId)) ?? '')),
          unavailable: this.data.constraints
            .filter(c => (c.constraintType === 'C' && String(c.objectId) === String(p.participantId))
              || (c.constraintType === 'G' && p.groupId && String(c.objectId) === String(p.groupId)))
            .map(constraintRange)
        };
      });

    this.locationRows = [];
    for (const loc of this.data.locations) {
      for (let slot = 1; slot <= loc.slots; slot++) {
        const lane = byLane.get(`${loc.locationId}|${slot}`);
        if (!lane) continue;
        this.locationRows.push({
          key: `${loc.locationId}|${slot}`,
          name: loc.locationName,
          sub: `Lane ${slot}`,
          search: `${loc.locationName} ${slot}`.toUpperCase(),
          blocks: lane.map(tz => {
            const p = participants.get(String(tz.participantId));
            return block(tz, p ? `#${p.competitorNumber} ${p.participantName}` : '');
          }),
          unavailable: this.data.constraints
            .filter(c => c.constraintType === 'L' && String(c.objectId) === String(loc.locationId))
            .map(constraintRange)
        });
      }
    }
  }

  private fitZoom(): number {
    const available = Math.min(window.innerWidth * 0.9, 1400) - (this.isMobile ? 120 : 180) - 40;
    const span = Math.max(60, Math.min(this.activeEnd - this.activeStart, 1440));
    return this.clampZoom(available / span);
  }

  private clampZoom(z: number): number {
    return Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, z));
  }

  zoomIn() { this.zoom = this.clampZoom(this.zoom * 1.25); }
  zoomOut() { this.zoom = this.clampZoom(this.zoom / 1.25); }
  zoomFit() { this.zoom = this.fitZoom(); }

  private minutesFromStart(value: string | Date): number {
    return Math.round((new Date(value).getTime() - this.startDate.getTime()) / 60000);
  }

  private formatMinute(minute: number, withDay = true): string {
    const d = new Date(this.startDate.getTime() + minute * 60000);
    const time = d.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' });
    if (!withDay || this.totalMinutes <= 1440) return time;
    return `${d.toLocaleDateString('en-GB', { day: 'numeric', month: 'short' })} ${time}`;
  }

  private textColorFor(hex: string): string {
    const n = parseInt(hex.slice(1), 16);
    const r = (n >> 16) & 255, g = (n >> 8) & 255, b = n & 255;
    return (0.299 * r + 0.587 * g + 0.114 * b) > 150 ? '#111827' : '#ffffff';
  }

  openInfo(id: string) {
    this.dialog.open(TimezoneInfoComponent, {
      panelClass: 'custom-dialog-radius',
      data: { data: this.data, index: id }
    });
  }

  backToEvent() {
    this.router.navigate(['event'], { queryParams: { id: this.id } });
  }

  export() {
    this.isExporting = true;
    this.scheduleService.downloadSchedule(this.id).subscribe({
      next: blob => {
        this.isExporting = false;
        const a = document.createElement('a');
        const objectUrl = URL.createObjectURL(blob);
        a.href = objectUrl;
        a.download = `${this.data.eventData.eventName || 'schedule'}.xlsx`;
        a.click();
        URL.revokeObjectURL(objectUrl);
      },
      error: (err) => {
        this.isExporting = false;
        this.alertService.error("The download failed.");
        if (environment.production == false) { console.error('Export error! ', err); }
      }
    });
  }
}
