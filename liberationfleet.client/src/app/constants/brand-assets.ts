export type BrandLogoVariant = 'lf' | 'fleet' | 'crew';

/**
 * Built-in brand assets. Crew/fleet marks are painted with theme tokens via CSS
 * mask in BrandLogoComponent; inactive/muted treatment uses the muted token.
 */
export const BRAND_LOGO_ASSETS: Record<BrandLogoVariant, string> = {
  lf: 'assets/images/LFleetLogo.png',
  fleet: 'assets/images/Fleeticon.png',
  crew: 'assets/images/CrewIcon.png'
};

/** Favicon / PWA / launcher source (Liberation Fleet logo). */
export const APP_ICON_ASSET = BRAND_LOGO_ASSETS.lf;
