// Public API of the core module: singletons and the layout used once by the app.
// Features import from '../../core' (this file) only; Sheriff refuses deep imports.
export { ApiError, CLIENT_ERROR_CODES } from './api/api-error';
export { errorMessage, messageOf } from './api/error-messages';
export type * from './api/models';
export { provideAppErrorHandler } from './errors/app-error-handler';
export { ErrorNotifier } from './errors/error-notifier';
export { cursorPager, type CursorPager } from './http/cursor-pager';
export { provideApiHttp } from './http/provide-api-http';
export { ifMatch } from './http/versions';
export { newId } from './ids/new-id';
export { provideAppIcons } from './icons/app-icons';
export { APP_NAME, providePageTitles } from './layout/page-title-strategy';
export { Shell } from './layout/shell';
