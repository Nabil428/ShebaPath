import { Component, input, signal } from '@angular/core';

@Component({
  selector: 'app-share-button',
  standalone: true,
  template: `
    <button class="btn btn-outline share-btn" type="button" (click)="share()">
      {{ copied() ? '✓ Link copied' : '↗ Share' }}
    </button>
  `,
  styles: [
    `
      .share-btn {
        min-width: 120px;
      }
    `,
  ],
})
export class ShareButtonComponent {
  readonly title = input.required<string>();
  readonly url = input.required<string>();

  readonly copied = signal(false);

  async share(): Promise<void> {
    const shareData = { title: this.title(), url: this.url() };
    const nav = navigator as Navigator & { share?: (data: typeof shareData) => Promise<void> };

    if (nav.share) {
      try {
        await nav.share(shareData);
        return;
      } catch {
        // user cancelled or share failed — fall through to clipboard copy
      }
    }

    try {
      await navigator.clipboard.writeText(this.url());
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 2000);
    } catch {
      // clipboard also unavailable — silently no-op
    }
  }
}
