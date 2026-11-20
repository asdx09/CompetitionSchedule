import { AfterViewInit, Component, ElementRef, EventEmitter, Input, OnChanges, Output, SimpleChanges, ViewChild } from '@angular/core';

export interface TimelineBlock {
  id: string;
  start: number;
  end: number;
  label: string;
  color: string;
  textColor: string;
  tooltip: string;
}

export interface TimelineRow {
  key: string;
  name: string;
  sub: string;
  search: string;
  blocks: TimelineBlock[];
  unavailable: { start: number; end: number }[];
}

interface AxisMark {
  left: number;
  label: string;
}

@Component({
  selector: 'app-schedule-timeline',
  standalone: false,
  templateUrl: './schedule-timeline.component.html',
  styleUrl: './schedule-timeline.component.css'
})
export class ScheduleTimelineComponent implements OnChanges, AfterViewInit {
  @Input() title = '';
  @Input() nameHeader = '';
  @Input() rows: TimelineRow[] = [];
  @Input() startDate: Date = new Date();
  @Input() totalMinutes = 1440;
  @Input() activeStart = 0;
  @Input() activeEnd = 1440;
  @Input() zoom = 2;
  @Input() showUnavailable = true;
  @Input() compact = false;
  @Output() blockClick = new EventEmitter<string>();

  @ViewChild('scroller') scroller?: ElementRef<HTMLDivElement>;

  filter = '';
  nameWidth = 180;
  hourMarks: AxisMark[] = [];
  dayMarks: AxisMark[] = [];
  gridStep = 60;
  private initialScrollDone = false;

  get width(): number {
    return this.totalMinutes * this.zoom;
  }

  get visibleRows(): TimelineRow[] {
    const f = this.filter.trim().toUpperCase();
    return f ? this.rows.filter(r => r.search.includes(f)) : this.rows;
  }

  get gridBackground(): string {
    return `${1440 * this.zoom}px 100%, ${this.gridStep * this.zoom}px 100%`;
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['compact']) {
      this.nameWidth = this.compact ? 120 : 180;
    }
    if (changes['zoom'] && !changes['zoom'].firstChange && this.scroller) {
      const el = this.scroller.nativeElement;
      const oldZoom = changes['zoom'].previousValue as number;
      const centerMinute = (el.scrollLeft + (el.clientWidth - this.nameWidth) / 2) / oldZoom;
      setTimeout(() => el.scrollLeft = centerMinute * this.zoom - (el.clientWidth - this.nameWidth) / 2);
    }
    this.buildAxis();
    this.scrollToStart();
  }

  ngAfterViewInit() {
    this.scrollToStart();
  }

  private scrollToStart() {
    if (this.initialScrollDone || !this.scroller || this.rows.length === 0) return;
    this.initialScrollDone = true;
    setTimeout(() => {
      if (this.scroller) this.scroller.nativeElement.scrollLeft = Math.max(0, this.activeStart * this.zoom - 24);
    });
  }

  private buildAxis() {
    const minLabelGap = 56;
    const steps = [15, 30, 60, 120, 180, 360, 720];
    this.gridStep = steps.find(s => s * this.zoom >= minLabelGap) ?? 1440;

    this.hourMarks = [];
    for (let m = 0; m < this.totalMinutes; m += this.gridStep) {
      if (m % 1440 === 0) continue;
      this.hourMarks.push({ left: m * this.zoom, label: this.formatTime(m) });
    }

    this.dayMarks = [];
    for (let m = 0; m < this.totalMinutes; m += 1440) {
      const d = new Date(this.startDate.getTime() + m * 60000);
      this.dayMarks.push({
        left: m * this.zoom,
        label: d.toLocaleDateString('en-GB', { weekday: 'short', day: 'numeric', month: 'short' })
      });
    }
  }

  private formatTime(minutes: number): string {
    const m = minutes % 1440;
    return `${Math.floor(m / 60).toString().padStart(2, '0')}:${(m % 60).toString().padStart(2, '0')}`;
  }

  trackRow(_: number, row: TimelineRow) {
    return row.key;
  }

  startResize(event: MouseEvent | TouchEvent) {
    event.preventDefault();
    const startX = event instanceof MouseEvent ? event.clientX : event.touches[0].clientX;
    const startWidth = this.nameWidth;

    const onMove = (e: MouseEvent | TouchEvent) => {
      const x = e instanceof MouseEvent ? e.clientX : e.touches[0].clientX;
      this.nameWidth = Math.min(400, Math.max(90, startWidth + x - startX));
    };
    const onEnd = () => {
      document.removeEventListener('mousemove', onMove);
      document.removeEventListener('mouseup', onEnd);
      document.removeEventListener('touchmove', onMove);
      document.removeEventListener('touchend', onEnd);
    };

    document.addEventListener('mousemove', onMove);
    document.addEventListener('mouseup', onEnd);
    document.addEventListener('touchmove', onMove, { passive: false });
    document.addEventListener('touchend', onEnd);
  }
}
