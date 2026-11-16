import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Title } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { providePageTitles } from './page-title-strategy';

@Component({ template: '' })
class Blank {}

describe('PageTitleStrategy', () => {
  async function titleAfterVisiting(url: string): Promise<string> {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'titled', component: Blank, title: 'Conti' },
          { path: 'untitled', component: Blank },
        ]),
        providePageTitles(),
      ],
    });
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url);
    return TestBed.inject(Title).getTitle();
  }

  it('puts the page title before the product name', async () => {
    expect(await titleAfterVisiting('/titled')).toBe('Conti · Personal Finance');
  });

  it('uses the product name alone when the page has no title', async () => {
    expect(await titleAfterVisiting('/untitled')).toBe('Personal Finance');
  });
});
