import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ApiError, DataChanges } from '../../../core';
import { aCategory, problem, provideApiHttpTesting } from '../../../core/testing';
import { CategoriesStore } from './categories-store';

describe('CategoriesStore', () => {
  let store: CategoriesStore;
  let changes: DataChanges;
  let backend: HttpTestingController;
  const casa = aCategory({ name: 'Casa', hasSubcategories: true, version: 5 });
  const bollette = aCategory({ name: 'Bollette', parentId: casa.id });
  const affitto = aCategory({ name: 'Affitto', parentId: casa.id });

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideApiHttpTesting()] });
    store = TestBed.inject(CategoriesStore);
    changes = TestBed.inject(DataChanges);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    // The list in core may have been read meanwhile: not what these tests are about.
    backend.match('/api/categories?includeArchived=true');
    backend.verify();
  });

  const tick = () => new Promise((resolve) => setTimeout(resolve));

  it('creates, updates with If-Match and deletes with If-Match', async () => {
    const body = { name: 'Bollette', kind: 'expense' as const, parentId: casa.id };
    const created = store.create(body);
    const post = backend.expectOne('/api/categories');
    expect(post.request.method).toBe('POST');
    expect(post.request.body).toEqual(body);
    post.flush(bollette);
    await created;

    const updated = store.update(casa, { name: 'Abitazione', kind: 'expense', parentId: null });
    const put = backend.expectOne(`/api/categories/${casa.id}`);
    expect(put.request.method).toBe('PUT');
    expect(put.request.headers.get('If-Match')).toBe('"5"');
    put.flush(casa);
    await updated;

    const deleted = store.delete(casa);
    const del = backend.expectOne(`/api/categories/${casa.id}`);
    expect(del.request.method).toBe('DELETE');
    expect(del.request.headers.get('If-Match')).toBe('"5"');
    del.flush(null, { status: 204, statusText: 'No Content' });
    await deleted;

    expect(changes.version('categories')).toBe(3);
  });

  it('restores a parent, then the given subcategories one by one', async () => {
    const restored = store.unarchive(casa, [bollette, affitto]);

    backend.expectOne(`/api/categories/${casa.id}/unarchive`).flush(casa);
    await tick();
    backend.expectOne(`/api/categories/${bollette.id}/unarchive`).flush(bollette);
    await tick();
    backend.expectOne(`/api/categories/${affitto.id}/unarchive`).flush(affitto);

    expect(await restored).toEqual(casa);
    expect(changes.version('categories')).toBe(3);
  });

  it('archives with POST and no version', async () => {
    const archived = store.archive(casa);
    const request = backend.expectOne(`/api/categories/${casa.id}/archive`);
    expect(request.request.headers.has('If-Match')).toBe(false);
    request.flush({ ...casa, isArchived: true });
    await archived;
  });

  it('has the categories read again after a failed write too', async () => {
    const deleted = store.delete(casa);
    backend
      .expectOne(`/api/categories/${casa.id}`)
      .flush(...problem(409, 'category.has_subcategories'));

    await expect(deleted).rejects.toBeInstanceOf(ApiError);
    expect(changes.version('categories')).toBe(1);
  });
});
