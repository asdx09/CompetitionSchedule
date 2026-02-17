import { ComponentFixture, TestBed } from '@angular/core/testing';

import { TimezoneInfoComponent } from './timezone-info.component';

describe('TimezoneInfoComponent', () => {
  let component: TimezoneInfoComponent;
  let fixture: ComponentFixture<TimezoneInfoComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [TimezoneInfoComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(TimezoneInfoComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
