import { ShareButtonComponent } from './../../Shared/share-button/share-button';
import { Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe, DOCUMENT } from '@angular/common';
import { Meta, Title } from '@angular/platform-browser';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { switchMap } from 'rxjs';
import { GuidesService } from '../../core/services/guides.service';
import { PdfExportService } from '../../core/services/pdf-export.service';
import { AuthService } from '../../core/services/auth.service';
import { BookmarkService } from '../../core/services/bookmark.service';
import { GuideDetail } from '../../core/models/models';

import { TagChipsComponent } from '../../Shared/tag-chips/tag-chips';
import { TranslateSyncService } from '../../core/services/translate-sync.service';
import { GuideSummary } from '../../core/models/models';

@Component({
  selector: 'app-guide-detail',
  standalone: true,
  imports: [RouterLink, DatePipe, ShareButtonComponent, TagChipsComponent],
  templateUrl: './guide-detail.html',
  styleUrl: './guide-detail.scss',
})
export class GuideDetailPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly guidesService = inject(GuidesService);
  private readonly pdfExport = inject(PdfExportService);
  private readonly document = inject(DOCUMENT);
  protected readonly auth = inject(AuthService);
  protected readonly bookmarks = inject(BookmarkService);
  private readonly translateSync = inject(TranslateSyncService);
  private readonly metaService = inject(Meta);
  private readonly titleService = inject(Title);

  readonly guide = signal<GuideDetail | null>(null);
  readonly notFound = signal(false);
  readonly loading = signal(true);
  readonly savingBookmark = signal(false);
  readonly relatedGuides = signal<GuideSummary[]>([]);

  get pageUrl(): string {
    return this.document.location.href;
  }

  ngOnInit(): void {
    this.route.paramMap
      .pipe(switchMap((params) => this.guidesService.get(params.get('slug')!)))
      .subscribe({
        next: (guide) => {
          this.guide.set(guide);
          this.loading.set(false);
          this.applySeoTags(guide);
          this.translateSync.resync();
          this.guidesService.related(guide.slug).subscribe((g) => this.relatedGuides.set(g));
        },
        error: () => {
          this.notFound.set(true);
          this.loading.set(false);
        },
      });

    if (this.auth.isAuthenticated() && !this.bookmarks.loaded()) {
      this.bookmarks.loadAll().subscribe();
    }
  }

  toggleBookmark(): void {
    const guide = this.guide();
    if (!guide || this.savingBookmark()) return;
    this.savingBookmark.set(true);
    this.bookmarks.toggle(guide.slug).subscribe({
      complete: () => this.savingBookmark.set(false),
      error: () => this.savingBookmark.set(false),
    });
  }

  private applySeoTags(guide: GuideDetail): void {
    this.titleService.setTitle(`${guide.title} — ShebaPath`);
    this.metaService.updateTag({ name: 'description', content: guide.metaDescription || guide.summary });
    const keywordList = guide.keywords || guide.tags?.join(', ');
    if (keywordList) {
      this.metaService.updateTag({ name: 'keywords', content: keywordList });
    }
  }

  downloadPdf(): void {
    const guide = this.guide();
    if (guide) {
      this.pdfExport.exportGuide(guide);
    }
  }
}
