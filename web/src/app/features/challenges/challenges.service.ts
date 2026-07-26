import { HttpClient, HttpErrorResponse, httpResource } from '@angular/common/http';
import { inject, Service } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { isProblemDetails } from '../../shared/problem-details';
import { Challenge, CreateChallengeRequest } from './challenge.model';

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
      if (error instanceof HttpErrorResponse && isProblemDetails(error.error)) {
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
