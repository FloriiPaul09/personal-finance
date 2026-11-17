import { HttpTestingController } from '@angular/common/http/testing';
import { ApplicationRef, Injector, runInInjectionContext, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ApiError, DataChanges } from '../../../core';
import { aDashboard, problem, provideApiHttpTesting } from '../../../core/testing';
import { IsoMonth } from '../../../shared/dates';
import { DashboardStore } from './dashboard-store';

describe('DashboardStore', () => {
  let store: DashboardStore;
  let changes: DataChanges;
  let backend: HttpTestingController;
  const month = signal<IsoMonth>('2026-11');
  const november = aDashboard({ month: '2026-11' });

  beforeEach(() => {
    month.set('2026-11');
    TestBed.configureTestingModule({ providers: [provideApiHttpTesting()] });
    store = TestBed.inject(DashboardStore);
    changes = TestBed.inject(DataChanges);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  const settle = () => TestBed.inject(ApplicationRef).whenStable();
  const dashboard = () => runInInjectionContext(TestBed.inject(Injector), () => store.of(month));

  it('reads the month it is given, and again after every write it is computed from', async () => {
    const view = dashboard();
    TestBed.tick();
    expect(view.loading()).toBe(true);
    backend.expectOne('/api/dashboard?month=2026-11').flush(november);
    await settle();
    expect(view.value()).toEqual(november);
    expect(view.loading()).toBe(false);

    for (const resource of [
      'accounts',
      'transactions',
      'transfers',
      'budgets',
      'categories',
    ] as const) {
      changes.notify(resource);
      TestBed.tick();
      // The numbers stay while the same month is read again.
      expect(view.value()).toEqual(november);
      backend.expectOne('/api/dashboard?month=2026-11').flush(november);
      await settle();
    }
  });

  it('shows nothing of the previous month while another one loads', async () => {
    const view = dashboard();
    TestBed.tick();
    backend.expectOne('/api/dashboard?month=2026-11').flush(november);
    await settle();

    month.set('2026-10');
    TestBed.tick();
    expect(view.value()).toBeUndefined();
    expect(view.loading()).toBe(true);
    const october = aDashboard({ month: '2026-10' });
    backend.expectOne('/api/dashboard?month=2026-10').flush(october);
    await settle();
    expect(view.value()).toEqual(october);
  });

  it('exposes the error and reads again on retry', async () => {
    const view = dashboard();
    TestBed.tick();
    backend.expectOne('/api/dashboard?month=2026-11').flush(...problem(500, 'server.error'));
    await settle();

    expect(view.error()).toBeInstanceOf(ApiError);
    expect(view.loading()).toBe(false);
    view.reload();
    TestBed.tick();
    backend.expectOne('/api/dashboard?month=2026-11').flush(november);
    await settle();
    expect(view.error()).toBeUndefined();
    expect(view.value()).toEqual(november);
  });
});
