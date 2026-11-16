import { inject, Provider, Service } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterStateSnapshot, TitleStrategy } from '@angular/router';

/** Product name: not translated. */
export const APP_NAME = 'Personal Finance';

/** Builds the browser title from the `title` of the active route: "Conti · Personal Finance". */
@Service({ autoProvided: false })
export class PageTitleStrategy extends TitleStrategy {
  private readonly title = inject(Title);

  override updateTitle(snapshot: RouterStateSnapshot): void {
    const page = this.buildTitle(snapshot);
    this.title.setTitle(page ? `${page} · ${APP_NAME}` : APP_NAME);
  }
}

export function providePageTitles(): Provider {
  return { provide: TitleStrategy, useClass: PageTitleStrategy };
}
