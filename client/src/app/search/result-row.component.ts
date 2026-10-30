import { DecimalPipe } from '@angular/common';
import { Component, Input } from '@angular/core';
import { ResultRow } from '../core/models';
import { StatusBadgeComponent } from '../shared/status-badge.component';

@Component({
  selector: 'app-result-row',
  standalone: true,
  imports: [DecimalPipe, StatusBadgeComponent],
  templateUrl: './result-row.component.html',
  styleUrl: './result-row.component.css'
})
export class ResultRowComponent {
  @Input({ required: true }) row!: ResultRow;
  @Input() cheapest = false;
}