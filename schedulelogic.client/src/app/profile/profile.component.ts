import { Component, Inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { AuthGuardService } from '../auth-guard.service';
import { Router } from '@angular/router';
import { AlertService } from '../alert.service';
import { environment } from '../../environments/environment';

@Component({
  selector: 'app-profile',
  standalone: false,
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.css',
})
export class ProfileComponent {
  constructor( private auth: AuthGuardService, private router: Router, private alertService: AlertService, 
    public dialogRef: MatDialogRef<ProfileComponent>,
    @Inject(MAT_DIALOG_DATA)  public data: {name: string}
  ) {}

  deleteAccount()
  {
    if (confirm("Are you sure you want to permanently delete your account?")) {
      this.auth.deleteUser().subscribe({
        next: (res) => {
          this.router.navigate(['login']);
        },
        error: (err) => {
          this.alertService.error("Something went wrong!");
          if (environment.production == false) 
          {console.error('Delete error! ', err);}
        }
      });
    }
    this.dialogRef.close();
  }

  logout()
  {
    this.auth.logout().subscribe({
      next: () => {
        localStorage.clear();
        this.router.navigate(['/login']);
      },
      error: (err) => {
        this.alertService.error("Something went wrong!");
        if (environment.production == false) 
        {console.error('Logout error! ', err);}
      }
    });
    this.dialogRef.close();
  }

}
