import { Injectable } from '@angular/core';
import { Observable, Subject } from 'rxjs';

export interface Alert {
  message: string;
  type: 'success' | 'error' | 'info' | 'warning';
  id?: number; 
}

@Injectable({
  providedIn: 'root',
})
export class AlertService {
  private alertSubject = new Subject<Alert>();
  private counter = 0;

  getAlerts(): Observable<Alert> {
    return this.alertSubject.asObservable();
  }

  show(message: string, type: Alert['type'] = 'info', duration = 3000) {
    const id = ++this.counter;
    const alert: Alert = { message, type, id };
    this.alertSubject.next(alert);

    setTimeout(() => {
      this.alertSubject.next({ ...alert, message: '', id }); 
    }, duration);
  }

  success(msg: string, duration?: number) { this.show(msg, 'success', duration); }
  error(msg: string, duration?: number) { this.show(msg, 'error', duration); }
  info(msg: string, duration?: number) { this.show(msg, 'info', duration); }
  warning(msg: string, duration?: number) { this.show(msg, 'warning', duration); }
}
