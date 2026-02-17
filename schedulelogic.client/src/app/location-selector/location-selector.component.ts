import { Component, Inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { EventType } from '@angular/router';
import { data, LocationTable } from '../events.service';

@Component({
  selector: 'app-location-selector',
  standalone: false,
  templateUrl: './location-selector.component.html',
  styleUrl: './location-selector.component.css'
})
export class LocationSelectorComponent {
constructor(
    public dialogRef: MatDialogRef<LocationSelectorComponent>,
    @Inject(MAT_DIALOG_DATA)  public data: { data: data; index: number }
  ) {}
  

  closeDialog() {
    this.dialogRef.close();
  }

  NewLocation()
  {
    if (!Array.isArray(this.data.data.locationTable)) {
      this.data.data.locationTable = [];
    }
    let temp: LocationTable = new LocationTable();
    temp.eventTypeId = this.data.index.toString();
    temp.locationTableId = crypto.randomUUID();
    temp.eventId = this.data.data.eventData?.eventId ?? 0;
    temp.locationId = "0";
    this.data.data.locationTable.push(temp);
  }

  getLocationsForEventType(eventTypeId: number) {
    return (this.data.data.locationTable || []).filter(
      x => x.eventTypeId === eventTypeId.toString()
    );
  }

  removeLocation(locationTableid: string){
    this.data.data.locationTable = this.data.data.locationTable.filter(w => w.locationTableId! != locationTableid);
  }
}
