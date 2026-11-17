import {
  Component,
  computed,
  effect,
  inject,
  Injectable,
  input,
  model,
  output,
  untracked,
  viewChild,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { BreakpointObserver } from '@angular/cdk/layout';
import { FormValueControl, transformedValue, ValidationError } from '@angular/forms/signals';
import { DateAdapter, ErrorStateMatcher } from '@angular/material/core';
import {
  MatDatepickerInput,
  MatDatepickerInputEvent,
  MatDatepickerIntl,
  MatDatepickerModule,
} from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInput, MatInputModule } from '@angular/material/input';
import { map } from 'rxjs';
import { INVALID_DATE, IsoDate, provideIsoDateAdapter } from '../dates';

/** Kind of the error reported when the typed text is not a day. */
export const DATE_PARSE_ERROR_KIND = 'date';

/** Phones: the calendar opens in a centered dialog (Material `touchUi`) instead of a popup under the field. */
const PHONE = '(max-width: 599.98px)';

/** Italian texts of the date picker (Material ships them in English). */
@Injectable()
export class ItalianDatepickerIntl extends MatDatepickerIntl {
  override calendarLabel = $localize`:Date picker:Calendario`;
  override openCalendarLabel = $localize`:Date picker:Apri il calendario`;
  override closeCalendarLabel = $localize`:Date picker:Chiudi il calendario`;
  override prevMonthLabel = $localize`:Date picker:Mese precedente`;
  override nextMonthLabel = $localize`:Date picker:Mese successivo`;
  override prevYearLabel = $localize`:Date picker:Anno precedente`;
  override nextYearLabel = $localize`:Date picker:Anno successivo`;
  override prevMultiYearLabel = $localize`:Date picker:24 anni precedenti`;
  override nextMultiYearLabel = $localize`:Date picker:24 anni successivi`;
  override switchToMonthViewLabel = $localize`:Date picker:Scegli la data`;
  override switchToMultiYearViewLabel = $localize`:Date picker:Scegli mese e anno`;
  override startDateLabel = $localize`:Date picker:Data di inizio`;
  override endDateLabel = $localize`:Date picker:Data di fine`;
  override comparisonDateLabel = $localize`:Date picker:Periodo di confronto`;

  override formatYearRange(start: string, end: string): string {
    return `${start} – ${end}`;
  }

  override formatYearRangeLabel(start: string, end: string): string {
    return $localize`:Date picker:da ${start}:start: a ${end}:end:`;
  }
}

/**
 * A date field: `<app-date-input label="Data" [formField]="form.date" />`.
 *
 * The field holds a `YYYY-MM-DD` string or `null`, like the API (`IsoDateAdapter`). The person
 * picks a day in the calendar (a dialog on phones) or types it day first (`17/11/2026`,
 * `17/11/26`); text that is not a day is a Signal Forms parse error with an Italian message, so
 * the form cannot be submitted. Like `<app-amount-input>`, it holds the whole Material form field
 * and shows the first error once touched (docs/frontend/ui-components.md#forms).
 */
@Component({
  selector: 'app-date-input',
  imports: [MatDatepickerModule, MatFormFieldModule, MatInputModule],
  // The adapter is provided here, not at startup: Material's date code stays out of the initial
  // bundle (docs/frontend/app-structure.md#boundaries). Use this field for every date.
  providers: [
    provideIsoDateAdapter(),
    { provide: MatDatepickerIntl, useClass: ItalianDatepickerIntl },
  ],
  template: `
    <mat-form-field>
      <mat-label>{{ label() }}</mat-label>
      <input
        matInput
        autocomplete="off"
        [matDatepicker]="picker"
        [min]="min() ?? null"
        [max]="max() ?? null"
        [name]="name()"
        [disabled]="disabled()"
        [readonly]="readonly()"
        [required]="required()"
        [errorStateMatcher]="errorStateMatcher"
        (input)="onInput($event)"
        (dateChange)="onDateChange($event)"
        (blur)="touch.emit()"
      />
      <mat-datepicker-toggle matIconSuffix [for]="picker" />
      <mat-datepicker #picker [touchUi]="phone()" (closed)="touch.emit()" />
      @if (hint(); as hint) {
        <mat-hint>{{ hint }}</mat-hint>
      }
      @if (error(); as error) {
        <mat-error>{{ error.message }}</mat-error>
      }
    </mat-form-field>
  `,
  styles: `
    :host {
      display: block;
    }

    mat-form-field {
      width: 100%;
    }
  `,
})
export class DateInput implements FormValueControl<IsoDate | null> {
  private readonly adapter = inject<DateAdapter<IsoDate>>(DateAdapter);

  readonly value = model<IsoDate | null>(null);
  readonly label = input.required<string>();
  readonly hint = input<string>();
  /** Earliest and latest day of the calendar (the form still validates the range). */
  readonly min = input<IsoDate>();
  readonly max = input<IsoDate>();

  // Bound by the `FormField` directive of Signal Forms.
  readonly errors = input<readonly ValidationError.WithOptionalFieldTree[]>([]);
  readonly touched = input(false);
  readonly disabled = input(false);
  readonly readonly = input(false);
  readonly required = input(false);
  readonly name = input('');
  readonly touch = output<void>();

  // Not a module constant: a top-level $localize call would keep this file, and the whole date
  // picker, in every bundle that imports the barrel (app.config imports the adapter from it).
  private readonly parseMessage = $localize`:Date input error:Data non valida, ad esempio 17/11/2026.`;

  /** What the person entered: a day, nothing, or `INVALID_DATE` (reported as a parse error). */
  private readonly entered = transformedValue(this.value, {
    parse: (entered: IsoDate | null) =>
      entered === INVALID_DATE
        ? { error: { kind: DATE_PARSE_ERROR_KIND, message: this.parseMessage } }
        : { value: entered },
    format: (value) => value,
  });

  protected readonly phone = toSignal(
    inject(BreakpointObserver)
      .observe(PHONE)
      .pipe(map((state) => state.matches)),
    { initialValue: false },
  );

  protected readonly error = computed(() =>
    this.touched() ? (this.errors()[0] ?? this.entered.parseErrors()[0] ?? null) : null,
  );

  protected readonly errorStateMatcher: ErrorStateMatcher = {
    isErrorState: () => this.error() !== null,
  };

  private readonly matInput = viewChild.required(MatInput);
  private readonly datepickerInput = viewChild.required(MatDatepickerInput<IsoDate>);
  /** The last value this field put in the model: other changes come from the page. */
  private fromPerson: IsoDate | null | undefined;

  constructor() {
    // Write the value into the field only when the page changes it (first value, reset):
    // rewriting it on every keystroke would turn "1/2/2" into "01/02/2002" while typing.
    effect(() => {
      const value = this.value();
      untracked(() => {
        if (value !== this.fromPerson) {
          this.datepickerInput().value = value;
        }
        this.fromPerson = undefined;
      });
    });
    effect(() => {
      this.error();
      untracked(() => this.matInput().updateErrorState());
    });
  }

  focus(options?: FocusOptions): void {
    this.matInput().focus(options);
  }

  protected onInput(event: Event): void {
    const parsed = this.adapter.parse((event.target as HTMLInputElement).value, null);
    this.enter(parsed);
  }

  /** A day picked in the calendar. */
  protected onDateChange(event: MatDatepickerInputEvent<IsoDate>): void {
    if (event.value !== null && this.adapter.isValid(event.value)) {
      this.enter(event.value);
    }
  }

  private enter(entered: IsoDate | null): void {
    if (entered !== INVALID_DATE) {
      this.fromPerson = entered;
    }
    this.entered.set(entered);
  }
}
