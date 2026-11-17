import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { MovementKindSwitch } from './movement-kind-switch';

@Component({
  imports: [MovementKindSwitch],
  template: `<app-movement-kind-switch current="income"
    ><p class="form">Form</p></app-movement-kind-switch
  >`,
})
class Host {}

describe('MovementKindSwitch', () => {
  it('links the three kinds of movement, marks the current one and holds the form', async () => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
    const links = Array.from<HTMLAnchorElement>(fixture.nativeElement.querySelectorAll('a'));

    expect(links.map((a) => [a.textContent?.trim(), a.getAttribute('href')])).toEqual([
      ['Uscita', '/transactions/new'],
      ['Entrata', '/transactions/new?kind=income'],
      ['Trasferimento', '/transfers/new'],
    ]);
    expect(
      links.map(
        (a) => a.getAttribute('aria-current') === 'page' || a.classList.contains('mdc-tab--active'),
      ),
    ).toEqual([false, true, false]);
    expect(fixture.nativeElement.querySelector('[role="tabpanel"] .form')).not.toBeNull();
  });
});
