import type { UserInfo } from '@/types';
import { defineStore } from 'pinia';
import { computed, ref } from 'vue';

/**
 * Pinia store for authentication state.
 * Follows jasper's CommonStore pattern for user info management.
 */
export const useAuthStore = defineStore('AuthStore', () => {
  const userInfo = ref<UserInfo | null>(null);
  const isLoading = ref(false);

  const isAuthenticated = computed(
    () => userInfo.value?.isAuthenticated ?? false
  );

  const userName = computed(() => userInfo.value?.name ?? null);

  function setUserInfo(info: UserInfo | null) {
    userInfo.value = info;
  }

  function setLoading(loading: boolean) {
    isLoading.value = loading;
  }

  function clearUserInfo() {
    userInfo.value = null;
  }

  return {
    userInfo,
    isLoading,
    isAuthenticated,
    userName,
    setUserInfo,
    setLoading,
    clearUserInfo,
  };
});
