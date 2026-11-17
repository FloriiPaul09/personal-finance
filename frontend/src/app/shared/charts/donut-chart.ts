import { DOCUMENT } from '@angular/common';
import { Component, computed, DestroyRef, inject, input, signal } from '@angular/core';
import { ArcElement, ChartData, ChartOptions, DoughnutController, Tooltip } from 'chart.js';
import { BaseChartDirective, provideCharts } from 'ng2-charts';
import { resolveCssColor } from './css-color';

/** A part of the whole: what it is, how much, and its color (usually a token). */
export interface DonutSlice {
  readonly label: string;
  /** Positive: the size of the slice. */
  readonly value: number;
  /** A CSS color, e.g. `var(--pf-chart-1)`; resolved for the canvas by the chart. */
  readonly color: string;
}

/**
 * Categorical colors in their fixed order (styles.scss, `--pf-chart-*`): the first part takes the
 * first color, and so on. Past the fifth, parts take `CHART_OTHER_COLOR`: more colors would no
 * longer be told apart.
 */
export const CHART_COLORS: readonly string[] = [
  'var(--pf-chart-1)',
  'var(--pf-chart-2)',
  'var(--pf-chart-3)',
  'var(--pf-chart-4)',
  'var(--pf-chart-5)',
];
export const CHART_OTHER_COLOR = 'var(--pf-chart-other)';

/** The color of the part at `index` (0-based) in a chart with `CHART_COLORS`. */
export function chartColor(index: number): string {
  return CHART_COLORS[index] ?? CHART_OTHER_COLOR;
}

/**
 * A donut of the parts of a whole (Chart.js through ng2-charts), e.g. the spending of a month
 * per category: `<app-donut-chart [slices]="slices" [label]="…" [format]="formatAmount" />`.
 *
 * - The parts are drawn in the order given, separated by a gap of the surface color.
 * - Hover or tap shows the name and `format(value)`; the canvas has no other text, so the page
 *   always shows the names and amounts beside it (a list with a swatch per row).
 * - Colors follow the color scheme: they are read again when the system switches light / dark.
 * - Without animation when the system asks for reduced motion.
 *
 * Only the parts of Chart.js a donut needs are registered, here, so the library lands in the
 * chunk of the page that uses the chart, not in the initial bundle.
 */
@Component({
  selector: 'app-donut-chart',
  imports: [BaseChartDirective],
  providers: [provideCharts({ registerables: [DoughnutController, ArcElement, Tooltip] })],
  template: `
    <canvas
      baseChart
      type="doughnut"
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
      inline-size: 176px;
      block-size: 176px;
    }
  `,
})
export class DonutChart {
  private readonly document = inject(DOCUMENT);

  readonly slices = input.required<readonly DonutSlice[]>();
  /** What the chart shows, for screen readers (e.g. "Spesa di novembre per categoria"). */
  readonly label = input.required<string>();
  /** How a value reads in the tooltip (e.g. an amount with its currency). */
  readonly format = input<(value: number) => string>(String);
  /** The color behind the chart: the gaps between the parts are drawn with it. */
  readonly surface = input('var(--mat-sys-surface-container-low)');

  /** Bumped when the system switches light / dark, so the colors are read again. */
  private readonly scheme = signal(0);
  private readonly reducedMotion =
    this.document.defaultView?.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false;

  protected readonly data = computed<ChartData<'doughnut'>>(() => {
    this.scheme();
    const slices = this.slices();
    return {
      labels: slices.map((slice) => slice.label),
      datasets: [
        {
          data: slices.map((slice) => slice.value),
          backgroundColor: slices.map((slice) => this.resolve(slice.color)),
          borderColor: this.resolve(this.surface()),
          borderWidth: 2,
          borderRadius: 2,
          hoverOffset: 4,
        },
      ],
    };
  });

  protected readonly options = computed<ChartOptions<'doughnut'>>(() => {
    this.scheme();
    const format = this.format();
    const font = { family: 'Roboto, sans-serif', size: 14 };
    return {
      responsive: true,
      maintainAspectRatio: false,
      cutout: '62%',
      animation: this.reducedMotion ? false : undefined,
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
          callbacks: { label: (item) => format(item.parsed) },
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
