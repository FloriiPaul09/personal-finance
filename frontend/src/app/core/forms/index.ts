// Public API of core/forms: helpers for Signal Forms. A barrel of its own, so that importing
// `core` at startup does not load @angular/forms: only the lazy pages with forms use it.
export { apiFormErrors, type ApiFormErrorOptions, SERVER_ERROR_KIND } from './api-form-errors';
