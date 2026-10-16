/**
 * Asks a Form.io instance to re-run calculations, conditionals and logic after
 * the host mutated its data (cross-step injection, data.currentStep, ...).
 *
 * Form.io 4.x `triggerChange` is debounced (100ms) and keeps only the LAST
 * call's arguments. While a form is loading, a calculated component that
 * changes value calls triggerChange with `flags.triggeredComponentId` set to
 * itself; if that lands inside the debounce window it replaces the host's
 * flags, and checkData then skips calculateValue for that component. For
 * `formActivationTest` (which drives window.wizardSetSubstepVisibility) that
 * means the rule never re-runs after a refresh until the user changes
 * something.
 *
 * So recalculate synchronously via checkData first (no debounce, fresh flags),
 * then still fire a debounced change so listeners and the UI update.
 * `noValidate` keeps this from surfacing errors on untouched fields.
 */
export function recalculateForm(formioInstance: any): void {
  if (!formioInstance) return;
  const flags = () => ({ noValidate: true, hostRecalculate: true });
  try {
    if (formioInstance.data && typeof formioInstance.checkData === 'function') {
      formioInstance.checkData(formioInstance.data, flags());
    }
  } catch {
    /* fall through to the debounced change */
  }
  formioInstance.triggerChange?.(flags());
}
