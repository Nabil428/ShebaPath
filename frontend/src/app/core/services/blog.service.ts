import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiBase } from './api-base';
import { BlogDetail, BlogSummary } from '../models/models';

@Injectable({ providedIn: 'root' })
export class BlogService {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiBase);

  list(): Observable<BlogSummary[]> {
    return this.http.get<BlogSummary[]>(this.api.endpoint('blog'));
  }

  get(slug: string): Observable<BlogDetail> {
    return this.http.get<BlogDetail>(this.api.endpoint(`blog/${slug}`));
  }
}
