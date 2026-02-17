import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { Observable } from 'rxjs/internal/Observable';
import { environment } from '../environments/environment';

export class ScheduleData{
  timeZones: ScheduleTimeZone[] = [];
  event_ID: number = 0;
  eventName: string = "";
  startDate: Date = new Date();
  endDate: Date = new Date();
  eventTypes: ScheduleEventType[] = [];
  participans: ScheduleParticipans[] = [];
  locations: ScheduleLocations[] = [];
  constraints: ScheduleConstraint[] = [];
}

export class ScheduleTimeZone {
  schedule_ID: number = 0;
  eventType_ID: number = 0;
  participant_ID: number = 0;
  location_ID: number = 0;
  startTime: number = 0;
  endTime: number = 0;
  slot: number = 0;
}


export class ScheduleConstraint{
  constraintId: number = 0;
  eventId: number = 0;
  object_ID: number = 0;
  constraintType: string = "";
  startTime: number = 0;
  endTime: number = 0;
}


export class ScheduleEventType{
  eventType_ID: number = 0;
  eventTypeName: string = "";
}

export class ScheduleParticipans{
  participant_ID: number = 0;
  participantName: string = "";
}

export class ScheduleLocations{
  location_ID: number = 0;
  locationName: string = "";
}

@Injectable({
  providedIn: 'root'
})
export class ScheduleService {

  constructor(private router: Router, private http: HttpClient) { }

  private apiUrl = `${environment.apiUrl}api/Schedule`;
  
    schedule(id: string): Observable<any> {
      return this.http.post(`${this.apiUrl}/`+id,null);
    }

    scheduleGet(id: string): Observable<any> {
      return this.http.get(`${this.apiUrl}/`+id);
    }

    checkSolver(id: string): Observable<any> {
      return this.http.get(`${this.apiUrl}/isRunning`, {
        params: { id }
      });
    }

    StopSchedule(id: string): Observable<any> {
      return this.http.post(`${this.apiUrl}/stop?id=${id}`, {});
    }
}
