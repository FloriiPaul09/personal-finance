import { afterNextRender, DestroyRef, Directive, ElementRef, inject, output } from '@angular/core';

/**
 * Emits `appScrollEnd` when the element comes near the visible area: put it after the last row
 * to load the next page while scrolling. Without `IntersectionObserver` (old browsers, tests) it
 * never emits, and the "Carica altri" button next to it does the same job.
 */
@Directive({ selector: '[appScrollEnd]' })
export class ScrollEnd {
  readonly appScrollEnd = output<void>();

  constructor() {
    const element = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
    const destroyRef = inject(DestroyRef);
    afterNextRender(() => {
      if (typeof IntersectionObserver === 'undefined') {
        return;
      }
      const observer = new IntersectionObserver(
        (entries) => {
          if (entries.some((entry) => entry.isIntersecting)) {
            this.appScrollEnd.emit();
          }
        },
        { rootMargin: '400px 0px' },
      );
      observer.observe(element);
      destroyRef.onDestroy(() => observer.disconnect());
    });
  }
}
