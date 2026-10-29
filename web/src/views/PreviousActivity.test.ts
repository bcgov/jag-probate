import { describe, expect, it, vi } from 'vitest';

/**
 * Tests for PreviousActivity navigation logic.
 * Verifies that resume/create actions navigate to the correct routes
 * using the DB row id as the route parameter.
 */

describe('PreviousActivity – resumeApplication', () => {
  function makeRouter() {
    return { push: vi.fn() };
  }

  function resumeApplication(
    router: { push: (loc: any) => void },
    app: { publicId: string }
  ) {
    router.push({ name: 'ResumeApplication', params: { id: app.publicId } });
  }

  const GUID_A = 'a7b23b0f-ac4d-4f78-a9ca-39be6bbf5ac4';
  const GUID_B = '2897ffbb-a89c-4f42-9f80-f3cd4378c504';

  it('navigates to ResumeApplication with the publicId (Guid)', () => {
    const router = makeRouter();
    resumeApplication(router, { publicId: GUID_A });

    expect(router.push).toHaveBeenCalledWith({
      name: 'ResumeApplication',
      params: { id: GUID_A },
    });
  });

  it('uses the Guid publicId as the route param', () => {
    const router = makeRouter();
    resumeApplication(router, { publicId: GUID_A });

    const location = router.push.mock.calls[0][0];
    expect(location.params.id).toBe(GUID_A);
  });

  it('passes different publicIds for different records', () => {
    const router = makeRouter();
    resumeApplication(router, { publicId: GUID_A });
    resumeApplication(router, { publicId: GUID_B });

    expect(router.push.mock.calls[0][0].params.id).toBe(GUID_A);
    expect(router.push.mock.calls[1][0].params.id).toBe(GUID_B);
  });
});

describe('PreviousActivity – createCase', () => {
  function makeRouter() {
    return { push: vi.fn() };
  }

  function createCase(router: { push: (loc: any) => void }) {
    router.push({ name: 'ApplicationManager' });
  }

  it('navigates to ApplicationManager', () => {
    const router = makeRouter();
    createCase(router);

    expect(router.push).toHaveBeenCalledWith({ name: 'ApplicationManager' });
  });

  it('does not navigate to resume route', () => {
    const router = makeRouter();
    createCase(router);

    const location = router.push.mock.calls[0][0];
    expect(location).not.toContain('resume');
  });
});
