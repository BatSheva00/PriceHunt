import { Component, Input } from '@angular/core';

const LABELS: Record<string, string> = {
  Failed: 'Failed',
  Timeout: 'Timed out',
  NoResponse: 'Never responds',
  Completed: 'Completed',
  TimedOut: 'Timed out',
  Cancelled: 'Cancelled'
};

const TONES: Record<string, string> = {
  pending: 'idle',
  Success: 'ok',
  Failed: 'bad',
  Timeout: 'warn',
  NoResponse: 'warn',
  Running: 'accent',
  Completed: 'ok',
  TimedOut: 'warn',
  Cancelled: 'idle'
};

@Component({
  selector: 'app-status-badge',
  standalone: true,
  templateUrl: './status-badge.component.html',
  styleUrl: './status-badge.component.css'
})
export class StatusBadgeComponent {
  @Input({ required: true }) status = '';

  label(): string {
    return LABELS[this.status] ?? '';
  }

  tone(): string {
    return TONES[this.status] ?? 'idle';
  }
}