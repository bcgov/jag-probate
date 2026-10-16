import { pinia } from '@/stores/pinia';
import { useWizardStateStore } from '@/stores/WizardStateStore';
import type { StepStatus, WizardNavState } from '@/types/applicationStep';
import { storeToRefs } from 'pinia';

/**
 * Thin wrapper over WizardStateStore, kept so existing call sites keep their
 * shape. Always binds to the app-wide Pinia instance, so it works outside
 * component setup and is shared by every sidebar instance (desktop + mobile)
 * and the window.wizard* bridge.
 */
export function useWizardState() {
  const store = useWizardStateStore(pinia);
  const { activeSubstep } = storeToRefs(store);
  return {
    navState: store.navState,
    statusMap: store.statusMap,
    disabledMap: store.disabledMap,
    activeSubstep,
  };
}

/** Seeds initial values (initial load / resume). Visibility overrides set by CHEFS forms are kept. */
export function initWizardState(
  initialStep: string,
  initialNavState: WizardNavState,
  initialStatusMap: Record<string, StepStatus>,
  initialDisabledMap: Record<string, boolean>
) {
  useWizardStateStore(pinia).init(
    initialStep,
    initialNavState,
    initialStatusMap,
    initialDisabledMap
  );
}
