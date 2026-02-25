import { Component } from '@angular/core';
import { EventsData, EventsService } from '../events.service';
import { Router } from '@angular/router';
import { environment } from '../../environments/environment';
import { AlertService } from '../alert.service';

@Component({
  selector: 'app-home',
  standalone: false,
  templateUrl: './home.component.html',
  styleUrl: './home.component.css'
})

export class HomeComponent {
  constructor(private eventsService: EventsService, private router: Router, private alertService: AlertService) { }
  events: EventsData[] = [];

  ngOnInit(){
    this.RefreshList();
  }

  RefreshList()
  {
      this.eventsService.GetEvents().subscribe({
      next: (res) => {
        this.events = res;
        const splash = document.getElementById('splash');
        if (splash) {
          splash.style.opacity = '0';
          splash.style.transition = '0.5s';
        }
      },
      error: (err) => {
        this.alertService.error("Something went wrong!");
        if (environment.production == false) 
        {console.error('Get events error! ', err)}
        this.router.navigate(['login']);
      }
    });
  }

  AddEvent()
  {
    this.eventsService.NewEvent().subscribe({
      next: (res) => {
        this.RefreshList();
      },
      error: (err) => {
        this.alertService.error("Something went wrong!");
        if (environment.production == false) 
        {console.error('Add event error! ', err)}
        window.location.reload();
      }
    });
  }

  DeleteEvent(id: number, event: MouseEvent)
  {
    event.stopPropagation()
    this.eventsService.DeleteEvent(id.toString()).subscribe({
      next: (res) => {
        this.RefreshList();
      },
      error: (err) => {
        this.alertService.error("Something went wrong!");
        if (environment.production == false) 
        {console.error('Delete event hiba!', err);}
        window.location.reload();
      }
    });
  }

  GoToEvent(id:number)
  {
    console.log(this.events);
    console.log(id);
    this.router.navigate(['event'],{ queryParams: { id: id } });
  }

  GoToWizard()
  {
    this.router.navigate(['wizard']);
  }
}
