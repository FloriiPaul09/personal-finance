import { Location } from '@angular/common';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { PageExit, providePageExit } from './page-exit';

@Component({ template: '' })
class Blank {}

describe('PageExit', () => {
  let router: Router;
  let location: Location;
  let exit: PageExit;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'list', component: Blank },
          { path: 'new', component: Blank },
          { path: 'transfer', component: Blank },
        ]),
        providePageExit(),
      ],
    });
    router = TestBed.inject(Router);
    // As after the first navigation of the app: back and forward navigate.
    router.setUpLocationChangeListener();
    location = TestBed.inject(Location);
    exit = TestBed.inject(PageExit);
  });

  /** Leaves with `location.back` stubbed; tells whether it went back. */
  async function leave(): Promise<boolean> {
    const back = vi.spyOn(location, 'back').mockImplementation(() => undefined);
    await exit.leave('/list');
    const wentBack = back.mock.calls.length > 0;
    back.mockRestore();
    return wentBack;
  }

  async function goBack(to: string): Promise<void> {
    location.back();
    await vi.waitFor(() => expect(router.url).toBe(to));
  }

  it('goes back when the form was opened from a page of the app', async () => {
    await router.navigateByUrl('/list');
    await router.navigateByUrl('/new');

    expect(await leave()).toBe(true);
  });

  it('opens the fallback when the form was the first page', async () => {
    await router.navigateByUrl('/new');

    expect(await leave()).toBe(false);
    expect(router.url).toBe('/list');
  });

  it('opens the fallback when the first page was replaced by another form', async () => {
    await router.navigateByUrl('/new');
    await router.navigateByUrl('/transfer', { replaceUrl: true });

    expect(await leave()).toBe(false);
    expect(router.url).toBe('/list');
  });

  it('goes back past a replaced form to the page the person came from', async () => {
    await router.navigateByUrl('/list');
    await router.navigateByUrl('/new');
    await router.navigateByUrl('/transfer', { replaceUrl: true });

    expect(await leave()).toBe(true);
  });

  it('keeps the count of the entries reached with back', async () => {
    await router.navigateByUrl('/new');
    await router.navigateByUrl('/list');
    await goBack('/new');

    expect(await leave()).toBe(false);
  });

  it('counts again from a page reached with back', async () => {
    await router.navigateByUrl('/list');
    await router.navigateByUrl('/transfer');
    await goBack('/list');
    await router.navigateByUrl('/new');

    expect(await leave()).toBe(true);
  });
});
