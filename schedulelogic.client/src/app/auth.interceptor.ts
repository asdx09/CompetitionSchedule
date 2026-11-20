import { Injectable } from '@angular/core';
import { HttpInterceptor, HttpRequest, HttpHandler, HttpEvent, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthGuardService } from './auth-guard.service';
import { Router } from '@angular/router';
import { AlertService } from './alert.service';

@Injectable()
export class CredentialsInterceptor implements HttpInterceptor {
  intercept(req: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    return next.handle(req.clone({ withCredentials: true }));
  }
}

@Injectable()
export class AuthErrorInterceptor implements HttpInterceptor {
  private lastUnavailableAlert = 0;

  constructor(private router: Router, private authService: AuthGuardService, private alertService: AlertService) {}

  intercept(req: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    return next.handle(req).pipe(
      catchError((error: HttpErrorResponse) => {
        if (error.status === 401) {
          this.authService.logout();
        } else if (error.status === 403) {
          this.router.navigate(['/home']);
        } else if (error.status === 503 || error.status === 0) {
          this.showUnavailable(error.status === 0
            ? 'Cannot reach the server. Please check your connection.'
            : 'The service is temporarily unavailable. Please try again later.');
        }
        return throwError(() => error);
      })
    );
  }

  private showUnavailable(message: string) {
    const now = Date.now();
    if (now - this.lastUnavailableAlert < 5000) return;
    this.lastUnavailableAlert = now;
    this.alertService.error(message, 5000);
  }
}