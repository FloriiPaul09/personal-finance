// Public API of shared/charts: the only place that imports Chart.js (ADR-0028). A barrel of its
// own, so the library enters only the chunks of the pages that draw a chart.
export { BAR_LABEL_LENGTH, BAR_ROW_HEIGHT, BarChart, type BarItem, shortLabel } from './bar-chart';
export { resolveCssColor } from './css-color';
