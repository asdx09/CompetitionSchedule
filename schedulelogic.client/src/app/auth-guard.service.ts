import { Injectable } from '@angular/core';
import { CanActivate, Router } from '@angular/router';
import { BehaviorSubject, Observable, of } from 'rxjs';
import { catchError, map, tap } from 'rxjs/operators';
import { HttpClient } from '@angular/common/http';
import { environment } from '../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class AuthGuardService implements CanActivate {
  private apiUrl = `${environment.apiUrl}api/Authentication`;

  // A HttpOnly cookie-t JS nem olvashatja, ezért az authentikált
  // állapotot a szerver válasza alapján tartjuk nyilván, nem token
  // meglétéből. Publikus, mert több komponens (event, home, schedule,
  // wizard) is frissíti a saját checkToken()-hívása után.
  usernameSubject = new BehaviorSubject<string | null>(null);
  username$ = this.usernameSubject.asObservable();

  constructor(private router: Router, private http: HttpClient) { }

  // Minden route-váltásnál megkérdezi a szervert, érvényes-e még a session.
  // Ez a cookie sliding expiration miatt is fontos: ha csak a kliens
  // memóriájára hagyatkoznánk, egy régen betöltött oldalon a kliens
  // "bejelentkezve" hihetné magát, miközben a cookie már lejárt.
  canActivate(): Observable<boolean> {
    return this.checkToken().pipe(
      tap(res => this.usernameSubject.next(res.name)),
      map(() => true),
      catchError(() => {
        this.usernameSubject.next(null);
        this.router.navigate(['/login']);
        return of(false);
      })
    );
  }

  login(loginUsername: string, loginPassword: string): Observable<any> {
    return this.http.post(
      `${this.apiUrl}/login`,
      { Username: loginUsername, Password: loginPassword },
      { withCredentials: true }
    ).pipe(
      tap(() => this.usernameSubject.next(loginUsername))
    );
  }

  register(regUsername: string, regPassword: string, regEmail: string): Observable<any> {
    return this.http.post(`${this.apiUrl}/register`,
      { Username: regUsername, Password: regPassword, Email: regEmail });
  }

  resendConfirmation(usernameOrEmail: string): Observable<any> {
    return this.http.post(`${this.apiUrl}/resend-confirmation`, { UsernameOrEmail: usernameOrEmail });
  }
 
  forgotPassword(usernameOrEmail: string): Observable<any> {
    return this.http.post(`${this.apiUrl}/forgot-password`, { UsernameOrEmail: usernameOrEmail });
  }
 
  resetPassword(token: string, newPassword: string): Observable<any> {
    return this.http.post(`${this.apiUrl}/reset-password`, { Token: token, NewPassword: newPassword });
  }

  deleteUser(): Observable<any> {
    return this.http.delete(`${this.apiUrl}/delete`, { withCredentials: true });
  }

  checkToken(): Observable<{ name: string }> {
    return this.http.get<{ name: string }>(`${this.apiUrl}/check-token`, { withCredentials: true });
  }

  logout(): Observable<any> {
    return this.http.post(`${this.apiUrl}/logout`, {}, { withCredentials: true }).pipe(
      tap(() => {
        this.usernameSubject.next(null);
        this.router.navigate(['/login']);
      })
    );
  }
}