import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { HistoryPage, HistoryQuery, Place, SearchCreated, SearchFormValue } from './models';

const BASE = '/api';

export interface StreamMessage {
  event: string;
  data: unknown;
}

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);

  suppliers(): Promise<string[]> {
    return firstValueFrom(this.http.get<string[]>(`${BASE}/suppliers`));
  }

  history(query: HistoryQuery): Promise<HistoryPage> {
    let params = new HttpParams()
      .set('sortBy', query.sortBy)
      .set('sortDir', query.sortDir)
      .set('page', query.page)
      .set('pageSize', query.pageSize);

    if (query.from) {
      params = params.set('from', query.from);
    }
    if (query.to) {
      params = params.set('to', query.to);
    }
    if (query.fromLocation) {
      params = params.set('fromLocation', query.fromLocation);
    }
    if (query.toLocation) {
      params = params.set('toLocation', query.toLocation);
    }
    for (const supplier of query.suppliers) {
      params = params.append('suppliers', supplier);
    }

    return firstValueFrom(this.http.get<HistoryPage>(`${BASE}/history`, { params }));
  }

  places(query: string): Promise<Place[]> {
    const params = new HttpParams().set('q', query);
    return firstValueFrom(this.http.get<Place[]>(`${BASE}/places`, { params }));
  }

  createSearch(value: SearchFormValue): Promise<SearchCreated> {
    return firstValueFrom(this.http.post<SearchCreated>(`${BASE}/search`, value));
  }

  cancelSearch(searchId: string): Promise<void> {
    return firstValueFrom(this.http.post<void>(`${BASE}/search/${searchId}/cancel`, {}));
  }

  /**
   * Reads the SSE stream with fetch instead of EventSource: EventSource cannot POST, cannot be
   * aborted on demand, and silently reconnects - which here would start a duplicate search.
   */
  async stream(searchId: string, onMessage: (message: StreamMessage) => void, signal: AbortSignal): Promise<void> {
    const response = await fetch(`${BASE}/search/${searchId}/stream`, {
      signal,
      headers: { Accept: 'text/event-stream' },
      cache: 'no-store'
    });

    if (!response.ok || !response.body) {
      throw new Error(`The search stream failed (HTTP ${response.status}).`);
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';

    try {
      while (true) {
        const { value, done } = await reader.read();
        if (done) {
          break;
        }

        // Normalise line endings so a proxy that rewrites them cannot merge two events.
        buffer = (buffer + decoder.decode(value, { stream: true })).replace(/\r\n/g, '\n');

        let boundary = buffer.indexOf('\n\n');
        while (boundary >= 0) {
          const chunk = buffer.slice(0, boundary);
          buffer = buffer.slice(boundary + 2);

          const parsed = this.parseChunk(chunk);
          if (parsed) {
            onMessage(parsed);
          }

          boundary = buffer.indexOf('\n\n');
        }
      }
    } finally {
      reader.releaseLock();
    }
  }

  private parseChunk(chunk: string): StreamMessage | null {
    let event = 'message';
    const dataLines: string[] = [];

    for (const line of chunk.split('\n')) {
      if (line.startsWith('event:')) {
        event = line.slice(6).trim();
      } else if (line.startsWith('data:')) {
        dataLines.push(line.slice(5).trim());
      }
    }

    if (dataLines.length === 0) {
      return null;
    }

    try {
      return { event, data: JSON.parse(dataLines.join('\n')) as unknown };
    } catch {
      return null;
    }
  }
}