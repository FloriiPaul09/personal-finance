import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Title } from '@angular/platform-browser';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from './app.routes';
import { provideAppIcons, providePageTitles } from './core';

// Routing and shell together, as the app runs them (icon requests are left unanswered).
describe('App routes and shell', () => {
  let harness: RouterTestingHarness;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes),
        provideHttpClient(),
        provideHttpClientTesting(),
        providePageTitles(),
        provideAppIcons(),
      ],
    });
    harness = await RouterTestingHarness.create();
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
});
