import { Component } from '@angular/core';
import { Alert, AlertService } from '../alert.service';

@Component({
  selector: 'app-alert',
  standalone: false,
  templateUrl: './alert.component.html',
  styleUrl: './alert.component.css',
})
export class AlertComponent {
  alerts: Alert[] = [];

  constructor(private alertService: AlertService) {}

  ngOnInit() {
    this.alertService.getAlerts().subscribe(alert => {
      if (alert.message) {
        this.alerts.push(alert);
      } else {
        this.alerts = this.alerts.filter(a => a.id !== alert.id);
      }
    });
  }
}
