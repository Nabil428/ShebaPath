import { Component, input } from '@angular/core';

@Component({
  selector: 'app-skeleton-card',
  standalone: true,
  template: `
    <div class="skeleton-card">
      <div class="skel-line short"></div>
      <div class="skel-line title"></div>
      <div class="skel-line"></div>
      <div class="skel-line" style="width: 70%"></div>
    </div>
  `,
  styles: [
    `
      .skeleton-card {
        background: #fff;
        border-radius: 12px;
        padding: 20px;
        box-shadow: 0 1px 3px rgba(0, 0, 0, 0.06);
      }
      .skel-line {
        height: 12px;
        border-radius: 6px;
        margin-bottom: 10px;
        background: linear-gradient(90deg, #eee 25%, #f5f5f5 50%, #eee 75%);
        background-size: 200% 100%;
        animation: shimmer 1.4s infinite;
      }
      .skel-line.short { width: 40%; height: 10px; }
      .skel-line.title { width: 85%; height: 18px; margin-bottom: 14px; }
      @keyframes shimmer {
        0% { background-position: 200% 0; }
        100% { background-position: -200% 0; }
      }
    `,
  ],
})
export class SkeletonCardComponent {
  readonly count = input(1);
}
