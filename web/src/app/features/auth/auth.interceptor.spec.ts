import {
  HttpErrorResponse,
  HttpHandlerFn,
  HttpRequest,
  HttpResponse,
  provideHttpClient,
} from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

describe('authInterceptor', () => {
  let httpMock: HttpTestingController;
  let authService: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    httpMock = TestBed.inject(HttpTestingController);
    authService = TestBed.inject(AuthService);
  });

  afterEach(() => {
    httpMock.verify();
  });

  function run(req: HttpRequest<unknown>, next: HttpHandlerFn) {
    return TestBed.runInInjectionContext(() => authInterceptor(req, next));
  }

  it('attaches the Authorization header when a token is present', async () => {
    const loginPromise = authService.login({ username: 'alice', password: 'hunter2' });
    httpMock.expectOne('/api/auth/login').flush({ accessToken: 'tok', expiresAt: '2026-07-30T12:00:00Z' });
    await loginPromise;

    const req = new HttpRequest('GET', '/api/challenges/');
    const next = vi.fn<HttpHandlerFn>(() => of(new HttpResponse({ status: 200 })));

    await firstValueFrom(run(req, next));

    expect(next).toHaveBeenCalledTimes(1);
    const sentReq = next.mock.calls[0][0] as HttpRequest<unknown>;
    expect(sentReq.headers.get('Authorization')).toBe('Bearer tok');
  });

  it('does not attach a header when no token is present', async () => {
    const req = new HttpRequest('GET', '/api/challenges/');
    const next = vi.fn<HttpHandlerFn>(() => of(new HttpResponse({ status: 200 })));

    await firstValueFrom(run(req, next));

    const sentReq = next.mock.calls[0][0] as HttpRequest<unknown>;
    expect(sentReq.headers.has('Authorization')).toBe(false);
  });

  it('on a 401 from a non-auth request, refreshes once and retries with the new token', async () => {
    const req = new HttpRequest('GET', '/api/challenges/');
    let callCount = 0;
    const next = vi.fn<HttpHandlerFn>(() => {
      callCount++;
      return callCount === 1
        ? throwError(() => new HttpErrorResponse({ status: 401 }))
        : of(new HttpResponse({ status: 200 }));
    });

    const resultPromise = firstValueFrom(run(req, next));

    httpMock
      .expectOne('/api/auth/refresh')
      .flush({ accessToken: 'new-tok', expiresAt: '2026-07-30T12:00:00Z' });

    await resultPromise;

    expect(next).toHaveBeenCalledTimes(2);
    const retriedReq = next.mock.calls[1][0] as HttpRequest<unknown>;
    expect(retriedReq.headers.get('Authorization')).toBe('Bearer new-tok');
  });

  it('does not attempt a refresh for a 401 from an /auth/ request itself', async () => {
    const req = new HttpRequest('POST', '/api/auth/login', {});
    const next = vi.fn<HttpHandlerFn>(() =>
      throwError(() => new HttpErrorResponse({ status: 401 })),
    );

    await expect(firstValueFrom(run(req, next))).rejects.toBeInstanceOf(
      HttpErrorResponse,
    );

    expect(next).toHaveBeenCalledTimes(1);
    httpMock.expectNone('/api/auth/refresh');
  });

  it('propagates the original 401 when the refresh itself fails', async () => {
    const req = new HttpRequest('GET', '/api/challenges/');
    const next = vi.fn<HttpHandlerFn>(() =>
      throwError(() => new HttpErrorResponse({ status: 401 })),
    );

    const resultPromise = firstValueFrom(run(req, next));

    httpMock
      .expectOne('/api/auth/refresh')
      .flush(
        { title: 'Unauthorized', status: 401, detail: 'Session expired or invalid.' },
        { status: 401, statusText: 'Unauthorized' },
      );

    await expect(resultPromise).rejects.toMatchObject({ status: 401 });
    expect(next).toHaveBeenCalledTimes(1);
  });
});
