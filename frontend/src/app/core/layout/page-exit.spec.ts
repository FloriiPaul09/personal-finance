import { Location } from '@angular/common';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { PageExit } from './page-exit';

@Component({ template: '' })
class Blank {}

describe('PageExit', () => {
  let router: Router;
  let back: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'list', component: Blank },
          { path: 'form', component: Blank },
        ]),
      ],
    });
    router = TestBed.inject(Router);
    back = vi.spyOn(TestBed.inject(Location), 'back').mockImplementation(() => undefined);
  });

  it('goes back when the form was opened from a page of the app', async () => {
    await router.navigateByUrl('/list');
    await router.navigateByUrl('/form');

    await TestBed.inject(PageExit).leave('/list');

    expect(back).toHaveBeenCalled();
  });

  it('opens the fallback when the form was the first page', async () => {
    await router.navigateByUrl('/form');

    await TestBed.inject(PageExit).leave('/list');

    expect(back).not.toHaveBeenCalled();
    expect(router.url).toBe('/list');
  });
});
