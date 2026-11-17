import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Title } from '@angular/platform-browser';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from './app.routes';
import { provideAppIcons, providePageTitles, SessionStatus, SessionStore } from './core';
import { provideIsoDateAdapter } from './shared/dates';

// Routing and shell together, as the app runs them (icon requests are left unanswered).
// The session is a stub: its own behavior is tested in core/auth.
async function startApp(status: SessionStatus): Promise<RouterTestingHarness> {
  TestBed.configureTestingModule({
    providers: [
      provideRouter(routes),
      provideHttpClient(),
      provideHttpClientTesting(),
      providePageTitles(),
      provideAppIcons(),
      provideIsoDateAdapter(),
      {
        provide: SessionStore,
        useValue: {
          ensureLoaded: () => Promise.resolve(status),
          status: signal(status),
          user: signal(status === 'authenticated' ? { id: '1', email: 'dev@example.com' } : null),
        },
      },
    ],
  });
  return RouterTestingHarness.create();
}

describe('App routes and shell', () => {
  let harness: RouterTestingHarness;

  beforeEach(async () => {
    harness = await startApp('authenticated');
  });

  function navLinks(): HTMLAnchorElement[] {
    return Array.from(harness.routeNativeElement!.querySelectorAll<HTMLAnchorElement>('.nav-item'));
  }

  function heading(): string | undefined {
    return harness.routeNativeElement!.querySelector('h1')?.textContent?.trim();
  }

  it('opens the dashboard from the root path', async () => {
    await harness.navigateByUrl('/');

    expect(TestBed.inject(Router).url).toBe('/dashboard');
    expect(heading()).toBe('Home');
  });

  it('shows the five main destinations in Italian', async () => {
    await harness.navigateByUrl('/dashboard');

    expect(navLinks().map((a) => [a.textContent?.trim(), a.getAttribute('href')])).toEqual([
      ['Home', '/dashboard'],
      ['Movimenti', '/transactions'],
      ['Budget', '/budgets'],
      ['Conti', '/accounts'],
      ['Categorie', '/categories'],
    ]);
  });

  it.each([
    ['/transactions', 'Movimenti'],
    ['/budgets', 'Budget'],
    ['/accounts', 'Conti'],
    ['/categories', 'Categorie'],
  ])('loads %s lazily, marks its link as current and titles the page', async (url, title) => {
    await harness.navigateByUrl(url);

    expect(heading()).toBe(title);
    const current = navLinks().filter((a) => a.getAttribute('aria-current') === 'page');
    expect(current.map((a) => a.getAttribute('href'))).toEqual([url]);
    expect(TestBed.inject(Title).getTitle()).toBe(`${title} · Personal Finance`);
  });

  it('offers the new expense form from every page', async () => {
    await harness.navigateByUrl('/accounts');

    const button = harness.routeNativeElement!.querySelector<HTMLAnchorElement>(
      '[data-testid="new-expense"]',
    )!;
    expect(button.getAttribute('href')).toBe('/transactions/new');
    expect(button.getAttribute('aria-label')).toBe('Nuova spesa');

    button.click();
    await harness.fixture.whenStable();
    expect(heading()).toBe('Nuova spesa');
  });

  it('sends unknown addresses to the dashboard', async () => {
    await harness.navigateByUrl('/does-not-exist');

    expect(TestBed.inject(Router).url).toBe('/dashboard');
  });

  it('opens the catalog of the shared components in development, outside the navigation', async () => {
    await harness.navigateByUrl('/dev/components');

    expect(heading()).toBe('Componenti');
    expect(navLinks().some((a) => a.getAttribute('aria-current') === 'page')).toBe(false);
  });

  it('has no developer pages in the production build', async () => {
    const production = await import('./dev-routes.prod');

    expect(production.DEV_ROUTES).toEqual([]);
  });

  it('opens the settings from the account button', async () => {
    await harness.navigateByUrl('/accounts');

    const account = harness.routeNativeElement!.querySelector<HTMLAnchorElement>(
      '[data-testid="settings"]',
    )!;
    expect(account.getAttribute('href')).toBe('/settings');
    expect(account.getAttribute('aria-label')).toBe('Impostazioni');

    account.click();
    await harness.fixture.whenStable();
    expect(heading()).toBe('Impostazioni');
    expect(TestBed.inject(Title).getTitle()).toBe('Impostazioni · Personal Finance');
  });
});

describe('App routes without a session', () => {
  it('opens the login page instead of a page of the shell, and comes back afterwards', async () => {
    const harness = await startApp('anonymous');

    await harness.navigateByUrl('/accounts');

    expect(TestBed.inject(Router).url).toBe('/login?returnUrl=%2Faccounts');
    expect(harness.routeNativeElement!.querySelector('h1')?.textContent?.trim()).toBe(
      'Personal Finance',
    );
    expect(harness.routeNativeElement!.querySelector('nav')).toBeNull();
    expect(TestBed.inject(Title).getTitle()).toBe('Accedi · Personal Finance');
  });

  it('shows the unavailable page when the API cannot be reached', async () => {
    const harness = await startApp('unavailable');

    await harness.navigateByUrl('/dashboard');

    expect(TestBed.inject(Router).url).toBe('/unavailable?returnUrl=%2Fdashboard');
    expect(harness.routeNativeElement!.querySelector('h1')?.textContent?.trim()).toBe(
      'Il server non risponde',
    );
  });
});
