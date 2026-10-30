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
  }

  onInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.query.set(value);
    this.error.set(null);

    if (value.trim() === this.chosen) {
      this.matches.set([]);
      return;
    }

    this.chosen = null;
    this.onChange('');
    if (this.timer) {
      clearTimeout(this.timer);
    }

    const query = value.trim();
    if (query.length === 0) {
      this.matches.set([]);
      return;
    }

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
    const current = ++this.request;
    try {
      const places = await this.api.places(query);
      if (current === this.request && this.chosen === null) {
        this.matches.set(places);
      }
    } catch {
      if (current === this.request) {
        this.matches.set([]);
        this.error.set('The city list could not be loaded.');
      }
    }
  }
}
