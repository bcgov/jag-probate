import { createPinia, setActivePinia } from 'pinia';
import { beforeEach } from 'vitest';

// Activate a fresh Pinia instance before each test so that stores
// (useAuthStore, useLayoutStore, etc.) can be used without mounting
// a full Vue application.
beforeEach(() => {
  setActivePinia(createPinia());
});
