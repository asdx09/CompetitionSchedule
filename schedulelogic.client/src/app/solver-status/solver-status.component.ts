import { Component, Input } from '@angular/core';
import { isSolverActive, solverStatusLabel, solverStatusText, solverStatusTone } from '../schedule.service';

@Component({
  selector: 'app-solver-status',
  standalone: false,
  templateUrl: './solver-status.component.html',
  styleUrl: './solver-status.component.css'
})
export class SolverStatusComponent {
  @Input() status: string | null = null;
  @Input() updatedAt: string | null = null;
  @Input() showDetail = true;

  get label(): string {
    return solverStatusLabel(this.status);
  }

  get detail(): string {
    return solverStatusText(this.status);
  }

  get tone(): string {
    return solverStatusTone(this.status);
  }

  get active(): boolean {
    return isSolverActive(this.status);
  }
}
