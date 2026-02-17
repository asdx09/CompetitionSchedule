import { HttpClient } from '@angular/common/http';
import { Component } from '@angular/core';
import { Observable } from 'rxjs';
import { AuthGuardService } from '../auth-guard.service';
import { Router } from '@angular/router';
import { AlertService } from '../alert.service';
import { environment } from '../../environments/environment';

@Component({
  selector: 'app-login',
  standalone: false,
  templateUrl: './login.component.html',
  styleUrl: './login.component.css'
})
export class LoginComponent {
  constructor(private http: HttpClient, private auth: AuthGuardService, private router: Router, private alertService: AlertService) { }
  IsLogin: boolean = true;
  WaitForAnswer = false;
  loginUsername = "";
  loginPassword = "";
  regUsername = "";
  regPassword = "";
  regEmail = "";

  ngOnInit(){
    localStorage.removeItem('token');
    localStorage.removeItem('username');
  }

  ngAfterViewInit() {
    const splash = document.getElementById('splash');
    if (splash) {
      splash.style.opacity = '0';
      splash.style.transition = '0.5s';
    }
  }

  toggleIsLogin()
  {
    if(this.IsLogin) this.IsLogin = false;
    else this.IsLogin = true;
  }
  
  login() 
  {
    this.WaitForAnswer = true;
    this.auth.login(this.loginUsername, this.loginPassword).subscribe({
      next: (res) => {
        localStorage.setItem('token', res.token);
        this.router.navigate(['home']);
         this.WaitForAnswer = false;
      },
      error: (err) => {
        this.alertService.error("Invalid username or password!");
        if (environment.production == false) 
        {console.error('Login error! ', err);}
         this.WaitForAnswer = false;
      }
    });
  }

  register() 
  {
    this.WaitForAnswer = true;
    this.auth.register(this.regUsername, this.regPassword, this.regEmail).subscribe({
      next: (res) => {
        this.loginUsername = this.regUsername;
        this.loginPassword = this.regPassword;
        this.login();
         this.WaitForAnswer = false;
      },
      error: (err) => {
        this.alertService.error("Invalid registration!");
        if (environment.production == false) 
        {console.error('Register error! ', err);}
        this.WaitForAnswer = false;
      }
    });
  }

  logout() {
    localStorage.removeItem('token');
  }

  public get token(): string | null {
    return localStorage.getItem('token');
  }
}
