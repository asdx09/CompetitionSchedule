import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { Observable } from 'rxjs/internal/Observable';
import { environment } from '../environments/environment';

export interface SolverStatusInfo {
  status: string | null;
  updatedAt: string | null;
}

const SOLVER_STATUS_TEXT: { [status: string]: string } = {
  QUEUED: 'Waiting for the solver to start…',
  RUNNING: 'Searching… a schedule has been found and is being improved.',
  OPTIMAL: 'Optimal schedule found.',
  FEASIBLE: 'Schedule found. The time limit was reached, so a better one may exist.',
  INFEASIBLE: 'No schedule is possible with these settings. Try a longer competition, more lanes or fewer unavailable times.',
  UNKNOWN: 'The time limit was reached before any schedule was found.',
  MODEL_INVALID: 'The data cannot be scheduled, for example an event is longer than the whole competition.',
  STOPPED: 'Stopped. The best schedule found until then is kept.',
  MEMORY_LIMIT: 'Stopped: the competition is too large for the solver. The best schedule found until then is kept.',
  ERROR: 'The solver failed. Please try again.',
};

export function solverStatusText(status: string | null | undefined): string {
  return status ? (SOLVER_STATUS_TEXT[status] ?? status) : 'The schedule has not been generated yet.';
}

export function isSolverActive(status: string | null | undefined): boolean {
  return status === 'QUEUED' || status === 'RUNNING';
}

export function isSolverSuccess(status: string | null | undefined): boolean {
  return status === 'OPTIMAL' || status === 'FEASIBLE';
}

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

    getStatus(id: string): Observable<SolverStatusInfo> {
      return this.http.get<SolverStatusInfo>(`${this.apiUrl}/status`, {
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
