// Public API of the core module: singletons and the layout used once by the app.
// Features import from '../../core' (this file) only; Sheriff refuses deep imports.
export { ApiError, CLIENT_ERROR_CODES } from './api/api-error';
export { errorMessage, messageOf } from './api/error-messages';
export { authGuard, guestGuard } from './auth/auth-guards';
export { safeReturnUrl } from './auth/return-url';
export { SessionStore, type SessionStatus } from './auth/session-store';
export type * from './api/models';
export { AccountsDirectory } from './data/accounts-directory';
export { CategoriesDirectory, type CategoryGroup } from './data/categories-directory';
export { DataChanges, type DataResource } from './data/data-changes';
export { RECENT_CATEGORIES, RECENT_STORAGE, RecentChoices } from './data/recent-choices';
export { provideAppErrorHandler } from './errors/app-error-handler';
export { ErrorNotifier } from './errors/error-notifier';
export { ErrorState } from './feedback/error-state';
export { Notifier, type NotifyOptions } from './feedback/notifier';
export { apiErrorInterceptor } from './http/api-error-interceptor';
export { cursorPager, type CursorPager } from './http/cursor-pager';
export { provideApiHttp } from './http/provide-api-http';
export { ifMatch } from './http/versions';
export { newId } from './ids/new-id';
export { provideAppIcons } from './icons/app-icons';
export { APP_NAME, providePageTitles } from './layout/page-title-strategy';
export { FORM_PAGE } from './layout/nav-items';
export { PageExit, providePageExit } from './layout/page-exit';
export { Shell } from './layout/shell';
export { UnavailablePage } from './layout/unavailable-page';
