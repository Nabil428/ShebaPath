import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { tap } from 'rxjs';
import { ApiBase } from './api-base';
import { GuideSummary } from '../models/models';

@Injectable({ providedIn: 'root' })
export class BookmarkService {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiBase);

  // Slugs of guides the current user has saved — kept in memory so buttons
  // across the app (list cards, detail page) stay in sync without refetching.
  readonly savedSlugs = signal<Set<string>>(new Set());
  readonly loaded = signal(false);

  loadAll() {
    return this.http
      .get<GuideSummary[]>(this.api.endpoint('account/bookmarks'), { withCredentials: true })
      .pipe(
        tap((guides) => {
          this.savedSlugs.set(new Set(guides.map((g) => g.slug)));
          this.loaded.set(true);
        }),
      );
  }

  isSaved(slug: string): boolean {
    return this.savedSlugs().has(slug);
  }

  toggle(slug: string) {
    const isSaved = this.isSaved(slug);
    const request = isSaved
      ? this.http.delete(this.api.endpoint(`account/bookmarks/${slug}`), { withCredentials: true })
      : this.http.post(this.api.endpoint(`account/bookmarks/${slug}`), {}, { withCredentials: true });

    return request.pipe(
      tap(() => {
        const next = new Set(this.savedSlugs());
        if (isSaved) {
          next.delete(slug);
        } else {
          next.add(slug);
        }
        this.savedSlugs.set(next);
      }),
    );
  }

  clear() {
    this.savedSlugs.set(new Set());
    this.loaded.set(false);
  }
}
