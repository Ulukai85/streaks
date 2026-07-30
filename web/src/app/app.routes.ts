import { Routes } from '@angular/router';
import { authGuard } from './features/auth/auth.guard';
import { ChallengeList } from './features/challenges/challenge-list';
import { DashboardPage } from './features/dashboard/dashboard-page';
import { HealthStatus } from './features/health/health-status';

export const routes: Routes = [
  { path: '', component: DashboardPage, canActivate: [authGuard] },
  { path: 'health', component: HealthStatus },
  { path: 'challenges', component: ChallengeList, canActivate: [authGuard] },
];
