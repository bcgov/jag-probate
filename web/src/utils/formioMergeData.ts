// Form.io children retain container data references. Replacing those objects
// leaves children reading defaults and writing blanks over persisted answers.
export function mergeIntoFormData(
  target: Record<string, unknown>,
  source: Record<string, unknown>
): boolean {
  let changed = false;
  for (const [key, value] of Object.entries(source)) {
    const current = target[key];
    if (isPlainObject(current) && isPlainObject(value)) {
      if (current !== value && mergeIntoFormData(current, value)) {
        changed = true;
      }
    } else if (current !== value) {
      target[key] = value;
      changed = true;
    }
  }
  return changed;
}

function isPlainObject(value: unknown): value is Record<string, unknown> {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) {
    return false;
  }
  const proto = Object.getPrototypeOf(value);
  return proto === Object.prototype || proto === null;
}
