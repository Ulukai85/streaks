import {Environment} from './environment.model';

export const environment: Environment = {
  production: false,
  apiUrl: '/api',
  sentryDsn: '', // Explicitly left empty, so Sentry is not initialized in development mode
};
