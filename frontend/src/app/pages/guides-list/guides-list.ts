import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { GuidesService } from '../../core/services/guides.service';
import { GuideSummary } from '../../core/models/models';

@Component({
  selector: 'app-guides-list',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './guides-list.html',
  styleUrl: './guides-list.scss',
})
export class GuidesListPage implements OnInit {
  private readonly guidesService = inject(GuidesService);

  readonly guides = signal<GuideSummary[]>([]);
  readonly loading = signal(true);

  ngOnInit(): void {
    this.guidesService.list().subscribe((guides) => {
      this.guides.set(guides);
      this.loading.set(false);
    });
  }
}
