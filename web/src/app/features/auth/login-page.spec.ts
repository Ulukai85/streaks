import { Component } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { LoginPage } from './login-page';

@Component({ selector: 'streaks-dashboard-stub', template: '' })
class DashboardStub {}

function setInputValue(input: HTMLInputElement, value: string): void {
  input.value = value;
  input.dispatchEvent(new Event('input'));
}

// The login form's submission action is a plain async function, not tied to a
// router navigation or an httpResource — neither of which registers as an
// Angular "pending task", so `fixture.whenStable()` can resolve before it
// settles. Draining the microtask queue directly is what actually waits for it.
async function flushMicrotasks(): Promise<void> {
  for (let i = 0; i < 10; i++) {
    await Promise.resolve();
  }
}

describe('LoginPage', () => {
  let httpMock: HttpTestingController;
  let fixture: ComponentFixture<LoginPage>;
  let router: Router;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LoginPage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: '', component: DashboardStub }]),
      ],
    }).compileComponents();
    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);

    fixture = TestBed.createComponent(LoginPage);
    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('does not submit and shows an error when fields are blank', () => {
    const form = fixture.nativeElement.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Pflichtfeld.');
    httpMock.expectNone('/api/auth/login');
  });

  it('submits valid credentials and navigates to / on success', async () => {
    const usernameInput = fixture.nativeElement.querySelector('#username') as HTMLInputElement;
    const passwordInput = fixture.nativeElement.querySelector('#password') as HTMLInputElement;
    setInputValue(usernameInput, 'alice');
    setInputValue(passwordInput, 'hunter2');
    fixture.detectChanges();

    const form = fixture.nativeElement.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    const postReq = httpMock.expectOne('/api/auth/login');
    expect(postReq.request.body).toEqual({ username: 'alice', password: 'hunter2' });
    postReq.flush({ accessToken: 'tok', expiresAt: '2026-07-30T12:00:00Z' });

    await flushMicrotasks();

    expect(router.url).toBe('/');
  });

  it('shows the generic server error on a 401 and does not navigate', async () => {
    const navigateByUrl = vi.spyOn(router, 'navigateByUrl');

    const usernameInput = fixture.nativeElement.querySelector('#username') as HTMLInputElement;
    const passwordInput = fixture.nativeElement.querySelector('#password') as HTMLInputElement;
    setInputValue(usernameInput, 'alice');
    setInputValue(passwordInput, 'wrong');
    fixture.detectChanges();

    const form = fixture.nativeElement.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    const postReq = httpMock.expectOne('/api/auth/login');
    postReq.flush(
      { title: 'Unauthorized', status: 401, detail: 'Invalid username or password.' },
      { status: 401, statusText: 'Unauthorized' },
    );

    await flushMicrotasks();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Invalid username or password.');
    expect(navigateByUrl).not.toHaveBeenCalled();
  });
});
