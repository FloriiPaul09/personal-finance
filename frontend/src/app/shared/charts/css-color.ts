/**
 * The color a CSS value stands for right now (`var(--pf-chart-1)` → `rgb(42, 120, 214)`), as
 * Chart.js draws on a canvas and cannot read CSS. Tokens are `light-dark()` pairs, so the answer
 * depends on the color scheme in force: read it again when the scheme changes. Without a computed
 * color (tests in jsdom) the value is returned as it is.
 */
export function resolveCssColor(document: Document, color: string): string {
  const probe = document.createElement('span');
  probe.style.display = 'none';
  probe.style.color = color;
  document.body.append(probe);
  try {
    const resolved = document.defaultView?.getComputedStyle(probe).color;
    return resolved && !resolved.includes('var(') ? resolved : color;
  } finally {
    probe.remove();
  }
}
