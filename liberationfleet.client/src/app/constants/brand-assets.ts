export type BrandLogoVariant = 'lf' | 'fleet' | 'crew';

/** Color brand assets. Inactive/grey treatment is applied in CSS (filter: grayscale). */
export const BRAND_LOGO_ASSETS: Record<BrandLogoVariant, string> = {
  lf: 'assets/images/LFleetLogo.png',
  fleet: 'assets/images/Fleeticon.png',
  crew: 'assets/images/CrewIcon.png'
};

/** Favicon / PWA / launcher source (Liberation Fleet logo). */
export const APP_ICON_ASSET = BRAND_LOGO_ASSETS.lf;
