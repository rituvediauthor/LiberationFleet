export type BrandLogoVariant = 'lf' | 'crew';

export const BRAND_LOGO_ASSETS: Record<BrandLogoVariant, { hex: string; grey: string }> = {
  lf: {
    hex: 'assets/images/LF Logo hex.png',
    grey: 'assets/images/LF Logo grey.png'
  },
  crew: {
    hex: 'assets/images/Crew Logo hex.png',
    grey: 'assets/images/Crew Logo Grey.png'
  }
};
