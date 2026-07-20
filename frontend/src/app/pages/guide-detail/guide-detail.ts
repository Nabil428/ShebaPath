import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { switchMap } from 'rxjs';
import { GuidesService } from '../../core/services/guides.service';
import { GuideDetail } from '../../core/models/models';

@Component({
  selector: 'app-guide-detail',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './guide-detail.html',
  styleUrl: './guide-detail.scss',
})
export class GuideDetailPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly guidesService = inject(GuidesService);

  readonly guide = signal<GuideDetail | null>(null);
  readonly notFound = signal(false);
  readonly loading = signal(true);

  ngOnInit(): void {
    this.route.paramMap
      .pipe(switchMap((params) => this.guidesService.get(params.get('slug')!)))
      .subscribe({
        next: (guide) => {
          this.guide.set(guide);
          this.loading.set(false);
        },
        error: () => {
          this.notFound.set(true);
          this.loading.set(false);
        },
      });
  }
}
