import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthGuardService } from '../auth-guard.service';

@Component({
  selector: 'app-reset-password',
  templateUrl: './reset-password.component.html',
  standalone: false,
  styleUrl: './reset-password.component.css'
})
export class ResetPasswordComponent {
  token = '';
  newPassword = '';
  confirmPassword = '';
  loading = false;
  success = false;
  errorMessage = '';

  missingToken = false;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private auth: AuthGuardService
  ) { }

  ngOnInit() {
    const splash = document.getElementById('splash');
    if (splash) {
      splash.style.opacity = '0';
      splash.style.transition = '0.5s';
    }

    this.token = this.route.snapshot.queryParamMap.get('token') ?? '';
    this.missingToken = !this.token;
  }

  onSubmit(): void {
    this.errorMessage = '';

    if (this.newPassword.length < 8) {
      this.errorMessage = 'The password must be at least 8 characters long.';
      return;
    }

    if (this.newPassword !== this.confirmPassword) {
      this.errorMessage = 'The passwords do not match.';
      return;
    }

    this.loading = true;

    this.auth.resetPassword(this.token, this.newPassword).subscribe({
      next: () => {
        this.loading = false;
        this.success = true;

        setTimeout(() => this.router.navigate(['/login']), 2500);
      },

      error: () => {
        this.loading = false;
        this.errorMessage = 'The link is invalid or has expired. Please request a new link.';
      }
    });
  }
}