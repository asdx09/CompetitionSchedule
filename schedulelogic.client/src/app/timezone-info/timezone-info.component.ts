import { Component, Inject } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { LocationSelectorComponent } from '../location-selector/location-selector.component';
import { ScheduleData } from '../schedule.service';

@Component({
  selector: 'app-timezone-info',
  standalone: false,
  templateUrl: './timezone-info.component.html',
  styleUrl: './timezone-info.component.css'
})
export class TimezoneInfoComponent {
  constructor(
      public dialogRef: MatDialogRef<LocationSelectorComponent>,
      @Inject(MAT_DIALOG_DATA)  public data: { data: ScheduleData; index: number }
    ) {}

  getLocationName(): string {
    const p = this.data.data.timeZones.find(p => p.schedule_ID === this.data.index);
    const q = this.data.data.locations.find(q => q.location_ID === p?.location_ID);
    return q ? q.locationName ?? '' : '';
  }

  getParticipantName(): string {
    const p = this.data.data.timeZones.find(p => p.schedule_ID === this.data.index);
    const q = this.data.data.participans.find(q => q.participant_ID === p?.participant_ID);
    return q ? q.participantName ?? '' : '';
  }

  getEventtypeName(): string {
    const p = this.data.data.timeZones.find(p => p.schedule_ID === this.data.index);
    const q = this.data.data.eventTypes.find(q => q.eventType_ID === p?.eventType_ID);
    return q ? q.eventTypeName ?? '' : '';
  }

  getSlot(): number {
    const p = this.data.data.timeZones.find(p => p.schedule_ID === this.data.index);
    return p ? p?.slot ?? 0 : 0;
  }

  getTimeZone(): string {
    const p = this.data.data.timeZones.find(p => p.schedule_ID === this.data.index);
    const t = p?.startTime ?? 0;
    const t2 = p?.endTime ?? 0;
    return (Math.floor(t / 60)%24).toString() + ':' + (t % 60).toString().padStart(2, '0') + '-' + (Math.floor(t2 / 60)%24).toString() + ':' + (t2 % 60).toString().padStart(2, '0') ;
  }
}
