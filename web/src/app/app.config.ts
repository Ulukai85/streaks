import { ApplicationConfig, ErrorHandler, provideBrowserGlobalErrorListeners} from '@angular/core';
import * as Sentry from '@sentry/angular';

import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';
import { authInterceptor } from './features/auth/auth.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    {
      provide: ErrorHandler,
      useValue: Sentry.createErrorHandler(),
    },
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideRouter(routes),
  ],
};
