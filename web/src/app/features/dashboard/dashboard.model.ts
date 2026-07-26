import { Cadence, ChallengeColor } from '../challenges/challenge.model';

export interface DashboardStreak {
  length: number;
  isAlive: boolean;
  lastCompletedPeriod: string | null;
}

export interface DashboardItem {
  id: string;
  name: string;
  url: string | null;
  cadence: Cadence;
  color: ChallengeColor;
  sortOrder: number;
  streak: DashboardStreak;
}

export interface DashboardResponse {
  open: DashboardItem[];
  doneThisPeriod: DashboardItem[];
}

export interface CompletionResponse {
  id: string;
  challengeId: string;
  periodStart: string;
  completedAt: string;
  note: string | null;
}
