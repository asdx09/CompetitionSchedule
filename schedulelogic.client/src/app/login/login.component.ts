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
  regPasswordAgain = "";
  regEmail = "";
  currentPasswordStrength = 1;
  strengthColor = "transparent";

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
        this.alertService.error(err.error);
        if (environment.production == false) 
        {console.error('Login error! ', err);}
         this.WaitForAnswer = false;
      }
    });
  }

  register() 
  {
    if (this.regPassword == this.regPasswordAgain)
    {
      this.WaitForAnswer = true;
      this.auth.register(this.regUsername, this.regPassword, this.regEmail).subscribe({
        next: (res) => {
          this.loginUsername = this.regUsername;
          this.IsLogin = true;
          this.alertService.info("We’ve sent a confirmation email to your inbox.");
          this.WaitForAnswer = false;
        },
        error: (err) => {
          this.alertService.error(err.error);
          if (environment.production == false) 
          {console.error('Register error! ', err);}
          this.WaitForAnswer = false;
        }
      });
    }else this.alertService.error("Passwords do not match!");
  }

  logout() {
    localStorage.removeItem('token');
  }

  public get token(): string | null {
    return localStorage.getItem('token');
  }


  public checkPasswordStrength() {
      let numberOfElements = 0;
      numberOfElements = /.*[a-z].*/.test(this.regPassword) ? ++numberOfElements : numberOfElements;     
      numberOfElements = /.*[A-Z].*/.test(this.regPassword) ? ++numberOfElements : numberOfElements;     
      numberOfElements = /.*[0-9].*/.test(this.regPassword) ? ++numberOfElements : numberOfElements;     
      numberOfElements = /[^a-zA-Z0-9]/.test(this.regPassword) ? ++numberOfElements : numberOfElements;   

      this.currentPasswordStrength = 1;
      if (numberOfElements > 1) this.currentPasswordStrength++;
      if (numberOfElements > 2) this.currentPasswordStrength++;
      if (numberOfElements > 3) this.currentPasswordStrength++;
      if (this.regPassword.length < 1) this.currentPasswordStrength = 0;
      if (this.regPassword.length > 5) this.currentPasswordStrength++;
      if (this.isPasswordCommon(this.regPassword) == true) this.currentPasswordStrength = 1;

      switch (this.currentPasswordStrength) {
      case 1: this.strengthColor = 'red'; break;
      case 2: this.strengthColor = 'orange'; break;
      case 3: this.strengthColor = 'yellow'; break;
      case 4: this.strengthColor = 'lightgreen'; break;
      case 5: this.strengthColor = 'green'; break;
      default: this.strengthColor = 'transparent';
    }

    }

    public isPasswordCommon(password: string): boolean {
        return this.commonPasswordPatterns.test(password);
    }

    private commonPasswordPatterns = /passw.*|12345.*|09876.*|qwert.*|asdfg.*|zxcvb.*|footb.*|baseb.*|drago.*/;

}
