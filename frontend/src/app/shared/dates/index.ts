// Public API of shared/dates: calendar dates as `YYYY-MM-DD` strings, never shifted by time zones,
// and the date adapter. No components: app.config imports it at startup (see shared/date-pickers).
export {
  addDays,
  addMonths,
  addMonthsToMonth,
  currentMonth,
  dateParts,
  dayOfWeek,
  daysInMonth,
  formatIsoDate,
  formatMonth,
  isIsoDate,
  isIsoMonth,
  isoDate,
  monthOf,
  monthRange,
  todayIso,
  type DateParts,
  type IsoDate,
  type IsoMonth,
} from './iso-date';
export {
  INVALID_DATE,
  ISO_DATE_FORMATS,
  IsoDateAdapter,
  provideIsoDateAdapter,
} from './iso-date-adapter';
