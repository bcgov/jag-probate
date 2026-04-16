import { defineStore } from 'pinia';
import { computed, ref } from 'vue';

const DEFAULT_ENVIRONMENT = 'dev';
const DEFAULT_BCEID_REGISTER_URL =
  'https://www.bceid.ca/register/basic/account_details.aspx?type=regular&serviceId=7493&eServiceType=all';

interface RuntimeConfigFile {
  environment?: string;
  bceidRegisterUrl?: string;
  ENVIRONMENT?: string;
  BCEID_REGISTER_URL?: string;
}

export const useRuntimeConfigStore = defineStore('RuntimeConfigStore', () => {
  const environment = ref<string>(DEFAULT_ENVIRONMENT);
  const bceidRegisterUrl = ref<string>(DEFAULT_BCEID_REGISTER_URL);
  const isLoaded = ref(false);

  async function loadConfig() {
    try {
      const response = await fetch(`${import.meta.env.BASE_URL}config.json`, {
        cache: 'no-store',
      });

      if (!response.ok) {
        throw new Error(`Failed to load runtime config: ${response.status}`);
      }

      const config = (await response.json()) as RuntimeConfigFile;

      environment.value =
        config.environment?.trim() ||
        config.ENVIRONMENT?.trim() ||
        DEFAULT_ENVIRONMENT;
      bceidRegisterUrl.value =
        config.bceidRegisterUrl?.trim() ||
        config.BCEID_REGISTER_URL?.trim() ||
        DEFAULT_BCEID_REGISTER_URL;
    } catch (error) {
      console.warn('Could not load runtime config, using defaults', error);
      environment.value = DEFAULT_ENVIRONMENT;
      bceidRegisterUrl.value = DEFAULT_BCEID_REGISTER_URL;
    } finally {
      isLoaded.value = true;
    }
  }

  const environmentLabel = computed(() => {
    const normalizedEnvironment = environment.value.toLowerCase();

    if (
      normalizedEnvironment === 'dev' ||
      normalizedEnvironment === 'development'
    ) {
      return 'DEV';
    }

    if (normalizedEnvironment === 'test') {
      return 'TEST';
    }

    if (
      normalizedEnvironment === 'prod' ||
      normalizedEnvironment === 'production'
    ) {
      return 'PROD';
    }

    return normalizedEnvironment.toUpperCase();
  });

  return {
    environment,
    bceidRegisterUrl,
    isLoaded,
    environmentLabel,
    loadConfig,
  };
});
