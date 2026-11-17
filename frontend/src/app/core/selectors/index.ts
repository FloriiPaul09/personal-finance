// Public API of core/selectors: fields shared by the movement forms (accounts, categories, kind
// of movement). A barrel of its own, like core/forms: these are components, and a component
// exported from `core` would enter the initial bundle with Material's select
// (docs/frontend/app-structure.md#boundaries).
export { AccountSelect } from './account-select';
export { CategorySelect } from './category-select';
export { type MovementKind, MovementKindSwitch } from './movement-kind-switch';
