import { Component, Inject } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { LocationSelectorComponent } from '../location-selector/location-selector.component';
import { data } from '../events.service';

@Component({
  selector: 'app-timezone-info',
  standalone: false,
  templateUrl: './timezone-info.component.html',
  styleUrl: './timezone-info.component.css'
})
export class TimezoneInfoComponent {
  constructor(
      public dialogRef: MatDialogRef<LocationSelectorComponent>,
      @Inject(MAT_DIALOG_DATA)  public data: { data: data; index: string }
    ) {}

  getLocationName(): string {
    const p = this.data.data.timeZones.find(p => p.scheduleId === this.data.index);
    const q = this.data.data.locations.find(q => q.locationId === p?.locationId.toString());
    return q ? q.locationName ?? '' : '';
  }

  getParticipantName(): string {
    const p = this.data.data.timeZones.find(p => p.scheduleId === this.data.index);
    const q = this.data.data.participants.find(q => q.participantId === p?.participantId.toString());
    return q ? q.participantName ?? '' : '';
  }

  getEventtypeName(): string {
    const p = this.data.data.timeZones.find(p => p.scheduleId === this.data.index);
    const q = this.data.data.eventTypes.find(q => q.eventTypeId === p?.eventTypeId.toString());
    return q ? q.typeName ?? '' : '';
  }

  getSlot(): number {
    const p = this.data.data.timeZones.find(p => p.scheduleId === this.data.index);
    return p ? p?.slot ?? 0 : 0;
  }

  getTimeZone(): string {
    const p = this.data.data.timeZones.find(p => p.scheduleId === this.data.index);
    const t = p?.startTime ?? 0;
    const t2 = p?.endTime ?? 0;
    return (Math.floor(t / 60)%24).toString() + ':' + (t % 60).toString().padStart(2, '0') + '-' + (Math.floor(t2 / 60)%24).toString() + ':' + (t2 % 60).toString().padStart(2, '0') ;
  }
}
