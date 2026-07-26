import { Routes } from '@angular/router';
import { ChallengeList } from './features/challenges/challenge-list';
import { DashboardPage } from './features/dashboard/dashboard-page';
import { HealthStatus } from './features/health/health-status';

export const routes: Routes = [
  { path: '', component: DashboardPage },
  { path: 'health', component: HealthStatus },
  { path: 'challenges', component: ChallengeList },
];
