import { describe, expect, it, vi } from 'vitest';
import { recalculateForm } from './formioRecalculate';

/**
 * Minimal model of Form.io 4.x observed in the browser: triggerChange is
 * debounced and keeps only the last call's flags; a calculated component that
 * changes value re-triggers with triggeredComponentId = itself; checkData
 * skips calculateValue for the stamped component.
 */
function createFormioLike(rule: (data: Record<string, any>) => unknown) {
  const component = { id: 'formActivationTest' };
  const data: Record<string, any> = {};
  let pendingFlags: Record<string, any> | null = null;
  const form = {
    data,
    checkData(d: Record<string, any>, flags: Record<string, any> = {}) {
      if (component.id !== flags.triggeredComponentId) rule(d);
      return true;
    },
    triggerChange(flags: Record<string, any> = {}) {
      pendingFlags = flags;
    },
    /** Runs the debounced onChange with whichever flags arrived last. */
    flush() {
      const flags = pendingFlags;
      pendingFlags = null;
      if (flags) form.checkData(data, flags);
    },
  };
  return form;
}

describe('recalculateForm', () => {
  it('runs the rule even when a self-stamped change wins the debounce', () => {
    const bridge = vi.fn();
    const rule = (data: Record<string, any>) => {
      if (data.currentStep === 'docs') bridge();
    };

    // Old behaviour: debounced only, then the component's own change lands.
    const buggy = createFormioLike(rule);
    buggy.data.currentStep = 'docs';
    buggy.triggerChange({ noValidate: true });
    buggy.triggerChange({ triggeredComponentId: 'formActivationTest' });
    buggy.flush();
    expect(bridge).not.toHaveBeenCalled();

    const fixed = createFormioLike(rule);
    fixed.data.currentStep = 'docs';
    recalculateForm(fixed);
    fixed.triggerChange({ triggeredComponentId: 'formActivationTest' });
    fixed.flush();
    expect(bridge).toHaveBeenCalledOnce();
  });

  it('passes fresh non-validating flags and tolerates missing instances', () => {
    const triggerChange = vi.fn();
    const checkData = vi.fn();
    const instance = { data: {}, checkData, triggerChange };
    recalculateForm(instance);
    recalculateForm(instance);
    expect(checkData).toHaveBeenCalledTimes(2);
    expect(checkData.mock.calls[0][0]).toBe(instance.data);
    expect(checkData.mock.calls[0][1]).toMatchObject({ noValidate: true });
    expect(triggerChange.mock.calls[0][0]).not.toBe(
      triggerChange.mock.calls[1][0]
    );
    expect(() => recalculateForm(undefined)).not.toThrow();
    expect(() => recalculateForm({ triggerChange })).not.toThrow();
  });
});
