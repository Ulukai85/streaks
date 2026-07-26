export const CADENCES = ['Daily', 'Weekly', 'Monthly'] as const;

export type Cadence = (typeof CADENCES)[number];

export const CHALLENGE_COLORS = [
  'red',
  'orange',
  'amber',
  'green',
  'teal',
  'blue',
  'indigo',
  'pink',
] as const;

export type ChallengeColor = (typeof CHALLENGE_COLORS)[number];

export const COLOR_SWATCH_CLASS: Record<ChallengeColor, string> = {
  red: 'bg-red-500',
  orange: 'bg-orange-500',
  amber: 'bg-amber-500',
  green: 'bg-green-500',
  teal: 'bg-teal-500',
  blue: 'bg-blue-500',
  indigo: 'bg-indigo-500',
  pink: 'bg-pink-500',
};

export const COLOR_LABEL: Record<ChallengeColor, string> = {
  red: 'Rot',
  orange: 'Orange',
  amber: 'Bernstein',
  green: 'Grün',
  teal: 'Türkis',
  blue: 'Blau',
  indigo: 'Indigo',
  pink: 'Pink',
};

export const CADENCE_LABEL: Record<Cadence, string> = {
  Daily: 'Täglich',
  Weekly: 'Wöchentlich',
  Monthly: 'Monatlich',
};

export interface Challenge {
  id: string;
  name: string;
  url: string | null;
  cadence: Cadence;
  targetCount: number;
  startsOn: string;
  archivedAt: string | null;
  color: ChallengeColor;
  sortOrder: number;
}

export interface CreateChallengeRequest {
  name: string;
  url: string | null;
  cadence: Cadence;
  color: ChallengeColor;
}
