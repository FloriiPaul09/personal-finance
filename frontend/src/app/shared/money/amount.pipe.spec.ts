import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AmountPipe } from './amount.pipe';

@Component({
  imports: [AmountPipe],
  template: `<span class="plain">{{ value() | amount: 'EUR' }}</span>
    <span class="signed">{{ value() | amount: 'EUR' : { signed: true } }}</span>`,
})
class Host {
  readonly value = signal<number | null>(1234.5);
}

describe('AmountPipe', () => {
  const text = (fixture: { nativeElement: HTMLElement }, selector: string) =>
    fixture.nativeElement.querySelector(selector)!.textContent!.replace(/[\u00a0\u202f]/g, ' ');

  it('shows amounts in the app locale (it), with their currency', async () => {
    const fixture = TestBed.createComponent(Host);
    await fixture.whenStable();

    expect(text(fixture, '.plain')).toBe('1.234,50 €');
    expect(text(fixture, '.signed')).toBe('+1.234,50 €');
  });

  it('shows nothing while there is no amount', async () => {
    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.value.set(null);
    await fixture.whenStable();

    expect(text(fixture, '.plain')).toBe('');
  });
});
