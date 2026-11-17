import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import type { TooltipItem } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { resolveCssColor } from './css-color';
import { CHART_COLORS, CHART_OTHER_COLOR, chartColor, DonutChart, DonutSlice } from './donut-chart';

@Component({
  imports: [DonutChart],
  template: `<app-donut-chart [slices]="slices()" label="Spesa per categoria" [format]="format" />`,
})
class Host {
  readonly slices = signal<DonutSlice[]>([
    { label: 'Spesa', value: 230, color: chartColor(0) },
    { label: 'Casa', value: 80, color: chartColor(1) },
  ]);
  readonly format = (value: number) => `${value} €`;
}

/** A `matchMedia` whose answers and change events the test controls. */
function fakeMatchMedia(matches: Record<string, boolean>) {
  const listeners: (() => void)[] = [];
  const matchMedia = vi.fn((query: string) => ({
    matches: matches[query] ?? false,
    addEventListener: (_: string, listener: () => void) => listeners.push(listener),
    removeEventListener: (_: string, listener: () => void) =>
      listeners.splice(listeners.indexOf(listener), 1),
  }));
  vi.stubGlobal('matchMedia', matchMedia);
  return { change: () => listeners.forEach((listener) => listener()), listeners };
}

describe('DonutChart', () => {
  let fixture: ComponentFixture<Host>;

  beforeEach(() => {
    // jsdom has no canvas: the chart is configured but never drawn.
    vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(null);
  });

  afterEach(() => vi.unstubAllGlobals());

  function create(): BaseChartDirective<'doughnut'> {
    fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    return fixture.debugElement
      .query(By.directive(BaseChartDirective))
      .injector.get(BaseChartDirective) as BaseChartDirective<'doughnut'>;
  }

  it('gives the first five parts a color each, then the neutral one', () => {
    expect(CHART_COLORS).toHaveLength(5);
    expect([0, 4, 5, 12].map(chartColor)).toEqual([
      'var(--pf-chart-1)',
      'var(--pf-chart-5)',
      CHART_OTHER_COLOR,
      CHART_OTHER_COLOR,
    ]);
  });

  it('draws the parts in order with their colors, separated by the surface', () => {
    const chart = create();

    expect(chart.type).toBe('doughnut');
    expect(chart.data!.labels).toEqual(['Spesa', 'Casa']);
    const [dataset] = chart.data!.datasets;
    expect(dataset.data).toEqual([230, 80]);
    expect(dataset.backgroundColor).toEqual(['var(--pf-chart-1)', 'var(--pf-chart-2)']);
    expect(dataset.borderColor).toBe('var(--mat-sys-surface-container-low)');
    expect(dataset.borderWidth).toBe(2);
  });

  it('is an image with a description for screen readers', () => {
    create();

    const canvas = fixture.nativeElement.querySelector('canvas') as HTMLCanvasElement;
    expect(canvas.getAttribute('role')).toBe('img');
    expect(canvas.getAttribute('aria-label')).toBe('Spesa per categoria');
  });

  it('shows the formatted value in the tooltip, without a legend', () => {
    const chart = create();

    const plugins = chart.options!.plugins!;
    expect(plugins.legend!.display).toBe(false);
    const label = plugins.tooltip!.callbacks!.label as (item: TooltipItem<'doughnut'>) => string;
    expect(label.call(undefined as never, { parsed: 80 } as TooltipItem<'doughnut'>)).toBe('80 €');
  });

  it('follows changes of the parts', () => {
    const chart = create();

    fixture.componentInstance.slices.set([{ label: 'Svago', value: 45, color: chartColor(0) }]);
    fixture.detectChanges();

    expect(chart.data!.labels).toEqual(['Svago']);
  });

  it('reads the colors again when the system switches light / dark', () => {
    const media = fakeMatchMedia({});
    const chart = create();
    const before = chart.data;

    media.change();
    fixture.detectChanges();

    expect(chart.data).not.toBe(before);
    fixture.destroy();
    expect(media.listeners).toHaveLength(0);
  });

  it('does not animate when the system asks for reduced motion', () => {
    fakeMatchMedia({ '(prefers-reduced-motion: reduce)': true });

    expect(create().options!.animation).toBe(false);
  });

  it('animates otherwise', () => {
    fakeMatchMedia({});

    expect(create().options!.animation).toBeUndefined();
  });
});

describe('resolveCssColor', () => {
  it('returns the computed color and leaves nothing in the page', () => {
    expect(resolveCssColor(document, 'rgb(1, 2, 3)')).toBe('rgb(1, 2, 3)');
    expect(document.body.children).toHaveLength(0);
  });

  it('returns the value as it is when the browser cannot compute it', () => {
    expect(resolveCssColor(document, 'var(--pf-chart-1)')).toBe('var(--pf-chart-1)');
  });
});
