import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { CADENCE_LABEL, COLOR_SWATCH_CLASS } from './challenge.model';
import { ChallengeForm } from './challenge-form';
import { ChallengesService } from './challenges.service';

@Component({
  selector: 'streaks-challenge-list',
  imports: [RouterLink, ChallengeForm, ...HlmCardImports, ...HlmButtonImports],
  template: `
    <main class="mx-auto flex max-w-2xl flex-col gap-6 p-4">
      <div class="flex items-center justify-between">
        <h1 class="text-lg font-semibold">Challenges</h1>
        <a routerLink="/" class="text-primary text-sm underline">Zum Dashboard</a>
      </div>

      <section hlmCard>
        <div hlmCardHeader>
          <h3 hlmCardTitle>Neue Challenge</h3>
        </div>
        <div hlmCardContent>
          <streaks-challenge-form />
        </div>
      </section>

      @if (challenges.isLoading()) {
        <p class="text-muted-foreground text-sm">Lädt…</p>
      } @else if (challenges.error()) {
        <p class="text-destructive text-sm">Fehler beim Laden der Challenges.</p>
      } @else if (challenges.hasValue()) {
        @if (challenges.value().length === 0) {
          <p class="text-muted-foreground text-sm">Noch keine Challenges angelegt.</p>
        } @else {
          <div class="flex flex-col gap-3">
            @for (challenge of challenges.value(); track challenge.id) {
              <section hlmCard>
                <div hlmCardHeader class="flex-row items-center justify-between">
                  <div class="flex items-center gap-2">
                    <span class="size-3 rounded-full {{ colorSwatchClass[challenge.color] }}"></span>
                    <h3 hlmCardTitle>{{ challenge.name }}</h3>
                  </div>
                  <span class="text-muted-foreground text-sm">{{ cadenceLabel[challenge.cadence] }}</span>
                </div>
                @if (challenge.url) {
                  <div hlmCardContent>
                    <a
                      [href]="challenge.url"
                      target="_blank"
                      rel="noopener noreferrer"
                      class="text-primary text-sm underline"
                      >{{ challenge.url }}</a
                    >
                  </div>
                }
                <div hlmCardFooter class="justify-end">
                  <button hlmBtn variant="outline" (click)="archive(challenge.id, challenge.name)">
                    Archivieren
                  </button>
                </div>
              </section>
            }
          </div>
        }
      }
    </main>
  `,
})
export class ChallengeList {
  private readonly challengesService = inject(ChallengesService);

  protected readonly challenges = this.challengesService.challenges;
  protected readonly cadenceLabel = CADENCE_LABEL;
  protected readonly colorSwatchClass = COLOR_SWATCH_CLASS;

  protected async archive(id: string, name: string): Promise<void> {
    if (!window.confirm(`„${name}“ wirklich archivieren? Das kann nicht rückgängig gemacht werden.`)) {
      return;
    }
    await this.challengesService.archive(id);
  }
}
