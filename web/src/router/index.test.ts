import { describe, expect, it } from 'vitest';
import router from './index';

describe('Router', () => {
  it('should be defined', () => {
    expect(router).toBeDefined();
  });

  it('should have routes configured', () => {
    expect(router.getRoutes().length).toBeGreaterThan(0);
  });

  it('should have LandingPage route', () => {
    const landingRoute = router
      .getRoutes()
      .find((route) => route.name === 'LandingPage');
    expect(landingRoute).toBeDefined();
    expect(landingRoute?.path).toBe('/');
  });

  it('should have About route', () => {
    const aboutRoute = router
      .getRoutes()
      .find((route) => route.name === 'About');
    expect(aboutRoute).toBeDefined();
    expect(aboutRoute?.path).toBe('/about');
  });

  it('should have RepresentSomeoneWhoDied route with correct meta', () => {
    const representRoute = router
      .getRoutes()
      .find((route) => route.name === 'RepresentSomeoneWhoDied');
    expect(representRoute).toBeDefined();
    expect(representRoute?.path).toBe('/represent-someone-who-died');
    expect(representRoute?.meta.navHeader).toBe('Probate Application');
  });
});
