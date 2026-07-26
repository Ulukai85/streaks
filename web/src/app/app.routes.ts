import { Routes } from '@angular/router';
import { ChallengeList } from './features/challenges/challenge-list';
import { HealthStatus } from './features/health/health-status';

export const routes: Routes = [
  { path: '', component: HealthStatus },
  { path: 'challenges', component: ChallengeList },
];
