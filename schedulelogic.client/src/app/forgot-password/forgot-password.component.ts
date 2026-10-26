import { Component } from '@angular/core';
import { AuthGuardService } from '../auth-guard.service';

@Component({
  selector: 'app-forgot-password',
  templateUrl: './forgot-password.component.html',
  standalone: false,
  styleUrl: './forgot-password.component.css'
})
export class ForgotPasswordComponent{
  usernameOrEmail = '';
  submitted = false;
  loading = false;

  constructor(private auth: AuthGuardService) { }

  onSubmit(): void {
    if (!this.usernameOrEmail.trim() || this.loading) return;

    this.loading = true;
    this.auth.forgotPassword(this.usernameOrEmail.trim()).subscribe({

      next: () => { this.loading = false; this.submitted = true; },
      error: () => { this.loading = false; this.submitted = true; }
    });
  }

  ngOnInit()
  {
    const splash = document.getElementById('splash');
    if (splash) {
      splash.style.opacity = '0';
      splash.style.transition = '0.5s';
    }
  }
}