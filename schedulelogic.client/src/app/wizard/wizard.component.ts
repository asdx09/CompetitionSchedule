import { Component } from '@angular/core';
import * as ExcelJS from 'exceljs';
import { Constraint, EventData, EventsData, EventsService, EventType, Group, Location, LocationTable, Participant, PauseTable, Registration } from '../events.service';
import { data } from '../events.service';
import { timeInterval } from 'rxjs';
import { Router } from '@angular/router';
import { AlertService } from '../alert.service';
import { environment } from '../../environments/environment';

@Component({
  selector: 'app-wizard',
  standalone: false,
  templateUrl: './wizard.component.html',
  styleUrl: './wizard.component.css'
})
export class WizardComponent {
  constructor(private eventsService: EventsService, private router: Router , private alertService: AlertService){};
  Data: data  = new data;
  selection: number = 0;
  workbook = new ExcelJS.Workbook();
  sheetNames: string[] = [];

  selectedLocationSheet = null;
  locationSheetColumns: string[] = [];
  location_SelectedIdColumn = null;
  location_SelectedNameColumn = null;
  location_SelectedSlotColumn = null;

  selectedEventTypeSheet = null;
  eventTypeSheetColumns: string[] = [];
  eventType_SelectedIdColumn = null;
  eventType_SelectedNameColumn = null;
  eventType_SelectedTimeColumn = null;
  eventType_SelectedLocationsColumn = null;

  selectedGroupSheet = null;
  groupSheetColumns: string[] = [];
  group_SelectedIdColumn = null;
  group_SelectedNameColumn = null;

  selectedParticipantSheet = null;
  participantSheetColumns: string[] = [];
  participant_SelectedIdColumn = null;
  participant_SelectedNameColumn = null;
  participant_SelectedNumberColumn = null;
  participant_SelectedGroupColumn = null;

  selectedRegistrationSheet = null;
  registrationSheetColumns: string[] = [];
  registration_SelectedIdColumn = null;
  registration_SelectedParticipantColumn = null;
  registration_SelectedEventTypeColumn = null;

  selectedConstraintSheet = null;
  constraintSheetColumns: string[] = [];
  constraint_SelectedIdColumn = null;
  constraint_SelectedObjectColumn = null;
  constraint_SelectedTypeColumn = null;
  constraint_SelectedStartColumn = null;
  constraint_SelectedEndColumn = null;

  selectedTravelTimeSheet = null;
  travelTimeSheetColumns: string[] = [];
  travelTime_SelectedIdColumn = null;
  travelTime_SelectedLocation1Column = null;
  travelTime_SelectedLocation2Column = null;
  travelTime_SelectedPauseColumn = null;

  ngOnInit()
  {
    this.Data.eventData = new EventData;
    const splash = document.getElementById('splash');
    if (splash) {
      splash.style.opacity = '0';
      splash.style.transition = '0.5s';
    }
  }

  Next(except: number = 0){
    if(except == 1 && (
      this.selectedLocationSheet == null || 
      this.location_SelectedIdColumn == null || 
      this.location_SelectedNameColumn == null || 
      this.location_SelectedSlotColumn == null
    )) return;
    if(except == 2 && (
      this.selectedEventTypeSheet == null || 
      this.eventType_SelectedIdColumn == null || 
      this.eventType_SelectedNameColumn == null || 
      this.eventType_SelectedLocationsColumn == null ||
      this.eventType_SelectedTimeColumn == null 
    )) return;
    if(except == 3 && (
      this.selectedGroupSheet == null || 
      this.group_SelectedIdColumn == null || 
      this.group_SelectedNameColumn == null
    )) return;
    if(except == 4 && (
      this.selectedParticipantSheet == null || 
      this.participant_SelectedIdColumn == null || 
      this.participant_SelectedNameColumn == null || 
      this.participant_SelectedNumberColumn == null || 
      this.participant_SelectedGroupColumn == null
    )) return;
    if(except == 5 && (
      this.selectedRegistrationSheet == null || 
      this.registration_SelectedIdColumn == null || 
      this.registration_SelectedParticipantColumn == null || 
      this.registration_SelectedEventTypeColumn == null
    )) return;
    if(except == 6 && (
      this.selectedConstraintSheet == null || 
      this.constraint_SelectedIdColumn == null || 
      this.constraint_SelectedTypeColumn == null || 
      this.constraint_SelectedObjectColumn == null || 
      this.constraint_SelectedStartColumn == null || 
      this.constraint_SelectedEndColumn == null 
    )) return;
    if(except == 7 && (
      this.selectedTravelTimeSheet == null || 
      this.travelTime_SelectedIdColumn == null || 
      this.travelTime_SelectedLocation1Column == null || 
      this.travelTime_SelectedLocation2Column == null || 
      this.travelTime_SelectedPauseColumn == null
    )) return;
    this.selection += 1;
    if(this.selection < 0) this.selection = 0;
  }
  Back(){
    this.selection -= 1;
  }

  onLocationSheetSelected(event: Event): void {
    try {
        this.locationSheetColumns = [];
        const worksheet = this.workbook.getWorksheet(this.selectedLocationSheet!);
        const headerRow = worksheet!.getRow(1);
        headerRow.eachCell((cell, colNumber) => {
          this.locationSheetColumns.push(cell.text.trim());
        });

      } catch (err) {
        this.alertService.error("Something went wrong with locations!");
        if (environment.production == false) 
        {console.error('Location header error!  ', err)}
      }
  }

  onEventTypeSheetSelected(event: Event): void {
    try {
        this.eventTypeSheetColumns = [];
        const worksheet = this.workbook.getWorksheet(this.selectedEventTypeSheet!);
        const headerRow = worksheet!.getRow(1);
        headerRow.eachCell((cell, colNumber) => {
          this.eventTypeSheetColumns.push(cell.text.trim());
        });

      } catch (err) {
        this.alertService.error("Something went wrong with event types!");
        if (environment.production == false) 
        {console.error('EventType header error! ', err);}
      }
  }

  onGroupSheetSelected(event: Event): void {
    try {
        this.groupSheetColumns = [];
        const worksheet = this.workbook.getWorksheet(this.selectedGroupSheet!);
        const headerRow = worksheet!.getRow(1);
        headerRow.eachCell((cell, colNumber) => {
          this.groupSheetColumns.push(cell.text.trim());
        });

      } catch (err) {
        this.alertService.error("Something went wrong with groups!");
        if (environment.production == false) 
        {console.error('Group header error! ', err);}
        
      }
  }

  onParticipantSheetSelected(event: Event): void {
    try {
        this.participantSheetColumns = [];
        const worksheet = this.workbook.getWorksheet(this.selectedParticipantSheet!);
        const headerRow = worksheet!.getRow(1);
        headerRow.eachCell((cell, colNumber) => {
          this.participantSheetColumns.push(cell.text.trim());
        });

      } catch (err) {
        this.alertService.error("Something went wrong with participants!");
        if (environment.production == false) 
        { console.error('Participant header error! ', err);}
      }
  }

  onRegistrationSheetSelected(event: Event): void {
    try {
        this.registrationSheetColumns = [];
        const worksheet = this.workbook.getWorksheet(this.selectedRegistrationSheet!);
        const headerRow = worksheet!.getRow(1);
        headerRow.eachCell((cell, colNumber) => {
          this.registrationSheetColumns.push(cell.text.trim());
        });

      } catch (err) {
        this.alertService.error("Something went wrong with registrations!");
        if (environment.production == false) 
        {  console.error('Registration header error', err);}
      }
  }

  onConstraintSheetSelected(event: Event): void {
    try {
        this.constraintSheetColumns = [];
        const worksheet = this.workbook.getWorksheet(this.selectedConstraintSheet!);
        const headerRow = worksheet!.getRow(1);
        headerRow.eachCell((cell, colNumber) => {
          this.constraintSheetColumns.push(cell.text.trim());
        });

      } catch (err) {
        this.alertService.error("Something went wrong with constraints!");
        if (environment.production == false) 
        {  console.error('Constraint header error! ', err);}
        
      }
  }

  onPauseTableSheetSelected(event: Event): void {
    try {
        this.travelTimeSheetColumns = [];
        const worksheet = this.workbook.getWorksheet(this.selectedTravelTimeSheet!);
        const headerRow = worksheet!.getRow(1);
        headerRow.eachCell((cell, colNumber) => {
          this.travelTimeSheetColumns.push(cell.text.trim());
        });

      } catch (err) {
        this.alertService.error("Something went wrong with pause times!");
        if (environment.production == false) 
        {  console.error('Pause header error! ', err);}
      }
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;

    if (!input.files || input.files.length !== 1) {
      return;
    }

    const file = input.files[0];

    if (file.size > 2 * 1024 * 1024) {
      this.alertService.error("File is too large!");
      if (environment.production == false) 
      {  console.error('File is too large! ');}
      return;
    }

    const reader = new FileReader();

    reader.onload = async () => {
      try {
        const arrayBuffer = reader.result as ArrayBuffer;

        
        await this.workbook.xlsx.load(arrayBuffer);

        this.sheetNames = this.workbook.worksheets.map(ws => ws.name);

        this.selection = 1;

      } catch (err) {
        this.alertService.error("Excel parsing error!");
        if (environment.production == false) 
        {  console.error('Excel parsing error! ', err);}
      }
    };

    reader.readAsArrayBuffer(file);
  }

  Generate()
  {
    let currentWorksheet = this.workbook.getWorksheet(this.selectedLocationSheet!);
    if (currentWorksheet != null)
    {
      let headerRow = currentWorksheet.getRow(1); 
      let headerIndexMap: { [key: string]: number } = {};

      headerRow.eachCell((cell, colNumber) => {
        const mapping = this.locationSheetColumns.find(m => m === cell.text.trim());
        if (mapping) headerIndexMap[mapping] = colNumber;
      });

      currentWorksheet.eachRow((row, rowNumber) => {
        if (rowNumber === 1) return;

        let tempLocation = new Location;
        tempLocation.locationId = row.getCell(headerIndexMap[this.location_SelectedIdColumn!]).text;
        tempLocation.locationName = row.getCell(headerIndexMap[this.location_SelectedNameColumn!]).text;
        tempLocation.slots = (Number)(row.getCell(headerIndexMap[this.location_SelectedSlotColumn!]).text);

        this.Data.locations.push(tempLocation);
      });
    }

    currentWorksheet = this.workbook.getWorksheet(this.selectedEventTypeSheet!);
    if (currentWorksheet != null)
    {
      let headerRow = currentWorksheet.getRow(1); 
      let headerIndexMap: { [key: string]: number } = {};

      headerRow.eachCell((cell, colNumber) => {
        const mapping = this.eventTypeSheetColumns.find(m => m === cell.text.trim());
        if (mapping) headerIndexMap[mapping] = colNumber;
      });

      currentWorksheet.eachRow((row, rowNumber) => {
        if (rowNumber === 1) return;

        let tempEventType = new EventType;
        tempEventType.eventTypeId = row.getCell(headerIndexMap[this.eventType_SelectedIdColumn!]).text;
        tempEventType.timeRange = row.getCell(headerIndexMap[this.eventType_SelectedTimeColumn!]).text;
        tempEventType.typeName = row.getCell(headerIndexMap[this.eventType_SelectedNameColumn!]).text;
        console.log(row.getCell(headerIndexMap[this.eventType_SelectedLocationsColumn!]).text.split(';'));
        row.getCell(headerIndexMap[this.eventType_SelectedLocationsColumn!]).text.split(';').forEach(element => {
          let tempLocationTable = new LocationTable;
          tempLocationTable.eventTypeId = (row.getCell(headerIndexMap[this.eventType_SelectedIdColumn!]).text);
          tempLocationTable.locationId = (element);
          let id = Math.max(0,...this.Data.locationTable.map(item => { const val = item.locationTableId;if (val === null || val === undefined || val === '') return 1;return Number(val)+1;}));
          tempLocationTable.locationTableId = (String)(id);
          this.Data.locationTable.push(tempLocationTable);
        });


        this.Data.eventTypes.push(tempEventType);
      });
    }


    currentWorksheet = this.workbook.getWorksheet(this.selectedGroupSheet!);
    if (currentWorksheet != null)
    {
      let headerRow = currentWorksheet.getRow(1); 
      let headerIndexMap: { [key: string]: number } = {};

      headerRow.eachCell((cell, colNumber) => {
        const mapping = this.groupSheetColumns.find(m => m === cell.text.trim());
        if (mapping) headerIndexMap[mapping] = colNumber;
      });

      currentWorksheet.eachRow((row, rowNumber) => {
        if (rowNumber === 1) return;

        let tempGroup = new Group;
        tempGroup.groupId = row.getCell(headerIndexMap[this.group_SelectedIdColumn!]).text;
        tempGroup.groupName = row.getCell(headerIndexMap[this.group_SelectedNameColumn!]).text;

        this.Data.groups.push(tempGroup);
      });
    }

    currentWorksheet = this.workbook.getWorksheet(this.selectedParticipantSheet!);
    if (currentWorksheet != null)
    {
      let headerRow = currentWorksheet.getRow(1); 
      let headerIndexMap: { [key: string]: number } = {};

      headerRow.eachCell((cell, colNumber) => {
        const mapping = this.participantSheetColumns.find(m => m === cell.text.trim());
        if (mapping) headerIndexMap[mapping] = colNumber;
      });

      currentWorksheet.eachRow((row, rowNumber) => {
        if (rowNumber === 1) return;

        let tempParticipant = new Participant;
        tempParticipant.participantId = row.getCell(headerIndexMap[this.participant_SelectedIdColumn!]).text;
        tempParticipant.participantName = row.getCell(headerIndexMap[this.participant_SelectedNameColumn!]).text;
        tempParticipant.groupId = (row.getCell(headerIndexMap[this.participant_SelectedGroupColumn!]).text);
        tempParticipant.competitorNumber = (Number)(row.getCell(headerIndexMap[this.participant_SelectedNumberColumn!]).text);
        this.Data.participants.push(tempParticipant);
      });
    }


    currentWorksheet = this.workbook.getWorksheet(this.selectedRegistrationSheet!);
    if (currentWorksheet != null)
    {
      let headerRow = currentWorksheet.getRow(1); 
      let headerIndexMap: { [key: string]: number } = {};

      headerRow.eachCell((cell, colNumber) => {
        const mapping = this.registrationSheetColumns.find(m => m === cell.text.trim());
        if (mapping) headerIndexMap[mapping] = colNumber;
      });

      currentWorksheet.eachRow((row, rowNumber) => {
        if (rowNumber === 1) return;

        let tempRegistration = new Registration;
        tempRegistration.eventTypeId = (row.getCell(headerIndexMap[this.registration_SelectedEventTypeColumn!]).text);
        tempRegistration.participantId = (row.getCell(headerIndexMap[this.registration_SelectedParticipantColumn!]).text);
        tempRegistration.registrationId = (row.getCell(headerIndexMap[this.registration_SelectedIdColumn!]).text);
        this.Data.registrations.push(tempRegistration);
      });
    }

    currentWorksheet = this.workbook.getWorksheet(this.selectedConstraintSheet!);
    if (currentWorksheet != null)
    {
      let headerRow = currentWorksheet.getRow(1); 
      let headerIndexMap: { [key: string]: number } = {};

      headerRow.eachCell((cell, colNumber) => {
        const mapping = this.constraintSheetColumns.find(m => m === cell.text.trim());
        if (mapping) headerIndexMap[mapping] = colNumber;
      });

      currentWorksheet.eachRow((row, rowNumber) => {
        if (rowNumber === 1) return;

        let tempConstraint= new Constraint;
        tempConstraint.constraintId = (row.getCell(headerIndexMap[this.constraint_SelectedIdColumn!]).text);
        tempConstraint.constraintType = (row.getCell(headerIndexMap[this.constraint_SelectedTypeColumn!]).text);
        tempConstraint.objectId = (row.getCell(headerIndexMap[this.constraint_SelectedObjectColumn!]).text);
        tempConstraint.startTime = new Date(row.getCell(headerIndexMap[this.constraint_SelectedStartColumn!]).text).toISOString();
        tempConstraint.endTime = new Date(row.getCell(headerIndexMap[this.constraint_SelectedEndColumn!]).text).toISOString();
        this.Data.constraints.push(tempConstraint);
      });
    }

    currentWorksheet = this.workbook.getWorksheet(this.selectedTravelTimeSheet!);
    if (currentWorksheet != null)
    {
      let headerRow = currentWorksheet.getRow(1); 
      let headerIndexMap: { [key: string]: number } = {};

      headerRow.eachCell((cell, colNumber) => {
        const mapping = this.travelTimeSheetColumns.find(m => m === cell.text.trim());
        if (mapping) headerIndexMap[mapping] = colNumber;
      });

      currentWorksheet.eachRow((row, rowNumber) => {
        if (rowNumber === 1) return;

        let tempPauseTable= new PauseTable;
        tempPauseTable.pauseId = (row.getCell(headerIndexMap[this.travelTime_SelectedIdColumn!]).text);
        tempPauseTable.locationId1 = (row.getCell(headerIndexMap[this.travelTime_SelectedLocation1Column!]).text);
        tempPauseTable.locationId2 = (row.getCell(headerIndexMap[this.travelTime_SelectedLocation2Column!]).text);
        tempPauseTable.pause = row.getCell(headerIndexMap[this.travelTime_SelectedPauseColumn!]).text;
        this.Data.pauseTable.push(tempPauseTable);
      });
    }

    this.eventsService.NewWizardEvent(this.Data).subscribe({
        next: (res) => {
          this.router.navigate(['home']);
        },
        error: (err) => {
          this.alertService.error("Error at creating wizard template!");
          if (environment.production == false) 
          {  console.error('Error at creating wizard template!', err);}
        }
      });
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

    return `${y}-${m}-${day}`;
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
    const time = item[field]!.slice(0, 16).split('T')[1] ?? '00:00';
    item[field] = `${newDate}T${time}`;
  }

  updateTime(item: any, newTime: string, field: string) {
    const date = item[field]!.slice(0, 16).split('T')[0] ?? '2025-01-01';
    item[field] = `${date}T${newTime}`;
  }
}
