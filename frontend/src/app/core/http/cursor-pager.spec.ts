import { DestroyRef } from '@angular/core';
import { Subject } from 'rxjs';
import { ApiError } from '../api/api-error';
import { PageResponse } from '../api/models';
import { cursorPager } from './cursor-pager';

/** A fake API: each call records its cursor and answers when the test says so. */
function fakeApi() {
  const calls: { cursor: string | null; answer: Subject<PageResponse<number>> }[] = [];
  const fetchPage = (cursor: string | null) => {
    const answer = new Subject<PageResponse<number>>();
    calls.push({ cursor, answer });
    return answer.asObservable();
  };
  const reply = (index: number, items: number[], nextCursor: string | null) => {
    calls[index].answer.next({ items, nextCursor });
    calls[index].answer.complete();
  };
  return { calls, fetchPage, reply };
}

describe('cursorPager', () => {
  it('starts empty, with more to load', () => {
    const pager = cursorPager(fakeApi().fetchPage);

    expect(pager.items()).toEqual([]);
    expect(pager.loaded()).toBe(false);
    expect(pager.hasMore()).toBe(true);
    expect(pager.loading()).toBe(false);
  });

  it('loads pages one after the other, passing the cursor back unchanged', () => {
    const api = fakeApi();
    const pager = cursorPager(api.fetchPage);

    pager.loadMore();
    expect(pager.loading()).toBe(true);
    api.reply(0, [1, 2], 'c-1');
    pager.loadMore();
    api.reply(1, [3], null);

    expect(api.calls.map((c) => c.cursor)).toEqual([null, 'c-1']);
    expect(pager.items()).toEqual([1, 2, 3]);
    expect(pager.hasMore()).toBe(false);
    expect(pager.loading()).toBe(false);
  });

  it('ignores loadMore while a page is loading and after the last page', () => {
    const api = fakeApi();
    const pager = cursorPager(api.fetchPage);

    pager.loadMore();
    pager.loadMore();
    expect(api.calls.length).toBe(1);

    api.reply(0, [1], null);
    pager.loadMore();
    expect(api.calls.length).toBe(1);
  });

  it('on reset, drops the old list and never shows a page of the old filters', () => {
    const api = fakeApi();
    const pager = cursorPager(api.fetchPage);
    pager.loadMore();
    api.reply(0, [1, 2], 'c-1');
    pager.loadMore(); // page 2 of the old filters still running

    pager.reset();
    api.reply(1, [99], null); // arrives late: must be ignored
    api.reply(2, [10], null);

    expect(api.calls.map((c) => c.cursor)).toEqual([null, 'c-1', null]);
    expect(pager.items()).toEqual([10]);
  });

  it('keeps the items and retries the same cursor after an error', () => {
    const api = fakeApi();
    const pager = cursorPager(api.fetchPage);
    pager.loadMore();
    api.reply(0, [1], 'c-1');
    pager.loadMore();
    const error = new ApiError(0, 'network.unavailable');

    api.calls[1].answer.error(error);
    expect(pager.error()).toBe(error);
    expect(pager.items()).toEqual([1]);
    expect(pager.loading()).toBe(false);

    pager.loadMore();
    expect(api.calls[2].cursor).toBe('c-1');
    expect(pager.error()).toBeUndefined();
  });

  it('cancels the running request when its owner is destroyed', () => {
    const api = fakeApi();
    let destroy = () => undefined as void;
    const destroyRef = {
      onDestroy: (fn: () => void) => ((destroy = fn), () => undefined),
    } as DestroyRef;
    const pager = cursorPager(api.fetchPage, destroyRef);

    pager.loadMore();
    destroy();
    api.reply(0, [1], null);

    expect(api.calls[0].answer.observed).toBe(false);
    expect(pager.items()).toEqual([]);
  });
});
