import { Component, HostListener } from '@angular/core';
import { EventsData, EventsService } from '../events.service';
import { Router } from '@angular/router';
import { environment } from '../../environments/environment';
import { AlertService } from '../alert.service';
import { AuthGuardService } from '../auth-guard.service';

@Component({
  selector: 'app-home',
  standalone: false,
  templateUrl: './home.component.html',
  styleUrl: './home.component.css'
})

export class HomeComponent {
  constructor(private auth: AuthGuardService, private eventsService: EventsService, private router: Router, private alertService: AlertService) { }
  events: EventsData[] = [];
  isMobile = false;

  ngOnInit(){
    this.auth.checkToken().subscribe({
       next: (res) => {
        this.auth.usernameSubject.next(res.name);
        this.RefreshList();
       },
      error: (err) => {
        this.router.navigate(['login']);
      }
    });
    this.isMobile = window.innerWidth <= 768;
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
    event.stopPropagation();
    if (confirm("Are you sure you want to permanently delete this event?")) {
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
  }

  GoToEvent(id:number)
  {
    this.router.navigate(['event'],{ queryParams: { id: id } });
  }

  GoToSolution(id:number)
  {
    this.router.navigate(['schedule'],{ queryParams: { id: id } });
  }

  GoToWizard()
  {
    this.router.navigate(['wizard']);
  }

  @HostListener('window:resize', ['$event'])
  onResize(event: Event) {
    this.isMobile = window.innerWidth <= 768;
  }
}
