// Public API of shared/charts: the only place that imports Chart.js (ADR-0028). A barrel of its
// own, so the library enters only the chunks of the pages that draw a chart.
export {
  CHART_COLORS,
  CHART_OTHER_COLOR,
  chartColor,
  DonutChart,
  type DonutSlice,
} from './donut-chart';
export { resolveCssColor } from './css-color';
