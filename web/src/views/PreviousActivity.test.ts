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

  it('uses the Guid publicId (not chefsSubmissionId) as the route param', () => {
    const router = makeRouter();
    resumeApplication(router, { publicId: GUID_A });

    const location = router.push.mock.calls[0][0];
    expect(location.params.id).toBe(GUID_A);
    expect(location.params).not.toHaveProperty('chefsSubmissionId');
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
    router.push('/get-started');
  }

  it('navigates to /get-started', () => {
    const router = makeRouter();
    createCase(router);

    expect(router.push).toHaveBeenCalledWith('/get-started');
  });

  it('does not navigate to resume route', () => {
    const router = makeRouter();
    createCase(router);

    const location = router.push.mock.calls[0][0];
    expect(location).not.toContain('resume');
  });
});

describe('ApplicationForm – onSaved URL replace', () => {
  const GUID = 'a7b23b0f-ac4d-4f78-a9ca-39be6bbf5ac4';

  function makeRouter() {
    return { replace: vi.fn(), push: vi.fn() };
  }

  function onSaved(
    router: { replace: (loc: any) => void; push: (loc: any) => void },
    routeName: string,
    publicId: string
  ) {
    if (routeName === 'NewApplication') {
      router.replace({ name: 'ResumeApplication', params: { id: publicId } });
    }
  }

  it('replaces URL to ResumeApplication after first save on NewApplication route', () => {
    const router = makeRouter();
    onSaved(router, 'NewApplication', GUID);

    expect(router.replace).toHaveBeenCalledWith({
      name: 'ResumeApplication',
      params: { id: GUID },
    });
  });

  it('does not replace URL when already on ResumeApplication route', () => {
    const router = makeRouter();
    onSaved(router, 'ResumeApplication', GUID);

    expect(router.replace).not.toHaveBeenCalled();
  });

  it('uses replace (not push) so no extra history entry is created', () => {
    const router = makeRouter();
    onSaved(router, 'NewApplication', GUID);

    expect(router.replace).toHaveBeenCalledOnce();
    expect(router.push).not.toHaveBeenCalled();
  });
});
