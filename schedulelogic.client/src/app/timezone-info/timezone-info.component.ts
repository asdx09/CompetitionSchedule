import { Component, Inject } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { data, TimeZone } from '../events.service';

@Component({
  selector: 'app-timezone-info',
  standalone: false,
  templateUrl: './timezone-info.component.html',
  styleUrl: './timezone-info.component.css'
})
export class TimezoneInfoComponent {
  constructor(
    public dialogRef: MatDialogRef<TimezoneInfoComponent>,
    @Inject(MAT_DIALOG_DATA) public data: { data: data; index: string }
  ) {
    this.entry = data.data.timeZones.find(t => String(t.scheduleId) === String(data.index));
  }

  entry?: TimeZone;

  get eventTypeName(): string {
    return this.data.data.eventTypes.find(t => String(t.eventTypeId) === String(this.entry?.eventTypeId))?.typeName ?? '';
  }

  get participantName(): string {
    const p = this.data.data.participants.find(p => String(p.participantId) === String(this.entry?.participantId));
    return p ? `${p.participantName} (#${p.competitorNumber})` : '';
  }

  get groupName(): string {
    const p = this.data.data.participants.find(p => String(p.participantId) === String(this.entry?.participantId));
    return this.data.data.groups.find(g => String(g.groupId) === String(p?.groupId))?.groupName ?? '';
  }

  get locationName(): string {
    const l = this.data.data.locations.find(l => String(l.locationId) === String(this.entry?.locationId));
    return l ? `${l.locationName}, lane ${this.entry?.slot}` : '';
  }

  get time(): string {
    if (!this.entry) return '';
    const day = new Date(this.data.data.eventData.startDate);
    day.setHours(0, 0, 0, 0);
    const start = new Date(day.getTime() + this.entry.startTime * 60000);
    const end = new Date(day.getTime() + this.entry.endTime * 60000);
    const t = (d: Date) => d.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' });
    return `${start.toLocaleDateString('en-GB', { weekday: 'short', day: 'numeric', month: 'short' })}, ${t(start)} – ${t(end)}`;
  }
}
