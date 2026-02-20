import { Component } from '@angular/core';
import { Constraint, EventsService, EventType, Group, Location, Participant, PauseTable, Registration } from '../events.service';
import { ActivatedRoute, Router } from '@angular/router';
import { data } from '../events.service';
import { MatDialog } from '@angular/material/dialog';
import { LocationSelectorComponent } from '../location-selector/location-selector.component';
import { ScheduleService } from '../schedule.service';
import { HostListener } from '@angular/core';
import { DateRange } from 'igniteui-angular';
import { environment } from '../../environments/environment';
import { AlertService } from '../alert.service';

@Component({
  selector: 'app-event',
  standalone: false,
  templateUrl: './event.component.html',
  styleUrl: './event.component.css'
})
export class EventComponent {
  constructor(private eventsService: EventsService, private route: ActivatedRoute, private router: Router, public dialog: MatDialog, public scheduleService: ScheduleService, private alertService: AlertService) { }

  id: string = "";
  data: data = new data();
  condition: number = 0;
  loading: boolean = false;
  isRunning: boolean = false;
  isSolving: boolean = false;
  types = [
    { code: 'L', label: 'Location' },
    { code: 'G', label: 'Group' },
    { code: 'C', label: 'Competitor' },
    { code: 'T', label: 'Eventtype' }
  ];
  dropdowns: boolean[] = [false,false,false,false,false,false,false];

  public range: DateRange = { start: new Date(), end: new Date(new Date().setDate(new Date().getDate() + 5)) };

  switchDropdown(i:number){
    if(this.dropdowns[i]) this.dropdowns[i] = false;
    else this.dropdowns[i] = true;
  }

  ngOnInit(){
    this.route.queryParamMap.subscribe(params => {
        this.id = params.get('id') ?? "";
      });
    this.RefreshData();
  }

  @HostListener('window:beforeunload', ['$event'])
  unloadNotification($event: BeforeUnloadEvent) {
    if (this.condition === 2) {
      $event.preventDefault();
      $event.returnValue = ''; 
    }
  }

  RefreshData(){
    this.eventsService.GetEvent(this.id).subscribe({
      next: (res) => {
        if(res == null) this.router.navigate(['home']);
        this.data = res;
        const splash = document.getElementById('splash');
        if (splash) {
          splash.style.opacity = '0';
          splash.style.transition = '0.5s';
        }
      },
      error: (err) => {
        this.alertService.error("Something went wrong!");
        if (environment.production == false) 
        {console.error('GetEvent error!', err);}
      }
    });
    this.CheckSolver();
  }


  //NEW OBJECTS ----------------------------------------------------------------------

  NewEventType()
  {
    let temp:EventType = new EventType();
    temp.eventTypeId = crypto.randomUUID();
    temp.eventId = this.data.eventData?.eventId ?? -1;
    temp.typeName = "New EventType";
    temp.timeRange = '00:05';
    this.data.eventTypes.push(temp);
    this.condition = 2;
  }

  NewGroup()
  {
    let temp:Group = new Group();
    temp.groupId = crypto.randomUUID();
    temp.eventId = this.data.eventData?.eventId ?? -1;
    temp.groupName = "New Group";
    this.data.groups.push(temp);
    this.condition = 2;
  }

  NewLocation()
  {
    let temp: Location = new Location();
    temp.locationId = crypto.randomUUID();
    temp.eventId = this.data.eventData?.eventId ?? -1;
    temp.locationName = "New Location";
    temp.slots = 12;
    this.data.locations.push(temp);
    this.condition = 2;
  }

  NewParticipant()
  {
    let temp:Participant = new Participant();
    temp.participantId = crypto.randomUUID();
    temp.eventId = this.data.eventData?.eventId ?? -1;
    temp.participantName = "New Participant";

    if (this.data.participants.length === 0) {
        temp.competitorNumber = 1;
    } else {
        const maxNumber = Math.max(...this.data.participants.map(p => p.competitorNumber || 0));
        temp.competitorNumber = maxNumber + 1;
    }

    this.data.participants.push(temp);
    this.condition = 2;
  }

  NewRegistration()
  {
    let temp:Registration = new Registration();
    temp.eventId = this.data.eventData?.eventId ?? -1;
    temp.registrationId = crypto.randomUUID();
    this.data.registrations.push(temp);
    this.condition = 2;
  }

  NewConstraint()
  {
    let temp:Constraint = new Constraint();
    temp.eventId = this.data.eventData?.eventId ?? -1;
    temp.constraintType = 'P';
    temp.constraintId = crypto.randomUUID();
    temp.startTime = this.data.eventData?.startDate ?? new Date();
    temp.endTime = this.data.eventData?.endDate ?? new Date();
    this.data.constraints.push(temp);
    this.condition = 2;
  }

  NewPause()
  {
    let temp:PauseTable = new PauseTable();
    temp.eventId = this.data.eventData?.eventId ?? -1;
    temp.locationId1 = "0";
    temp.locationId2 = "0";
    temp.pause = "00:05";
    temp.pauseId = crypto.randomUUID();
    this.data.pauseTable.push(temp);
    this.condition = 2;
  }

  //REMOVE OBJECTS -----------------------------------------------------------------------

  removeEventType(index: number) {
    this.data.registrations = this.data.registrations.filter( reg => reg.eventTypeId !== this.data.eventTypes[index].eventTypeId );

    this.data.locationTable = this.data.locationTable.filter( reg => reg.eventTypeId !== this.data.eventTypes[index].eventTypeId );
    this.data.eventTypes.splice(index, 1);
    this.condition = 2;
  }

  removeGroup(index: number) {
    const groupId = this.data.groups[index].groupId;
    this.data.participants.forEach(reg => {
      if (reg.groupId === groupId) {
        reg.groupId = null;
      }
    });
    this.data.groups.splice(index, 1);
    this.condition = 2;
  }

  removeLocation(index: number) {
    this.data.locationTable = this.data.locationTable.filter( reg => reg.locationId !== this.data.locationTable[index].locationId );
    this.data.locations.splice(index, 1);
    this.condition = 2;
  }

  removeParticipant(index: number) {
    const participantId = this.data.participants[index].participantId;
    this.data.registrations.forEach(reg => {
      if (reg.participantId === participantId) {
        reg.participantId = null;
      }
    });
    this.data.participants.splice(index, 1);
    this.condition = 2;
  }

  removeRegistration(index: number) {
    this.data.registrations.splice(index, 1);
    this.condition = 2;
  }

  removeConstraint(index: number) {
    this.data.constraints.splice(index, 1);
    this.condition = 2;
  }

   removePause(index: number) {
    this.data.pauseTable.splice(index, 1);
    this.condition = 2;
  }

  //GET OBJECTS -----------------------------------------------------------------------

  search_EventTypeName = "";
  GetEventTypes()
  {
    return this.data.eventTypes.filter( reg => reg.typeName!.toUpperCase().includes(this.search_EventTypeName.toUpperCase()));
  }

  search_GroupName = "";
  GetGroups()
  {
    return this.data.groups.filter( reg => reg.groupName!.toUpperCase().includes(this.search_GroupName.toUpperCase()));
  }

  search_LocationName = "";
  search_LocationSlotCount = 0;
  GetLocations()
  {
    return this.data.locations.filter( reg => reg.locationName!.toUpperCase().includes(this.search_LocationName.toUpperCase()) && reg.slots! >= this.search_LocationSlotCount);
  }

  search_PauseTable = null;
  GetPauseTable()
  {
    if (this.search_PauseTable != null) return this.data.pauseTable.filter( reg => reg.locationId1! == this.search_PauseTable || reg.locationId2! == this.search_PauseTable );
    else return this.data.pauseTable;
  }

  search_ParticipantName = "";
  search_participantGroup = null;
  GetParticipants()
  {
    if (this.search_participantGroup != null) return this.data.participants.filter( reg => reg.participantName!.toUpperCase().includes(this.search_ParticipantName.toUpperCase()) && reg.groupId! == this.search_participantGroup);
    else return this.data.participants.filter( reg => reg.participantName!.toUpperCase().includes(this.search_ParticipantName.toUpperCase()))
  }

  search_RegistrationCompetitor = null;
  search_RegistrationEventType = null;
  GetRegistrations()
  {
    if (this.search_RegistrationEventType != null && this.search_RegistrationCompetitor != null) return this.data.registrations.filter( reg => reg.eventTypeId! == this.search_RegistrationEventType && reg.participantId! == this.search_RegistrationCompetitor);
    else if (this.search_RegistrationEventType != null) return this.data.registrations.filter( reg => reg.eventTypeId! == this.search_RegistrationEventType);
    else if (this.search_RegistrationCompetitor != null) return this.data.registrations.filter( reg => reg.participantId! == this.search_RegistrationCompetitor);
    else return this.data.registrations;
  }

  search_ConstraintType = null;
  search_ConstraintObject = null;
  GetConstraints()
  {
    if (this.search_ConstraintType != null && this.search_ConstraintObject != null) return this.data.constraints.filter( reg => reg.constraintType! == this.search_ConstraintType && reg.objectId! == this.search_ConstraintObject);
    else if (this.search_ConstraintType != null) return this.data.constraints.filter( reg => reg.constraintType! == this.search_ConstraintType);
    else return this.data.constraints;
  }

  //SAVE -----------------------------------------------------------------------

  SaveEvent()
  {
    if (confirm("Current solution will be deleted. Are you sure?")) {
      this.loading = true;
      this.eventsService.SaveEvent(this.data).subscribe({
        next: (res) => {
          this.RefreshData();
          this.condition = 0;
          this.loading = false;
        },
        error: (err) => {
          this.alertService.error("Something went wrong!");
          if (environment.production == false) 
          {console.error('Error at saving!', err);}
          this.condition = 1;
          this.loading = false;
        }
      });
    }
  }

  //CHECK OBJECTS -----------------------------------------------------------------------

  isDuplicateNumber(number: number): boolean {
    if (!number) return false;
    return this.data.participants
      .filter(p => p.competitorNumber === number).length > 1;
  }

  isBetween(min: number, max: number, number: number):boolean{
    if(number>=min && number<=max) return true;
    else return false;
  }

  openLocationSelector(i:string) {
    this.dialog.open(LocationSelectorComponent, {
      width: '500px',
      panelClass: 'custom-dialog-container',
      data: { data: this.data, index: i}
    });
  }

  CheckSolver()
  {
    this.scheduleService.checkSolver(this.id).subscribe({
      next: (res) => {
        this.isRunning = true;
        this.isSolving = res;
      },
      error: (err) => {
        this.alertService.error("Solver not found!");
        if (environment.production == false) 
        {console.error('Error at checking!', err);}
        this.isRunning = false;
      }
    });
  }

  get gradient() {
    let values: number[] = [this.data.eventData.locWeight,this.data.eventData.typeWeight,this.data.eventData.groupWeight,this.data.eventData.compWeight];
    const total = values.reduce((a,b)=>a+b,0);

    let current = 0;
    const colors = ['#2E145D','#0C1443','#1C1853','#04122D'];

    let result = 'conic-gradient(';

    values.forEach((value, i) => {
      const start = current;
      current += (value / total) * 100;
      result += `${colors[i]} ${start}% ${current}%,`;
    });

    result = result.slice(0, -1) + ')';
    return result;
  }

  GenerateSchedule()
  {
    if (!this.isSolving)
    {
      this.scheduleService.schedule(this.data.eventData?.eventId).subscribe({
        next: (res) => {
          this.isSolving = true;
        },
        error: (err) => {
          this.alertService.error("Something went wrong!");
          if (environment.production == false) 
          {console.error('Error at start solving! ', err);}
        }
      });
    }
    else
    {
      this.scheduleService.StopSchedule(this.id).subscribe({
        next: (res) => {
          this.isSolving = false;
        },
        error: (err) => {
          this.alertService.error("Something went wrong!");
          if (environment.production == false) 
          {console.error('Error at stop solving! ', err);}
        }
      });
    }
    
  }

  // DATE conversions
  getDateString(date?: Date | string | null): string {
    let d: Date;

    if (!date) {
      d = new Date();
    } else if (typeof date === 'string') {
      d = new Date(date);
    } else {
      d = date;
    }

    const y = d.getFullYear();
    const m = (d.getMonth() + 1).toString().padStart(2, '0');
    const day = d.getDate().toString().padStart(2, '0');

    return `${y}-${m}-${day}`; // HELYI dátum
  }

  getTimeString(date?: Date | string | null): string {
    let d: Date;

    if (!date) {
      d = new Date();
    } else if (typeof date === 'string') {
      d = new Date(date);
    } else {
      d = date;
    }

    const h = d.getHours().toString().padStart(2, '0');
    const m = d.getMinutes().toString().padStart(2, '0');

    return `${h}:${m}`;
  }

  updateDate(item: any, newDate: string, field: string) {
    const time = item[field]?.split('T')[1] ?? '00:00';
    item[field] = `${newDate}T${time}`;
  }

  updateTime(item: any, newTime: string, field: string) {
    const date = item[field]?.split('T')[0] ?? '2025-01-01';
    item[field] = `${date}T${newTime}`;
  }

  GoToSolution() {
     this.router.navigate(['schedule'],{ queryParams: { id: this.id } });
  }


}