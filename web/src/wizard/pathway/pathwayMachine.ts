/**
 * evaluatePathway(steps, facts) → sidebar availability. Pure: availability is
 * a function of the accumulated form data only, so re-running a step's form
 * (revisit, hydration, background recalculation) with unchanged answers can
 * never change it.
 */
import {
  deriveBaselineNavState,
  getKeysBannedAfter,
  getOrderedSubstepKeys,
} from '@/config/wizardSteps';
import type { WizardStep } from '@/types/applicationStep';
import type { PathwayFacts } from './facts';
import {
  BAN_RULES,
  CLICKABLE_RULES,
  computeGates,
  currentState,
  type Gates,
  type PathwayState,
  VISIBILITY_RULES,
} from './rules';

export interface PathwayResult {
  state: PathwayState;
  gates: Gates;
  hiddenSteps: Record<string, boolean>;
  hiddenSubsteps: Record<string, boolean>;
  disabledMap: Record<string, boolean>;
  /** Active ban points, in config order (the first one decides). */
  activeBans: string[];
}

export function evaluatePathway(
  steps: WizardStep[],
  facts: PathwayFacts
): PathwayResult {
  const gates = computeGates(facts);
  const { hiddenSteps, hiddenSubsteps } = deriveBaselineNavState(steps);

  const parentByKey: Record<string, string> = {};
  for (const step of steps) {
    for (const sub of step.substeps) parentByKey[sub.key] = step.key;
  }
  const parentOf = (key: string) => parentByKey[key] ?? null;
  const stepKeys = new Set(steps.map((s) => s.key));

  for (const rule of VISIBILITY_RULES) {
    const known =
      rule.kind === 'step'
        ? stepKeys.has(rule.target)
        : rule.target in parentByKey;
    if (!known) continue;
    const record = rule.kind === 'step' ? hiddenSteps : hiddenSubsteps;
    if (rule.visible(facts, gates)) delete record[rule.target];
    else record[rule.target] = true;
  }

  const isSubstepReachable = (key: string) => {
    const parent = parentOf(key);
    return !hiddenSubsteps[key] && !(parent && hiddenSteps[parent]);
  };

  // A ban point only counts while its own substep is reachable: a stale
  // answer on a hidden substep (e.g. Parent after Spouse flips to "yes")
  // must not block the pathway.
  const ordered = getOrderedSubstepKeys(steps);
  const activeBans = BAN_RULES.filter(
    (rule) =>
      rule.after in parentByKey &&
      isSubstepReachable(rule.after) &&
      rule.active(facts)
  )
    .map((rule) => rule.after)
    .sort((a, b) => ordered.indexOf(a) - ordered.indexOf(b));

  const disabledMap: Record<string, boolean> = {};
  for (const after of activeBans) {
    for (const key of getKeysBannedAfter(ordered, parentOf, after)) {
      disabledMap[key] = true;
    }
  }

  for (const rule of CLICKABLE_RULES) {
    if (disabledMap[rule.parentStep]) continue;
    if (rule.when(facts)) delete disabledMap[rule.target];
  }

  return {
    state: currentState(gates),
    gates,
    hiddenSteps,
    hiddenSubsteps,
    disabledMap,
    activeBans,
  };
}
