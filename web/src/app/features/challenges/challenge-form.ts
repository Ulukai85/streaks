import { Component, inject, signal } from '@angular/core';
import {
  FormField,
  FormRoot,
  form,
  maxLength,
  required,
  validate,
  type ValidationError,
} from '@angular/forms/signals';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmFieldImports } from '@spartan-ng/helm/field';
import { HlmInputImports } from '@spartan-ng/helm/input';
import {
  CADENCE_LABEL,
  Cadence,
  CHALLENGE_COLORS,
  ChallengeColor,
  COLOR_LABEL,
  COLOR_SWATCH_CLASS,
  CreateChallengeRequest,
} from './challenge.model';
import { ChallengesService, isValidationProblemDetails } from './challenges.service';
import { ToggleGroupField, ToggleGroupOption } from './toggle-group-field';

interface ChallengeFormModel {
  name: string;
  url: string;
  cadence: Cadence | '';
  color: ChallengeColor | '';
}

function emptyModel(): ChallengeFormModel {
  return { name: '', url: '', cadence: '', color: '' };
}

function isAbsoluteHttpUrl(value: string): boolean {
  try {
    const url = new URL(value);
    return url.protocol === 'http:' || url.protocol === 'https:';
  } catch {
    return false;
  }
}

function toCamelCase(key: string): string {
  return key.charAt(0).toLowerCase() + key.slice(1);
}

@Component({
  selector: 'streaks-challenge-form',
  imports: [FormField, FormRoot, ToggleGroupField, ...HlmFieldImports, ...HlmInputImports, ...HlmButtonImports],
  template: `
    <form [formRoot]="challengeForm" class="flex flex-col gap-4">
      <div hlmField>
        <label hlmFieldLabel for="name">Name</label>
        <input hlmInput id="name" [formField]="challengeForm.name" />
        @if (challengeForm.name().touched() && challengeForm.name().invalid()) {
          @for (error of challengeForm.name().errors(); track error) {
            <hlm-field-error [forceShow]="true">{{ error.message }}</hlm-field-error>
          }
        }
      </div>

      <div hlmField>
        <label hlmFieldLabel for="url">Link (optional)</label>
        <input hlmInput id="url" placeholder="https://…" [formField]="challengeForm.url" />
        @if (challengeForm.url().touched() && challengeForm.url().invalid()) {
          @for (error of challengeForm.url().errors(); track error) {
            <hlm-field-error [forceShow]="true">{{ error.message }}</hlm-field-error>
          }
        }
      </div>

      <fieldset hlmFieldSet>
        <legend hlmFieldLegend>Rhythmus</legend>
        <streaks-toggle-group-field [formField]="challengeForm.cadence" [options]="cadenceOptions" />
        @if (challengeForm.cadence().touched() && challengeForm.cadence().invalid()) {
          @for (error of challengeForm.cadence().errors(); track error) {
            <hlm-field-error [forceShow]="true">{{ error.message }}</hlm-field-error>
          }
        }
      </fieldset>

      <fieldset hlmFieldSet>
        <legend hlmFieldLegend>Farbe</legend>
        <streaks-toggle-group-field [formField]="challengeForm.color" [options]="colorOptions" />
        @if (challengeForm.color().touched() && challengeForm.color().invalid()) {
          @for (error of challengeForm.color().errors(); track error) {
            <hlm-field-error [forceShow]="true">{{ error.message }}</hlm-field-error>
          }
        }
      </fieldset>

      @if (challengeForm().errors(); as rootErrors) {
        @for (error of rootErrors; track error) {
          <p class="text-destructive text-sm">{{ error.message }}</p>
        }
      }

      <button hlmBtn type="submit" class="self-start" [disabled]="challengeForm().submitting()">
        Hinzufügen
      </button>
    </form>
  `,
})
export class ChallengeForm {
  private readonly challengesService = inject(ChallengesService);

  protected readonly cadenceOptions: ToggleGroupOption<Cadence>[] = (
    ['Daily', 'Weekly', 'Monthly'] as const
  ).map((cadence) => ({ value: cadence, label: CADENCE_LABEL[cadence] }));

  protected readonly colorOptions: ToggleGroupOption<ChallengeColor>[] = CHALLENGE_COLORS.map((color) => ({
    value: color,
    label: COLOR_LABEL[color],
    swatchClass: COLOR_SWATCH_CLASS[color],
  }));

  private readonly model = signal<ChallengeFormModel>(emptyModel());

  protected readonly challengeForm = form(
    this.model,
    (path) => {
      required(path.name, { message: 'Pflichtfeld.' });
      maxLength(path.name, 200, { message: 'Zu lang.' });

      validate(path.url, ({ value }) => {
        const url = value();
        if (!url || isAbsoluteHttpUrl(url)) {
          return null;
        }
        return { kind: 'url', message: 'Bitte eine gültige http(s)-URL angeben.' };
      });

      required(path.cadence, { message: 'Bitte einen Rhythmus wählen.' });
      required(path.color, { message: 'Bitte eine Farbe wählen.' });
    },
    {
      submission: {
        action: async (field): Promise<ValidationError | ValidationError[] | undefined> => {
          const value = this.model();
          const request: CreateChallengeRequest = {
            name: value.name,
            url: value.url || null,
            cadence: value.cadence as Cadence,
            color: value.color as ChallengeColor,
          };

          try {
            await this.challengesService.create(request);
            this.model.set(emptyModel());
            return undefined;
          } catch (error) {
            if (isValidationProblemDetails(error)) {
              return Object.entries(error.errors).map(([key, messages]) => ({
                kind: 'server',
                message: messages[0],
                fieldTree: field[toCamelCase(key) as keyof typeof field] ?? field,
              }));
            }
            return {
              kind: 'network',
              message: 'Der Server ist nicht erreichbar. Bitte später erneut versuchen.',
            };
          }
        },
      },
    },
  );
}
