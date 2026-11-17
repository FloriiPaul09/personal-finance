import { DOCUMENT } from '@angular/common';
import { Component, computed, DestroyRef, inject, input, output, signal } from '@angular/core';
import {
  ActiveElement,
  BarController,
  BarElement,
  CategoryScale,
  ChartData,
  ChartEvent,
  ChartOptions,
  LinearScale,
  Tooltip,
} from 'chart.js';
import { BaseChartDirective, provideCharts } from 'ng2-charts';
import { resolveCssColor } from './css-color';

/** A bar: what it is and how much (positive). */
export interface BarItem {
  readonly label: string;
  readonly value: number;
}

/** Height of a bar and its gap, in pixels: the chart grows with its bars. */
export const BAR_ROW_HEIGHT = 32;
/** Longer names are cut on the axis (the tooltip and the text beside the chart show them all). */
export const BAR_LABEL_LENGTH = 16;

/** The name as the axis shows it: cut with "…" past `BAR_LABEL_LENGTH` characters. */
export function shortLabel(label: string): string {
  return label.length > BAR_LABEL_LENGTH ? `${label.slice(0, BAR_LABEL_LENGTH - 1)}…` : label;
}

/**
 * Horizontal bars of one series, in the order given, each as long as its value compared with the
 * largest (Chart.js through ng2-charts), e.g. the spending of a month per category, largest first:
 * `<app-bar-chart [items]="items" [label]="…" [format]="formatAmount" (itemClick)="open($event)" />`.
 *
 * - The names are on the axis; hover or tap shows the whole name and `format(value)`. A canvas is
 *   not read by screen readers nor reached with the keyboard, so the page always shows the names,
 *   the amounts and their links in text beside the chart.
 * - A click on a bar emits its index (`itemClick`), e.g. to open the movements behind it.
 * - One color (`--pf-chart-1` unless given), read again when the system switches light / dark.
 * - Without animation when the system asks for reduced motion.
 *
 * Only the parts of Chart.js a bar chart needs are registered, here, so the library lands in the
 * chunk of the page that uses the chart, not in the initial bundle.
 */
@Component({
  selector: 'app-bar-chart',
  imports: [BaseChartDirective],
  providers: [
    provideCharts({
      registerables: [BarController, BarElement, CategoryScale, LinearScale, Tooltip],
    }),
  ],
  host: { '[style.block-size.px]': 'height()' },
  template: `
    <canvas
      baseChart
      type="bar"
      [data]="data()"
      [options]="options()"
      role="img"
      [attr.aria-label]="label()"
    ></canvas>
  `,
  styles: `
    :host {
      display: block;
      position: relative;
      inline-size: 100%;
    }
  `,
})
export class BarChart {
  private readonly document = inject(DOCUMENT);

  readonly items = input.required<readonly BarItem[]>();
  /** What the chart shows, for screen readers (e.g. "Spesa di novembre per categoria"). */
  readonly label = input.required<string>();
  /** How a value reads in the tooltip (e.g. an amount with its currency). */
  readonly format = input<(value: number) => string>(String);
  /** A CSS color for the bars, e.g. a token. */
  readonly color = input('var(--pf-chart-1)');

  /** The index of the bar clicked or tapped. */
  readonly itemClick = output<number>();

  protected readonly height = computed(() => this.items().length * BAR_ROW_HEIGHT + 8);

  /** Bumped when the system switches light / dark, so the colors are read again. */
  private readonly scheme = signal(0);
  private readonly reducedMotion =
    this.document.defaultView?.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false;

  protected readonly data = computed<ChartData<'bar'>>(() => {
    this.scheme();
    const items = this.items();
    return {
      labels: items.map((item) => shortLabel(item.label)),
      datasets: [
        {
          data: items.map((item) => item.value),
          backgroundColor: this.resolve(this.color()),
          borderRadius: 4,
          borderSkipped: 'start',
          barPercentage: 0.75,
          categoryPercentage: 1,
          maxBarThickness: 24,
        },
      ],
    };
  });

  protected readonly options = computed<ChartOptions<'bar'>>(() => {
    this.scheme();
    const format = this.format();
    const font = { family: 'Roboto, sans-serif', size: 14 };
    return {
      indexAxis: 'y',
      responsive: true,
      maintainAspectRatio: false,
      animation: this.reducedMotion ? false : undefined,
      layout: { padding: { right: 8 } },
      scales: {
        x: { display: false, beginAtZero: true },
        y: {
          grid: { display: false },
          border: { display: false },
          ticks: { autoSkip: false, color: this.resolve('var(--mat-sys-on-surface)'), font },
        },
      },
      onClick: (_event: ChartEvent, elements: ActiveElement[]) => {
        const [hit] = elements;
        if (hit) {
          this.itemClick.emit(hit.index);
        }
      },
      onHover: (event: ChartEvent, elements: ActiveElement[]) => {
        const target = event.native?.target as HTMLElement | null | undefined;
        if (target) {
          target.style.cursor = elements.length > 0 ? 'pointer' : 'default';
        }
      },
      plugins: {
        legend: { display: false },
        tooltip: {
          backgroundColor: this.resolve('var(--mat-sys-inverse-surface)'),
          titleColor: this.resolve('var(--mat-sys-inverse-on-surface)'),
          bodyColor: this.resolve('var(--mat-sys-inverse-on-surface)'),
          titleFont: { ...font, weight: 'normal' },
          bodyFont: font,
          cornerRadius: 4,
          displayColors: false,
          callbacks: {
            title: ([item]) => (item ? (this.items()[item.dataIndex]?.label ?? '') : ''),
            label: (item) => format(item.parsed.x ?? 0),
          },
        },
      },
    };
  });

  constructor() {
    const query = this.document.defaultView?.matchMedia?.('(prefers-color-scheme: dark)');
    if (query) {
      const changed = () => this.scheme.update((count) => count + 1);
      query.addEventListener('change', changed);
      inject(DestroyRef).onDestroy(() => query.removeEventListener('change', changed));
    }
  }

  private resolve(color: string): string {
    return resolveCssColor(this.document, color);
  }
}
