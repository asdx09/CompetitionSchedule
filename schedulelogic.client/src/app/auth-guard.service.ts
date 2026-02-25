import { Injectable } from '@angular/core';
import { CanActivate, Router } from '@angular/router';
import { BehaviorSubject, Observable } from 'rxjs';
import { HttpClient } from '@angular/common/http';
import { environment } from '../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class AuthGuardService implements CanActivate {
  constructor(private router: Router, private http: HttpClient) { }
  private apiUrl = `${environment.apiUrl}api/Authentication`;
  private usernameSubject = new BehaviorSubject<string | null>(localStorage.getItem('username'));
  username$ = this.usernameSubject.asObservable();

  canActivate(): boolean {
    const token = localStorage.getItem('token');
    if (token) {
      return true;
    }
    
    return false;
  }

  login(loginUsername: string, loginPassword: string): Observable<any> {
    this.usernameSubject.next(loginUsername);

    return this.http.post(`${this.apiUrl}/login`, 
      { Username: loginUsername, Password: loginPassword }, 
      { withCredentials: true } 
    );
  }
  
  register(regUsername: string, regPassword:string, regEmail:string): Observable<any> {
    return this.http.post(`${this.apiUrl}/register`, { Username: regUsername, Password: regPassword, Email: regEmail });
  }

  checkToken(): Observable<any> {
    return this.http.post(`${this.apiUrl}/check-token`, null);
  }

  logout() {
    localStorage.removeItem('token');
  }

  public get token(): string | null {
    return localStorage.getItem('token');
  }
}
