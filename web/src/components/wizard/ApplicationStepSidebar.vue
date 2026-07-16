<template>
  <div
    id="wizardSidebar"
    class="wiz-sidebar"
    :class="{ 'is-collapsed': collapsed }"
  >
    <div class="wiz-header">
      <strong class="wiz-header-title">Application Steps</strong>
      <button
        class="wiz-toggle-btn"
        :aria-label="collapsed ? 'Expand sidebar' : 'Collapse sidebar'"
        @click="collapsed = !collapsed"
      >
        <font-awesome-icon :icon="collapsed ? 'circle-arrow-right' : 'circle-arrow-left'" />
      </button>
    </div>

    <div class="wiz-steps-container">
      <template
        v-for="step in steps"
        :key="step.key"
      >
        <!-- Step header row -->
        <div
          class="wiz-step"
          :class="{
            'is-active': isStepActive(step.key),
            'is-disabled': isStepDisabled(step.key),
          }"
          :style="{ display: isStepHidden(step.key) ? 'none' : 'flex' }"
          @click="onStepClick(step)"
          @mouseenter="onStepMouseEnter(step, $event)"
          @mouseleave="onStepMouseLeave"
        >
          <div class="wiz-icon">
            <font-awesome-icon
              v-if="step.icon"
              :icon="step.icon"
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
            :aria-label="expandedSteps[step.key] ? 'Collapse substeps' : 'Expand substeps'"
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
            }"
            :style="{ display: isSubstepHidden(substep.key) ? 'none' : 'flex' }"
            @click="onSubstepClick(substep.key)"
          >
            <span
              class="wiz-dot"
              :class="statusDotClass(substep.key)"
            >
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
        }"
        :style="{ display: isSubstepHidden(substep.key) ? 'none' : 'flex' }"
        @click="onSubstepClick(substep.key); hoveredStepKey = null"
      >
        <span
          class="wiz-dot"
          :class="statusDotClass(substep.key)"
        >
          <font-awesome-icon :icon="statusIconName(substep.key)" />
        </span>
        {{ substep.label }}
      </div>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
  import type { WizardNavState, WizardStep, StepStatus } from '@/types/applicationStep';
  import { computed, onMounted, onUnmounted, ref } from 'vue';

  // ── Props ────────────────────────────────────────────────────────────────────

  interface Props {
    /** Full ordered step config. Adding/moving a step = edit wizardSteps.ts only. */
    steps: WizardStep[];
    /** Substep key to activate on mount. */
    initialStep?: string;
    initialNavState?: WizardNavState;
    initialStatusMap?: Record<string, StepStatus>;
    initialDisabledMap?: Record<string, boolean>;
  }

  const props = withDefaults(defineProps<Props>(), {
    initialStep: 'step1',
    initialNavState: () => ({ hiddenSteps: {}, hiddenSubsteps: {} }),
    initialStatusMap: () => ({}),
    initialDisabledMap: () => ({}),
  });

  // ── Emits ────────────────────────────────────────────────────────────────────

  const emit = defineEmits<{
    /** Fired after every successful navigation. Parent shows the correct panel/form. */
    (e: 'navigate', stepKey: string): void;
    /** Fired when a substep's status changes (for parent bookkeeping). */
    (e: 'validation', stepKey: string, status: StepStatus | null): void;
  }>();

  // ── Reactive state ───────────────────────────────────────────────────────────

  const activeSubstep = ref(props.initialStep);

  const navState = ref<WizardNavState>({
    hiddenSteps: { ...props.initialNavState.hiddenSteps },
    hiddenSubsteps: { ...props.initialNavState.hiddenSubsteps },
  });

  const statusMap = ref<Record<string, StepStatus>>({ ...props.initialStatusMap });

  const disabledMap = ref<Record<string, boolean>>({ ...props.initialDisabledMap });

  const collapsed = ref(false);

  // ── Hover flyout (collapsed mode) ─────────────────────────────────────────────

  const hoveredStepKey = ref<string | null>(null);
  const flyoutTop = ref(0);
  const flyoutLeft = ref(0);
  let hoverClearTimer: ReturnType<typeof setTimeout> | null = null;

  const hoveredStepData = computed(() =>
    hoveredStepKey.value ? props.steps.find((s) => s.key === hoveredStepKey.value) : null
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
    hoverClearTimer = setTimeout(() => { hoveredStepKey.value = null; }, 150);
  }

  function cancelHoverClear() {
    if (hoverClearTimer) { clearTimeout(hoverClearTimer); hoverClearTimer = null; }
  }

  function clearHover() {
    hoverClearTimer = setTimeout(() => { hoveredStepKey.value = null; }, 150);
  }

  /** Steps manually expanded via the chevron button (independent of active step). */
  const expandedSteps = ref<Record<string, boolean>>({});

  /**
   * Substeps that were just started (no prior status). Skip validation on first exit.
   */
  const skipValidationOnFirstExit: Record<string, boolean> = {};

  // ── Computed ─────────────────────────────────────────────────────────────────

  /** Flat ordered list of ALL substep keys derived from config. */
  const allSubstepKeys = computed<string[]>(() => {
    const result: string[] = [];
    for (const step of props.steps) {
      for (const sub of step.substeps) {
        result.push(sub.key);
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
      if (navState.value.hiddenSubsteps[key]) return false;
      const parent = getParentStepKey(key);
      if (parent && navState.value.hiddenSteps[parent]) return false;
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
      const prefParent = preferred.split('_')[0];
      const sameParent = ordered.find((k) => k.split('_')[0] === prefParent);
      if (sameParent) return sameParent;
    }
    if (activeSubstep.value && ordered.includes(activeSubstep.value)) {
      return activeSubstep.value;
    }
    return ordered[0];
  }

  // ── Status icon helpers ───────────────────────────────────────────────────────

  function statusDotClass(substepKey: string): string {
    const s = statusMap.value[substepKey];
    return s ? `status-${s}` : '';
  }

  function statusIconName(substepKey: string): string {
    const s = statusMap.value[substepKey];
    if (s === 'completed') return 'circle-check';
    if (s === 'incomplete') return 'circle-half-stroke';
    if (s === 'error') return 'circle-xmark';
    return 'circle';
  }

  // ── UI state helpers ──────────────────────────────────────────────────────────

  function isStepActive(stepKey: string): boolean {
    return getParentStepKey(activeSubstep.value) === stepKey;
  }

  function isSubMenuOpen(stepKey: string): boolean {
    if (navState.value.hiddenSteps[stepKey]) return false;
    return isStepActive(stepKey) || !!expandedSteps.value[stepKey];
  }

  function toggleExpand(stepKey: string) {
    expandedSteps.value[stepKey] = !expandedSteps.value[stepKey];
  }

  function isStepHidden(stepKey: string): boolean {
    return !!navState.value.hiddenSteps[stepKey];
  }

  function isSubstepHidden(substepKey: string): boolean {
    return !!navState.value.hiddenSubsteps[substepKey];
  }

  function isStepDisabled(stepKey: string): boolean {
    return !!disabledMap.value[stepKey];
  }

  function isSubstepDisabled(substepKey: string): boolean {
    return !!disabledMap.value[substepKey];
  }

  // ── Step status lifecycle ─────────────────────────────────────────────────────

  function ensureStartedStatus(substepKey: string) {
    if (!statusMap.value[substepKey]) {
      statusMap.value[substepKey] = 'incomplete';
      skipValidationOnFirstExit[substepKey] = true;
      emit('validation', substepKey, 'incomplete');
    }
  }

  /**
   * Called when leaving a substep. Skips validation on very first exit
   * (user hasn't had a chance to fill anything in yet).
   * Full validation wiring happens in Step 4 of the refactor plan.
   */
  function finalizeSubstepStatus(substepKey: string) {
    if (!statusMap.value[substepKey]) return;
    if (skipValidationOnFirstExit[substepKey]) {
      skipValidationOnFirstExit[substepKey] = false;
      return;
    }
    // TODO Step 4: call actual panel validation here; for now status stays as-is.
  }

  // ── Core navigation ───────────────────────────────────────────────────────────

  function updateSidebar(stepKey: string) {
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
      finalizeSubstepStatus(activeSubstep.value);
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

  // ── Public API ────────────────────────────────────────────────────────────────

  function navigateNext() {
    const next = getAdjacentVisibleSubstep(activeSubstep.value, 1);
    if (next) updateSidebar(next);
  }

  function navigatePrevious() {
    const prev = getAdjacentVisibleSubstep(activeSubstep.value, -1);
    if (prev) updateSidebar(prev);
  }

  function setStepStatus(substepKey: string, status: StepStatus | null) {
    if (status) {
      statusMap.value[substepKey] = status;
    } else {
      delete statusMap.value[substepKey];
    }
    emit('validation', substepKey, status);
  }

  function setAllStatuses(map: Record<string, StepStatus>) {
    statusMap.value = { ...map };
  }

  function setStepVisibility(stepKey: string, isVisible: boolean) {
    if (isVisible) {
      delete navState.value.hiddenSteps[stepKey];
    } else {
      navState.value.hiddenSteps[stepKey] = true;
    }
    // If current substep belongs to a newly-hidden step, navigate away.
    const parentOfCurrent = getParentStepKey(activeSubstep.value);
    if (parentOfCurrent === stepKey && !isVisible) {
      const fallback =
        getFallbackSubstep(activeSubstep.value) ??
        getVisibleSubstepsOrdered()[0] ??
        'step1';
      updateSidebar(fallback);
    }
  }

  function setSubstepVisibility(substepKey: string, isVisible: boolean) {
    if (isVisible) {
      delete navState.value.hiddenSubsteps[substepKey];
    } else {
      navState.value.hiddenSubsteps[substepKey] = true;
    }
    if (activeSubstep.value === substepKey && !isVisible) {
      const fallback =
        getFallbackSubstep(substepKey) ??
        getVisibleSubstepsOrdered()[0] ??
        'step1';
      updateSidebar(fallback);
    }
  }

  function setAllVisibility(nextState: WizardNavState) {
    navState.value = {
      hiddenSteps: { ...(nextState.hiddenSteps ?? {}) },
      hiddenSubsteps: { ...(nextState.hiddenSubsteps ?? {}) },
    };
    const ordered = getVisibleSubstepsOrdered();
    if (!ordered.includes(activeSubstep.value)) {
      const fallback = getFallbackSubstep(activeSubstep.value) ?? ordered[0] ?? 'step1';
      updateSidebar(fallback);
    }
  }

  function setStepClickable(stepKey: string, isClickable: boolean) {
    if (isClickable) {
      delete disabledMap.value[stepKey];
    } else {
      disabledMap.value[stepKey] = true;
    }
  }

  // ── Computed exposed values ───────────────────────────────────────────────────

  const hasPrev = computed(
    () => getAdjacentVisibleSubstep(activeSubstep.value, -1) !== null
  );

  const hasNext = computed(
    () => getAdjacentVisibleSubstep(activeSubstep.value, 1) !== null
  );

  const isLastStep = computed(() => !hasNext.value);

  // ── Window function bridge ────────────────────────────────────────────────────
  // Preserves backward compat with CHEFS form JS that calls window.wizard* functions.
  // Remove these once CHEFS forms no longer contain wizard/nav JS (Step 3).

  function registerWindowFunctions() {
    window.wizardUpdateSidebar = updateSidebar;
    window.wizardNavigateNext = navigateNext;
    window.wizardNavigatePrevious = navigatePrevious;
    window.wizardSetStepStatus = setStepStatus;
    window.wizardSetAllStatuses = setAllStatuses;
    window.wizardSetStepVisibility = setStepVisibility;
    window.wizardSetSubstepVisibility = setSubstepVisibility;
    window.wizardSetAllVisibility = setAllVisibility;
    window.wizardSetStepClickable = setStepClickable;
    // Stub — returns true until Step 4 wires actual panel validation.
    window.wizardValidateStep = (_substep: string) => true;
  }

  function unregisterWindowFunctions() {
    const fns: (keyof Window)[] = [
      'wizardUpdateSidebar',
      'wizardNavigateNext',
      'wizardNavigatePrevious',
      'wizardValidateStep',
      'wizardSetStepStatus',
      'wizardSetAllStatuses',
      'wizardSetStepVisibility',
      'wizardSetSubstepVisibility',
      'wizardSetAllVisibility',
      'wizardSetStepClickable',
    ];
    fns.forEach((fn) => delete window[fn]);
  }

  let resizeCleanup: (() => void) | null = null;

  onMounted(() => {
    registerWindowFunctions();

    // Keep collapsed state in sync with viewport — reset to expanded on small screens
    // so Vue state never contradicts the CSS override.
    function syncCollapsedToViewport() {
      if (window.innerWidth <= 768 && collapsed.value) {
        collapsed.value = false;
      }
    }
    window.addEventListener('resize', syncCollapsedToViewport);
    resizeCleanup = () => window.removeEventListener('resize', syncCollapsedToViewport);
    syncCollapsedToViewport();

    // Navigate to first visible substep (or initialStep if already visible).
    const ordered = getVisibleSubstepsOrdered();
    const initial = ordered.includes(props.initialStep)
      ? props.initialStep
      : (ordered[0] ?? 'step1');
    updateSidebar(initial);
  });

  onUnmounted(() => {
    unregisterWindowFunctions();
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
    position: sticky;
    top: 62px;
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
    transition: width 0.25s ease, min-width 0.25s ease;
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
    transition: opacity 0.2s ease, width 0.25s ease;
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
    padding: 8px 0;
  }

  .wiz-sidebar.is-collapsed .wiz-step-text,
  .wiz-sidebar.is-collapsed .wiz-subitems,
  .wiz-sidebar.is-collapsed .wiz-expand-btn {
    display: none !important;
  }

  .wiz-sidebar.is-collapsed .wiz-step {
    justify-content: center;
    align-items: center;
    padding: 13px 0;
    gap: 0;
    border-left-color: transparent !important;
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
    margin-left: 8px;
    border-left: 4px solid transparent;
    cursor: pointer;
    transition: color 0.2s ease;
  }

  .wiz-step.is-active {
    background: #e8a825;
    border-left-color: #e8a825;
    color: #fff;
  }

  .wiz-step.is-active .wiz-icon {
    background: rgba(255, 255, 255, 0.3);
    color: #fff;
  }

  .wiz-step.is-disabled {
    opacity: 0.5;
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
    width: 32px;
    height: 32px;
    border-radius: 50%;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    font-weight: 700;
    background: #d9d9d9;
    color: #555;
    flex: 0 0 32px;
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
    color: #2e8540;
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
</style>
