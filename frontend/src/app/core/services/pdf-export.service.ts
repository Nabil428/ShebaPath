import { Injectable } from '@angular/core';
import jsPDF from 'jspdf';
import { GuideDetail, BlogDetail } from '../models/models';

const BRAND_GREEN: [number, number, number] = [10, 107, 62];
const BRAND_DARK: [number, number, number] = [1, 50, 32];
const INK: [number, number, number] = [30, 33, 30];
const MUTED: [number, number, number] = [110, 116, 110];
const PAGE_WIDTH = 210;
const MARGIN = 18;
const CONTENT_WIDTH = PAGE_WIDTH - MARGIN * 2;

@Injectable({ providedIn: 'root' })
export class PdfExportService {
  exportGuide(guide: GuideDetail): void {
    const doc = new jsPDF({ unit: 'mm', format: 'a4' });
    let y = this.drawHeader(doc, guide.category);

    y = this.drawTitle(doc, guide.title, y);
    y = this.drawParagraph(doc, guide.summary, y, { italic: true, color: MUTED });
    y += 4;

    // Key facts
    const facts: [string, string | null][] = [
      ['Fees', guide.fees],
      ['Processing time', guide.processingTime],
      ['Where to go', guide.office],
      ['Last verified', this.formatDate(guide.lastVerified)],
    ];
    y = this.drawFactBox(doc, facts, y);
    y += 6;

    y = this.drawSectionHeading(doc, 'Steps to apply', y);
    y = this.drawNumberedList(doc, guide.steps, y);
    y += 4;

    y = this.drawSectionHeading(doc, "What you'll need", y);
    y = this.drawBulletList(doc, guide.requirements, y);

    this.drawFooter(doc);
    doc.save(`${guide.slug}-shebapath-guide.pdf`);
  }

  exportBlogPost(post: BlogDetail): void {
    const doc = new jsPDF({ unit: 'mm', format: 'a4' });
    let y = this.drawHeader(doc, 'Blog');

    y = this.drawTitle(doc, post.title, y);
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(9.5);
    doc.setTextColor(...MUTED);
    doc.text(this.formatDate(post.publishedAt), MARGIN, y);
    y += 8;

    const paragraphs = post.content.split(/\n+/).filter((p) => p.trim().length > 0);
    for (const para of paragraphs) {
      y = this.drawParagraph(doc, para, y);
      y += 3;
    }

    this.drawFooter(doc);
    doc.save(`${post.slug}-shebapath-blog.pdf`);
  }

  // ---------- drawing helpers ----------

  private drawHeader(doc: jsPDF, tag: string): number {
    doc.setFillColor(...BRAND_DARK);
    doc.rect(0, 0, PAGE_WIDTH, 22, 'F');
    doc.setTextColor(255, 255, 255);
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(15);
    doc.text('ShebaPath', MARGIN, 14);
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(9);
    doc.text('Bangladesh Government Service Guides', MARGIN, 19);

    doc.setFillColor(...BRAND_GREEN);
    doc.roundedRect(PAGE_WIDTH - MARGIN - 28, 6, 28, 9, 2, 2, 'F');
    doc.setFontSize(8);
    doc.setTextColor(255, 255, 255);
    doc.text(tag.toUpperCase(), PAGE_WIDTH - MARGIN - 14, 11.5, { align: 'center' });

    return 34;
  }

  private drawTitle(doc: jsPDF, title: string, y: number): number {
    doc.setTextColor(...INK);
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(18);
    const lines = doc.splitTextToSize(title, CONTENT_WIDTH);
    doc.text(lines, MARGIN, y);
    return y + lines.length * 7 + 3;
  }

  private drawSectionHeading(doc: jsPDF, text: string, y: number): number {
    y = this.ensureSpace(doc, y, 12);
    doc.setTextColor(...BRAND_GREEN);
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(12.5);
    doc.text(text, MARGIN, y);
    doc.setDrawColor(...BRAND_GREEN);
    doc.setLineWidth(0.4);
    doc.line(MARGIN, y + 1.5, MARGIN + 20, y + 1.5);
    return y + 8;
  }

  private drawParagraph(
    doc: jsPDF,
    text: string,
    y: number,
    opts?: { italic?: boolean; color?: [number, number, number] }
  ): number {
    doc.setFont('helvetica', opts?.italic ? 'italic' : 'normal');
    doc.setFontSize(10.5);
    doc.setTextColor(...(opts?.color ?? INK));
    const lines = doc.splitTextToSize(text, CONTENT_WIDTH);
    for (const line of lines) {
      y = this.ensureSpace(doc, y, 6);
      doc.text(line, MARGIN, y);
      y += 5.5;
    }
    return y;
  }

  private drawNumberedList(doc: jsPDF, items: string[], y: number): number {
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(10.5);
    doc.setTextColor(...INK);
    items.forEach((item, i) => {
      const marker = `${i + 1}.`;
      const lines = doc.splitTextToSize(item, CONTENT_WIDTH - 8);
      y = this.ensureSpace(doc, y, lines.length * 5.5 + 2);
      doc.setFont('helvetica', 'bold');
      doc.text(marker, MARGIN, y);
      doc.setFont('helvetica', 'normal');
      doc.text(lines, MARGIN + 7, y);
      y += lines.length * 5.5 + 2;
    });
    return y;
  }

  private drawBulletList(doc: jsPDF, items: string[], y: number): number {
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(10.5);
    doc.setTextColor(...INK);
    items.forEach((item) => {
      const lines = doc.splitTextToSize(item, CONTENT_WIDTH - 8);
      y = this.ensureSpace(doc, y, lines.length * 5.5 + 2);
      doc.setFillColor(...BRAND_GREEN);
      doc.circle(MARGIN + 1.5, y - 1.5, 1, 'F');
      doc.text(lines, MARGIN + 7, y);
      y += lines.length * 5.5 + 2;
    });
    return y;
  }

  private drawFactBox(doc: jsPDF, facts: [string, string | null][], y: number): number {
    const visible = facts.filter(([, v]) => !!v);
    if (visible.length === 0) return y;

    const rowHeight = 8;
    const boxHeight = visible.length * rowHeight + 6;
    y = this.ensureSpace(doc, y, boxHeight);

    doc.setFillColor(245, 248, 245);
    doc.roundedRect(MARGIN, y, CONTENT_WIDTH, boxHeight, 2, 2, 'F');

    let rowY = y + 8;
    for (const [label, value] of visible) {
      doc.setFont('helvetica', 'bold');
      doc.setFontSize(9.5);
      doc.setTextColor(...BRAND_GREEN);
      doc.text(label.toUpperCase(), MARGIN + 5, rowY);

      doc.setFont('helvetica', 'normal');
      doc.setFontSize(9.5);
      doc.setTextColor(...INK);
      const lines = doc.splitTextToSize(value ?? '', CONTENT_WIDTH - 55);
      doc.text(lines[0] ?? '', MARGIN + 48, rowY);
      rowY += rowHeight;
    }
    return y + boxHeight;
  }

  private drawFooter(doc: jsPDF): void {
    const pageCount = doc.getNumberOfPages();
    for (let i = 1; i <= pageCount; i++) {
      doc.setPage(i);
      doc.setDrawColor(...MUTED);
      doc.setLineWidth(0.2);
      doc.line(MARGIN, 285, PAGE_WIDTH - MARGIN, 285);
      doc.setFont('helvetica', 'normal');
      doc.setFontSize(8);
      doc.setTextColor(...MUTED);
      doc.text(
        'ShebaPath is an independent guide, not an official government website. Always confirm with the relevant office.',
        MARGIN,
        290
      );
      doc.text(`Generated ${this.formatDate(new Date().toISOString())}`, PAGE_WIDTH - MARGIN, 290, {
        align: 'right',
      });
    }
  }

  private ensureSpace(doc: jsPDF, y: number, needed: number): number {
    if (y + needed > 278) {
      doc.addPage();
      return 20;
    }
    return y;
  }

  private formatDate(iso: string): string {
    return new Date(iso).toLocaleDateString('en-GB', {
      day: 'numeric',
      month: 'long',
      year: 'numeric',
    });
  }
}
