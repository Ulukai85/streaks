import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Service, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { isProblemDetails } from '../../shared/problem-details';
import { LoginRequest, LoginResponse } from './auth.model';

@Service()
export class AuthService {
  private readonly http = inject(HttpClient);

  private readonly accessToken = signal<string | null>(null);

  readonly token = this.accessToken.asReadonly();
  readonly isAuthenticated = computed(() => this.accessToken() !== null);

  async login(request: LoginRequest): Promise<void> {
    try {
      const response = await firstValueFrom(
        this.http.post<LoginResponse>(`${environment.apiUrl}/auth/login`, request, {
          withCredentials: true,
        }),
      );
      this.accessToken.set(response.accessToken);
    } catch (error) {
      if (error instanceof HttpErrorResponse && isProblemDetails(error.error)) {
        throw error.error;
      }
      throw error;
    }
  }

  async refresh(): Promise<void> {
    try {
      const response = await firstValueFrom(
        this.http.post<LoginResponse>(`${environment.apiUrl}/auth/refresh`, null, {
          withCredentials: true,
        }),
      );
      this.accessToken.set(response.accessToken);
    } catch (error) {
      this.accessToken.set(null);
      if (error instanceof HttpErrorResponse && isProblemDetails(error.error)) {
        throw error.error;
      }
      throw error;
    }
  }

  async logout(): Promise<void> {
    try {
      await firstValueFrom(
        this.http.post(`${environment.apiUrl}/auth/logout`, null, { withCredentials: true }),
      );
    } finally {
      this.accessToken.set(null);
    }
  }
}
