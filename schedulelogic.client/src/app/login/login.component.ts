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
  PasswordCheckStrength: string[] = ["Short","Common","Weak","Ok","Strong"];
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


  public checkPasswordStrength(password: string): number {
      let numberOfElements = 0;
      numberOfElements = /.*[a-z].*/.test(password) ? ++numberOfElements : numberOfElements;     
      numberOfElements = /.*[A-Z].*/.test(password) ? ++numberOfElements : numberOfElements;     
      numberOfElements = /.*[0-9].*/.test(password) ? ++numberOfElements : numberOfElements;     
      numberOfElements = /[^a-zA-Z0-9]/.test(password) ? ++numberOfElements : numberOfElements;   

      let currentPasswordStrength = 1;

      if (password === null || password.length < 5) {
          currentPasswordStrength = 1;
      } else if (this.isPasswordCommon(password) === true) {
          currentPasswordStrength = 2;
      } else if (numberOfElements === 0 || numberOfElements === 1 || numberOfElements === 2) {
          currentPasswordStrength = 3;
      } else if (numberOfElements === 3) {
          currentPasswordStrength = 4;
      } else {
          currentPasswordStrength = 5;
      }

      return currentPasswordStrength;
    }

    public isPasswordCommon(password: string): boolean {
        return this.commonPasswordPatterns.test(password);
    }

    private commonPasswordPatterns = /passw.*|12345.*|09876.*|qwert.*|asdfg.*|zxcvb.*|footb.*|baseb.*|drago.*/;

}
