import { Location } from '@angular/common';
import {
  DestroyRef,
  EnvironmentProviders,
  inject,
  provideAppInitializer,
  Service,
} from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';

/** What Angular keeps in `history.state` for every entry it writes. */
interface RouterHistoryState {
  readonly navigationId?: number;
}

/**
 * Leaves a form page after saving or deleting: back to the page the person came from when it was
 * a page of the app, otherwise (the form opened from a link, a bookmark or a reload) to `fallback`.
 *
 * "A page of the app behind" is not the same as "a navigation before this one": the kind switch
 * of the new-movement form replaces the history entry (`/transactions/new` → `/transfers/new`),
 * so a form opened from a bookmark has a previous navigation but nothing of the app behind it,
 * and going back would reload the page the browser had before (and lose the snackbar). So the
 * service counts how many entries of the app sit behind the current one: a navigation adds one,
 * a `replaceUrl` adds none, back and forward restore the count of the entry they reach. It must
 * exist from the first navigation: `providePageExit()` creates it at startup.
 */
@Service()
export class PageExit {
  private readonly router = inject(Router);
  private readonly location = inject(Location);
  /** Entries of the app behind each history entry, by the id of the navigation that wrote it. */
  private readonly depths = new Map<number, number>();
  private depth = 0;

  constructor() {
    const subscription = this.router.events.subscribe((event) => {
      if (event instanceof NavigationEnd) {
        this.track();
      }
    });
    inject(DestroyRef).onDestroy(() => subscription.unsubscribe());
  }

  leave(fallback: string): Promise<boolean> | void {
    if (this.depth > 0) {
      this.location.back();
      return;
    }
    return this.router.navigateByUrl(fallback);
  }

  private track(): void {
    const navigation = this.router.lastSuccessfulNavigation();
    const entry = (this.location.getState() as RouterHistoryState | null)?.navigationId;
    if (!navigation?.previousNavigation) {
      this.depth = 0;
    } else if (navigation.trigger !== 'imperative') {
      // Back or forward: the count of that entry, unknown (0) for entries before a reload.
      this.depth = entry === undefined ? 0 : (this.depths.get(entry) ?? 0);
    } else if (!navigation.extras.replaceUrl && !navigation.extras.skipLocationChange) {
      this.depth++;
    }
    if (entry !== undefined) {
      this.depths.set(entry, this.depth);
    }
  }
}

/** Creates `PageExit` at startup, so that it sees every navigation. */
export function providePageExit(): EnvironmentProviders {
  return provideAppInitializer(() => void inject(PageExit));
}
