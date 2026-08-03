import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { DatePipe, DOCUMENT } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ShareButtonComponent } from '../../Shared/share-button/share-button';
import { TagChipsComponent } from '../../Shared/tag-chips/tag-chips';
import { switchMap } from 'rxjs';
import { BlogService } from '../../core/services/blog.service';
import { PdfExportService } from '../../core/services/pdf-export.service';
import { BlogDetail } from '../../core/models/models';
import { Meta, Title } from '@angular/platform-browser';

@Component({
  selector: 'app-blog-detail',
  standalone: true,
  imports: [RouterLink, DatePipe, ShareButtonComponent, TagChipsComponent],
  templateUrl: './blog-detail.html',
  styleUrl: './blog-detail.scss',
})
export class BlogDetailPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly blogService = inject(BlogService);
  private readonly pdfExport = inject(PdfExportService);
  private readonly document = inject(DOCUMENT);
  private readonly metaService = inject(Meta);
  private readonly titleService = inject(Title);

  get pageUrl(): string {
    return this.document.location.href;
  }

  readonly post = signal<BlogDetail | null>(null);
  readonly notFound = signal(false);
  readonly loading = signal(true);

  readonly readingTime = computed(() => {
    const content = this.post()?.content ?? '';
    const words = content.trim().split(/\s+/).filter(Boolean).length;
    return Math.max(1, Math.round(words / 200));
  });

  ngOnInit(): void {
    this.route.paramMap
      .pipe(switchMap((params) => this.blogService.get(params.get('slug')!)))
      .subscribe({
        next: (post) => {
          this.post.set(post);
          this.loading.set(false);
          this.applySeoTags(post);
        },
        error: () => {
          this.notFound.set(true);
          this.loading.set(false);
        },
      });
  }

  private applySeoTags(post: BlogDetail): void {
    const url = this.document.location.href;
    this.titleService.setTitle(`${post.title} — ShebaPath Blog`);
    this.metaService.updateTag({ name: 'description', content: post.excerpt });

    this.metaService.updateTag({ property: 'og:type', content: 'article' });
    this.metaService.updateTag({ property: 'og:title', content: post.title });
    this.metaService.updateTag({ property: 'og:description', content: post.excerpt });
    this.metaService.updateTag({ property: 'og:url', content: url });
    this.metaService.updateTag({ name: 'twitter:card', content: 'summary_large_image' });
    this.metaService.updateTag({ name: 'twitter:title', content: post.title });
    this.metaService.updateTag({ name: 'twitter:description', content: post.excerpt });
    if (post.coverImageUrl) {
      this.metaService.updateTag({ property: 'og:image', content: post.coverImageUrl });
      this.metaService.updateTag({ name: 'twitter:image', content: post.coverImageUrl });
    }
  }

  downloadPdf(): void {
    const post = this.post();
    if (post) {
      this.pdfExport.exportBlogPost(post);
    }
  }
}
