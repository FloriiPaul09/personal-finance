// Public API of shared/ui: generic UI pieces that know nothing about the app.
export { confirmAction, ConfirmDialog, type ConfirmDialogData } from './confirm-dialog';
export { EmptyState } from './empty-state';
export {
  BUDGET_WARNING_RATIO,
  BudgetProgress,
  budgetState,
  type BudgetFigures,
  type BudgetState,
} from './budget-progress';
