import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { Observable } from 'rxjs/internal/Observable';
import { environment } from '../environments/environment';

/*export class ScheduleData{
  timeZones: ScheduleTimeZone[] = [];
  eventID: number = 0;
  eventName: string = "";
  startDate: Date = new Date();
  endDate: Date = new Date();
  eventTypes: ScheduleEventType[] = [];
  participants: Scheduleparticipants[] = [];
  locations: ScheduleLocations[] = [];
  constraints: ScheduleConstraint[] = [];
}

export class ScheduleTimeZone {
  scheduleID: number = 0;
  eventTypeID: number = 0;
  participantID: number = 0;
  locationID: number = 0;
  startTime: number = 0;
  endTime: number = 0;
  slot: number = 0;
}


export class ScheduleConstraint{
  constraintId: number = 0;
  eventId: number = 0;
  objectID: number = 0;
  constraintType: string = "";
  startTime: number = 0;
  endTime: number = 0;
}


export class ScheduleEventType{
  eventTypeID: number = 0;
  eventTypeName: string = "";
}

export class Scheduleparticipants{
  participantID: number = 0;
  participantName: string = "";
}

export class ScheduleLocations{
  locationID: number = 0;
  locationName: string = "";
}*/

@Injectable({
  providedIn: 'root'
})
export class ScheduleService {

  constructor(private router: Router, private http: HttpClient) { }

  private apiUrl = `${environment.apiUrl}api/Schedule`;
  
    schedule(id: string): Observable<any> {
      return this.http.post(`${this.apiUrl}/`+id,null, { withCredentials: true });
    }

    scheduleGet(id: string): Observable<any> {
      return this.http.get(`${this.apiUrl}/`+id, { withCredentials: true });
    }

    checkSolver(id: string): Observable<any> {
      return this.http.get(`${this.apiUrl}/isRunning`, {
        params: { id },
        withCredentials: true
      });
    }

    StopSchedule(id: string): Observable<any> {
      return this.http.post(`${this.apiUrl}/stop?id=${id}`, null, { withCredentials: true });
    }

    downloadSchedule(id: string) {
      return this.http.get(
        `${this.apiUrl}/export`,
        {
          responseType: 'blob',
          params: { id },
          withCredentials: true
        }
      );
    }
}
