import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { authGuard, guestGuard } from './auth-guards';
import { SessionStatus, SessionStore } from './session-store';

@Component({ template: 'page' })
class Page {}

describe('auth guards', () => {
  async function visit(url: string, status: SessionStatus): Promise<string> {
    const ensureLoaded = vi.fn().mockResolvedValue(status);
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'login', canMatch: [guestGuard], component: Page },
          { path: 'unavailable', component: Page },
          {
            path: '',
            canMatch: [authGuard],
            children: [
              { path: 'dashboard', component: Page },
              { path: 'transactions', component: Page },
            ],
          },
        ]),
        { provide: SessionStore, useValue: { ensureLoaded, status: signal(status) } },
      ],
    });
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url);
    return TestBed.inject(Router).url;
  }

  it('lets a signed-in user in', async () => {
    expect(await visit('/transactions?kind=expense', 'authenticated')).toBe(
      '/transactions?kind=expense',
    );
  });

  it('sends a signed-out user to the login page, with the page to come back to', async () => {
    expect(await visit('/transactions?kind=expense', 'anonymous')).toBe(
      '/login?returnUrl=%2Ftransactions%3Fkind%3Dexpense',
    );
  });

  it('shows the unavailable page when the API cannot be reached', async () => {
    expect(await visit('/dashboard', 'unavailable')).toBe('/unavailable?returnUrl=%2Fdashboard');
  });

  it('opens the login page for a signed-out user', async () => {
    expect(await visit('/login', 'anonymous')).toBe('/login');
  });

  it('sends a signed-in user away from the login page, to where they were heading', async () => {
    expect(await visit('/login?returnUrl=%2Ftransactions', 'authenticated')).toBe('/transactions');
  });

  it('never follows a return address outside the app', async () => {
    expect(await visit('/login?returnUrl=%2F%2Fevil.example', 'authenticated')).toBe('/dashboard');
  });
});
