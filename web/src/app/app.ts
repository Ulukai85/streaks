import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmButtonImports } from '@spartan-ng/helm/button';

@Component({
  selector: 'streaks-root',
  imports: [RouterOutlet, ...HlmCardImports, ...HlmButtonImports],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  protected readonly title = signal('streaks');
}
