import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiBase } from './api-base';
import { GuideDetail, GuideSummary } from '../models/models';

@Injectable({ providedIn: 'root' })
export class GuidesService {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiBase);

  list(): Observable<GuideSummary[]> {
    return this.http.get<GuideSummary[]>(this.api.endpoint('guides'));
  }

  get(slug: string): Observable<GuideDetail> {
    return this.http.get<GuideDetail>(this.api.endpoint(`guides/${slug}`));
  }

  related(slug: string): Observable<GuideSummary[]> {
    return this.http.get<GuideSummary[]>(this.api.endpoint(`guides/${slug}/related`));
  }
}
