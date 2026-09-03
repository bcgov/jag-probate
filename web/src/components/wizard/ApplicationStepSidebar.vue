<template>
  <div class="wiz-sidebar" :class="{ 'is-collapsed': collapsed }">
    <div class="wiz-header">
      <strong class="wiz-header-title">Application Steps</strong>
      <button
        class="wiz-toggle-btn"
        :aria-label="collapsed ? 'Expand sidebar' : 'Collapse sidebar'"
        :title="collapsed ? 'Expand sidebar' : 'Collapse sidebar'"
        @click="collapsed = !collapsed"
      >
        <font-awesome-icon
          :icon="collapsed ? 'chevron-right' : 'chevron-left'"
        />
      </button>
    </div>

    <div class="wiz-steps-container">
      <!-- Back to survey — not a real wizard step, just a shortcut back to step0 -->
      <button
        class="wiz-survey-btn"
        type="button"
        aria-label="Back to Survey"
        :title="collapsed ? 'Back to Survey' : undefined"
        @click="goToSurvey"
      >
        <font-awesome-icon icon="list-check" class="me-1" />
        <span class="wiz-survey-btn-label">Back to Survey</span>
      </button>

      <template v-for="step in steps" :key="step.key">
        <!-- Step header row -->
        <div
          class="wiz-step"
          :class="{
            'is-active': isStepActive(step.key),
            'is-disabled': isStepDisabled(step.key),
            'is-ghost': isStepHidden(step.key),
          }"
          @click="onStepClick(step)"
          @mouseenter="onStepMouseEnter(step, $event)"
          @mouseleave="onStepMouseLeave"
        >
          <div class="wiz-icon" :class="stepIconClass(step)">
            <font-awesome-icon
              v-if="stepIconName(step)"
              :icon="stepIconName(step)!"
            />
            <template v-else>{{ step.number }}</template>
          </div>
          <div class="wiz-step-text">
            <div class="wiz-label">STEP {{ step.number }}</div>
            <div class="wiz-title">{{ step.title }}</div>
          </div>
          <button
            v-if="!isStepActive(step.key)"
            class="wiz-expand-btn"
            :aria-label="
              expandedSteps[step.key] ? 'Collapse substeps' : 'Expand substeps'
            "
            @click.stop="toggleExpand(step.key)"
          >
            <font-awesome-icon
              icon="chevron-down"
              :class="{ 'is-rotated': isSubMenuOpen(step.key) }"
            />
          </button>
        </div>

        <!-- Substep list -->
        <div
          class="wiz-subitems"
          :style="{ display: isSubMenuOpen(step.key) ? 'block' : 'none' }"
        >
          <div
            v-for="substep in step.substeps"
            :key="substep.key"
            class="wiz-subitem"
            :class="{
              'is-active': activeSubstep === substep.key,
              'is-disabled': isSubstepDisabled(substep.key),
              'is-ghost': isSubstepHidden(substep.key),
            }"
            @click="onSubstepClick(substep.key)"
          >
            <span class="wiz-dot" :class="statusDotClass(substep.key)">
              <font-awesome-icon :icon="statusIconName(substep.key)" />
            </span>
            {{ substep.label }}
          </div>
        </div>
      </template>
    </div>
  </div>

  <!-- Collapsed hover flyout (teleported to body to escape overflow:hidden) -->
  <Teleport to="body">
    <div
      v-if="collapsed && hoveredStepData"
      class="wiz-flyout"
      :style="{ top: flyoutTop + 'px', left: flyoutLeft + 'px' }"
      @mouseenter="cancelHoverClear"
      @mouseleave="clearHover"
    >
      <div class="wiz-flyout-title">{{ hoveredStepData.title }}</div>
      <div
        v-for="substep in hoveredStepData.substeps"
        :key="substep.key"
        class="wiz-flyout-item"
        :class="{
          'is-active': activeSubstep === substep.key,
          'is-disabled': isSubstepDisabled(substep.key),
          'is-ghost': isSubstepHidden(substep.key),
        }"
        @click="
          onSubstepClick(substep.key);
          hoveredStepKey = null;
        "
      >
        <span class="wiz-dot" :class="statusDotClass(substep.key)">
          <font-awesome-icon :icon="statusIconName(substep.key)" />
        </span>
        {{ substep.label }}
      </div>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
  import {
    initWizardState,
    useWizardState,
  } from '@/composables/useWizardState';
  import type {
    StepStatus,
    WizardNavState,
    WizardStep,
  } from '@/types/applicationStep';
  import { computed, onMounted, onUnmounted, ref, watch } from 'vue';

  // ── Props ────────────────────────────────────────────────────────────────────

  interface Props {
    /** Full ordered step config. Adding/moving a step = edit wizardSteps.ts only. */
    steps: WizardStep[];
    /** Substep key to activate on mount. */
    initialStep?: string;
    initialNavState?: WizardNavState;
    initialStatusMap?: Record<string, StepStatus>;
    initialDisabledMap?: Record<string, boolean>;
    /**
     * When true (default), registers window.wizard* bridge functions on mount.
     * Set false for secondary instances (e.g. mobile drawer) so the primary
     * desktop sidebar always owns the bridge.
     */
    registerBridge?: boolean;
    /** Logical key of the pre-qualifying survey form. */
    surveyStepKey?: string;
  }

  const props = withDefaults(defineProps<Props>(), {
    initialStep: '',
    initialNavState: () => ({ hiddenSteps: {}, hiddenSubsteps: {} }),
    initialStatusMap: () => ({}),
    initialDisabledMap: () => ({}),
    registerBridge: true,
    surveyStepKey: '',
  });

  // ── Emits ────────────────────────────────────────────────────────────────────

  const emit = defineEmits<{
    /** Fired after every successful navigation. Parent shows the correct panel/form. */
    (e: 'navigate', stepKey: string): void;
    /** Fired when a substep's status changes (for parent bookkeeping). */
    (e: 'validation', stepKey: string, status: StepStatus | null): void;
  }>();

  // ── Reactive state (shared across all instances via composable) ──────────────

  const { navState, statusMap, disabledMap, activeSubstep } = useWizardState();

  const collapsed = ref(false);

  // ── Hover flyout (collapsed mode) ─────────────────────────────────────────────

  const hoveredStepKey = ref<string | null>(null);
  const flyoutTop = ref(0);
  const flyoutLeft = ref(0);
  let hoverClearTimer: ReturnType<typeof setTimeout> | null = null;

  const hoveredStepData = computed(() =>
    hoveredStepKey.value
      ? props.steps.find((s) => s.key === hoveredStepKey.value)
      : null
  );

  function onStepMouseEnter(step: WizardStep, event: MouseEvent) {
    if (!collapsed.value) return;
    cancelHoverClear();
    const el = event.currentTarget as HTMLElement;
    const rect = el.getBoundingClientRect();
    flyoutTop.value = rect.top;
    flyoutLeft.value = rect.right + 6;
    hoveredStepKey.value = step.key;
  }

  function onStepMouseLeave() {
    hoverClearTimer = setTimeout(() => {
      hoveredStepKey.value = null;
    }, 150);
  }

  function cancelHoverClear() {
    if (hoverClearTimer) {
      clearTimeout(hoverClearTimer);
      hoverClearTimer = null;
    }
  }

  function clearHover() {
    hoverClearTimer = setTimeout(() => {
      hoveredStepKey.value = null;
    }, 150);
  }

  /** Steps manually expanded via the chevron button (independent of active step). */
  const expandedSteps = ref<Record<string, boolean>>({});

  // ── Computed ─────────────────────────────────────────────────────────────────

  /** Flat ordered list of ALL substep keys derived from config. */
  const allSubstepKeys = computed<string[]>(() => {
    const result: string[] = [];
    for (const step of props.steps) {
      if (step.substeps.length === 0) {
        // Single-page step with no children — the step key IS the substep.
        result.push(step.key);
      } else {
        for (const sub of step.substeps) {
          result.push(sub.key);
        }
      }
    }
    return result;
  });

  // ── Helpers ──────────────────────────────────────────────────────────────────

  function getParentStepKey(substepKey: string): string | null {
    for (const step of props.steps) {
      if (step.substeps.some((s) => s.key === substepKey)) return step.key;
    }
    return null;
  }

  /** Returns visible substeps in config order. */
  function getVisibleSubstepsOrdered(): string[] {
    return allSubstepKeys.value.filter((key) => {
      if (navState.hiddenSubsteps[key]) return false;
      const parent = getParentStepKey(key);
      if (parent && navState.hiddenSteps[parent]) return false;
      return true;
    });
  }

  function getAdjacentVisibleSubstep(
    current: string,
    direction: 1 | -1
  ): string | null {
    const ordered = getVisibleSubstepsOrdered();
    const idx = ordered.indexOf(current);
    if (idx === -1) return null;
    const next = idx + direction;
    if (next < 0 || next >= ordered.length) return null;
    return ordered[next];
  }

  function getFallbackSubstep(preferred?: string): string | null {
    const ordered = getVisibleSubstepsOrdered();
    if (!ordered.length) return null;
    if (preferred) {
      const prefParent = getParentStepKey(preferred);
      const sameParent = prefParent
        ? ordered.find((k) => getParentStepKey(k) === prefParent)
        : null;
      if (sameParent) return sameParent;
    }
    if (activeSubstep.value && ordered.includes(activeSubstep.value)) {
      return activeSubstep.value;
    }
    return ordered[0];
  }

  // ── Status icon helpers ───────────────────────────────────────────────────────

  function statusDotClass(substepKey: string): string {
    const s = statusMap[substepKey];
    return s ? `status-${s}` : '';
  }

  function statusIconName(substepKey: string): string {
    const s = statusMap[substepKey];
    if (s === 'completed') return 'circle-check';
    if (s === 'incomplete') return 'circle-half-stroke';
    if (s === 'error') return 'circle-xmark';
    return 'circle';
  }

  function isStepCompleted(step: WizardStep): boolean {
    const relevantSubsteps = step.substeps
      .map((s) => s.key)
      .filter((k) => !isSubstepHidden(k) && !isSubstepDisabled(k));

    if (!relevantSubsteps.length) return false;

    return relevantSubsteps.every((k) => statusMap[k] === 'completed');
  }

  function stepIconName(step: WizardStep): string | null {
    if (isStepCompleted(step)) return 'check';
    return step.icon || null;
  }

  function stepIconClass(step: WizardStep): string | undefined {
    return isStepCompleted(step) ? 'wiz-icon-complete' : undefined;
  }

  // ── UI state helpers ──────────────────────────────────────────────────────────

  function isStepActive(stepKey: string): boolean {
    return getParentStepKey(activeSubstep.value) === stepKey;
  }

  function isSubMenuOpen(stepKey: string): boolean {
    if (navState.hiddenSteps[stepKey]) return false;
    return isStepActive(stepKey) || !!expandedSteps.value[stepKey];
  }

  function toggleExpand(stepKey: string) {
    expandedSteps.value[stepKey] = !expandedSteps.value[stepKey];
  }

  function isStepHidden(stepKey: string): boolean {
    return !!navState.hiddenSteps[stepKey];
  }

  function isSubstepHidden(substepKey: string): boolean {
    return !!navState.hiddenSubsteps[substepKey];
  }

  function isStepDisabled(stepKey: string): boolean {
    return !!disabledMap[stepKey];
  }

  function isSubstepDisabled(substepKey: string): boolean {
    return !!disabledMap[substepKey];
  }

  // ── Step status lifecycle ─────────────────────────────────────────────────────

  function ensureStartedStatus(substepKey: string) {
    if (!statusMap[substepKey]) {
      statusMap[substepKey] = 'incomplete';
      emit('validation', substepKey, 'incomplete');
    }
  }

  /**
   * Called when leaving a substep to update its sidebar status icon. If the
   * caller already knows the validity (e.g. the Next button just validated
   * before navigating — see navigateNext/attemptNext), pass it via
   * knownValidity: window.wizardValidateStep already updated the status as a
   * side effect in that case, so calling it again would be a second
   * checkValidity call in the same tick, which was found to break Form.io's
   * error rendering.
   */
  function finalizeSubstepStatus(substepKey: string, knownValidity?: boolean) {
    if (!statusMap[substepKey]) return;
    if (knownValidity !== undefined) return;
    window.wizardValidateStep?.(substepKey);
  }

  // ── Core navigation ───────────────────────────────────────────────────────────

  /**
   * Sets the active substep directly, without emitting 'navigate' or running
   * the leave-substep side effects (status finalization, forced save). Used
   * only to seed the initial/resumed position - real user-driven navigation
   * goes through updateSidebar() below, which the parent listens to.
   */
  function seedActiveSubstep(stepKey: string) {
    const ordered = getVisibleSubstepsOrdered();
    const resolved = ordered.includes(stepKey)
      ? stepKey
      : (getFallbackSubstep(stepKey) ?? ordered[0] ?? '');
    if (!resolved) return;
    activeSubstep.value = resolved;
    ensureStartedStatus(resolved);
  }

  function updateSidebar(stepKey: string, leavingValidity?: boolean) {
    const ordered = getVisibleSubstepsOrdered();

    let resolved = stepKey;
    if (!ordered.includes(resolved)) {
      resolved =
        getAdjacentVisibleSubstep(activeSubstep.value, -1) ??
        getAdjacentVisibleSubstep(activeSubstep.value, 1) ??
        getFallbackSubstep(activeSubstep.value) ??
        activeSubstep.value;
    }
    if (!resolved) return;

    if (activeSubstep.value && activeSubstep.value !== resolved) {
      finalizeSubstepStatus(activeSubstep.value, leavingValidity);
      // Save the departing step's data immediately (bypassing the auto-save
      // debounce) — covers Next, Previous, and direct sidebar clicks, since
      // they all funnel through this single navigation choke point.
      void window
        .wizardSaveStep?.(
          activeSubstep.value,
          getParentStepKey(activeSubstep.value) ?? activeSubstep.value
        )
        .catch(() => undefined);
    }

    activeSubstep.value = resolved;
    ensureStartedStatus(resolved);
    emit('navigate', resolved);
  }

  // ── Click handlers ────────────────────────────────────────────────────────────

  function onSubstepClick(substepKey: string) {
    if (isSubstepDisabled(substepKey) || isSubstepHidden(substepKey)) return;
    updateSidebar(substepKey);
  }

  function onStepClick(step: WizardStep) {
    const stepKey = step.key;
    if (isStepDisabled(stepKey) || isStepHidden(stepKey)) return;
    updateSidebar(step.defaultSubstep);
  }

  /**
   * Back to the step0 pre-qualifying survey. step0 isn't part of the wizard's
   * own step/substep bookkeeping (no status, no disabled/hidden map, no
   * validation), so this bypasses updateSidebar entirely and just tells the
   * parent (ApplicationManager) to show it directly.
   */
  function goToSurvey() {
    if (!props.surveyStepKey) return;
    emit('navigate', props.surveyStepKey);
  }

  // ── Public API ────────────────────────────────────────────────────────────────

  // Pure navigation — trusts that the caller (the CHEFS-native Next button script,
  // or attemptNext() below) has already validated the current substep. Never calls
  // window.wizardValidateStep itself: calling Form.io's checkValidity twice for the
  // same click was found to break its error rendering.
  function navigateNext() {
    const current = activeSubstep.value;
    const next = getAdjacentVisibleSubstep(current, 1);
    if (next) updateSidebar(next, true);
  }

  /**
   * Validate-then-navigate entry point for the Vue "Next >" button, so it behaves
   * identically to the CHEFS-native Next button (which validates via its own
   * schema script before calling window.wizardNavigateNext). Only the Next button
   * blocks navigation on invalid input. window.wizardValidateStep sets the status
   * icon itself as a side effect (see StepFormViewer.vue).
   *
   * Also unlocks (makes clickable) the immediately-following substep, matching
   * the progressive unlock previously done by the CHEFS-embedded Next buttons
   * (e.g. step3's nextS3s/nextS3c calling window.wizardSetStepClickable) — now
   * that in-form nav buttons are being removed, this is the only place that
   * still does it.
   */
  function attemptNext() {
    const current = activeSubstep.value;
    const isValid = window.wizardValidateStep
      ? window.wizardValidateStep(current)
      : true;
    const next = getAdjacentVisibleSubstep(current, 1);
    if (next) setStepClickable(next, true);
    if (!isValid) return;
    navigateNext();
  }

  function navigatePrevious() {
    const prev = getAdjacentVisibleSubstep(activeSubstep.value, -1);
    if (prev) updateSidebar(prev);
  }

  function setStepStatus(substepKey: string, status: StepStatus | null) {
    if (status) {
      statusMap[substepKey] = status;
    } else {
      delete statusMap[substepKey];
    }
    emit('validation', substepKey, status);
  }

  function setAllStatuses(map: Record<string, StepStatus>) {
    Object.keys(statusMap).forEach((k) => delete statusMap[k]);
    Object.assign(statusMap, map);
  }

  function setStepVisibility(stepKey: string, isVisible: boolean) {
    if (isVisible) {
      delete navState.hiddenSteps[stepKey];
    } else {
      navState.hiddenSteps[stepKey] = true;
    }
    const parentOfCurrent = getParentStepKey(activeSubstep.value);
    if (parentOfCurrent === stepKey && !isVisible) {
      const fallback =
        getFallbackSubstep(activeSubstep.value) ??
        getVisibleSubstepsOrdered()[0] ??
        '';
      updateSidebar(fallback);
    }
  }

  function setSubstepVisibility(substepKey: string, isVisible: boolean) {
    if (isVisible) {
      delete navState.hiddenSubsteps[substepKey];
    } else {
      navState.hiddenSubsteps[substepKey] = true;
    }
    if (activeSubstep.value === substepKey && !isVisible) {
      const fallback =
        getFallbackSubstep(substepKey) ?? getVisibleSubstepsOrdered()[0] ?? '';
      updateSidebar(fallback);
    }
  }

  function setAllVisibility(nextState: WizardNavState) {
    Object.keys(navState.hiddenSteps).forEach(
      (k) => delete navState.hiddenSteps[k]
    );
    Object.assign(navState.hiddenSteps, nextState.hiddenSteps ?? {});
    Object.keys(navState.hiddenSubsteps).forEach(
      (k) => delete navState.hiddenSubsteps[k]
    );
    Object.assign(navState.hiddenSubsteps, nextState.hiddenSubsteps ?? {});
    const ordered = getVisibleSubstepsOrdered();
    if (!ordered.includes(activeSubstep.value)) {
      const fallback =
        getFallbackSubstep(activeSubstep.value) ?? ordered[0] ?? '';
      updateSidebar(fallback);
    }
  }

  function setStepClickable(stepKey: string, isClickable: boolean) {
    if (isClickable) {
      delete disabledMap[stepKey];
    } else {
      disabledMap[stepKey] = true;
    }
  }

  function isActiveBridgeSource(sourceStepKey?: string): boolean {
    if (!sourceStepKey) return true;
    const activeRoot =
      getParentStepKey(activeSubstep.value) ?? activeSubstep.value;
    const sourceRoot = getParentStepKey(sourceStepKey) ?? sourceStepKey;
    if (sourceRoot === activeRoot) return true;
    console.warn(
      `[ApplicationStepSidebar] Ignored bridge call from inactive step "${sourceRoot}".`
    );
    return false;
  }

  // ── Computed exposed values ───────────────────────────────────────────────────

  const hasPrev = computed(
    () => getAdjacentVisibleSubstep(activeSubstep.value, -1) !== null
  );

  const hasNext = computed(
    () => getAdjacentVisibleSubstep(activeSubstep.value, 1) !== null
  );

  // True only on the literal final substep (last substep of the last step in
  // config order) — NOT simply "no visible next substep", which can be true
  // temporarily whenever later steps/substeps haven't been unlocked yet. Using
  // !hasNext here previously showed the Submit button prematurely (e.g. on
  // step6 while step7 was still hidden).
  const isLastStep = computed(() => {
    const all = allSubstepKeys.value;
    return all.length > 0 && activeSubstep.value === all[all.length - 1];
  });

  // ── Window function bridge ────────────────────────────────────────────────────
  // Preserves backward compat with CHEFS form JS that calls window.wizard* functions.
  // Remove these once CHEFS forms no longer contain wizard/nav JS (Step 3).

  function registerWindowFunctions() {
    window.wizardUpdateSidebar = (stepKey, sourceStepKey) => {
      if (isActiveBridgeSource(sourceStepKey)) updateSidebar(stepKey);
    };
    window.wizardNavigateNext = (sourceStepKey) => {
      if (isActiveBridgeSource(sourceStepKey)) navigateNext();
    };
    window.wizardNavigatePrevious = (sourceStepKey) => {
      if (isActiveBridgeSource(sourceStepKey)) navigatePrevious();
    };
    window.wizardSetStepStatus = (substepKey, status, sourceStepKey) => {
      if (isActiveBridgeSource(sourceStepKey))
        setStepStatus(substepKey, status);
    };
    window.wizardSetAllStatuses = (map, sourceStepKey) => {
      if (isActiveBridgeSource(sourceStepKey)) setAllStatuses(map);
    };
    window.wizardSetStepVisibility = (stepKey, isVisible, sourceStepKey) => {
      if (isActiveBridgeSource(sourceStepKey)) {
        setStepVisibility(stepKey, isVisible);
      }
    };
    window.wizardSetSubstepVisibility = (
      substepKey,
      isVisible,
      sourceStepKey
    ) => {
      if (isActiveBridgeSource(sourceStepKey)) {
        setSubstepVisibility(substepKey, isVisible);
      }
    };
    window.wizardSetAllVisibility = (nextState, sourceStepKey) => {
      if (isActiveBridgeSource(sourceStepKey)) setAllVisibility(nextState);
    };
    window.wizardSetStepClickable = (stepKey, isClickable, sourceStepKey) => {
      if (isActiveBridgeSource(sourceStepKey)) {
        setStepClickable(stepKey, isClickable);
      }
    };
    // Note: window.wizardValidateStep is owned by StepFormViewer (it has direct
    // access to the Form.io instance) — not assigned here.
  }

  function unregisterWindowFunctions() {
    const fns: (keyof Window)[] = [
      'wizardUpdateSidebar',
      'wizardNavigateNext',
      'wizardNavigatePrevious',
      'wizardSetStepStatus',
      'wizardSetAllStatuses',
      'wizardSetStepVisibility',
      'wizardSetSubstepVisibility',
      'wizardSetAllVisibility',
      'wizardSetStepClickable',
    ];
    fns.forEach((fn) => delete window[fn]);
  }

  // Register before mounted hooks run so CHEFS schema scripts can call the
  // bridge while the first form is initializing.
  if (props.registerBridge) {
    initWizardState(
      props.initialStep,
      props.initialNavState,
      props.initialStatusMap,
      props.initialDisabledMap
    );
    registerWindowFunctions();
  }

  let resizeCleanup: (() => void) | null = null;
  let hasAppliedInitialStep = false;

  function applyInitialStep(stepKey: string) {
    seedActiveSubstep(stepKey);
  }

  onMounted(() => {
    // Keep collapsed state in sync with viewport — reset to expanded on small screens
    // so Vue state never contradicts the CSS override.
    function syncCollapsedToViewport() {
      if (window.innerWidth <= 768 && collapsed.value) {
        collapsed.value = false;
      }
    }
    window.addEventListener('resize', syncCollapsedToViewport);
    resizeCleanup = () =>
      window.removeEventListener('resize', syncCollapsedToViewport);
    syncCollapsedToViewport();

    // Only the primary instance seeds shared state. The hidden mobile sidebar
    // mounts when the form becomes ready and must not overwrite resume state.
    if (props.registerBridge) {
      applyInitialStep(props.initialStep);
      hasAppliedInitialStep = true;
    }
  });

  // The parent resolves the resumed step/substep from an independent network
  // fetch that can resolve after this component has already mounted and
  // seeded itself with the default first step (a race, not a one-time value).
  // Re-seed once if the resolved initial step changes after mount.
  watch(
    () => props.initialStep,
    (newVal, oldVal) => {
      if (!hasAppliedInitialStep || !newVal || newVal === oldVal) return;
      if (props.registerBridge) {
        initWizardState(
          newVal,
          props.initialNavState,
          props.initialStatusMap,
          props.initialDisabledMap
        );
      }
      applyInitialStep(newVal);
    }
  );

  onUnmounted(() => {
    if (props.registerBridge) unregisterWindowFunctions();
    resizeCleanup?.();
    if (hoverClearTimer) clearTimeout(hoverClearTimer);
  });

  // ── Expose for parent ref access ──────────────────────────────────────────────

  defineExpose({
    activeSubstep,
    hasPrev,
    hasNext,
    isLastStep,
    navigateNext,
    attemptNext,
    navigatePrevious,
    updateSidebar,
    setStepStatus,
    setAllStatuses,
    setStepVisibility,
    setSubstepVisibility,
    setAllVisibility,
    setStepClickable,
    collapsed,
  });
</script>

<style scoped>
  .wiz-sidebar {
    position: fixed;
    left: 0;
    top: 62px;
    z-index: 1;
    height: calc(100vh - 62px);
    width: 320px;
    min-width: 320px;
    background: #f0f4f8;
    font-family: BCSans, 'Noto Sans', Verdana, Arial, sans-serif;
    border-right: 1px solid #dee2e6;
    border-radius: 0 8px 8px 0;
    box-shadow: 2px 0 8px rgba(0, 0, 0, 0.06);
    display: flex;
    flex-direction: column;
    overflow: hidden;
    transition:
      width 0.25s ease,
      min-width 0.25s ease;
  }

  .wiz-sidebar.is-collapsed {
    width: 62px;
    min-width: 62px;
  }

  .wiz-header {
    padding: 20px 22px;
    border-bottom: 1px solid #dee2e6;
    background: #e8eef5;
    flex-shrink: 0;
    font-size: 16px;
    color: #234075;
    letter-spacing: 0.01em;
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
    overflow: hidden;
  }

  .wiz-header-title {
    white-space: nowrap;
    overflow: hidden;
    transition:
      opacity 0.2s ease,
      width 0.25s ease;
  }

  .wiz-sidebar.is-collapsed .wiz-header {
    justify-content: center;
    padding: 14px 0;
  }

  .wiz-sidebar.is-collapsed .wiz-header-title {
    display: none;
  }

  .wiz-toggle-btn {
    background: none;
    border: none;
    cursor: pointer;
    color: #234075;
    padding: 2px 4px;
    flex-shrink: 0;
    display: flex;
    align-items: center;
    opacity: 0.7;
    transition: opacity 0.15s;
  }

  .wiz-toggle-btn:hover {
    opacity: 1;
  }

  .wiz-steps-container {
    flex: 1;
    overflow-y: auto;
    overflow-x: hidden;
    padding: 8px 0 100px;
  }

  .wiz-survey-btn {
    display: flex;
    align-items: center;
    width: auto;
    margin: 12px;
    padding: 8px 12px;
    background: none;
    border: 1px solid #234075;
    border-radius: 4px;
    color: #234075;
    font-size: 13px;
    font-weight: 500;
    cursor: pointer;
    transition: background-color 0.15s;
  }

  .wiz-survey-btn:hover {
    background-color: rgba(35, 64, 117, 0.08);
  }

  .wiz-sidebar.is-collapsed .wiz-survey-btn {
    width: 46px;
    height: 46px;
    margin: 8px auto 12px;
    padding: 0;
    justify-content: center;
  }

  .wiz-sidebar.is-collapsed .wiz-survey-btn .me-1 {
    margin: 0 !important;
  }

  .wiz-sidebar.is-collapsed .wiz-survey-btn-label {
    display: none;
  }

  .wiz-sidebar.is-collapsed .wiz-step-text,
  .wiz-sidebar.is-collapsed .wiz-subitems,
  .wiz-sidebar.is-collapsed .wiz-expand-btn {
    display: none !important;
  }

  .wiz-sidebar.is-collapsed .wiz-step {
    justify-content: center;
    align-items: center;
    width: 100%;
    height: 62px;
    min-height: 62px;
    margin-left: 0;
    padding: 0;
    gap: 0;
    border-left-width: 0;
    border-left-color: transparent !important;
  }

  .wiz-sidebar.is-collapsed .wiz-icon {
    margin: 0;
  }

  /* Keep active highlight full-width in collapsed mode */
  .wiz-sidebar.is-collapsed .wiz-step.is-active {
    background: #e8a825;
  }

  .wiz-step {
    display: flex;
    align-items: flex-start;
    gap: 12px;
    padding: 14px 18px;
    margin-left: 0;
    border-left: 4px solid transparent;
    cursor: pointer;
    transition:
      background-color 0.15s ease,
      color 0.15s ease,
      border-left-color 0.15s ease;
  }

  .wiz-step:hover:not(.is-active):not(.is-disabled):not(.is-ghost) {
    background: rgba(35, 64, 117, 0.06);
    border-left-color: rgba(35, 64, 117, 0.35);
  }

  .wiz-step.is-active {
    background: #e8a825;
    border-left-color: #e8a825;
    color: #fff;
  }

  .wiz-step.is-active .wiz-icon {
    background: #fff;
    color: #234075;
  }

  .wiz-step.is-disabled {
    opacity: 0.5;
    cursor: not-allowed;
    pointer-events: none;
  }

  /* Ghosted (not-yet-reachable) steps: shown at low opacity instead of being
     hidden entirely, so users can see how many steps remain. Not clickable. */
  .wiz-step.is-ghost {
    opacity: 0.3;
    cursor: not-allowed;
    pointer-events: none;
  }

  .wiz-step-text {
    flex: 1;
  }

  .wiz-expand-btn {
    background: none;
    border: none;
    padding: 4px 6px;
    cursor: pointer;
    color: inherit;
    opacity: 0.6;
    flex-shrink: 0;
    display: flex;
    align-items: center;
    margin-left: auto;
    transition: opacity 0.15s;
  }

  .wiz-expand-btn:hover {
    opacity: 1;
  }

  .wiz-expand-btn svg {
    transition: transform 0.2s ease;
  }

  .wiz-expand-btn .is-rotated {
    transform: rotate(180deg);
  }

  .wiz-icon {
    width: 38px;
    height: 38px;
    border-radius: 50%;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    font-weight: 700;
    background: #fff;
    color: #555;
    flex: 0 0 38px;
  }

  .wiz-icon.wiz-icon-complete,
  .wiz-step.is-active .wiz-icon.wiz-icon-complete {
    background: #234075;
    border: none;
    color: #fff;
  }

  .wiz-label {
    font-size: 11px;
    font-weight: 700;
    letter-spacing: 0.08em;
    color: #4b5563;
  }

  .wiz-title {
    font-size: 15px;
    font-weight: 700;
    color: inherit;
    line-height: 1.3;
  }

  .wiz-subitems {
    display: none;
    padding: 0 0 8px 62px;
  }

  .wiz-subitem {
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 8px 12px 8px 0;
    font-size: 14px;
    color: #555;
    cursor: pointer;
    transition:
      background-color 0.15s ease,
      color 0.15s ease,
      transform 0.15s ease;
  }

  .wiz-subitem:hover:not(.is-active):not(.is-disabled):not(.is-ghost) {
    background: rgba(35, 64, 117, 0.06);
    color: #234075;
    transform: translateX(2px);
  }

  .wiz-subitem.is-active {
    color: #e8a825;
    font-weight: 700;
  }

  .wiz-subitem.is-disabled {
    opacity: 0.5;
    cursor: not-allowed;
    pointer-events: none;
  }

  .wiz-subitem.is-ghost {
    opacity: 0.3;
    cursor: not-allowed;
    pointer-events: none;
  }

  .wiz-dot {
    width: 18px;
    min-width: 18px;
    height: 18px;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    color: #8c949c;
    font-size: 16px;
    line-height: 1;
  }

  .wiz-dot.status-completed {
    color: #234075;
  }

  .wiz-dot.status-incomplete {
    color: #f2a900;
  }

  .wiz-dot.status-error {
    color: #d8292f;
  }

  /* Responsive: stack on mobile, always expanded */
  @media (max-width: 768px) {
    .wiz-sidebar {
      position: relative !important;
      top: 0 !important;
      height: auto !important;
      width: 100% !important;
      border-radius: 0 !important;
    }

    .wiz-toggle-btn {
      display: none !important;
    }

    /* Force expanded appearance regardless of collapsed state */
    .wiz-sidebar.is-collapsed {
      width: 100% !important;
      min-width: 0 !important;
    }

    .wiz-sidebar.is-collapsed .wiz-header {
      justify-content: space-between !important;
      padding: 20px 22px !important;
    }

    .wiz-sidebar.is-collapsed .wiz-header-title {
      display: block !important;
    }

    .wiz-sidebar.is-collapsed .wiz-step-text,
    .wiz-sidebar.is-collapsed .wiz-expand-btn {
      display: flex !important;
    }

    .wiz-sidebar.is-collapsed .wiz-step {
      justify-content: flex-start !important;
      align-items: flex-start !important;
      padding: 14px 18px !important;
      gap: 12px !important;
      margin-left: 8px !important;
      border-left: 4px solid transparent !important;
    }

    .wiz-sidebar.is-collapsed .wiz-step.is-active {
      border-left-color: #e8a825 !important;
    }
  }

  /* ── Collapsed hover flyout ──────────────────────────────────────────────── */

  .wiz-flyout {
    position: fixed;
    z-index: 9999;
    background: #fff;
    border: 1px solid #dee2e6;
    border-radius: 8px;
    box-shadow: 4px 4px 16px rgba(0, 0, 0, 0.12);
    min-width: 220px;
    max-width: 300px;
    padding: 8px 0;
    font-family: BCSans, 'Noto Sans', Verdana, Arial, sans-serif;
  }

  .wiz-flyout-title {
    font-size: 12px;
    font-weight: 700;
    letter-spacing: 0.07em;
    color: #8c949c;
    text-transform: uppercase;
    padding: 4px 16px 8px;
    border-bottom: 1px solid #f0f0f0;
    margin-bottom: 4px;
  }

  .wiz-flyout-item {
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 8px 16px;
    font-size: 14px;
    color: #555;
    cursor: pointer;
    transition: background 0.15s;
  }

  .wiz-flyout-item:hover {
    background: #f5f7fa;
  }

  .wiz-flyout-item.is-active {
    color: #e8a825;
    font-weight: 700;
  }

  .wiz-flyout-item.is-disabled {
    opacity: 0.5;
    cursor: not-allowed;
    pointer-events: none;
  }

  .wiz-flyout-item.is-ghost {
    opacity: 0.3;
    cursor: not-allowed;
    pointer-events: none;
  }
</style>
