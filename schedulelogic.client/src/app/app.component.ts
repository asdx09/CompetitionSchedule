import { HttpClient } from '@angular/common/http';
import { Component, OnInit } from '@angular/core';
import { AuthGuardService } from './auth-guard.service';
import { ActivatedRoute, NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs/internal/operators/filter';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  standalone: false,
  styleUrl: './app.component.css'
})
export class AppComponent implements OnInit {
  constructor(private router: Router, private AuthGuard: AuthGuardService) { }
  username: string = "";

  ngOnInit() {

    //Get username
    this.AuthGuard.username$.subscribe(name => {
      this.username = name ?? "";
    });

    //Check token
    this.AuthGuard.checkToken().subscribe({
      error: (err) => {
        this.router.navigate(['login']);
        this.AuthGuard.logout();
      }
    });
    

    this.router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe(() => {
        this.onRouteChange();
      });
  }

  ngAfterViewInit() {
    const splash = document.getElementById('splash');
    if (splash) {
      splash.style.opacity = '1';
      splash.style.transition = '0.5s';
    }
  }

  onRouteChange() {
    const splash = document.getElementById('splash');
    if (splash) {
      splash.style.opacity = '1';
      splash.style.transition = '0s';
    }
  }

  GoToHome()
  {
    this.router.navigate(['home']);
  }
}
