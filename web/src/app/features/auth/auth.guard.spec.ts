import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, UrlTree } from '@angular/router';
import { authGuard } from './auth.guard';
import { AuthService } from './auth.service';

describe('authGuard', () => {
  let httpMock: HttpTestingController;
  let authService: AuthService;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    httpMock = TestBed.inject(HttpTestingController);
    authService = TestBed.inject(AuthService);
    router = TestBed.inject(Router);
  });

  afterEach(() => {
    httpMock.verify();
  });

  function runGuard() {
    return TestBed.runInInjectionContext(() =>
      authGuard(
        {} as Parameters<typeof authGuard>[0],
        {} as Parameters<typeof authGuard>[1],
      ),
    );
  }

  it('allows navigation when already authenticated', async () => {
    const loginPromise = authService.login({ username: 'alice', password: 'hunter2' });
    httpMock.expectOne('/api/auth/login').flush({ accessToken: 'tok', expiresAt: '2026-07-30T12:00:00Z' });
    await loginPromise;

    const result = await runGuard();

    expect(result).toBe(true);
    httpMock.expectNone('/api/auth/refresh');
  });

  it('silently refreshes and allows navigation when refresh succeeds', async () => {
    const resultPromise = runGuard();

    httpMock
      .expectOne('/api/auth/refresh')
      .flush({ accessToken: 'tok', expiresAt: '2026-07-30T12:00:00Z' });

    expect(await resultPromise).toBe(true);
    expect(authService.isAuthenticated()).toBe(true);
  });

  it('redirects to /login when the silent refresh fails', async () => {
    const resultPromise = runGuard();

    httpMock
      .expectOne('/api/auth/refresh')
      .flush(
        { title: 'Unauthorized', status: 401, detail: 'Session expired or invalid.' },
        { status: 401, statusText: 'Unauthorized' },
      );

    const result = await resultPromise;

    expect(result).toBeInstanceOf(UrlTree);
    expect(router.serializeUrl(result as UrlTree)).toBe('/login');
  });
});
