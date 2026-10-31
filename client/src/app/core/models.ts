export type Phase = 'idle' | 'submitting' | 'streaming' | 'completed' | 'timed_out' | 'cancelled' | 'error';

export type RowStatus = 'pending' | 'Success' | 'Failed' | 'Timeout' | 'NoResponse' | 'Cancelled';

export interface ResultRow {
  supplier: string;
  price: number | null;
  status: RowStatus;
}

export interface Place {
  id: string;
  name: string;
}

export interface SearchFormValue {
  fromLocation: string;
  toLocation: string;
  fromDate: string;
  toDate: string;
  suppliers: string[];
}

export interface SearchCreated {
  searchId: string;
  suppliers: string[];
  deadlineMs: number;
}

export interface QuoteEvent {
  supplier: string;
  price: number;
  responseTimeMs: number;
  status: RowStatus;
  at: string;
}

export interface RunStartedEvent {
  searchId: string;
  total: number;
  suppliers: string[];
}

export interface ProgressEvent {
  responded: number;
  total: number;
}

export interface RunCompletedEvent {
  status: string;
  responded: number;
  total: number;
  durationMs: number;
}

export interface HistoryRow {
  id: string;
  date: string;
  route: string;
  supplier: string;
  price: number | null;
  responseTimeMs: number;
}

export interface HistoryPage {
  items: HistoryRow[];
  total: number;
  page: number;
  pageSize: number;
}

export type SortKey = 'date' | 'route' | 'supplier' | 'price' | 'responseTime';

export interface HistoryQuery {
  from: string;
  to: string;
  fromLocation: string;
  toLocation: string;
  suppliers: string[];
  sortBy: SortKey;
  sortDir: 'asc' | 'desc';
  page: number;
  pageSize: number;
}
