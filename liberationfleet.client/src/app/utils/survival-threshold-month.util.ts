/** Format survival-threshold calendar month as MM/YYYY. */
export function formatSurvivalThresholdMonth(year?: number | null, month?: number | null): string {
  if (year == null || month == null || month < 1 || month > 12 || year < 2000 || year > 2100) {
    return '';
  }
  return `${String(month).padStart(2, '0')}/${year}`;
}

/** Parse MM/YYYY (also accepts M/YYYY or YYYY-MM). */
export function parseSurvivalThresholdMonth(value: string | null | undefined): { year: number; month: number } | null {
  const raw = (value ?? '').trim();
  if (!raw) {
    return null;
  }

  const slash = /^(\d{1,2})\s*\/\s*(\d{4})$/.exec(raw);
  if (slash) {
    const month = Number(slash[1]);
    const year = Number(slash[2]);
    if (month >= 1 && month <= 12 && year >= 2000 && year <= 2100) {
      return { year, month };
    }
    return null;
  }

  const iso = /^(\d{4})-(\d{1,2})$/.exec(raw);
  if (iso) {
    const year = Number(iso[1]);
    const month = Number(iso[2]);
    if (month >= 1 && month <= 12 && year >= 2000 && year <= 2100) {
      return { year, month };
    }
  }

  return null;
}

export function currentSurvivalThresholdMonth(): { year: number; month: number } {
  const now = new Date();
  return { year: now.getUTCFullYear(), month: now.getUTCMonth() + 1 };
}
