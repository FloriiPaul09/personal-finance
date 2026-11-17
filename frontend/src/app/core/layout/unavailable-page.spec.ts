import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideAppIcons } from '../icons/app-icons';
import { UnavailablePage } from './unavailable-page';

describe('UnavailablePage', () => {
  async function retryFrom(returnUrl: string | undefined): Promise<ReturnType<typeof vi.spyOn>> {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideAppIcons(),
      ],
    });
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const fixture = TestBed.createComponent(UnavailablePage);
    fixture.componentRef.setInput('returnUrl', returnUrl);
    await fixture.whenStable();

    (fixture.nativeElement as HTMLElement).querySelector('button')!.click();
    return navigate;
  }

  it('tries again the page the user asked for', async () => {
    expect(await retryFrom('/budgets')).toHaveBeenCalledWith('/budgets');
  });

  it('tries the dashboard when the page is missing or outside the app', async () => {
    expect(await retryFrom(undefined)).toHaveBeenCalledWith('/dashboard');
    TestBed.resetTestingModule();
    expect(await retryFrom('https://evil.example')).toHaveBeenCalledWith('/dashboard');
  });
});
