import { HttpClient, HttpErrorResponse, httpResource } from '@angular/common/http';
import { inject, Service } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Challenge, CreateChallengeRequest } from './challenge.model';

export interface ValidationProblemDetails {
  title: string;
  status: number;
  errors: Record<string, string[]>;
}

export function isValidationProblemDetails(value: unknown): value is ValidationProblemDetails {
  return (
    typeof value === 'object' &&
    value !== null &&
    'errors' in value &&
    typeof (value as { errors: unknown }).errors === 'object'
  );
}

@Service()
export class ChallengesService {
  private readonly http = inject(HttpClient);

  readonly challenges = httpResource<Challenge[]>(() => `${environment.apiUrl}/challenges/`);

  async create(request: CreateChallengeRequest): Promise<Challenge> {
    try {
      const challenge = await firstValueFrom(
        this.http.post<Challenge>(`${environment.apiUrl}/challenges/`, request),
      );
      this.challenges.reload();
      return challenge;
    } catch (error) {
      if (error instanceof HttpErrorResponse && isValidationProblemDetails(error.error)) {
        throw error.error;
      }
      throw error;
    }
  }

  async archive(id: string): Promise<Challenge> {
    const challenge = await firstValueFrom(
      this.http.post<Challenge>(`${environment.apiUrl}/challenges/${id}/archive`, {}),
    );
    this.challenges.reload();
    return challenge;
  }
}
