import { DOCUMENT } from '@angular/common';
import { inject, Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class ApiBase {
  private readonly document = inject(DOCUMENT);

  get url(): string {
    return new URL('api/', this.document.baseURI).toString();
  }

  endpoint(path: string): string {
    return new URL(path.replace(/^\//, ''), this.url).toString();
  }
}
