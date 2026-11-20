import { Component } from '@angular/core';
import * as ExcelJS from 'exceljs';
import { data, EventData, EventsService } from '../events.service';
import { Router } from '@angular/router';
import { AlertService } from '../alert.service';
import { environment } from '../../environments/environment';
import { AuthGuardService } from '../auth-guard.service';
import { buildTemplate, ImportIssue, parseWorkbook } from './wizard-import';

@Component({
  selector: 'app-wizard',
  standalone: false,
  templateUrl: './wizard.component.html',
  styleUrl: './wizard.component.css'
})
export class WizardComponent {
  constructor(private auth: AuthGuardService, private eventsService: EventsService, private router: Router, private alertService: AlertService) {};

  Data: data = new data;
  selection: number = 0;
  fileName = '';
  errors: ImportIssue[] = [];
  warnings: ImportIssue[] = [];
  reading = false;
  generating = false;

  ngOnInit()
  {
    this.auth.checkToken().subscribe({
      next: (res) => {
        this.auth.usernameSubject.next(res.name);
        this.Data.eventData = new EventData;
        const splash = document.getElementById('splash');
        if (splash) {
          splash.style.opacity = '0';
          splash.style.transition = '0.5s';
        }
      },
      error: (err) => {
        this.router.navigate(['login']);
      }
    });
  }

  Next() {
    if (this.selection === 2 && this.Data.eventData.eventName.trim() === '') {
      this.alertService.warning('Please give the competition a name.');
      return;
    }
    if (this.selection === 3 && new Date(this.Data.eventData.endDate) <= new Date(this.Data.eventData.startDate)) {
      this.alertService.warning('The end of the competition must be later than its start.');
      return;
    }
    this.selection += 1;
  }

  Back() {
    this.selection = Math.max(0, this.selection - 1);
  }

  StartOver() {
    this.selection = 0;
  }

  async DownloadTemplate() {
    const buffer = await buildTemplate().xlsx.writeBuffer();
    const blob = new Blob([buffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'competition-template.xlsx';
    a.click();
    URL.revokeObjectURL(url);
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.files || input.files.length !== 1) return;
    const file = input.files[0];
    input.value = '';

    if (!file.name.toLowerCase().endsWith('.xlsx')) {
      this.alertService.error('Please choose an Excel file (.xlsx). Older .xls files must be saved as .xlsx first.', 6000);
      return;
    }
    if (file.size > 5 * 1024 * 1024) {
      this.alertService.error('The file is too large (max. 5 MB).');
      return;
    }

    this.reading = true;
    const reader = new FileReader();
    reader.onload = async () => {
      try {
        const workbook = new ExcelJS.Workbook();
        await workbook.xlsx.load(reader.result as ArrayBuffer);
        const result = parseWorkbook(workbook);
        this.fileName = file.name;
        this.errors = result.errors;
        this.warnings = result.warnings;
        const eventData = this.Data.eventData;
        this.Data = result.data;
        this.Data.eventData = eventData;
        if (this.Data.eventData.eventName.trim() === '') {
          this.Data.eventData.eventName = file.name.replace(/\.xlsx$/i, '').replace(/[_-]+/g, ' ').trim();
        }
        this.selection = 1;
      } catch (err) {
        this.alertService.error('The file could not be opened as an Excel workbook. Save it again as .xlsx and retry.', 6000);
        if (environment.production == false) { console.error('Excel parsing error! ', err); }
      } finally {
        this.reading = false;
      }
    };
    reader.onerror = () => {
      this.reading = false;
      this.alertService.error('The file could not be read.');
    };
    reader.readAsArrayBuffer(file);
  }

  issueText(issue: ImportIssue): string {
    return issue.row ? `${issue.sheet}, row ${issue.row}: ${issue.message}` : `${issue.sheet}: ${issue.message}`;
  }

  get outsideConstraints(): number {
    const start = new Date(this.Data.eventData.startDate).getTime();
    const end = new Date(this.Data.eventData.endDate).getTime();
    return this.Data.constraints.filter(c => new Date(c.endTime).getTime() <= start || new Date(c.startTime).getTime() >= end).length;
  }

  Generate()
  {
    if (this.errors.length > 0 || this.generating) return;
    this.generating = true;
    this.eventsService.NewWizardEvent(this.Data).subscribe({
      next: () => {
        this.generating = false;
        this.alertService.success('The competition has been created.');
        this.router.navigate(['home']);
      },
      error: (err) => {
        this.generating = false;
        this.alertService.error('The server could not create the competition. Please try again.', 6000);
        if (environment.production == false) { console.error('Error at creating wizard template!', err); }
      }
    });
  }

  getDateString(date?: Date | string | null): string {
    const d = !date ? new Date() : typeof date === 'string' ? new Date(date) : date;
    const y = d.getFullYear();
    const m = (d.getMonth() + 1).toString().padStart(2, '0');
    const day = d.getDate().toString().padStart(2, '0');
    return `${y}-${m}-${day}`;
  }

  getTimeString(date?: Date | string | null): string {
    const d = !date ? new Date() : typeof date === 'string' ? new Date(date) : date;
    const h = d.getHours().toString().padStart(2, '0');
    const m = d.getMinutes().toString().padStart(2, '0');
    return `${h}:${m}`;
  }

  updateDate(item: any, newDate: string, field: string) {
    const time = this.getTimeString(item[field]);
    item[field] = new Date(`${newDate}T${time}`).toISOString();
  }

  updateTime(item: any, newTime: string, field: string) {
    const date = this.getDateString(item[field]);
    item[field] = new Date(`${date}T${newTime}`).toISOString();
  }
}
