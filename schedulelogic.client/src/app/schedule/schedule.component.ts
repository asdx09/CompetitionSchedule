import { Component, ElementRef, ViewChild } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { ActivatedRoute, Router } from '@angular/router';
import { Constraint, data, EventsData, EventsService, TimeZone } from '../events.service';
import { ScheduleService } from '../schedule.service';
import { TimezoneInfoComponent } from '../timezone-info/timezone-info.component';
import html2canvas from 'html2canvas';
import { box } from '@igniteui/material-icons-extended';
import { AlertService } from '../alert.service';
import { environment } from '../../environments/environment';

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
  @ViewChild('box1') box1!: ElementRef<HTMLDivElement>;
  @ViewChild('box2') box2!: ElementRef<HTMLDivElement>;
  @ViewChild('box3') box3!: ElementRef<HTMLDivElement>;
  constructor(private eventsService: EventsService, private route: ActivatedRoute, private router: Router, public dialog: MatDialog, public scheduleService: ScheduleService , private alertService: AlertService) { }
  id: string = "";
  data: data = new data();
  zoomLevel = 1;
  laneHeight = 45;
  isConstraint = false;
  isExporting = false;

  ngOnInit()
  {
    this.route.queryParamMap.subscribe(params => {
        this.id = params.get('id') ?? "";
      });
    this.RefreshData();
  }

  RefreshData(){
    this.scheduleService.scheduleGet(this.id).subscribe({
      next: (res) => {
        if(res == null) this.router.navigate(['home']);
          this.data = res;
          this.computeLanes();
          this.computeLocationLanes();
          const msPerDay = 1000 * 60 * 60 * 24;
          const end = new Date(this.data.eventData.endDate);
          const start = new Date(this.data.eventData.startDate);
          start.setHours(0, 0, 0, 0);
          end.setHours(23, 59, 59, 999);
          const diffInMs = end.getTime() - start.getTime();
          const diffInDays = diffInMs / msPerDay;
          this.hours = Array.from({length: Math.ceil(diffInDays)*24},(_,i)=> i);
          this.zoomLevel = Math.max(((diffInDays*24*60)/(Math.max(...this.data.timeZones.map(item => item.endTime)) - Math.min(...this.data.timeZones.map(item => item.startTime))))/8, 1/diffInDays);
          const splash = document.getElementById('splash');
          if (splash) {
            splash.style.opacity = '0';
            splash.style.transition = '0.5s';
          }
          setTimeout(() => {
            if (!this.data.timeZones || this.data.timeZones.length == 0) return;

            const startTime = Math.min(...this.data.timeZones.map(item => item.startTime)); 
            const endTime = Math.max(...this.data.timeZones.map(item => item.endTime));    

            const containerWidth = this.box1.nativeElement.clientWidth;
            const contentWidth = this.box1.nativeElement.scrollWidth;
            const middleTime = (startTime + endTime) / 2;

            const targetPosition  = middleTime/(diffInDays*24*60) * contentWidth;
            const scrollLeft = targetPosition - containerWidth / 2;

            this.box1.nativeElement.scrollLeft = scrollLeft;
            this.box2.nativeElement.scrollLeft = scrollLeft;
            this.box3.nativeElement.scrollLeft = scrollLeft;
          }, 100);
      },
      error: (err) => {
        this.alertService.error("Something went wrong!");
        if (environment.production == false) 
        {console.error('Get schedule error! ', err)}
        this.router.navigate(['home']);
      }
    });
  }

  getDateWithPlusDays(tempDate: string | Date, hours: string | number): Date {
    const dateObj = new Date(tempDate); 
    const hoursNumber = typeof hours == 'string' ? parseInt(hours, 10) : hours;
    const daysToAdd = Math.floor(hoursNumber / 24);
    dateObj.setDate(dateObj.getDate() + daysToAdd);
    return dateObj;
  }

  getEventTypeRange(eventTypeId: string) {
    const events = this.data.timeZones.filter(e => e.eventTypeId == eventTypeId);
    const start = Math.min(...events.map(e => e.startTime ?? 0));
    const end = Math.max(...events.map(e => e.endTime ?? 0));
    return { start, end };
  }

  getLocationRange(locationId: string) {
    const events = this.data.timeZones.filter(e => e.locationId == locationId);
    const start = Math.min(...events.map(e => e.startTime ?? 0));
    const end = Math.max(...events.map(e => e.endTime ?? 0));
    return { start, end };
  }

  getLocationRangeTop(locationId: string): number {
    let top = 0;

    for (const loc of this.data.locations) {
      top += this.laneHeight;

      if (loc.locationId == locationId) {
        return top;
      }

      if (this.expandedLocations[loc.locationId]) {
        top += this.laneHeight * (this.locationsLanes[loc.locationId].length);
      }
    }

    return top;
  }

  getLocationLaneTop(locationId: string, laneIndex: number): number {
    let top = 0; 

    for (const loc of this.data.locations) {
      top += this.laneHeight;

      if (loc.locationId == locationId) {
        top += this.laneHeight * laneIndex;
        break;
      }

      if (this.expandedLocations[loc.locationId]) {
        top += this.laneHeight * (this.locationsLanes[loc.locationId].length);
      }
    }

    return top;
  }

  getTimeZonesForParticipant(participantId: string): TimeZone[] {
    return this.data.timeZones.filter(tz => tz.participantId == participantId);
  }

  getTimeZonesForConstraint(objectIds: string): Constraint[] {
    return this.data.constraints.filter(c => c.objectId == objectIds);
  }

  getTimeZonesForEventType(eventTypeId: string): TimeZone[] {
    return this.data.timeZones.filter(tz => tz.eventTypeId == eventTypeId);
  }

  getNumberFromDate(_date: string)
  {
    const date = new Date(_date);
    const startDate = new Date(this.data.eventData.startDate);
    startDate.setHours(0,0,0);
    return Math.floor((date.getTime() - startDate.getTime()) / 60000);
  }

  eventTypeColorMap: { [key: string]: string } = {};

  getEventTypeColor(eventTypeId: string): string {
    if (eventTypeId == null) return '#aaa';
    if (!this.eventTypeColorMap[eventTypeId]) {
      const r = Math.floor(Math.random() * 200 + 30); 
      const g = Math.floor(Math.random() * 200 + 30);
      const b = Math.floor(Math.random() * 200 + 30);
      const hex = '#' + [r, g, b].map(x => x.toString(16).padStart(2, '0')).join('');
      this.eventTypeColorMap[eventTypeId] = hex;
    }
    return this.eventTypeColorMap[eventTypeId];
  }

  getFilteredConstraints(type:string)
  {
    return this.data.constraints.filter(item => item.constraintType == type);
  }

  expandedEventTypes: { [key: string]: boolean } = {};

  toggleEventType(etId: string) {
    this.expandedEventTypes[etId] = !this.expandedEventTypes[etId];
  }

  private isSyncing = false;

  syncScroll(source: 'box1' | 'box2' | 'box3') {
    if (this.isSyncing) return;
    this.isSyncing = true;

    if (source == 'box1') {
      this.box2.nativeElement.scrollLeft = this.box1.nativeElement.scrollLeft;
      this.box3.nativeElement.scrollLeft = this.box1.nativeElement.scrollLeft;
    } else if (source == "box2")
    {
      this.box1.nativeElement.scrollLeft = this.box2.nativeElement.scrollLeft;
      this.box3.nativeElement.scrollLeft = this.box2.nativeElement.scrollLeft;
    } else {
      this.box2.nativeElement.scrollLeft = this.box3.nativeElement.scrollLeft;
      this.box1.nativeElement.scrollLeft = this.box3.nativeElement.scrollLeft;
    }

    this.isSyncing = false;
  }

  startOfDay = 0;
  endOfDay = 1440;  
  eventHeight = 45;
  hours = [0,0];

  lanesByEventType: { [key: string]: { participantName: string, events: TimeZone[] }[] } = {};

  locationsByGroup: {
    [key: string]: { participantName: string, events: TimeZone[] }[]
  } = {};

  locationsLanes: {
    [key: string]: { events: TimeZone[] }[]
  } = {};

  expandedLocations: { [key: string]: boolean } = {};

  toggleLocation(locId: string) {
    this.expandedLocations[locId] = !this.expandedLocations[locId];
  }

  getLaneGlobalIndex(eventTypeId: string, laneIndex: number): number {
    if (eventTypeId == null) return laneIndex;
    const lanes = this.lanesByEventType[eventTypeId] || [];
    let offset = 0;
    for (let et of this.data.eventTypes) {
      if (et.eventTypeId == eventTypeId) break;
      offset += (this.lanesByEventType[et.eventTypeId] || []).length;
    }
    return offset + laneIndex;
  }


  getLaneTop(etId: string, laneIndex: number): number {
    let top = this.laneHeight; 
    for (let et of this.data.eventTypes) {
      if (et.eventTypeId == etId) {
        if (this.expandedEventTypes[etId]) {
          top += this.laneHeight * laneIndex; 
        }
        break;
      } else if (this.expandedEventTypes[et.eventTypeId]) {
        top += this.laneHeight * (this.lanesByEventType[et.eventTypeId]?.length || 0);
      }
      top += this.laneHeight; 
    }
    return top;
  }

  getEventTypeRangeTop(etId: string): number {
    let top = this.laneHeight; 
    for (let et of this.data.eventTypes) {
      if (et.eventTypeId == etId) break;
      top += this.laneHeight;
      if (this.expandedEventTypes[et.eventTypeId]) {
        top += this.laneHeight * (this.lanesByEventType[et.eventTypeId]?.length || 0);
      }
    }
    return top - (this.laneHeight / 2); 
  }

  getEventTypeRowIndex(eventTypeId: string): number {
    let rowIndex = 0;

    for (let et of this.data.eventTypes) {
      if (et.eventTypeId == eventTypeId) {
        return rowIndex;
      }
      const lanes = this.lanesByEventType[et.eventTypeId] || [];
      rowIndex += lanes.length + 1; 
    }

    return 0; 
  }

  getParticipantName(participantId: string): string {
    if (participantId == null) return '';
    const p = this.data.participants.find(p => p.participantId == participantId);
    return p ? p.participantName ?? '' : '';
  }

  getEventTypeName(eventtypeid: string): string {
    if (eventtypeid == null) return '';
    const p = this.data.eventTypes.find(p => p.eventTypeId == eventtypeid);
    return p ? p.typeName ?? '' : '';
  }

  getLocationName(eventtypeid: string): string {
    if (eventtypeid == null) return '';
    const p = this.data.timeZones.find(p => p.eventTypeId == eventtypeid);
    const q = this.data.locations.find(q => q.locationId == p?.locationId);
    return q ? q.locationName ?? '' : '';
  }

  getLocationName2(locationId: string): string {
    if (locationId == null) return '';
    const q = this.data.locations.find(q => q.locationId == locationId)?.locationName;
    return q ?? '';
  }

  openInfo(i:string) {
      this.dialog.open(TimezoneInfoComponent, {
        panelClass: 'custom-dialog-radius',
        data: { data: this.data, index: i}
      });
  }

  hasRange(i:string)
  {
    return (this.data.timeZones.filter(tz => tz.locationId == i).length>0)
  }

  computeLanes() {
    for (let et of this.data.eventTypes) {
      const events = this.data.timeZones
        .filter(e => e.eventTypeId == et.eventTypeId)
        .sort((a,b) => (a.startTime ?? 0) - (b.startTime ?? 0));

      const lanes: { participantName: string, events: TimeZone[] }[] = [];

      for (let ev of events) {
        let placed = false;
        for (let lane of lanes) {
          if (!lane.events.some(e => !(e.endTime! <= ev.startTime! - 5 || e.startTime! >= ev.endTime! + 5))) {
            lane.events.push(ev);
            lane.participantName = this.data.participants.find(p => p.participantId == ev.participantId)!.participantName!;
            placed = true;
            break;
          }
        }
        if (!placed) {
          lanes.push({
            participantName: this.data.participants.find(p => p.participantId == ev.participantId)!.participantName!,
            events: [ev]
          });
        }
      }

      this.lanesByEventType[et.eventTypeId] = lanes;
    }
  }

  computeLocationLanes() {
    this.locationsLanes = {};

    for (let loc of this.data.locations) {
      const locId = loc.locationId!;

      const events = this.data.timeZones
        .filter(t => t.locationId == locId)
        .sort((a, b) => (a.slot ?? 0) - (b.slot ?? 0)); 

      const lanes: { events: TimeZone[] }[] = [];

      for (let ev of events) {
        const slotIndex = (ev.slot ?? 1) - 1; 
        while (lanes.length <= slotIndex) {
          lanes.push({ events: [] });
        }
        lanes[slotIndex].events.push(ev);
      }

      this.locationsLanes[locId] = lanes;
    }
  }


  get totalByLocationHeight(): number {
    let rows = 1;

    for (const loc of this.data.locations) {
      rows++; 

      if (this.expandedLocations[loc.locationId]) {
        rows += (this.locationsLanes[loc.locationId]?.length ?? 0);
      }
    }

    return rows * this.laneHeight;
  }

  get totalByEventTypeHeight(): number {
    let rows = 1; 

    for (const et of this.data.eventTypes) {
      rows++;
      if (this.expandedEventTypes[et.eventTypeId]) {
        rows += (this.lanesByEventType[et.eventTypeId]?.length ?? 0);
      }
    }

    return rows * this.laneHeight;
  }

  get totalByCompetitorHeight(): number {
    let rows = 1; 

    for (const par of this.data.participants) {
      rows++; 
    }

    return rows * this.laneHeight;
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