import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { form, FormField, required } from '@angular/forms/signals';
import { AmountInput } from './amount-input';

@Component({
  imports: [AmountInput, FormField],
  template: `<app-amount-input
    label="Importo"
    [currency]="currency()"
    [allowNegative]="allowNegative()"
    [formField]="expenseForm.amount"
  />`,
})
class Host {
  readonly model = signal<{ amount: number | null }>({ amount: null });
  readonly currency = signal('EUR');
  readonly allowNegative = signal(false);
  readonly expenseForm = form(this.model, (path) => {
    required(path.amount, { message: "Inserisci l'importo." });
  });
}

describe('AmountInput', () => {
  let fixture: ComponentFixture<Host>;
  let host: Host;
  let input: HTMLInputElement;

  beforeEach(async () => {
    fixture = TestBed.createComponent(Host);
    host = fixture.componentInstance;
    await fixture.whenStable();
    input = fixture.nativeElement.querySelector('input');
  });

  async function type(text: string): Promise<void> {
    input.value = text;
    input.dispatchEvent(new Event('input'));
    await fixture.whenStable();
  }

  async function leave(): Promise<void> {
    input.dispatchEvent(new Event('blur'));
    await fixture.whenStable();
  }

  const error = () => fixture.nativeElement.querySelector('mat-error')?.textContent.trim() ?? null;

  it('is a text field with the numeric keyboard, its label and the currency', () => {
    expect(input.type).toBe('text');
    expect(input.getAttribute('inputmode')).toBe('decimal');
    expect(fixture.nativeElement.querySelector('mat-label').textContent).toContain('Importo');
    expect(fixture.nativeElement.querySelector('.currency').textContent.trim()).toBe('€');
  });

  it('shows the symbol of the account currency', async () => {
    host.currency.set('CHF');
    await fixture.whenStable();

    expect(fixture.nativeElement.querySelector('.currency').textContent.trim()).toBe('CHF');
  });

  it('puts the amount typed the Italian way in the model, as a number', async () => {
    await type('1.234,5');

    expect(host.model().amount).toBe(1234.5);
  });

  it('leaves the text alone while typing, and writes it the canonical way on leaving', async () => {
    await type('12,');
    expect(input.value).toBe('12,');
    expect(host.model().amount).toBe(12);

    await type('1234,5');
    expect(input.value).toBe('1234,5');

    await leave();
    expect(input.value).toBe('1.234,50');
    expect(host.model().amount).toBe(1234.5);
  });

  it('accepts the dot of a phone keyboard as the decimal point', async () => {
    await type('12.5');
    await leave();

    expect(host.model().amount).toBe(12.5);
    expect(input.value).toBe('12,50');
  });

  it('shows an amount set by the page', async () => {
    host.model.set({ amount: 1234.56 });
    await fixture.whenStable();

    expect(input.value).toBe('1.234,56');
  });

  it('makes the form invalid when the text is not an amount, and says why after leaving', async () => {
    await type('12,345');

    expect(host.expenseForm.amount().valid()).toBe(false);
    expect(error()).toBeNull();

    await leave();

    expect(error()).toBe('Al massimo due decimali.');
    expect(input.value).toBe('12,345');
    expect(input.getAttribute('aria-invalid')).toBe('true');
    expect(
      fixture.nativeElement.querySelector('.mat-mdc-form-field-subscript-wrapper mat-error'),
    ).not.toBeNull();
  });

  it('says what an amount looks like', async () => {
    await type('dodici');
    await leave();

    expect(error()).toBe('Inserisci un importo, ad esempio 12,50.');
  });

  it('shows the validators of the form once the field is touched', async () => {
    expect(error()).toBeNull();

    await leave();

    expect(error()).toBe("Inserisci l'importo.");
  });

  it('becomes valid again when the text is corrected', async () => {
    await type('12,345');
    await leave();
    await type('12,34');
    await leave();

    expect(host.expenseForm.amount().valid()).toBe(true);
    expect(error()).toBeNull();
    expect(host.model().amount).toBe(12.34);
  });

  it('refuses amounts below zero unless the field allows them', async () => {
    await type('-50');
    await leave();
    expect(error()).toBe("L'importo non può essere negativo.");

    host.allowNegative.set(true);
    await fixture.whenStable();
    await type('-50');
    await leave();
    expect(error()).toBeNull();
    expect(host.model().amount).toBe(-50);
  });

  it('empties the text when the form is reset', async () => {
    await type('12,50');

    host.expenseForm().reset({ amount: null });
    await fixture.whenStable();

    expect(input.value).toBe('');
  });
});
