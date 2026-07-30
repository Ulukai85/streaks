import { Component, inject, signal } from '@angular/core';
import { FormField, FormRoot, form, required, type ValidationError } from '@angular/forms/signals';
import { Router } from '@angular/router';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmFieldImports } from '@spartan-ng/helm/field';
import { HlmInputImports } from '@spartan-ng/helm/input';
import { isProblemDetails } from '../../shared/problem-details';
import { LoginRequest } from './auth.model';
import { AuthService } from './auth.service';

function emptyModel(): LoginRequest {
  return { username: '', password: '' };
}

@Component({
  selector: 'streaks-login-page',
  imports: [FormField, FormRoot, ...HlmFieldImports, ...HlmInputImports, ...HlmButtonImports, ...HlmCardImports],
  template: `
    <main class="flex min-h-dvh flex-col items-center justify-center gap-4 p-4">
      <section hlmCard class="w-full max-w-sm">
        <div hlmCardHeader>
          <h3 hlmCardTitle>Anmelden</h3>
        </div>
        <div hlmCardContent>
          <form [formRoot]="loginForm" class="flex flex-col gap-4">
            <div hlmField>
              <label hlmFieldLabel for="username">Benutzername</label>
              <input hlmInput id="username" [formField]="loginForm.username" />
              @if (loginForm.username().touched() && loginForm.username().invalid()) {
                @for (error of loginForm.username().errors(); track error) {
                  <hlm-field-error [forceShow]="true">{{ error.message }}</hlm-field-error>
                }
              }
            </div>

            <div hlmField>
              <label hlmFieldLabel for="password">Passwort</label>
              <input hlmInput id="password" type="password" [formField]="loginForm.password" />
              @if (loginForm.password().touched() && loginForm.password().invalid()) {
                @for (error of loginForm.password().errors(); track error) {
                  <hlm-field-error [forceShow]="true">{{ error.message }}</hlm-field-error>
                }
              }
            </div>

            @if (loginForm().errors(); as rootErrors) {
              @for (error of rootErrors; track error) {
                <p class="text-destructive text-sm">{{ error.message }}</p>
              }
            }

            <button hlmBtn type="submit" [disabled]="loginForm().submitting()">Anmelden</button>
          </form>
        </div>
      </section>
    </main>
  `,
})
export class LoginPage {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  private readonly model = signal<LoginRequest>(emptyModel());

  protected readonly loginForm = form(
    this.model,
    (path) => {
      required(path.username, { message: 'Pflichtfeld.' });
      required(path.password, { message: 'Pflichtfeld.' });
    },
    {
      submission: {
        action: async (): Promise<ValidationError | ValidationError[] | undefined> => {
          try {
            await this.authService.login(this.model());
            await this.router.navigateByUrl('/');
            return undefined;
          } catch (error) {
            if (isProblemDetails(error)) {
              return { kind: 'server', message: error.detail ?? error.title };
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
