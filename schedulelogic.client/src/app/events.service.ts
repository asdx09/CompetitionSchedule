import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';

export class EventsData{
  eventName: string = "";
  event_ID: number = 0;
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
  eventTypeId: string | null = null;
  eventId: string = "0";
  typeName: string | null = null;
  timeRange: string | null = null;
}

export class Group{
  groupId: string | null = null;
  eventId: string = "0";
  groupName: string | null = null;
}

export class Location{
  locationId: string | null = null;
  eventId: string = "0";
  locationName: string | null = null;
  slots: number | null = null;
}

export class Participant{
  participantId: string | null = null;
  eventId: string = "0";
  competitorNumber: number | null = null;
  participantName: string | null = null;
  groupId: string | null = null;
}

export class Registration{
  registrationId: string | null = null;
  eventId: string = "0";
  participantId: string | null = null;
  eventTypeId: string | null = null;
}

export class PauseTable{
  pauseId: string | null = null;
  eventId: string = "0";
  locationId1: string | null = null;
  locationId2: string | null = null;
  pause: string | null = null;
}

export class LocationTable{
  locationTableId: string | null = null;
  eventId: string = "0";
  eventTypeId: string | null = null;
  locationId: string | null = null;
}

export class Constraint{
  constraintId: string | null = null;
  eventId: string = "0";
  objectId: string | null = null;
  constraintType: string | null = null;
  startTime: string | null = null;
  endTime: string | null = null;
}

@Injectable({
  providedIn: 'root'
})

export class EventsService {
  constructor(private router: Router, private http: HttpClient) { }
  private apiUrl = `${environment.apiUrl}api/Event`;

  GetEvents(): Observable<any> {
    return this.http.get(`${this.apiUrl}/get-events`);
  }

  NewEvent(): Observable<any> {
    return this.http.post(`${this.apiUrl}/new-event`,null);
  }

  GetEvent(id: string): Observable<data> {
    const params = new HttpParams()
        .set('id', id)
    return this.http.get<data>(`${this.apiUrl}/get-event`, { params });
  }

  DeleteEvent(id: string): Observable<data> {
    const params = new HttpParams()
        .set('id', id)
    return this.http.delete<data>(`${this.apiUrl}/delete-event`, { params });
  }

  SaveEvent(data: data): Observable<any> {
    return this.http.post(`${this.apiUrl}/save-event`, data);
  }
  
  NewWizardEvent(data: data): Observable<any> {
    return this.http.post(`${this.apiUrl}/new-wizard`, data);
  }
  
}
