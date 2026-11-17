import { Location } from '@angular/common';
import { inject, Service } from '@angular/core';
import { Router } from '@angular/router';

/**
 * Leaves a form page after saving or deleting: back to the page the person came from when it was
 * a page of the app, otherwise (the form opened from a link or a reload) to `fallback`.
 */
@Service()
export class PageExit {
  private readonly router = inject(Router);
  private readonly location = inject(Location);

  leave(fallback: string): Promise<boolean> | void {
    if (this.router.lastSuccessfulNavigation()?.previousNavigation) {
      this.location.back();
      return;
    }
    return this.router.navigateByUrl(fallback);
  }
}
