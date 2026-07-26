export interface ProblemDetails {
  title: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

export function isProblemDetails(value: unknown): value is ProblemDetails {
  return (
    typeof value === 'object' &&
    value !== null &&
    'title' in value &&
    typeof (value as { title: unknown }).title === 'string'
  );
}
