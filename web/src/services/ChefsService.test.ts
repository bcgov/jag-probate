import { describe, expect, it, vi, beforeEach } from 'vitest';
import ChefsService from './ChefsService';

describe('ChefsService – upsertSubmission', () => {
  let chefsService: ChefsService;
  let mockPost: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    mockPost = vi.fn().mockResolvedValue({
      publicId: 'a7b23b0f-ac4d-4f78-a9ca-39be6bbf5ac4',
      chefsSubmissionId: 'abc',
    });

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
      publicId: 'a7b23b0f-ac4d-4f78-a9ca-39be6bbf5ac4',
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

  it('passes publicId when provided (update existing)', async () => {
    const guid = 'b1c23d4e-0000-0000-0000-000000000007';
    const payload = {
      publicId: guid,
      chefsSubmissionId: 'new-chefs-id',
      createdBy: 'user@idir',
      applicantName: 'Jane',
      status: 'draft',
      lastUpdatedAt: '2026-05-28T11:00:00Z',
      lastFiledAt: null,
    };

    await chefsService.upsertSubmission(payload);

    const callArgs = mockPost.mock.calls[0][1];
    expect(callArgs.publicId).toBe(guid);
  });

  it('passes publicId as undefined for new submissions (create)', async () => {
    const payload = {
      publicId: undefined,
      chefsSubmissionId: 'first-chefs-id',
      createdBy: 'user@idir',
      applicantName: '',
      status: 'draft',
      lastUpdatedAt: '2026-05-28T11:00:00Z',
      lastFiledAt: null,
    };

    await chefsService.upsertSubmission(payload);

    const callArgs = mockPost.mock.calls[0][1];
    expect(callArgs.publicId).toBeUndefined();
  });

  it('returns response with publicId (Guid)', async () => {
    const result = await chefsService.upsertSubmission({
      chefsSubmissionId: 'xyz',
      createdBy: 'u',
      applicantName: '',
      status: 'draft',
      lastUpdatedAt: new Date().toISOString(),
      lastFiledAt: null,
    });

    expect(result.publicId).toBe('a7b23b0f-ac4d-4f78-a9ca-39be6bbf5ac4');
  });

  it('sends lastFiledAt as non-null for submitted forms', async () => {
    const now = '2026-05-28T12:00:00Z';
    const payload = {
      publicId: 'c0ffee00-0000-0000-0000-000000000010',
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
