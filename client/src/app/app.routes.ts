import { Routes } from '@angular/router';
import { SearchPageComponent } from './search/search-page.component';

export const routes: Routes = [
  { path: '', component: SearchPageComponent, title: 'PriceHunt — Live search' },
  { path: '**', redirectTo: '' }
];
