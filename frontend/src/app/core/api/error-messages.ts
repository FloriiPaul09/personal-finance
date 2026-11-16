import { ApiError, CLIENT_ERROR_CODES } from './api-error';

// Italian message for each stable error code of the API (docs/backend/validation-and-errors.md#codes)
// and of the app (CLIENT_ERROR_CODES). Codes are never renamed by the API, only added:
// an unknown code falls back to a generic message.

const GENERIC = $localize`:API error:Si è verificato un errore. Riprova.`;
const RELOAD = $localize`:API error:La richiesta non è valida. Ricarica la pagina e riprova.`;
const SERVER = $localize`:API error:Errore del server. Riprova più tardi.`;

const MESSAGES: Readonly<Record<string, string>> = {
  // App
  [CLIENT_ERROR_CODES.networkUnavailable]: $localize`:API error:Impossibile contattare il server. Controlla la connessione e riprova.`,
  [CLIENT_ERROR_CODES.serverUnavailable]: $localize`:API error:Il server non risponde. Riprova tra poco.`,
  [CLIENT_ERROR_CODES.unexpectedResponse]: SERVER,

  // Requests
  'validation.failed': $localize`:API error:Controlla i campi evidenziati.`,
  'request.invalid': RELOAD,
  'request.method_not_allowed': RELOAD,
  'request.unsupported_media_type': RELOAD,
  'request.too_many': $localize`:API error:Troppi tentativi. Attendi un minuto e riprova.`,
  'id.invalid': RELOAD,
  'server.error': SERVER,

  // Resources and versions
  'resource.not_found': $localize`:API error:Elemento non trovato: potrebbe essere stato eliminato.`,
  'resource.conflict': $localize`:API error:Conflitto con dati già esistenti. Riprova.`,
  'concurrency.version_mismatch': $localize`:API error:Questo elemento è stato modificato nel frattempo. Ricarica i dati e riprova.`,
  'concurrency.version_required': RELOAD,
  'concurrency.version_invalid': RELOAD,
  'account.not_found': $localize`:API error:Il conto scelto non esiste più.`,
  'category.not_found': $localize`:API error:La categoria scelta non esiste più.`,

  // Authentication
  'auth.antiforgery_invalid': $localize`:API error:La sessione di sicurezza è scaduta. Ricarica la pagina e riprova.`,
  'auth.required': $localize`:API error:Sessione scaduta: accedi di nuovo.`,
  'auth.invalid_credentials': $localize`:API error:Email o password non corretti.`,
  'auth.forbidden': $localize`:API error:Non hai i permessi per questa operazione.`,
  'auth.password_incorrect': $localize`:API error:La password attuale non è corretta.`,
  'auth.password_rejected': $localize`:API error:La password deve avere almeno 12 caratteri.`,
  'auth.email_taken': $localize`:API error:Questa email è già registrata.`,

  // Duplicates
  'account.name_taken': $localize`:API error:Esiste già un conto con questo nome.`,
  'category.name_taken': $localize`:API error:Esiste già una categoria con questo nome.`,
  'tag.name_taken': $localize`:API error:Esiste già un tag con questo nome.`,
  'budget.month_taken': $localize`:API error:Esiste già un budget per questo mese.`,

  // Invalid values (domain, 400)
  'amount.not_positive': $localize`:API error:L'importo deve essere maggiore di zero.`,
  'amount.too_many_decimals': $localize`:API error:Usa al massimo due decimali.`,
  'amount.out_of_range': $localize`:API error:L'importo è troppo grande.`,
  'name.required': $localize`:API error:Il nome è obbligatorio.`,
  'name.too_long': $localize`:API error:Il nome è troppo lungo.`,
  'description.too_long': $localize`:API error:La descrizione è troppo lunga.`,
  'currency.invalid': $localize`:API error:Valuta non valida: usa un codice di tre lettere, per esempio EUR.`,
  'month.invalid': $localize`:API error:Mese non valido.`,
  'account.type_invalid': $localize`:API error:Tipo di conto non valido.`,
  'category.kind_invalid': $localize`:API error:Tipo di categoria non valido.`,
  'category.too_deep': $localize`:API error:Una sottocategoria non può avere altre sottocategorie.`,
  'category.parent_itself': $localize`:API error:Una categoria non può essere la madre di sé stessa.`,
  'category.kind_mismatch': $localize`:API error:Il tipo (entrata o uscita) non corrisponde a quello della categoria.`,
  'category.kind_follows_parent': $localize`:API error:Una sottocategoria ha sempre il tipo della sua categoria madre.`,
  'category.not_a_subcategory': $localize`:API error:La categoria scelta non è una sottocategoria.`,
  'transfer.same_account': $localize`:API error:Scegli due conti diversi.`,
  'transfer.currency_mismatch': $localize`:API error:I due conti devono avere la stessa valuta.`,
  'budget.category_not_expense': $localize`:API error:Il budget si imposta solo su categorie di uscita.`,

  // State conflicts (domain, 409)
  'account.archived': $localize`:API error:Il conto è archiviato e non accetta nuovi movimenti.`,
  'account.currency_locked': $localize`:API error:La valuta non si può cambiare: il conto ha già dei movimenti.`,
  'account.has_movements': $localize`:API error:Il conto ha dei movimenti: puoi archiviarlo, non eliminarlo.`,
  'category.archived': $localize`:API error:La categoria è archiviata.`,
  'category.has_subcategories': $localize`:API error:La categoria ha delle sottocategorie.`,
  'category.kind_locked': $localize`:API error:Il tipo non si può cambiare: la categoria è già usata.`,
  'category.in_use': $localize`:API error:La categoria è usata: puoi archiviarla, non eliminarla.`,
  'transaction.is_transfer_leg': $localize`:API error:Questo movimento fa parte di un trasferimento: modificalo dal trasferimento.`,
  'budget.branch_conflict': $localize`:API error:Un budget sta sulla categoria madre oppure sulle sue sottocategorie, non su entrambe.`,
};

/** Codes with a message of their own (the rest get the generic one). */
export const KNOWN_ERROR_CODES: readonly string[] = Object.keys(MESSAGES);

/** The message to show for an error code; generic for codes the app does not know yet. */
export function errorMessage(code: string): string {
  return MESSAGES[code] ?? GENERIC;
}

/** The message to show for any error: an `ApiError` by its code, anything else generic. */
export function messageOf(error: unknown): string {
  return error instanceof ApiError ? errorMessage(error.code) : GENERIC;
}
