import { HTTP_INTERCEPTORS, HttpClientModule } from '@angular/common/http';
import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { LoginComponent } from './login/login.component';
import { HomeComponent } from './home/home.component';
import { FormsModule } from '@angular/forms';
import { JwtInterceptor } from './jwt.interceptor';
import { EventComponent } from './event/event.component';
import { LocationSelectorComponent } from './location-selector/location-selector.component';
import { MatDialogModule } from '@angular/material/dialog';
import { ScheduleComponent } from './schedule/schedule.component';
import { TimezoneInfoComponent } from './timezone-info/timezone-info.component';
import { WizardComponent } from './wizard/wizard.component';
import { BrowserAnimationsModule } from "@angular/platform-browser/animations";
import { IgxDatePickerModule, IgxDateRangePickerModule, IgxInputGroupModule } from 'igniteui-angular';
import { AlertComponent } from './alert/alert.component';


@NgModule({
	declarations: [
		AppComponent,
		LoginComponent,
		HomeComponent,
		EventComponent,
		LocationSelectorComponent,
		ScheduleComponent,
		TimezoneInfoComponent,
		WizardComponent,
  		AlertComponent
	],
	imports: [BrowserModule,
		HttpClientModule,
		AppRoutingModule,
		FormsModule,
		MatDialogModule,
		BrowserAnimationsModule,
		IgxDatePickerModule,
		IgxDateRangePickerModule,
		IgxInputGroupModule],
	providers: [
		{
			provide: HTTP_INTERCEPTORS,
			useClass: JwtInterceptor,
			multi: true
		}
	],
	bootstrap: [AppComponent]
})
export class AppModule {
}
