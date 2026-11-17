import { ErrorHandler, inject, Injector, Provider, Service } from '@angular/core';
import { ApiError } from '../api/api-error';
import { ErrorNotifier } from './error-notifier';

/**
 * Last stop of every error nobody handled (thrown in a component, a rejected promise of a store):
 * it is logged and shown with `ErrorNotifier`. Pages handle the errors they expect (e.g.
 * `account.name_taken` on the name field); everything else ends up here.
 */
@Service({ autoProvided: false })
export class AppErrorHandler implements ErrorHandler {
  // Resolved on first use: the error handler is created before the overlay services.
  private readonly injector = inject(Injector);

  handleError(error: unknown): void {
    if (error instanceof ApiError && error.hasCode('auth.required')) {
      // The session ended, an expected event: the session interceptor already sent the user
      // to the login page, so there is nothing to show or log.
      return;
    }
    console.error(error);
    this.injector.get(ErrorNotifier).show(error);
  }
}

export function provideAppErrorHandler(): Provider {
  return { provide: ErrorHandler, useClass: AppErrorHandler };
}
