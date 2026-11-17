import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatIcon } from '@angular/material/icon';
import { MatIconTestingModule } from '@angular/material/icon/testing';
import { By } from '@angular/platform-browser';
import { EmptyState } from './empty-state';

@Component({
  imports: [EmptyState],
  template: `<app-empty-state
    icon="account_balance_wallet"
    title="Nessun conto"
    [message]="message"
  >
    <button type="button">Aggiungi conto</button>
  </app-empty-state>`,
})
class Host {
  message: string | undefined = 'Aggiungi il primo conto.';
}

describe('EmptyState', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [MatIconTestingModule],
    });
  });

  it('shows icon, title, message and the action of the page', async () => {
    const fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;

    expect(fixture.debugElement.query(By.directive(MatIcon)).componentInstance.svgIcon).toBe(
      'account_balance_wallet',
    );
    expect(element.querySelector('.title')!.textContent).toBe('Nessun conto');
    expect(element.querySelector('.message')!.textContent).toBe('Aggiungi il primo conto.');
    expect(element.querySelector('button')!.textContent).toBe('Aggiungi conto');
  });

  it('leaves the message out when there is none', async () => {
    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.message = undefined;
    await fixture.whenStable();

    expect(fixture.nativeElement.querySelector('.message')).toBeNull();
  });
});
