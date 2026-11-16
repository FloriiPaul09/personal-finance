// Public API of the core module: singletons and the layout used once by the app.
// Features import from '../../core' (this file) only; Sheriff refuses deep imports.
export { provideAppIcons } from './icons/app-icons';
export { APP_NAME, providePageTitles } from './layout/page-title-strategy';
export { Shell } from './layout/shell';
