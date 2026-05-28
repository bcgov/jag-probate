import { describe, expect, it, vi, beforeEach } from 'vitest';
import ChefsService from './ChefsService';

describe('ChefsService – upsertSubmission', () => {
  let chefsService: ChefsService;
  let mockPost: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    mockPost = vi.fn().mockResolvedValue({ id: 99, chefsSubmissionId: 'abc' });

    const mockHttpService = {
      get: vi.fn(),
      post: mockPost,
      put: vi.fn(),
      delete: vi.fn(),
    } as any;

    chefsService = new ChefsService(mockHttpService);
  });

  it('calls POST api/Submissions/upsert with correct payload', async () => {
    const payload = {
      id: 42,
      chefsSubmissionId: 'chefs-id-123',
      createdBy: 'testuser@idir',
      applicantName: 'John Doe',
      status: 'draft',
      lastUpdatedAt: '2026-05-28T10:00:00Z',
      lastFiledAt: null,
    };

    await chefsService.upsertSubmission(payload);

    expect(mockPost).toHaveBeenCalledWith('api/Submissions/upsert', payload);
  });

  it('passes id field when provided (update existing)', async () => {
    const payload = {
      id: 7,
      chefsSubmissionId: 'new-chefs-id',
      createdBy: 'user@idir',
      applicantName: 'Jane',
      status: 'draft',
      lastUpdatedAt: '2026-05-28T11:00:00Z',
      lastFiledAt: null,
    };

    await chefsService.upsertSubmission(payload);

    const callArgs = mockPost.mock.calls[0][1];
    expect(callArgs.id).toBe(7);
  });

  it('passes id as undefined for new submissions (create)', async () => {
    const payload = {
      id: undefined,
      chefsSubmissionId: 'first-chefs-id',
      createdBy: 'user@idir',
      applicantName: '',
      status: 'draft',
      lastUpdatedAt: '2026-05-28T11:00:00Z',
      lastFiledAt: null,
    };

    await chefsService.upsertSubmission(payload);

    const callArgs = mockPost.mock.calls[0][1];
    expect(callArgs.id).toBeUndefined();
  });

  it('returns response with DB record id', async () => {
    const result = await chefsService.upsertSubmission({
      chefsSubmissionId: 'xyz',
      createdBy: 'u',
      applicantName: '',
      status: 'draft',
      lastUpdatedAt: new Date().toISOString(),
      lastFiledAt: null,
    });

    expect(result.id).toBe(99);
  });

  it('sends lastFiledAt as non-null for submitted forms', async () => {
    const now = '2026-05-28T12:00:00Z';
    const payload = {
      id: 10,
      chefsSubmissionId: 'submit-id',
      createdBy: 'user@idir',
      applicantName: 'Filed Person',
      status: 'submitted',
      lastUpdatedAt: now,
      lastFiledAt: now,
    };

    await chefsService.upsertSubmission(payload);

    const callArgs = mockPost.mock.calls[0][1];
    expect(callArgs.status).toBe('submitted');
    expect(callArgs.lastFiledAt).toBe(now);
  });
});
