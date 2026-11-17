import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatIconTestingModule } from '@angular/material/icon/testing';
import { MonthSelector } from './month-selector';

@Component({
  imports: [MonthSelector],
  template: `<app-month-selector [(month)]="month" [max]="max()" />`,
})
class Host {
  readonly month = signal('2026-11');
  readonly max = signal<string | undefined>(undefined);
}

describe('MonthSelector', () => {
  let fixture: ComponentFixture<Host>;
  let host: Host;

  beforeEach(async () => {
    vi.useFakeTimers({ now: new Date('2026-11-17T10:00:00Z'), toFake: ['Date'] });
    TestBed.configureTestingModule({
      imports: [MatIconTestingModule],
    });
    fixture = TestBed.createComponent(Host);
    host = fixture.componentInstance;
    await fixture.whenStable();
  });

  afterEach(() => vi.useRealTimers());

  const element = () => fixture.nativeElement as HTMLElement;
  const label = () => element().querySelector('.label')!.textContent!.trim();
  const button = (name: string) =>
    element().querySelector<HTMLButtonElement>(`button[aria-label="${name}"]`)!;
  const backButton = () =>
    Array.from(element().querySelectorAll('button')).find((b) =>
      b.textContent?.includes('Mese corrente'),
    );

  it('shows the month in Italian, with a capital letter', () => {
    expect(label()).toBe('Novembre 2026');
  });

  it('moves to the previous and next month, across years', async () => {
    button('Mese successivo').click();
    button('Mese successivo').click();
    await fixture.whenStable();
    expect(host.month()).toBe('2027-01');
    expect(label()).toBe('Gennaio 2027');

    button('Mese precedente').click();
    await fixture.whenStable();
    expect(host.month()).toBe('2026-12');
  });

  it('offers to go back to the current month only when another month is shown', async () => {
    expect(backButton()).toBeUndefined();

    button('Mese precedente').click();
    await fixture.whenStable();
    backButton()!.click();
    await fixture.whenStable();

    expect(host.month()).toBe('2026-11');
    expect(backButton()).toBeUndefined();
  });

  it('stops at the last month allowed', async () => {
    host.max.set('2026-11');
    await fixture.whenStable();

    expect(button('Mese successivo').disabled).toBe(true);
    expect(button('Mese precedente').disabled).toBe(false);
  });

  it('announces the month to screen readers when it changes', () => {
    expect(element().querySelector('.label')!.getAttribute('aria-live')).toBe('polite');
  });
});
