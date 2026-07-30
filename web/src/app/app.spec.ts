import { Component } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';
import { AuthService } from './features/auth/auth.service';

@Component({ selector: 'streaks-login-stub', template: '' })
class LoginStub {}

describe('App', () => {
  let httpMock: HttpTestingController;
  let fixture: ComponentFixture<App>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([{ path: 'login', component: LoginStub }]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    }).compileComponents();
    httpMock = TestBed.inject(HttpTestingController);

    fixture = TestBed.createComponent(App);
    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should create the app', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('hides the logout button when logged out', () => {
    const button = fixture.nativeElement.querySelector('button');
    expect(button).toBeNull();
  });

  it('shows the logout button when authenticated and logs out on click', async () => {
    const loginPromise = TestBed.inject(AuthService).login({ username: 'alice', password: 'hunter2' });
    httpMock.expectOne('/api/auth/login').flush({ accessToken: 'tok', expiresAt: '2026-07-30T12:00:00Z' });
    await loginPromise;
    fixture.detectChanges();

    const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    expect(button).not.toBeNull();
    expect(button.textContent).toContain('Abmelden');

    button.click();

    httpMock.expectOne('/api/auth/logout').flush(null, { status: 204, statusText: 'No Content' });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('button')).toBeNull();
  });
});
