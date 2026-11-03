/**
 * Prevents CHEFS/formio entrance animations (see chefs-form.scss) from
 * replaying when formio redraws a component.
 *
 * Formio re-renders dependent components (e.g. conditional questions whose
 * label interpolates another field) by removing their DOM and inserting a
 * fresh copy with the same id. Each fresh copy restarts the CSS animation, so
 * it blinks on every keystroke.
 *
 * Rule: once a component's animation actually plays, it gets a cooldown.
 * Copies inserted during the cooldown get `no-anim`; after it, animation is
 * allowed again.
 */

export const ANIMATED_SELECTOR =
  '.step-container, .blue-background, .red-background, .formio-component-fieldset, .formio-component-panel';

export const NO_ANIM_CLASS = 'no-anim';

export const ANIMATION_COOLDOWN_MS = 5000;

/** Class formio puts on conditionally hidden components (kept in the DOM). */
const HIDDEN_CLASS = 'formio-hidden';

export interface AnimationGuardOptions {
  selector?: string;
  cooldownMs?: number;
  now?: () => number;
}

function collectTargets(node: Node, selector: string): Element[] {
  if (node.nodeType !== Node.ELEMENT_NODE) return [];
  const el = node as Element;
  const targets: Element[] = [];
  if (el.matches(selector)) targets.push(el);
  targets.push(...Array.from(el.querySelectorAll(selector)));
  return targets;
}

/** Stable identity for an animated element across formio redraws. */
export function getAnimationKey(el: Element): string | null {
  if (el.id) return el.id;
  const owner = el.parentElement?.closest('[id]');
  if (!owner) return null;
  const cls =
    typeof el.className === 'string'
      ? el.className.replace(/\bno-anim\b/g, '').trim()
      : '';
  return `${owner.id}|${cls}`;
}

/**
 * Starts guarding `root`. Returns a disposer that stops it.
 */
export function guardFormioAnimations(
  root: Node,
  options: AnimationGuardOptions = {}
): () => void {
  const selector = options.selector ?? ANIMATED_SELECTOR;
  const cooldownMs = options.cooldownMs ?? ANIMATION_COOLDOWN_MS;
  const now = options.now ?? (() => Date.now());
  const lastAnimatedAt = new Map<string, number>();

  const inCooldown = (key: string | null): boolean => {
    if (!key) return false;
    const last = lastAnimatedAt.get(key);
    return last !== undefined && now() - last < cooldownMs;
  };

  // Hidden (display:none) components never fire animationstart, so only
  // animations the user actually saw start a cooldown.
  const onAnimationStart = (e: Event) => {
    const el = e.target as Element | null;
    if (!el || !el.matches?.(selector)) return;
    const key = getAnimationKey(el);
    if (key) lastAnimatedAt.set(key, now());
  };
  root.addEventListener('animationstart', onAnimationStart, true);

  const observer = new MutationObserver((mutations) => {
    for (const m of mutations) {
      if (m.type === 'attributes') {
        // A component suppressed while hidden is revealed after its
        // cooldown ended: let it animate.
        const el = m.target as Element;
        const wasHidden = (m.oldValue ?? '')
          .split(/\s+/)
          .includes(HIDDEN_CLASS);
        if (!wasHidden || el.classList.contains(HIDDEN_CLASS)) continue;
        for (const t of collectTargets(el, selector)) {
          if (!inCooldown(getAnimationKey(t)))
            t.classList.remove(NO_ANIM_CLASS);
        }
        continue;
      }
      m.addedNodes.forEach((node) => {
        for (const el of collectTargets(node, selector)) {
          if (el.isConnected && inCooldown(getAnimationKey(el))) {
            el.classList.add(NO_ANIM_CLASS);
          }
        }
      });
    }

    const ts = now();
    for (const [key, t] of lastAnimatedAt) {
      if (ts - t >= cooldownMs) lastAnimatedAt.delete(key);
    }
  });

  observer.observe(root, {
    childList: true,
    subtree: true,
    attributes: true,
    attributeFilter: ['class'],
    attributeOldValue: true,
  });

  return () => {
    observer.disconnect();
    root.removeEventListener('animationstart', onAnimationStart, true);
    lastAnimatedAt.clear();
  };
}
