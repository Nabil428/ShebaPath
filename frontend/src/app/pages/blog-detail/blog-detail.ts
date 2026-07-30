import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { DatePipe, DOCUMENT } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ShareButtonComponent } from '../../Shared/share-button/share-button';
import { TagChipsComponent } from '../../Shared/tag-chips/tag-chips';
import { switchMap } from 'rxjs';
import { BlogService } from '../../core/services/blog.service';
import { PdfExportService } from '../../core/services/pdf-export.service';
import { BlogDetail } from '../../core/models/models';

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
        },
        error: () => {
          this.notFound.set(true);
          this.loading.set(false);
        },
      });
  }

  downloadPdf(): void {
    const post = this.post();
    if (post) {
      this.pdfExport.exportBlogPost(post);
    }
  }
}
