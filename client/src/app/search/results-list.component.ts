import { Component, ElementRef, Input, OnChanges, QueryList, SimpleChanges, ViewChildren } from '@angular/core';
import { ResultRow } from '../core/models';
import { ResultRowComponent } from './result-row.component';

@Component({
  selector: 'app-results-list',
  standalone: true,
  imports: [ResultRowComponent],
  templateUrl: './results-list.component.html',
  styleUrl: './results-list.component.css'
})
export class ResultsListComponent implements OnChanges {
  @Input({ required: true }) rows: ResultRow[] = [];

  @ViewChildren(ResultRowComponent, { read: ElementRef }) private rowElements?: QueryList<ElementRef<HTMLElement>>;

  cheapestSupplier: string | null = null;

  /**
   * Rows arrive already sorted. Before the new order is painted, measure where every row sits;
   * once it is painted, animate each row from its old position to its new one (FLIP). Rows keep a
   * fixed height, so nothing below the table moves either.
   */
  ngOnChanges(changes: SimpleChanges): void {
    if (!changes['rows']) {
      return;
    }

    const previous = this.capturePositions();
    this.cheapestSupplier = this.rows.find(row => row.status === 'Success')?.supplier ?? null;
    setTimeout(() => this.playFlip(previous));
  }

  private capturePositions(): Map<string, number> {
    const positions = new Map<string, number>();
    this.rowElements?.forEach(reference => {
      const supplier = reference.nativeElement.dataset['supplier'];
      if (supplier) {
        positions.set(supplier, reference.nativeElement.getBoundingClientRect().top);
      }
    });
    return positions;
  }

  private playFlip(previous: Map<string, number>): void {
    this.rowElements?.forEach(reference => {
      const element = reference.nativeElement;
      const before = element.dataset['supplier'] ? previous.get(element.dataset['supplier'] as string) : undefined;
      if (before === undefined) {
        return;
      }

      const delta = before - element.getBoundingClientRect().top;
      if (Math.abs(delta) < 1) {
        return;
      }

      element.animate(
        [{ transform: `translateY(${delta}px)` }, { transform: 'translateY(0)' }],
        { duration: 240, easing: 'cubic-bezier(0.22, 1, 0.36, 1)' }
      );
    });
  }
}