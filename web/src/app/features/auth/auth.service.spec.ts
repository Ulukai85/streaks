import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  const loginResponse = { accessToken: 'access-token', expiresAt: '2026-07-30T12:00:00Z' };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('login() posts credentials with withCredentials and sets isAuthenticated on success', async () => {
    const loginPromise = service.login({ username: 'alice', password: 'hunter2' });

    const req = httpMock.expectOne('/api/auth/login');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ username: 'alice', password: 'hunter2' });
    expect(req.request.withCredentials).toBe(true);
    req.flush(loginResponse);

    await loginPromise;

    expect(service.isAuthenticated()).toBe(true);
    expect(service.token()).toBe('access-token');
  });

  it('login() rejects with problem details on failure and leaves isAuthenticated false', async () => {
    const loginPromise = service.login({ username: 'alice', password: 'wrong' });

    const req = httpMock.expectOne('/api/auth/login');
    req.flush(
      { title: 'Unauthorized', status: 401, detail: 'Invalid username or password.' },
      { status: 401, statusText: 'Unauthorized' },
    );

    await expect(loginPromise).rejects.toEqual({
      title: 'Unauthorized',
      status: 401,
      detail: 'Invalid username or password.',
    });
    expect(service.isAuthenticated()).toBe(false);
  });

  it('refresh() posts with no body and withCredentials, and sets isAuthenticated on success', async () => {
    const refreshPromise = service.refresh();

    const req = httpMock.expectOne('/api/auth/refresh');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toBeNull();
    expect(req.request.withCredentials).toBe(true);
    req.flush(loginResponse);

    await refreshPromise;

    expect(service.isAuthenticated()).toBe(true);
  });

  it('refresh() clears the token and rejects on failure', async () => {
    const refreshPromise = service.refresh();

    const req = httpMock.expectOne('/api/auth/refresh');
    req.flush(
      { title: 'Unauthorized', status: 401, detail: 'Session expired or invalid.' },
      { status: 401, statusText: 'Unauthorized' },
    );

    await expect(refreshPromise).rejects.toEqual({
      title: 'Unauthorized',
      status: 401,
      detail: 'Session expired or invalid.',
    });
    expect(service.isAuthenticated()).toBe(false);
  });

  it('logout() posts to the logout endpoint and clears isAuthenticated regardless of outcome', async () => {
    const loginPromise = service.login({ username: 'alice', password: 'hunter2' });
    httpMock.expectOne('/api/auth/login').flush(loginResponse);
    await loginPromise;
    expect(service.isAuthenticated()).toBe(true);

    const logoutPromise = service.logout();

    const req = httpMock.expectOne('/api/auth/logout');
    expect(req.request.method).toBe('POST');
    expect(req.request.withCredentials).toBe(true);
    req.flush(null, { status: 204, statusText: 'No Content' });

    await logoutPromise;

    expect(service.isAuthenticated()).toBe(false);
  });
});
