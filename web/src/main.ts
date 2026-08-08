import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';
import { environment } from './environments/environment';
import * as Sentry from "@sentry/angular";

Sentry.init({
  dsn: environment.sentryDsn,
})

bootstrapApplication(App, appConfig).catch((err) => console.error(err));
