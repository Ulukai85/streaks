import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmFieldImports } from '@spartan-ng/helm/field';
import { HlmInputImports } from '@spartan-ng/helm/input';
import { HlmToggleGroupImports } from '@spartan-ng/helm/toggle-group';
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

function absoluteHttpUrlValidator(control: AbstractControl<string>): ValidationErrors | null {
  const value = control.value;
  if (!value) {
    return null;
  }
  try {
    const url = new URL(value);
    return url.protocol === 'http:' || url.protocol === 'https:' ? null : { url: true };
  } catch {
    return { url: true };
  }
}

interface ChallengeFormControls {
  name: FormControl<string>;
  url: FormControl<string>;
  cadence: FormControl<Cadence | null>;
  color: FormControl<ChallengeColor | null>;
}

function buildForm(): FormGroup<ChallengeFormControls> {
  return new FormGroup<ChallengeFormControls>({
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
    url: new FormControl('', { nonNullable: true, validators: [absoluteHttpUrlValidator] }),
    cadence: new FormControl<Cadence | null>(null, { validators: [Validators.required] }),
    color: new FormControl<ChallengeColor | null>(null, { validators: [Validators.required] }),
  });
}

@Component({
  selector: 'streaks-challenge-form',
  imports: [
    ReactiveFormsModule,
    ...HlmFieldImports,
    ...HlmInputImports,
    ...HlmToggleGroupImports,
    ...HlmButtonImports,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form [formGroup]="form" (ngSubmit)="submit()" class="flex flex-col gap-4">
      <div hlmField>
        <label hlmFieldLabel for="name">Name</label>
        <input hlmInput id="name" [formControl]="form.controls.name" />
        @if (form.controls.name.invalid && form.controls.name.touched) {
          <hlm-field-error>{{ errorMessage(form.controls.name) }}</hlm-field-error>
        }
      </div>

      <div hlmField>
        <label hlmFieldLabel for="url">Link (optional)</label>
        <input hlmInput id="url" placeholder="https://…" [formControl]="form.controls.url" />
        @if (form.controls.url.invalid && form.controls.url.touched) {
          <hlm-field-error>{{ errorMessage(form.controls.url) }}</hlm-field-error>
        }
      </div>

      <fieldset hlmFieldSet>
        <legend hlmFieldLegend>Rhythmus</legend>
        <hlm-toggle-group type="single" [formControl]="form.controls.cadence">
          @for (cadence of cadences; track cadence) {
            <button hlmToggleGroupItem type="button" [value]="cadence">
              {{ cadenceLabel[cadence] }}
            </button>
          }
        </hlm-toggle-group>
        @if (form.controls.cadence.invalid && form.controls.cadence.touched) {
          <hlm-field-error [forceShow]="true">Bitte einen Rhythmus wählen.</hlm-field-error>
        }
      </fieldset>

      <fieldset hlmFieldSet>
        <legend hlmFieldLegend>Farbe</legend>
        <hlm-toggle-group type="single" [formControl]="form.controls.color">
          @for (color of colors; track color) {
            <button hlmToggleGroupItem type="button" [value]="color" [attr.aria-label]="colorLabel[color]">
              <span class="size-4 rounded-full {{ colorSwatchClass[color] }}"></span>
            </button>
          }
        </hlm-toggle-group>
        @if (form.controls.color.invalid && form.controls.color.touched) {
          <hlm-field-error [forceShow]="true">Bitte eine Farbe wählen.</hlm-field-error>
        }
      </fieldset>

      @if (submitError()) {
        <p class="text-destructive text-sm">{{ submitError() }}</p>
      }

      <button hlmBtn type="submit" class="self-start">Hinzufügen</button>
    </form>
  `,
})
export class ChallengeForm {
  private readonly challengesService = inject(ChallengesService);

  protected readonly cadences: Cadence[] = ['Daily', 'Weekly', 'Monthly'];
  protected readonly colors = CHALLENGE_COLORS;
  protected readonly cadenceLabel = CADENCE_LABEL;
  protected readonly colorLabel = COLOR_LABEL;
  protected readonly colorSwatchClass = COLOR_SWATCH_CLASS;

  protected readonly form = buildForm();
  protected readonly submitError = signal<string | null>(null);

  protected errorMessage(control: AbstractControl): string {
    const errors = control.errors;
    if (!errors) {
      return '';
    }
    if (errors['server']) {
      return errors['server'] as string;
    }
    if (errors['required']) {
      return 'Pflichtfeld.';
    }
    if (errors['maxlength']) {
      return 'Zu lang.';
    }
    if (errors['url']) {
      return 'Bitte eine gültige http(s)-URL angeben.';
    }
    return 'Ungültige Eingabe.';
  }

  protected async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitError.set(null);
    const value = this.form.getRawValue();
    const request: CreateChallengeRequest = {
      name: value.name,
      url: value.url || null,
      cadence: value.cadence!,
      color: value.color!,
    };

    try {
      await this.challengesService.create(request);
      this.form.reset({ name: '', url: '', cadence: null, color: null });
    } catch (error) {
      if (isValidationProblemDetails(error)) {
        this.applyServerErrors(error.errors);
      } else {
        this.submitError.set('Der Server ist nicht erreichbar. Bitte später erneut versuchen.');
      }
    }
  }

  private applyServerErrors(errors: Record<string, string[]>): void {
    for (const [key, messages] of Object.entries(errors)) {
      const controlName = (key.charAt(0).toLowerCase() + key.slice(1)) as keyof ChallengeFormControls;
      const control = this.form.controls[controlName];
      if (control) {
        control.setErrors({ server: messages[0] });
        control.markAsTouched();
      }
    }
  }
}
