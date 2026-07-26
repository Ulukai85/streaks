import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { DashboardItem, DashboardResponse } from './dashboard.model';
import { DashboardService } from './dashboard.service';

describe('DashboardService', () => {
  let service: DashboardService;
  let httpMock: HttpTestingController;

  const item: DashboardItem = {
    id: '11111111-1111-1111-1111-111111111111',
    name: 'Wordle',
    url: null,
    cadence: 'Daily',
    color: 'blue',
    sortOrder: 0,
    streak: { length: 0, isAlive: true, lastCompletedPeriod: null },
  };

  const emptyDashboard: DashboardResponse = { open: [], doneThisPeriod: [] };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(DashboardService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('completeChallenge() posts an empty body and reloads the dashboard on success', async () => {
    TestBed.tick();
    httpMock.expectOne('/api/dashboard/').flush({ open: [item], doneThisPeriod: [] });

    const completePromise = service.completeChallenge(item.id);

    const postReq = httpMock.expectOne(`/api/challenges/${item.id}/completions`);
    expect(postReq.request.method).toBe('POST');
    expect(postReq.request.body).toEqual({});
    postReq.flush({
      id: '22222222-2222-2222-2222-222222222222',
      challengeId: item.id,
      periodStart: '2026-07-26',
      completedAt: '2026-07-26T00:00:00Z',
      note: null,
    });

    await completePromise;

    TestBed.tick();
    httpMock.expectOne('/api/dashboard/').flush({ open: [], doneThisPeriod: [item] });
  });

  it('completeChallenge() surfaces a problem-details error without an unhandled rejection', async () => {
    TestBed.tick();
    httpMock.expectOne('/api/dashboard/').flush(emptyDashboard);

    const completePromise = service.completeChallenge(item.id);
    const postReq = httpMock.expectOne(`/api/challenges/${item.id}/completions`);
    postReq.flush(
      { title: 'Conflict', detail: 'This period has already been completed.' },
      { status: 409, statusText: 'Conflict' },
    );

    await expect(completePromise).rejects.toEqual({
      title: 'Conflict',
      detail: 'This period has already been completed.',
    });
  });
});
