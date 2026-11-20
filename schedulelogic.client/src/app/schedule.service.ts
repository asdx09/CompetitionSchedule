import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { Observable } from 'rxjs/internal/Observable';
import { environment } from '../environments/environment';

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
