import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import type { ActiveElement, ChartEvent, TooltipItem } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { BAR_ROW_HEIGHT, BarChart, BarItem, shortLabel } from './bar-chart';
import { resolveCssColor } from './css-color';

@Component({
  imports: [BarChart],
  template: `<app-bar-chart
    [items]="items()"
    label="Spesa per categoria"
    [format]="format"
    (itemClick)="clicked.push($event)"
  />`,
})
class Host {
  readonly items = signal<BarItem[]>([
    { label: 'Spesa', value: 230 },
    { label: 'Abbigliamento e accessori', value: 80 },
  ]);
  readonly format = (value: number) => `${value} €`;
  readonly clicked: number[] = [];
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

describe('BarChart', () => {
  let fixture: ComponentFixture<Host>;

  beforeEach(() => {
    // jsdom has no canvas: the chart is configured but never drawn.
    vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(null);
  });

  afterEach(() => vi.unstubAllGlobals());

  function create(): BaseChartDirective<'bar'> {
    fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    return fixture.debugElement
      .query(By.directive(BaseChartDirective))
      .injector.get(BaseChartDirective) as BaseChartDirective<'bar'>;
  }

  const host = () => fixture.nativeElement.querySelector('app-bar-chart') as HTMLElement;

  it('cuts long names on the axis', () => {
    expect(shortLabel('Spesa')).toBe('Spesa');
    expect(shortLabel('Abbigliamento e accessori')).toBe('Abbigliamento e…');
    expect(shortLabel('Abbigliamento e…')).toHaveLength(16);
  });

  it('draws horizontal bars in the order given, from zero, in one color', () => {
    const chart = create();

    expect(chart.type).toBe('bar');
    expect(chart.options!.indexAxis).toBe('y');
    expect(chart.data!.labels).toEqual(['Spesa', 'Abbigliamento e…']);
    const [dataset] = chart.data!.datasets;
    expect(dataset.data).toEqual([230, 80]);
    expect(dataset.backgroundColor).toBe('var(--pf-chart-1)');
    const x = chart.options!.scales!['x']!;
    expect(x.display).toBe(false);
    expect((x as { beginAtZero?: boolean }).beginAtZero).toBe(true);
  });

  it('grows with its bars', () => {
    create();
    expect(host().style.blockSize).toBe(`${2 * BAR_ROW_HEIGHT + 8}px`);

    fixture.componentInstance.items.update((items) => [...items, { label: 'Svago', value: 45 }]);
    fixture.detectChanges();

    expect(host().style.blockSize).toBe(`${3 * BAR_ROW_HEIGHT + 8}px`);
  });

  it('is an image with a description for screen readers', () => {
    create();

    const canvas = fixture.nativeElement.querySelector('canvas') as HTMLCanvasElement;
    expect(canvas.getAttribute('role')).toBe('img');
    expect(canvas.getAttribute('aria-label')).toBe('Spesa per categoria');
  });

  it('shows the whole name and the formatted value in the tooltip, without a legend', () => {
    const chart = create();

    const plugins = chart.options!.plugins!;
    expect(plugins.legend!.display).toBe(false);
    const callbacks = plugins.tooltip!.callbacks!;
    const item = { dataIndex: 1, parsed: { x: 80, y: 1 } } as TooltipItem<'bar'>;
    const title = callbacks.title as (items: TooltipItem<'bar'>[]) => string;
    const label = callbacks.label as (item: TooltipItem<'bar'>) => string;
    expect(title.call(undefined as never, [item])).toBe('Abbigliamento e accessori');
    expect(label.call(undefined as never, item)).toBe('80 €');
  });

  it('emits the index of the bar clicked, and nothing for a click beside the bars', () => {
    const chart = create();
    const onClick = chart.options!.onClick as (
      event: ChartEvent,
      elements: ActiveElement[],
    ) => void;

    onClick({} as ChartEvent, [{ index: 1 } as ActiveElement]);
    onClick({} as ChartEvent, []);

    expect(fixture.componentInstance.clicked).toEqual([1]);
  });

  it('shows a pointer over a bar', () => {
    const chart = create();
    const onHover = chart.options!.onHover as (
      event: ChartEvent,
      elements: ActiveElement[],
    ) => void;
    const canvas = document.createElement('canvas');
    const event = { native: { target: canvas } } as unknown as ChartEvent;

    onHover(event, [{ index: 0 } as ActiveElement]);
    expect(canvas.style.cursor).toBe('pointer');
    onHover(event, []);
    expect(canvas.style.cursor).toBe('default');
  });

  it('follows changes of the bars', () => {
    const chart = create();

    fixture.componentInstance.items.set([{ label: 'Svago', value: 45 }]);
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
