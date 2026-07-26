import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Challenge, CreateChallengeRequest } from './challenge.model';
import { ChallengesService } from './challenges.service';

describe('ChallengesService', () => {
  let service: ChallengesService;
  let httpMock: HttpTestingController;

  const challenge: Challenge = {
    id: '11111111-1111-1111-1111-111111111111',
    name: 'Wordle',
    url: null,
    cadence: 'Daily',
    targetCount: 1,
    startsOn: '2026-07-26',
    archivedAt: null,
    color: 'blue',
    sortOrder: 0,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ChallengesService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('create() posts the request body and reloads the list on success', async () => {
    TestBed.tick();
    httpMock.expectOne('/api/challenges/').flush([]);

    const request: CreateChallengeRequest = {
      name: 'Wordle',
      url: null,
      cadence: 'Daily',
      color: 'blue',
    };

    const createPromise = service.create(request);

    const postReq = httpMock.expectOne('/api/challenges/');
    expect(postReq.request.method).toBe('POST');
    expect(postReq.request.body).toEqual(request);
    postReq.flush(challenge);

    await createPromise;

    TestBed.tick();
    httpMock.expectOne('/api/challenges/').flush([challenge]);
  });

  it('create() surfaces a validation-problem response without an unhandled rejection', async () => {
    TestBed.tick();
    httpMock.expectOne('/api/challenges/').flush([]);

    const request: CreateChallengeRequest = {
      name: '',
      url: null,
      cadence: 'Daily',
      color: 'blue',
    };

    const createPromise = service.create(request);
    const postReq = httpMock.expectOne('/api/challenges/');
    postReq.flush(
      { title: 'One or more validation errors occurred.', status: 400, errors: { Name: ['Required'] } },
      { status: 400, statusText: 'Bad Request' },
    );

    await expect(createPromise).rejects.toEqual({
      title: 'One or more validation errors occurred.',
      status: 400,
      errors: { Name: ['Required'] },
    });
  });

  it('archive() posts to the archive URL and reloads the list on success', async () => {
    TestBed.tick();
    httpMock.expectOne('/api/challenges/').flush([challenge]);

    const archivePromise = service.archive(challenge.id);

    const archiveReq = httpMock.expectOne(`/api/challenges/${challenge.id}/archive`);
    expect(archiveReq.request.method).toBe('POST');
    archiveReq.flush({ ...challenge, archivedAt: '2026-07-26T00:00:00Z' });

    await archivePromise;

    TestBed.tick();
    httpMock.expectOne('/api/challenges/').flush([]);
  });
});
