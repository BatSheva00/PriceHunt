import { Routes } from '@angular/router';
import { HistoryPageComponent } from './history/history-page.component';
import { SearchPageComponent } from './search/search-page.component';

export const routes: Routes = [
  { path: '', component: SearchPageComponent, title: 'PriceHunt — Live search' },
  { path: 'history', component: HistoryPageComponent, title: 'PriceHunt — History' },
  { path: '**', redirectTo: '' }
];
