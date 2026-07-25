import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiBase } from './api-base';
import { HeroSlidePublic } from '../models/models';

@Injectable({ providedIn: 'root' })
export class HeroSlidesService {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiBase);

  list(): Observable<HeroSlidePublic[]> {
    return this.http.get<HeroSlidePublic[]>(this.api.endpoint('hero-slides'));
  }
}
