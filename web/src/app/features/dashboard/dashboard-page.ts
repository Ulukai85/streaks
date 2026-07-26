import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { HlmBadgeImports } from '@spartan-ng/helm/badge';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmCollapsibleImports } from '@spartan-ng/helm/collapsible';
import { isProblemDetails } from '../../shared/problem-details';
import { CADENCE_LABEL, COLOR_SWATCH_CLASS } from '../challenges/challenge.model';
import { DashboardService } from './dashboard.service';

@Component({
  selector: 'streaks-dashboard-page',
  imports: [RouterLink, ...HlmCardImports, ...HlmButtonImports, ...HlmBadgeImports, ...HlmCollapsibleImports],
  template: `
    <main class="mx-auto flex max-w-2xl flex-col gap-6 p-4">
      <div class="flex items-center justify-between">
        <h1 class="text-lg font-semibold">Dashboard</h1>
        <a routerLink="/challenges" class="text-primary text-sm underline">Challenges verwalten</a>
      </div>

      @if (dashboard.isLoading()) {
        <p class="text-muted-foreground text-sm">Lädt…</p>
      } @else if (dashboard.error()) {
        <p class="text-destructive text-sm">Fehler beim Laden des Dashboards.</p>
      } @else if (dashboard.hasValue()) {
        @let value = dashboard.value();
        @if (value.open.length === 0 && value.doneThisPeriod.length === 0) {
          <p class="text-muted-foreground text-sm">Noch keine Challenges angelegt.</p>
        } @else {
          @if (value.open.length === 0) {
            <p class="text-muted-foreground text-sm">Alles erledigt für heute!</p>
          } @else {
            <div class="flex flex-col gap-3">
              @for (item of value.open; track item.id) {
                <section hlmCard>
                  <div hlmCardHeader class="flex-row items-center justify-between">
                    <div class="flex items-center gap-2">
                      <span class="size-3 rounded-full {{ colorSwatchClass[item.color] }}"></span>
                      <h3 hlmCardTitle>{{ item.name }}</h3>
                    </div>
                    <div class="flex items-center gap-2">
                      <span hlmBadge [variant]="item.streak.isAlive ? 'default' : 'secondary'">{{
                        item.streak.length
                      }}</span>
                      <span class="text-muted-foreground text-sm">{{ cadenceLabel[item.cadence] }}</span>
                    </div>
                  </div>
                  @if (item.url) {
                    <div hlmCardContent>
                      <a
                        [href]="item.url"
                        target="_blank"
                        rel="noopener noreferrer"
                        class="text-primary text-sm underline"
                        >{{ item.url }}</a
                      >
                    </div>
                  }
                  @if (errorMessage(item.id); as message) {
                    <div hlmCardContent>
                      <p class="text-destructive text-sm">{{ message }}</p>
                    </div>
                  }
                  <div hlmCardFooter class="justify-end">
                    <button hlmBtn [disabled]="isSubmitting(item.id)" (click)="tickOff(item.id)">Erledigt</button>
                  </div>
                </section>
              }
            </div>
          }

          @if (value.doneThisPeriod.length > 0) {
            <div hlmCollapsible [(expanded)]="doneExpanded">
              <button hlmCollapsibleTrigger type="button" class="text-muted-foreground text-sm underline">
                @if (doneExpanded()) {
                  Erledigt ausblenden
                } @else {
                  Erledigt anzeigen ({{ value.doneThisPeriod.length }})
                }
              </button>
              <div hlmCollapsibleContent class="flex flex-col gap-3 pt-3">
                @for (item of value.doneThisPeriod; track item.id) {
                  <section hlmCard>
                    <div hlmCardHeader class="flex-row items-center justify-between">
                      <div class="flex items-center gap-2">
                        <span class="size-3 rounded-full {{ colorSwatchClass[item.color] }}"></span>
                        <h3 hlmCardTitle>{{ item.name }}</h3>
                      </div>
                      <div class="flex items-center gap-2">
                        <span hlmBadge [variant]="item.streak.isAlive ? 'default' : 'secondary'">{{
                          item.streak.length
                        }}</span>
                        <span class="text-muted-foreground text-sm">{{ cadenceLabel[item.cadence] }}</span>
                      </div>
                    </div>
                    @if (item.url) {
                      <div hlmCardContent>
                        <a
                          [href]="item.url"
                          target="_blank"
                          rel="noopener noreferrer"
                          class="text-primary text-sm underline"
                          >{{ item.url }}</a
                        >
                      </div>
                    }
                  </section>
                }
              </div>
            </div>
          }
        }
      }
    </main>
  `,
})
export class DashboardPage {
  private readonly dashboardService = inject(DashboardService);

  protected readonly dashboard = this.dashboardService.dashboard;
  protected readonly cadenceLabel = CADENCE_LABEL;
  protected readonly colorSwatchClass = COLOR_SWATCH_CLASS;
  protected readonly doneExpanded = signal(false);

  private readonly submitting = signal<ReadonlySet<string>>(new Set());
  private readonly itemErrors = signal<ReadonlyMap<string, string>>(new Map());

  protected isSubmitting = (id: string): boolean => this.submitting().has(id);
  protected errorMessage = (id: string): string | null => this.itemErrors().get(id) ?? null;

  protected async tickOff(id: string): Promise<void> {
    if (this.isSubmitting(id)) return;

    this.submitting.update((set) => new Set(set).add(id));
    this.itemErrors.update((map) => {
      const next = new Map(map);
      next.delete(id);
      return next;
    });

    try {
      await this.dashboardService.completeChallenge(id);
    } catch (error) {
      const message = isProblemDetails(error)
        ? (error.detail ?? error.title)
        : 'Der Server ist nicht erreichbar. Bitte später erneut versuchen.';
      this.itemErrors.update((map) => new Map(map).set(id, message));
    } finally {
      this.submitting.update((set) => {
        const next = new Set(set);
        next.delete(id);
        return next;
      });
    }
  }
}
