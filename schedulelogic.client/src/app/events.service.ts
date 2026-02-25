import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';

export class EventsData{
  eventName: string = "";
  eventId: number = 0;
}

export class data{
  eventData: EventData = new EventData;
  eventTypes: EventType[] = [];
  groups: Group[] = [];
  locations: Location[] = [];
  participants: Participant[] = [];
  registrations: Registration[] = [];
  pauseTable: PauseTable[] = [];
  locationTable: LocationTable[] = [];
  constraints: Constraint[]= [];
  timeZones: TimeZone[] = [];
}

export class EventData{
  eventId: string = "0";
  eventName:string = "";
  startDate: string = new Date().toISOString();
  endDate: string = new Date().toISOString();
  isPrivate: boolean = true;
  basePauseTime: number = 0;
  locationPauseTime: number = 0;
  locWeight: number = 0;
  groupWeight: number = 0;
  typeWeight: number = 0;
  compWeight: number = 0;
}

export class EventType{
  eventTypeId: string = "";
  eventId: string = "0";
  typeName: string = "";
  timeRange: string = "";
}

export class Group{
  groupId: string = "";
  eventId: string = "0";
  groupName: string = "";
}

export class Location{
  locationId: string = "";
  eventId: string = "0";
  locationName: string = "";
  slots: number = 0;
}

export class Participant{
  participantId: string = "";
  eventId: string = "0";
  competitorNumber: number = 0;
  participantName: string = "";
  groupId: string = "";
}

export class Registration{
  registrationId: string = "";
  eventId: string = "0";
  participantId: string = "";
  eventTypeId: string = "";
}

export class PauseTable{
  pauseId: string = "";
  eventId: string = "0";
  locationId1: string = "";
  locationId2: string = "";;
  pause: string = "";
}

export class LocationTable{
  locationTableId: string = "";
  eventId: string = "0";
  eventTypeId: string = "";
  locationId: string = "";
}

export class Constraint{
  constraintId: string = "";
  eventId: string = "0";
  objectId: string = "";
  constraintType: string = "";
  startTime: string = "";
  endTime: string = "";
}

export class TimeZone {
  scheduleId: string = "";
  eventTypeId: string = "";
  participantId: string = "";
  locationId: string = "";
  startTime: number = 0;
  endTime: number = 0;
  slot: number = 0;
}

@Injectable({
  providedIn: 'root'
})

export class EventsService {
  constructor(private router: Router, private http: HttpClient) { }
  private apiUrl = `${environment.apiUrl}api/Event`;

  GetEvents(): Observable<any> {
    return this.http.get(`${this.apiUrl}/get-events`, { withCredentials: true });
  }

  NewEvent(): Observable<any> {
    return this.http.post(`${this.apiUrl}/new-event`,null, { withCredentials: true });
  }

  GetEvent(id: string): Observable<data> {
    const params = new HttpParams()
        .set('id', id)
    return this.http.get<data>(`${this.apiUrl}/get-event`, { params,withCredentials: true });
  }

  DeleteEvent(id: string): Observable<data> {
    const params = new HttpParams()
        .set('id', id)
    return this.http.delete<data>(`${this.apiUrl}/delete-event`, { params,withCredentials: true });
  }

  SaveEvent(data: data): Observable<any> {
    return this.http.post(`${this.apiUrl}/save-event`, data, { withCredentials: true });
  }
  
  NewWizardEvent(data: data): Observable<any> {
    return this.http.post(`${this.apiUrl}/new-wizard`, data, { withCredentials: true });
  }
  
}
