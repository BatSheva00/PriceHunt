import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ApiService } from '../core/api.service';
import { HistoryPage, SortKey } from '../core/models';

@Component({
  selector: 'app-history-page',
  standalone: true,
  imports: [DatePipe, DecimalPipe],
  templateUrl: './history-page.component.html',
  styleUrl: './history-page.component.css'
})
export class HistoryPageComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly pageSize = 25;

  readonly result = signal<HistoryPage>({ items: [], total: 0, page: 1, pageSize: this.pageSize });
  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly suppliers = signal<string[]>([]);
  readonly suppliersError = signal<string | null>(null);
  readonly selected = signal<string[]>([]);
  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.result().total / this.pageSize)));

  from = '';
  to = '';
  fromLocation = '';
  toLocation = '';
  sortBy: SortKey = 'date';
  sortDir: 'asc' | 'desc' = 'desc';
  pageIndex = 1;
  private requestId = 0;

  ngOnInit(): void {
    void this.loadSuppliers();
    void this.load();
  }

  apply(event?: Event): void {
    event?.preventDefault();
    if (this.keepPageBecauseDatesAreReversed()) {
      return;
    }
    this.pageIndex = 1;
    void this.load();
  }

  reset(): void {
    this.from = '';
    this.to = '';
    this.fromLocation = '';
    this.toLocation = '';
    this.selected.set([]);
    this.pageIndex = 1;
    void this.load();
  }

  toggle(supplier: string): void {
    if (this.keepPageBecauseDatesAreReversed()) {
      return;
    }
    this.selected.update(current =>
      current.includes(supplier) ? current.filter(name => name !== supplier) : [...current, supplier]
    );
    this.pageIndex = 1;
    void this.load();
  }

  sort(column: SortKey): void {
    if (this.keepPageBecauseDatesAreReversed()) {
      return;
    }
    if (this.sortBy === column) {
      this.sortDir = this.sortDir === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortBy = column;
      this.sortDir = column === 'date' ? 'desc' : 'asc';
    }
    this.pageIndex = 1;
    void this.load();
  }

  go(target: number): void {
    const next = Math.min(Math.max(1, target), this.totalPages());
    if (next === this.pageIndex || this.loading()) {
      return;
    }
    if (this.keepPageBecauseDatesAreReversed()) {
      return;
    }
    this.pageIndex = next;
    void this.load();
  }

  valueOf(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  arrow(column: SortKey): string {
    if (this.sortBy !== column) {
      return '';
    }
    return this.sortDir === 'asc' ? '▲' : '▼';
  }

  /** Stored timestamps are UTC. A value without a zone would be read as local time. */
  stamp(value: string): string {
    return /(?:z|[+-]\d{2}:\d{2})$/i.test(value) ? value : `${value}Z`;
  }

  private async loadSuppliers(): Promise<void> {
    try {
      this.suppliers.set(await this.api.suppliers());
    } catch {
      this.suppliersError.set('The supplier list could not be loaded.');
    }
  }

  private datesAreReversed(): boolean {
    return this.from !== '' && this.to !== '' && this.from > this.to;
  }

  /** An invalid range is the newest state: drop any response still in flight. */
  private keepPageBecauseDatesAreReversed(): boolean {
    if (!this.datesAreReversed()) {
      return false;
    }

    this.requestId++;
    this.loading.set(false);
    this.errorMessage.set('The end date cannot be before the start date.');
    return true;
  }

  private async load(): Promise<void> {
    const requestId = ++this.requestId;
    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      const page = await this.api.history({
        from: this.from,
        to: this.to,
        fromLocation: this.fromLocation.trim(),
        toLocation: this.toLocation.trim(),
        suppliers: this.selected(),
        sortBy: this.sortBy,
        sortDir: this.sortDir,
        page: this.pageIndex,
        pageSize: this.pageSize
      });
      if (requestId !== this.requestId) {
        return;
      }
      this.result.set(page);
    } catch (error) {
      if (requestId !== this.requestId) {
        return;
      }
      this.errorMessage.set(this.failureMessage(error));
    } finally {
      if (requestId === this.requestId) {
        this.loading.set(false);
      }
    }
  }

  private failureMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      const body = error.error as { error?: unknown } | null;
      if (body && typeof body.error === 'string' && body.error.length > 0) {
        return body.error;
      }
    }

    return 'The history could not be loaded.';
  }
}
