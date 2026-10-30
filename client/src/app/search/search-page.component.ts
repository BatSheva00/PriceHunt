import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { ApiService } from '../core/api.service';
import { SearchStateService } from '../core/search-state.service';
import { PlaceFieldComponent } from './place-field.component';
import { ResultsListComponent } from './results-list.component';
import { StatusBadgeComponent } from '../shared/status-badge.component';

@Component({
  selector: 'app-search-page',
  standalone: true,
  imports: [ReactiveFormsModule, PlaceFieldComponent, ResultsListComponent, StatusBadgeComponent],
  templateUrl: './search-page.component.html',
  styleUrl: './search-page.component.css'
})
export class SearchPageComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ApiService);
  readonly state = inject(SearchStateService);

  readonly suppliers = signal<string[]>([]);
  readonly selected = signal<string[]>([]);
  readonly suppliersLoading = signal(true);
  readonly suppliersError = signal<string | null>(null);
  readonly formError = signal<string | null>(null);

  /** Membership checks stay O(1) while the chips re-render. */
  readonly selectedSet = computed(() => new Set(this.selected()));

  readonly progressPercent = computed(() => {
    const total = this.state.total();
    return total > 0 ? Math.round((this.state.responded() / total) * 100) : 0;
  });

  readonly form = this.fb.group(
    {
      fromLocation: ['Tel Aviv - Yafo', Validators.required],
      toLocation: ['Haifa', Validators.required],
      fromDate: [this.dateIn(1), Validators.required],
      toDate: [this.dateIn(3), Validators.required]
    },
    { validators: SearchPageComponent.datesInOrder }
  );

  async ngOnInit(): Promise<void> {
    try {
      const names = await this.api.suppliers();
      this.suppliers.set(names);
      this.selected.set([...names]);
    } catch {
      this.suppliersError.set('The supplier list could not be loaded.');
    } finally {
      this.suppliersLoading.set(false);
    }
  }

  toggle(supplier: string): void {
    this.selected.update(current =>
      current.includes(supplier) ? current.filter(name => name !== supplier) : [...current, supplier]
    );
  }

  selectAll(): void {
    this.selected.set([...this.suppliers()]);
  }

  clearAll(): void {
    this.selected.set([]);
  }

  async submit(): Promise<void> {
    this.formError.set(null);

    const value = this.form.getRawValue();
    const fromLocation = value.fromLocation?.trim() ?? '';
    const toLocation = value.toLocation?.trim() ?? '';

    if (fromLocation.length === 0 || toLocation.length === 0) {
      this.formError.set('Choose a pickup and a delivery city from the list.');
      return;
    }

    if (!value.fromDate || !value.toDate) {
      this.formError.set('Choose both dates.');
      return;
    }

    if (this.form.errors?.['order']) {
      this.formError.set('The delivery date cannot be before the pickup date.');
      return;
    }

    if (this.selected().length === 0) {
      this.formError.set('Select at least one supplier.');
      return;
    }

    await this.state.search({
      fromLocation,
      toLocation,
      fromDate: value.fromDate,
      toDate: value.toDate,
      suppliers: this.selected()
    });
  }

  async cancel(): Promise<void> {
    await this.state.cancel();
  }

  /** Local ISO day: toISOString would shift the date for users behind UTC. */
  private dateIn(days: number): string {
    const date = new Date();
    date.setDate(date.getDate() + days);
    return [
      date.getFullYear(),
      String(date.getMonth() + 1).padStart(2, '0'),
      String(date.getDate()).padStart(2, '0')
    ].join('-');
  }

  private static datesInOrder(group: AbstractControl): ValidationErrors | null {
    const from = group.get('fromDate')?.value;
    const to = group.get('toDate')?.value;
    if (!from || !to) {
      return null;
    }
    return from <= to ? null : { order: true };
  }
}