import { Component, ElementRef, ViewChild } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { ActivatedRoute, Router } from '@angular/router';
import { EventsService } from '../events.service';
import { ScheduleData, ScheduleService, ScheduleTimeZone } from '../schedule.service';
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
  data: ScheduleData = new ScheduleData();
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
          console.log(this.data);
          this.computeLanes();
          this.computeLocationLanes();
          const msPerDay = 1000 * 60 * 60 * 24;
          const end = new Date(this.data.endDate);
          const start = new Date(this.data.startDate);
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
            if (!this.data.timeZones || this.data.timeZones.length === 0) return;

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
    const hoursNumber = typeof hours === 'string' ? parseInt(hours, 10) : hours;
    const daysToAdd = Math.floor(hoursNumber / 24);
    dateObj.setDate(dateObj.getDate() + daysToAdd);
    return dateObj;
  }

  getEventTypeRange(eventTypeId: number) {
    const events = this.data.timeZones.filter(e => e.eventType_ID === eventTypeId);
    const start = Math.min(...events.map(e => e.startTime ?? 0));
    const end = Math.max(...events.map(e => e.endTime ?? 0));
    return { start, end };
  }

  getLocationRange(locationId: number) {
    const events = this.data.timeZones.filter(e => e.location_ID === locationId);
    const start = Math.min(...events.map(e => e.startTime ?? 0));
    const end = Math.max(...events.map(e => e.endTime ?? 0));
    return { start, end };
  }

  getLocationRangeTop(locationId: number): number {
    let top = 0;

    for (const loc of this.data.locations) {
      top += this.laneHeight;

      if (loc.location_ID === locationId) {
        return top;
      }

      if (this.expandedLocations[loc.location_ID ?? 0]) {
        top += this.laneHeight * (this.locationsLanes[loc.location_ID ?? 0].length);
      }
    }

    return top;
  }

  getLocationLaneTop(locationId: number, laneIndex: number): number {
    let top = 0; 

    for (const loc of this.data.locations) {
      top += this.laneHeight;

      if (loc.location_ID === locationId) {
        top += this.laneHeight * laneIndex;
        break;
      }

      if (this.expandedLocations[loc.location_ID ?? 0]) {
        top += this.laneHeight * (this.locationsLanes[loc.location_ID ?? 0].length);
      }
    }

    return top;
  }

  getTimeZonesForParticipant(participantID: number | null) {
    return this.data.timeZones.filter(tz => tz.participant_ID === participantID);
  }

  getTimeZonesForConstraint(objectIDs: number) {
    return this.data.constraints.filter(c => c.object_ID === objectIDs);
  }

  getTimeZonesForEventType(eventTypeID: number | null) {
    return this.data.timeZones.filter(tz => tz.eventType_ID === eventTypeID);
  }

  eventTypeColorMap: { [key: number]: string } = {};

  getEventTypeColor(eventTypeId: number | null): string {
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
    return this.data.constraints.filter(item => item.constraintType === type);
  }

  expandedEventTypes: { [key: number]: boolean } = {};

  toggleEventType(etId: number) {
    this.expandedEventTypes[etId] = !this.expandedEventTypes[etId];
  }

  private isSyncing = false;

  syncScroll(source: 'box1' | 'box2' | 'box3') {
    if (this.isSyncing) return;
    this.isSyncing = true;

    if (source === 'box1') {
      this.box2.nativeElement.scrollLeft = this.box1.nativeElement.scrollLeft;
      this.box3.nativeElement.scrollLeft = this.box1.nativeElement.scrollLeft;
    } else if (source === "box2")
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

  lanesByEventType: { [key: number]: { participantName: string, events: ScheduleTimeZone[] }[] } = {};

  locationsByGroup: {
    [key: number]: { participantName: string, events: ScheduleTimeZone[] }[]
  } = {};

  locationsLanes: {
    [key: number]: { events: ScheduleTimeZone[] }[]
  } = {};

  expandedLocations: { [key: number]: boolean } = {};

  toggleLocation(locId: number) {
    this.expandedLocations[locId] = !this.expandedLocations[locId];
  }

  getLaneGlobalIndex(eventTypeId: number | null, laneIndex: number): number {
    if (eventTypeId == null) return laneIndex;
    const lanes = this.lanesByEventType[eventTypeId] || [];
    let offset = 0;
    for (let et of this.data.eventTypes) {
      if (et.eventType_ID === eventTypeId) break;
      offset += (this.lanesByEventType[et.eventType_ID!] || []).length;
    }
    return offset + laneIndex;
  }


  getLaneTop(etId: number, laneIndex: number): number {
    let top = this.laneHeight; 
    for (let et of this.data.eventTypes) {
      if (et.eventType_ID === etId) {
        if (this.expandedEventTypes[etId]) {
          top += this.laneHeight * laneIndex; 
        }
        break;
      } else if (this.expandedEventTypes[et.eventType_ID!]) {
        top += this.laneHeight * (this.lanesByEventType[et.eventType_ID!]?.length || 0);
      }
      top += this.laneHeight; 
    }
    return top;
  }

  getEventTypeRangeTop(etId: number): number {
    let top = this.laneHeight; 
    for (let et of this.data.eventTypes) {
      if (et.eventType_ID === etId) break;
      top += this.laneHeight;
      if (this.expandedEventTypes[et.eventType_ID!]) {
        top += this.laneHeight * (this.lanesByEventType[et.eventType_ID!]?.length || 0);
      }
    }
    return top - (this.laneHeight / 2); 
  }

  getEventTypeRowIndex(eventTypeId: number): number {
    let rowIndex = 0;

    for (let et of this.data.eventTypes) {
      if (et.eventType_ID === eventTypeId) {
        return rowIndex;
      }
      const lanes = this.lanesByEventType[et.eventType_ID!] || [];
      rowIndex += lanes.length + 1; 
    }

    return 0; 
  }

  getParticipantName(participantId: number | null): string {
    if (participantId == null) return '';
    const p = this.data.participans.find(p => p.participant_ID === participantId);
    return p ? p.participantName ?? '' : '';
  }

  getEventTypeName(eventtypeid: number | null): string {
    if (eventtypeid == null) return '';
    const p = this.data.eventTypes.find(p => p.eventType_ID === eventtypeid);
    return p ? p.eventTypeName ?? '' : '';
  }

  getLocationName(eventtypeid: number | null): string {
    if (eventtypeid == null) return '';
    const p = this.data.timeZones.find(p => p.eventType_ID === eventtypeid);
    const q = this.data.locations.find(q => q.location_ID === p?.location_ID);
    return q ? q.locationName ?? '' : '';
  }

  getLocationName2(locationid: number | null): string {
    if (locationid == null) return '';
    const q = this.data.locations.find(q => q.location_ID === locationid)?.locationName;
    return q ?? '';
  }

  openInfo(i:number) {
      this.dialog.open(TimezoneInfoComponent, {
        panelClass: 'custom-dialog-radius',
        data: { data: this.data, index: i}
      });
  }

  hasRange(i:number)
  {
    return (this.data.timeZones.filter(tz => tz.location_ID == i).length>0)
  }

  computeLanes() {
    for (let et of this.data.eventTypes) {
      const events = this.data.timeZones
        .filter(e => e.eventType_ID === et.eventType_ID)
        .sort((a,b) => (a.startTime ?? 0) - (b.startTime ?? 0));

      const lanes: { participantName: string, events: ScheduleTimeZone[] }[] = [];

      for (let ev of events) {
        let placed = false;
        for (let lane of lanes) {
          if (!lane.events.some(e => !(e.endTime! <= ev.startTime! - 5 || e.startTime! >= ev.endTime! + 5))) {
            lane.events.push(ev);
            lane.participantName = this.data.participans.find(p => p.participant_ID === ev.participant_ID)!.participantName!;
            placed = true;
            break;
          }
        }
        if (!placed) {
          lanes.push({
            participantName: this.data.participans.find(p => p.participant_ID === ev.participant_ID)!.participantName!,
            events: [ev]
          });
        }
      }

      this.lanesByEventType[et.eventType_ID ?? 0] = lanes;
    }
  }

  computeLocationLanes() {
    this.locationsLanes = {};

    for (let loc of this.data.locations) {
      const locId = loc.location_ID!;

      const events = this.data.timeZones
        .filter(t => t.location_ID === locId)
        .sort((a, b) => (a.slot ?? 0) - (b.slot ?? 0)); 

      const lanes: { events: ScheduleTimeZone[] }[] = [];

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

      if (this.expandedLocations[loc.location_ID!]) {
        rows += (this.locationsLanes[loc.location_ID!]?.length ?? 0);
      }
    }

    return rows * this.laneHeight;
  }

  get totalByEventTypeHeight(): number {
    let rows = 1; 

    for (const et of this.data.eventTypes) {
      rows++;
      if (this.expandedEventTypes[et.eventType_ID!]) {
        rows += (this.lanesByEventType[et.eventType_ID!]?.length ?? 0);
      }
    }

    return rows * this.laneHeight;
  }

  get totalByCompetitorHeight(): number {
    let rows = 1; 

    for (const par of this.data.participans) {
      rows++; 
    }

    return rows * this.laneHeight;
  }

  export(){
    this.isExporting = true;
    console.log(this.isExporting);
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