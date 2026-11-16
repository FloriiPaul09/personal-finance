import { ErrorHandler } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ApiError } from '../api/api-error';
import { provideAppErrorHandler } from './app-error-handler';
import { ErrorNotifier } from './error-notifier';

describe('AppErrorHandler', () => {
  let show: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    show = vi.fn();
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
    TestBed.configureTestingModule({
      providers: [provideAppErrorHandler(), { provide: ErrorNotifier, useValue: { show } }],
    });
  });

  it('logs and shows every error nobody handled', () => {
    const error = new ApiError(409, 'account.archived');

    TestBed.inject(ErrorHandler).handleError(error);

    expect(console.error).toHaveBeenCalledWith(error);
    expect(show).toHaveBeenCalledWith(error);
  });

  it('shows errors that are not from the API too', () => {
    const error = new TypeError('x is undefined');

    TestBed.inject(ErrorHandler).handleError(error);

    expect(show).toHaveBeenCalledWith(error);
  });

  it('stays silent on an ended session, which sends the user to the login instead', () => {
    TestBed.inject(ErrorHandler).handleError(new ApiError(401, 'auth.required'));

    expect(show).not.toHaveBeenCalled();
  });
});
