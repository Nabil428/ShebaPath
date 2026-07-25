import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AdminService } from '../../../core/services/admin.service';
import { Tag } from '../../../core/models/models';

@Component({
  selector: 'app-admin-tags',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './tags.html',
  styleUrl: './tags.scss',
})
export class Tags implements OnInit {
  private readonly admin = inject(AdminService);

  readonly tags = signal<Tag[]>([]);
  readonly newName = signal('');
  readonly editingId = signal<number | null>(null);
  readonly editName = signal('');
  readonly errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.admin.getTags().subscribe((t) => this.tags.set(t));
  }

  create(): void {
    if (!this.newName().trim()) return;
    this.errorMessage.set(null);
    this.admin.createTag(this.newName().trim()).subscribe({
      next: () => {
        this.newName.set('');
        this.refresh();
      },
      error: (err) => this.errorMessage.set(err.error?.error ?? 'Could not create tag.'),
    });
  }

  startEdit(t: Tag): void {
    this.editingId.set(t.id);
    this.editName.set(t.name);
  }

  saveEdit(id: number): void {
    this.admin.updateTag(id, this.editName().trim()).subscribe(() => {
      this.editingId.set(null);
      this.refresh();
    });
  }

  remove(id: number): void {
    if (!confirm('Delete this tag from all guides/posts?')) return;
    this.admin.deleteTag(id).subscribe(() => this.refresh());
  }
}
