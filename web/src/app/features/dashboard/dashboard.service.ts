import { HttpClient, HttpErrorResponse, httpResource } from '@angular/common/http';
import { inject, Service } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CompletionResponse, DashboardResponse } from './dashboard.model';

@Service()
export class DashboardService {
  private readonly http = inject(HttpClient);

  readonly dashboard = httpResource<DashboardResponse>(() => `${environment.apiUrl}/dashboard/`);

  async completeChallenge(challengeId: string): Promise<CompletionResponse> {
    try {
      const completion = await firstValueFrom(
        this.http.post<CompletionResponse>(`${environment.apiUrl}/challenges/${challengeId}/completions`, {}),
      );
      this.dashboard.reload();
      return completion;
    } catch (error) {
      if (error instanceof HttpErrorResponse) {
        throw error.error;
      }
      throw error;
    }
  }
}
