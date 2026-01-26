import { describe, it, expect } from 'vitest';
import router from './index';

describe('Router', () => {
  it('should be defined', () => {
    expect(router).toBeDefined();
  });

  it('should have routes configured', () => {
    expect(router.getRoutes().length).toBeGreaterThan(0);
  });

  it('should have home route', () => {
    const homeRoute = router.getRoutes().find((route) => route.name === 'Home');
    expect(homeRoute).toBeDefined();
    expect(homeRoute?.path).toBe('/');
  });

  it('should have cases route', () => {
    const casesRoute = router
      .getRoutes()
      .find((route) => route.name === 'Cases');
    expect(casesRoute).toBeDefined();
    expect(casesRoute?.path).toBe('/cases');
  });

  it('should have create case route', () => {
    const createRoute = router
      .getRoutes()
      .find((route) => route.name === 'CreateCase');
    expect(createRoute).toBeDefined();
    expect(createRoute?.path).toBe('/cases/new');
  });
});
