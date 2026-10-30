import { Component, Input, OnDestroy, forwardRef, inject, signal } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { ApiService } from '../core/api.service';
import { Place } from '../core/models';

@Component({
  selector: 'app-place-field',
  standalone: true,
  templateUrl: './place-field.component.html',
  styleUrl: './place-field.component.css',
  providers: [{
    provide: NG_VALUE_ACCESSOR,
    useExisting: forwardRef(() => PlaceFieldComponent),
    multi: true
  }]
})
export class PlaceFieldComponent implements ControlValueAccessor, OnDestroy {
  @Input() label = '';
  @Input() placeholder = '';

  private readonly api = inject(ApiService);
  readonly query = signal('');
  readonly matches = signal<Place[]>([]);
  readonly error = signal<string | null>(null);

  private chosen: string | null = null;
  private timer: ReturnType<typeof setTimeout> | null = null;
  private retry: ReturnType<typeof setTimeout> | null = null;
  private suggestionsPausedUntil = 0;
  private request = 0;
  private onChange: (value: string) => void = () => undefined;
  private onTouched: () => void = () => undefined;

  writeValue(value: string | null): void {
    const name = value ?? '';
    this.query.set(name);
    this.chosen = name.length > 0 ? name : null;
  }

  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  ngOnDestroy(): void {
    if (this.timer) {
      clearTimeout(this.timer);
    }
    this.clearRetry();
  }

  onInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.query.set(value);
    const query = value.trim();
    this.onChange(query);

    if (query === this.chosen) {
      this.matches.set([]);
      this.error.set(null);
      return;
    }

    this.chosen = null;
    if (this.timer) {
      clearTimeout(this.timer);
    }

    if (query.length === 0) {
      this.matches.set([]);
      this.error.set(null);
      return;
    }

    if (Date.now() < this.suggestionsPausedUntil) {
      this.showSuggestionsPaused();
      return;
    }

    this.error.set(null);
    this.timer = setTimeout(() => void this.search(query), 250);
  }

  choose(place: Place): void {
    this.query.set(place.name);
    this.chosen = place.name;
    this.matches.set([]);
    this.error.set(null);
    this.onChange(place.name);
    this.onTouched();
  }

  blur(): void {
    this.onTouched();
  }

  private async search(query: string): Promise<void> {
    if (query.length === 0 || Date.now() < this.suggestionsPausedUntil) {
      return;
    }

    const current = ++this.request;
    try {
      const places = await this.api.places(query);
      if (current !== this.request || this.query().trim() !== query) {
        return;
      }

      this.suggestionsPausedUntil = 0;
      this.clearRetry();
      this.matches.set(places);
      this.error.set(null);
    } catch {
      if (current !== this.request) {
        return;
      }

      this.suggestionsPausedUntil = Date.now() + 30_000;
      this.showSuggestionsPaused();
      this.scheduleRetry();
    }
  }

  private showSuggestionsPaused(): void {
    this.matches.set([]);
    this.error.set('City suggestions are unavailable.');
  }

  private scheduleRetry(): void {
    this.clearRetry();
    this.retry = setTimeout(() => {
      this.suggestionsPausedUntil = 0;
      void this.search(this.query().trim());
    }, 31_000);
  }

  private clearRetry(): void {
    if (this.retry) {
      clearTimeout(this.retry);
      this.retry = null;
    }
  }
}
