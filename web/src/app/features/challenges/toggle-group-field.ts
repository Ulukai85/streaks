import { Component, input, model } from '@angular/core';
import { FormValueControl } from '@angular/forms/signals';
import { HlmToggleGroupImports } from '@spartan-ng/helm/toggle-group';

export interface ToggleGroupOption<T> {
  value: T;
  label: string;
  swatchClass?: string;
}

@Component({
  selector: 'streaks-toggle-group-field',
  imports: [...HlmToggleGroupImports],
  template: `
    <hlm-toggle-group type="single" [value]="value()" (valueChange)="onValueChange($event)">
      @for (option of options(); track option.value) {
        <button
          hlmToggleGroupItem
          type="button"
          [value]="option.value"
          [attr.aria-label]="option.swatchClass ? option.label : null"
        >
          @if (option.swatchClass) {
            <span class="size-4 rounded-full {{ option.swatchClass }}"></span>
          } @else {
            {{ option.label }}
          }
        </button>
      }
    </hlm-toggle-group>
  `,
})
export class ToggleGroupField<T> implements FormValueControl<T> {
  readonly value = model<T>('' as T);
  readonly options = input.required<ToggleGroupOption<T>[]>();

  protected onValueChange(next: T | T[] | null | undefined): void {
    if (next !== null && next !== undefined && !Array.isArray(next)) {
      this.value.set(next);
    }
  }
}
