import { ChangeDetectorRef, Component, ElementRef, HostListener, QueryList, ViewChild, ViewChildren } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { ActivatedRoute, Router } from '@angular/router';
import { Constraint, data, EventsData, EventsService, TimeZone } from '../events.service';
import { ScheduleService } from '../schedule.service';
import { TimezoneInfoComponent } from '../timezone-info/timezone-info.component';
import html2canvas from 'html2canvas';
import { box } from '@igniteui/material-icons-extended';
import { AlertService } from '../alert.service';
import { environment } from '../../environments/environment';
import { AuthGuardService } from '../auth-guard.service';

interface TimeRange {
  start: number;
  end: number;  
  color: string; 
  label: string; 
}

@Component({
  selector: 'app-schedule',
  standalone: false,
  templateUrl: './schedule.component.html',
  styleUrl: './schedule.component.css'
})
export class ScheduleComponent {
  constructor(private auth: AuthGuardService, private eventsService: EventsService, private route: ActivatedRoute, private router: Router, public dialog: MatDialog, public scheduleService: ScheduleService , private alertService: AlertService) { }
  
  isMobile = false;

  id: string = "";
  data: data = new data();

  zoomLevel = 5;
  laneHeight = 45;
  isConstraint = true;
  isExporting = false;

  rowWidth: number = 150;
  rowWidth2: number = 150;

  ColorMap: { [key: string]: string } = {};

  hours: number[] = [];
  startMin = 0;
  startOfDay = 0;
  endOfDay = 1440;  

  participantFilter = "";
  locationFilter = "";


  ngOnInit()
  {
     this.auth.checkToken().subscribe({
       next: (res) => {
        this.auth.usernameSubject.next(res.name);
        this.route.queryParamMap.subscribe(params => {
          this.id = params.get('id') ?? "";
        });
        this.RefreshData();
       },
      error: (err) => {
        this.router.navigate(['login']);
      }
    });
    this.isMobile = window.innerWidth <= 768;
  }

  @HostListener('window:resize', ['$event'])
  onResize(event: Event) {
    this.isMobile = window.innerWidth <= 768;
  }

  startResize(event: MouseEvent | TouchEvent) {
    event.preventDefault();

    const startX = event instanceof MouseEvent ? event.clientX : event.touches[0].clientX;
    const startWidth = this.rowWidth;

    const onMove = (e: MouseEvent | TouchEvent) => {
      const currentX = e instanceof MouseEvent ? e.clientX : e.touches[0].clientX;
      this.rowWidth = Math.max(50, startWidth + (currentX - startX));
    };

    const onEnd = () => {
      document.removeEventListener('mousemove', onMove);
      document.removeEventListener('mouseup', onEnd);
      document.removeEventListener('touchmove', onMove);
      document.removeEventListener('touchend', onEnd);
    };

    document.addEventListener('mousemove', onMove);
    document.addEventListener('mouseup', onEnd);
    document.addEventListener('touchmove', onMove, { passive: false });
    document.addEventListener('touchend', onEnd);
  }

  startResize2(event: MouseEvent | TouchEvent) {
    event.preventDefault();

    const startX = event instanceof MouseEvent ? event.clientX : event.touches[0].clientX;
    const startWidth = this.rowWidth2;

    const onMove = (e: MouseEvent | TouchEvent) => {
      const currentX = e instanceof MouseEvent ? e.clientX : e.touches[0].clientX;
      this.rowWidth2 = Math.max(50, startWidth + (currentX - startX));
    };

    const onEnd = () => {
      document.removeEventListener('mousemove', onMove);
      document.removeEventListener('mouseup', onEnd);
      document.removeEventListener('touchmove', onMove);
      document.removeEventListener('touchend', onEnd);
    };

    document.addEventListener('mousemove', onMove);
    document.addEventListener('mouseup', onEnd);
    document.addEventListener('touchmove', onMove, { passive: false });
    document.addEventListener('touchend', onEnd);
  }

  RefreshData(){
    this.scheduleService.scheduleGet(this.id).subscribe({
      next: (res) => {
        if(res == null) this.router.navigate(['home']);
          this.data = res;
          this.makeCalendars();
          this.refreshData();
          const splash = document.getElementById('splash');
          if (splash) {
            splash.style.opacity = '0';
            splash.style.transition = '0.5s';
          }
      },
      error: (err) => {
        this.alertService.error("Something went wrong!");
        if (environment.production == false) 
        {console.error('Get schedule error! ', err)}
        this.router.navigate(['home']);
      }
    });
  }

  makeCalendars()
  {
    const msPerDay = 1000 * 60 * 60 * 24;
    const end = new Date(this.data.eventData.endDate);
    const start = new Date(this.data.eventData.startDate);
    this.startMin = start.getHours() * 60 + start.getMinutes();
    start.setHours(0, 0, 0, 0);
    end.setHours(23, 59, 59, 999);
    const diffInMs = end.getTime() - start.getTime();
    const diffInDays = diffInMs / msPerDay;
    this.hours = Array.from({ length: diffInDays*24 + 1 }, (_, i) => i);
  }


  openInfo(id:string) {
      this.dialog.open(TimezoneInfoComponent, {
        panelClass: 'custom-dialog-radius',
        data: { data: this.data, index: id}
      });
  }



  //Get CAL-1

  getParticipants()
  {
    let groups = this.data.groups.filter(f => f.groupName.toUpperCase().includes(this.participantFilter.toUpperCase()));
    return this.data.participants.filter( reg => (reg.participantName.toUpperCase().includes(this.participantFilter.toUpperCase()) || groups.some(g => g.groupId == reg.groupId)) && this.getTimeZonesByParticipant(reg.participantId).length>0)
  }

  getTimeZonesByParticipant(id: String)
  {
    return this.data.timeZones.filter(t => t.participantId == id);
  }

  getConstraintsByParticipant(id: String)
  {
    return this.data.constraints.filter(t => t.constraintType == "C" &&t.objectId == id);
  }

  getParticipantNameByID(id: String)
  {
    let par = this.data.participants.filter(t => t.participantId == id);
    return par[0].participantName;
  }

  getEventTypeNameByID(id: String)
  {
    let par = this.data.eventTypes.filter(t => t.eventTypeId == id);
    return par[0].typeName;
  }

  getColorForId(id: string): string {
    if (id == null) return '#aaa';
    if (!this.ColorMap[id]) {
      const r = Math.floor(Math.random() * 200 + 30); 
      const g = Math.floor(Math.random() * 200 + 30);
      const b = Math.floor(Math.random() * 200 + 30);
      const hex = '#' + [r, g, b].map(x => x.toString(16).padStart(2, '0')).join('');
      this.ColorMap[id] = hex;
    }
    return this.ColorMap[id];
  }

  getGroupName(id:string)
  {
    let par = this.data.participants.filter(t => t.participantId == id);
    let groupID = par[0].groupId;
    let group = this.data.groups.filter(t => t.groupId == groupID);
    if (group[0] != null) return " ("+ group[0].groupName + ")";
    else return "";
  }

  getNumberFromDate(_date: string)
  {
    const date = new Date(_date);
    const startDate = new Date(this.data.eventData.startDate);
    startDate.setHours(0, 0, 0, 0); 
    const diffMs = date.getTime() - startDate.getTime(); 
    return ((diffMs/1000)/60); 
  }

  //Get CAL-2
  
  locRows: LocRow[] = [];  

  refreshData()
  {
    this.locRows = [];
    this.data.locations.filter( reg => reg.locationName.toUpperCase().includes(this.locationFilter.toUpperCase())).forEach(loc => {
      for (let i = 1; i <= loc.slots; i++) { 
        const row = new LocRow();
        row.locationId = loc.locationId;
        row.slot = i;
        row.title = loc.locationName + ": " + i;
        if (this.getTimeZonesByLocation(row.locationId,row.slot).length>0) this.locRows.push(row);
      }
    });
  }

  getTimeZonesByLocation(id: String, slot: number)
  {
    return this.data.timeZones.filter(t => t.locationId == id && t.slot == slot);
  }

  getConstraintsByLocation(id: String)
  {
    return this.data.constraints.filter(t => t.constraintType == "L" &&t.objectId == id);
  }

  export(){
    this.isExporting = true;
    this.scheduleService.downloadSchedule(this.id)
    .subscribe(blob => {
      this.isExporting = false;
      const fileName = `schedule.xlsx`;

      const a = document.createElement('a');
      const objectUrl = URL.createObjectURL(blob);

      a.href = objectUrl;
      a.download = fileName;
      a.click();

      URL.revokeObjectURL(objectUrl);
    });
  }
  
}

class LocRow{
  title = "";
  locationId: string = "";
  slot: number = 0;
}
