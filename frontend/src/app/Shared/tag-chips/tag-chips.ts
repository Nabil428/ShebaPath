import { Component, input } from '@angular/core';

@Component({
  selector: 'app-tag-chips',
  standalone: true,
  template: `
    @if (tags().length) {
      <div class="tag-chips">
        @for (t of tags(); track t) {
          <span class="tag-chip">#{{ t }}</span>
        }
      </div>
    }
  `,
  styles: [
    `
      .tag-chips {
        display: flex;
        flex-wrap: wrap;
        gap: 6px;
        margin: 8px 0;
      }
      .tag-chip {
        font-size: 0.8rem;
        color: var(--color-green-700, #0a6b3e);
        background: rgba(16, 135, 76, 0.1);
        border-radius: 999px;
        padding: 3px 10px;
      }
    `,
  ],
})
export class TagChipsComponent {
  readonly tags = input<string[]>([]);
}
