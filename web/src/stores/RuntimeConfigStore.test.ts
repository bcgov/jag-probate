import { useRuntimeConfigStore } from '@/stores';
import { describe, expect, it, vi } from 'vitest';

describe('RuntimeConfigStore', () => {
  it('loads runtime config from config.json', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          environment: 'test',
          bceidRegisterUrl: 'https://example.com/register',
        }),
      })
    );

    const runtimeConfigStore = useRuntimeConfigStore();
    await runtimeConfigStore.loadConfig();

    expect(runtimeConfigStore.environment).toBe('test');
    expect(runtimeConfigStore.environmentLabel).toBe('TEST');
    expect(runtimeConfigStore.bceidRegisterUrl).toBe(
      'https://example.com/register'
    );
    expect(runtimeConfigStore.isLoaded).toBe(true);
  });

  it('supports legacy uppercase config keys', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          ENVIRONMENT: 'Production',
          BCEID_REGISTER_URL: 'https://legacy.example/register',
        }),
      })
    );

    const runtimeConfigStore = useRuntimeConfigStore();
    await runtimeConfigStore.loadConfig();

    expect(runtimeConfigStore.environment).toBe('Production');
    expect(runtimeConfigStore.environmentLabel).toBe('PROD');
    expect(runtimeConfigStore.bceidRegisterUrl).toBe(
      'https://legacy.example/register'
    );
  });
});
