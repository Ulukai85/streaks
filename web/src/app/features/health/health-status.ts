import { Component } from '@angular/core';
import { httpResource } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { environment } from '../../../environments/environment';

interface HealthResponse {
  status: string;
  databaseConnected: boolean;
}

@Component({
  selector: 'streaks-health-status',
  imports: [RouterLink, ...HlmCardImports, ...HlmButtonImports],
  template: `
    <main class="flex min-h-dvh flex-col items-center justify-center gap-4 p-4">
      <section hlmCard class="w-full max-w-sm">
        <div hlmCardHeader>
          <h3 hlmCardTitle>Systemstatus</h3>
          <p hlmCardDescription>Verbindung zur Datenbank</p>
        </div>
        <div hlmCardContent>
          @if (health.isLoading()) {
            <p class="text-muted-foreground text-sm">Lädt…</p>
          } @else if (health.error()) {
            <p class="text-destructive text-sm">Fehler beim Abrufen des Status.</p>
          } @else if (health.hasValue()) {
            <p class="text-sm">
              Status: <strong>{{ health.value().status }}</strong><br />
              Datenbank verbunden: <strong>{{ health.value().databaseConnected ? 'ja' : 'nein' }}</strong>
            </p>
          }
        </div>
        <div hlmCardFooter class="justify-end">
          <button hlmBtn (click)="health.reload()">Neu laden</button>
        </div>
      </section>
      <a routerLink="/" class="text-primary text-sm underline">Zurück zum Dashboard</a>
    </main>
  `,
})
export class HealthStatus {
  protected readonly health = httpResource<HealthResponse>(() => `${environment.apiUrl}/health`);
}
