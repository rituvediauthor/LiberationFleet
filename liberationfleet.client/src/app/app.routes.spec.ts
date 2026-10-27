import { Type } from '@angular/core';
import { routes } from './app.routes';
import { ProductLandingComponent } from './pages/product-landing/product-landing.component';
import { SignInComponent } from './pages/sign-in/sign-in.component';
import { CrewHomeComponent } from './pages/crew-home/crew-home.component';
import { DonateComponent } from './pages/donate/donate.component';
import { authGuard } from './guards/auth.guard';
import { guestGuard } from './guards/guest.guard';

async function loadRouteComponent(path: string): Promise<Type<unknown>> {
  const route = routes.find(r => r.path === path);
  expect(route?.loadComponent).toEqual(jasmine.any(Function));
  expect(route?.component).toBeUndefined();
  return route!.loadComponent!() as Promise<Type<unknown>>;
}

describe('app.routes', () => {
  it('should define landing route at root', async () => {
    const route = routes.find(r => r.path === '');
    expect(route?.canActivate).toContain(guestGuard);
    expect(await loadRouteComponent('')).toBe(ProductLandingComponent);
  });

  it('should define auth-related routes', async () => {
    expect(routes.find(r => r.path === 'sign-in')?.canActivate).toContain(guestGuard);
    expect(await loadRouteComponent('sign-in')).toBe(SignInComponent);
    expect(routes.find(r => r.path === 'sign-up')?.loadComponent).toEqual(jasmine.any(Function));
    expect(routes.find(r => r.path === 'forgot-password')?.loadComponent).toEqual(jasmine.any(Function));
    expect(routes.find(r => r.path === 'reset-password')?.loadComponent).toEqual(jasmine.any(Function));
    expect(routes.find(r => r.path === 'sign-in-success')).toBeUndefined();
    expect(routes.find(r => r.path === 'how-to')?.canActivate).toBeUndefined();
    expect(routes.find(r => r.path === 'how-to/:topicId')?.canActivate).toBeUndefined();
  });

  it('should define authenticated app routes with authGuard', async () => {
    const appRoutes = ['app/crew', 'app/friends', 'app/profile', 'app/profile/user', 'app/profile/activity', 'app/profile/preferences', 'app/crew/find', 'app/crew/create', 'app/crew/join'];

    for (const path of appRoutes) {
      const route = routes.find(r => r.path === path);
      expect(route?.canActivate).toContain(authGuard);
      expect(route?.loadComponent).toEqual(jasmine.any(Function));
    }

    expect(await loadRouteComponent('app/crew')).toBe(CrewHomeComponent);
    expect(routes.find(r => r.path === 'app/crew')?.pathMatch).toBe('full');
    expect(routes.find(r => r.path === 'app/crew/chats')?.pathMatch).toBe('full');
    expect(routes.find(r => r.path === 'app/profile/preferences/placeholder')).toBeUndefined();
  });

  it('should allow guest access to donate without authGuard', async () => {
    const route = routes.find(r => r.path === 'app/donate');
    expect(route?.canActivate).toBeUndefined();
    expect(await loadRouteComponent('app/donate')).toBe(DonateComponent);
  });

  it('should expose public privacy and terms routes without authGuard', () => {
    for (const path of ['privacy', 'terms', 'community-standards']) {
      const route = routes.find(r => r.path === path);
      expect(route?.canActivate).toBeUndefined();
      expect(route?.loadComponent).toEqual(jasmine.any(Function));
      expect(route?.data?.['assetPath']).toBeTruthy();
    }
  });

  it('should lazy-load page components instead of eager imports', () => {
    const pageRoutes = routes.filter(route => !!route.path && route.path !== '**' && !route.redirectTo);
    expect(pageRoutes.length).toBeGreaterThan(50);
    for (const route of pageRoutes) {
      expect(route.component).toBeUndefined();
      expect(route.loadComponent).toEqual(jasmine.any(Function));
    }
  });

  it('should redirect unknown paths to root', () => {
    const wildcard = routes.find(r => r.path === '**');
    expect(wildcard?.redirectTo).toBe('');
  });
});
