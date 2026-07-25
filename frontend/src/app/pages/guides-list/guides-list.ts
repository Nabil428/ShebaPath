import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { GuidesService } from '../../core/services/guides.service';
import { TagChipsComponent } from '../../shared/tag-chips/tag-chips';
import { GuideSummary } from '../../core/models/models';
import { TranslateSyncService } from '../../core/services/translate-sync.service';

const PAGE_SIZE = 6;

@Component({
  selector: 'app-guides-list',
  standalone: true,
  imports: [RouterLink, TagChipsComponent],
  templateUrl: './guides-list.html',
  styleUrl: './guides-list.scss',
})
export class GuidesListPage implements OnInit {
  private readonly guidesService = inject(GuidesService);
  private readonly route = inject(ActivatedRoute);
  private readonly translateSync = inject(TranslateSyncService);

  readonly guides = signal<GuideSummary[]>([]);
  readonly loading = signal(true);
  readonly searchTerm = signal('');
  readonly page = signal(1);

  readonly filtered = computed(() => {
    const term = this.searchTerm().trim().toLowerCase();
    if (!term) return this.guides();
    return this.guides().filter(
      (g) =>
        g.title.toLowerCase().includes(term) ||
        g.category.toLowerCase().includes(term) ||
        g.summary.toLowerCase().includes(term) ||
        g.tags.some((t) => t.toLowerCase().includes(term))
    );
  });

  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.filtered().length / PAGE_SIZE)));

  readonly pageItems = computed(() => {
    const start = (this.page() - 1) * PAGE_SIZE;
    return this.filtered().slice(start, start + PAGE_SIZE);
  });

  readonly pageNumbers = computed(() => Array.from({ length: this.totalPages() }, (_, i) => i + 1));

  ngOnInit(): void {
    const initialQuery = this.route.snapshot.queryParamMap.get('q');
    if (initialQuery) {
      this.searchTerm.set(initialQuery);
    }
    this.guidesService.list().subscribe((guides) => {
      this.guides.set(guides);
      this.loading.set(false);
      this.translateSync.resync();
    });
  }

  onSearchInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.searchTerm.set(value);
    this.page.set(1);
    this.translateSync.resync();
  }

  goToPage(n: number): void {
    if (n >= 1 && n <= this.totalPages()) {
      this.page.set(n);
      window.scrollTo({ top: 0, behavior: 'smooth' });
      this.translateSync.resync();
    }
  }
}
