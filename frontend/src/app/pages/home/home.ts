import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { GuidesService } from '../../core/services/guides.service';
import { BlogService } from '../../core/services/blog.service';
import { GuideSummary, BlogSummary } from '../../core/models/models';
import { TranslateSyncService } from '../../core/services/translate-sync.service';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './home.html',
  styleUrl: './home.scss',
})
export class HomePage implements OnInit {
  private readonly guidesService = inject(GuidesService);
  private readonly blogService = inject(BlogService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly translateSync = inject(TranslateSyncService);

  readonly guides = signal<GuideSummary[]>([]);
  readonly posts = signal<BlogSummary[]>([]);
  readonly loading = signal(true);

  readonly slideIndex = signal(0);
  readonly searchQuery = signal('');

  ngOnInit(): void {
    this.guidesService.list().subscribe((guides) => {
      this.guides.set(guides.slice(0, 5));
      this.translateSync.resync();
    });
    this.blogService.list().subscribe((posts) => {
      this.posts.set(posts.slice(0, 4));
      this.loading.set(false);
      this.translateSync.resync();
    });

    const timer = setInterval(() => this.nextSlide(), 5000);
    this.destroyRef.onDestroy(() => clearInterval(timer));
  }

  nextSlide(): void {
    const total = this.posts().length || 1;
    this.slideIndex.set((this.slideIndex() + 1) % total);
  }

  prevSlide(): void {
    const total = this.posts().length || 1;
    this.slideIndex.set((this.slideIndex() - 1 + total) % total);
  }

  goToSlide(i: number): void {
    this.slideIndex.set(i);
  }

  onSearchSubmit(event: Event): void {
    event.preventDefault();
    const q = this.searchQuery().trim();
    this.router.navigate(['/guides'], q ? { queryParams: { q } } : {});
  }

  onSearchInput(event: Event): void {
    this.searchQuery.set((event.target as HTMLInputElement).value);
  }
}
