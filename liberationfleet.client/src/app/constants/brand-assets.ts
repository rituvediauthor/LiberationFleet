export type BrandLogoVariant = 'lf' | 'fleet' | 'crew';

const LF_LOGO_ASSETS = {
  hex: 'assets/images/LF Logo hex.png',
  grey: 'assets/images/LF Logo grey.png'
} as const;

export const BRAND_LOGO_ASSETS: Record<BrandLogoVariant, { hex: string; grey: string }> = {
  lf: { ...LF_LOGO_ASSETS },
  /** Fleet hub / nav — Liberation Fleet hex branding */
  fleet: { ...LF_LOGO_ASSETS },
  crew: {
    hex: 'assets/images/Crew Logo hex.png',
    grey: 'assets/images/Crew Logo Grey.png'
  }
};
