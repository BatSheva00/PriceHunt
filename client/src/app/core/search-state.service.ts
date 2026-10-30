import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { ApiService } from './api.service';
import {
  Phase,
  ProgressEvent,
  QuoteEvent,
  ResultRow,
  RunCompletedEvent,
  RunStartedEvent,
  SearchFormValue
} from './models';

/**
 * Holds the live search state. The server stays the single source of truth: this store only
 * mirrors the events it receives and ignores anything that belongs to a superseded search.
 */
@Injectable({ providedIn: 'root' })
export class SearchStateService {
  private readonly api = inject(ApiService);

  readonly phase = signal<Phase>('idle');
  readonly rows = signal<ResultRow[]>([]);
  readonly responded = signal(0);
  readonly total = signal(0);
  readonly finalStatus = signal<string | null>(null);
  readonly durationMs = signal<number | null>(null);
  readonly errorMessage = signal<string | null>(null);

  readonly isRunning = computed(() => this.phase() === 'submitting' || this.phase() === 'streaming');

  /** Cheapest first. Suppliers without a price sink to the bottom, in a stable order. */
  readonly sortedRows = computed(() => {
    const rank = (row: ResultRow) => (row.status === 'Success' ? 0 : 1);

    return [...this.rows()].sort((a, b) => {
      if (rank(a) !== rank(b)) {
        return rank(a) - rank(b);
      }
      if (rank(a) === 0) {
        const delta = (a.price ?? Number.MAX_VALUE) - (b.price ?? Number.MAX_VALUE);
        if (delta !== 0) {
          return delta;
        }
      }
      return a.supplier.localeCompare(b.supplier);
    });
  });

  /** Every accepted search gets a new number; anything arriving for an older one is dropped. */
  private sequence = 0;
  private controller: AbortController | null = null;
  private activeSearchId: string | null = null;

  async search(value: SearchFormValue): Promise<void> {
    const current = ++this.sequence;
    const abandonedId = this.activeSearchId;
    this.stopActiveStream();
    this.abandon(abandonedId);
    this.reset();

    try {
      const created = await this.api.createSearch(value);
      if (current !== this.sequence) {
        this.abandon(created.searchId);
        return;
      }

      this.activeSearchId = created.searchId;
      this.startRows(created.suppliers);
      this.phase.set('streaming');

      const controller = new AbortController();
      this.controller = controller;

      await this.api.stream(
        created.searchId,
        message => {
          if (current === this.sequence) {
            this.apply(message.event, message.data);
          }
        },
        controller.signal
      );

      // The stream ended without a completion event: the server never finished this run.
      if (current === this.sequence && this.phase() === 'streaming') {
        this.phase.set('error');
        this.errorMessage.set('The connection closed before the search finished.');
      }
    } catch (error) {
      if (current !== this.sequence || (error as Error).name === 'AbortError') {
        return;
      }
      this.phase.set('error');
      this.errorMessage.set(this.failureMessage(error));
    }
  }

  /** Cancels explicitly, so the server closes the run instead of waiting for its deadline. */
  async cancel(): Promise<void> {
    if (!this.isRunning()) {
      return;
    }

    const searchId = this.activeSearchId;

    this.sequence++;
    this.stopActiveStream();
    this.phase.set('cancelled');
    this.finalStatus.set('Cancelled');

    if (!searchId) {
      return;
    }

    try {
      await this.api.cancelSearch(searchId);
    } catch {
      // The stream is gone; the server also records a cancellation when the connection drops.
    }
  }

  private reset(): void {
    this.phase.set('submitting');
    this.rows.set([]);
    this.responded.set(0);
    this.total.set(0);
    this.finalStatus.set(null);
    this.durationMs.set(null);
    this.errorMessage.set(null);
  }

  private stopActiveStream(): void {
    this.controller?.abort();
    this.controller = null;
    this.activeSearchId = null;
  }

  /** Tells the server to stop a run that this screen has already left. Does not block the next search. */
  private abandon(searchId: string | null): void {
    if (!searchId) {
      return;
    }

    void this.api.cancelSearch(searchId).catch(() => undefined);
  }

  private failureMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      const body = error.error as { error?: unknown } | null;
      if (body && typeof body.error === 'string' && body.error.length > 0) {
        return body.error;
      }
    }

    if (error instanceof Error && error.message.length > 0) {
      return error.message;
    }

    return 'The search failed.';
  }

  /** Every row exists from the first moment: rows fill in, they never appear mid-list. */
  private startRows(suppliers: string[]): void {
    this.total.set(suppliers.length);
    this.rows.set(
      suppliers.map(supplier => ({
        supplier,
        price: null,
        status: 'pending' as const
      }))
    );
  }

  private apply(event: string, data: unknown): void {
    switch (event) {
      case 'run_started': {
        const started = data as RunStartedEvent;
        this.startRows(started.suppliers);
        break;
      }

      case 'quote': {
        const quote = data as QuoteEvent;
        this.rows.update(rows =>
          rows.map(row =>
            row.supplier === quote.supplier
              ? {
                  supplier: row.supplier,
                  price: quote.status === 'Success' ? quote.price : null,
                  status: quote.status
                }
              : row
          )
        );
        break;
      }

      case 'progress': {
        const progress = data as ProgressEvent;
        this.responded.set(progress.responded);
        this.total.set(progress.total);
        break;
      }

      case 'run_completed': {
        const completed = data as RunCompletedEvent;
        this.phase.set(
          completed.status === 'TimedOut' ? 'timed_out' : completed.status === 'Cancelled' ? 'cancelled' : 'completed'
        );
        this.finalStatus.set(completed.status);
        this.durationMs.set(completed.durationMs);
        this.responded.set(completed.responded);
        this.total.set(completed.total);
        this.activeSearchId = null;
        break;
      }
    }
  }
}