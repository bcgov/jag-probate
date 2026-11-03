// @vitest-environment happy-dom
import { afterEach, describe, expect, it } from 'vitest';
import { guardFormioAnimations, NO_ANIM_CLASS } from './formioAnimationGuard';

const flush = () => new Promise((r) => setTimeout(r, 0));

function makeComponent(id: string): HTMLElement {
  const el = document.createElement('div');
  el.id = id;
  el.className = 'formio-component formio-component-fieldset';
  return el;
}

/** happy-dom doesn't run CSS animations, so simulate the browser event. */
function playAnimation(el: Element) {
  el.dispatchEvent(new Event('animationstart', { bubbles: true }));
}

describe('guardFormioAnimations', () => {
  let dispose: (() => void) | null = null;
  let t = 0;
  const now = () => t;

  afterEach(() => {
    dispose?.();
    dispose = null;
    t = 0;
    document.body.innerHTML = '';
  });

  function setup(
    root: Node = document.body.appendChild(document.createElement('div'))
  ) {
    dispose = guardFormioAnimations(root, { now });
    return root;
  }

  it('lets a component animate the first time', async () => {
    const root = setup();
    const comp = makeComponent('e1');
    root.appendChild(comp);
    await flush();
    expect(comp.classList.contains(NO_ANIM_CLASS)).toBe(false);
  });

  it('suppresses redraws within 5s of the animation playing', async () => {
    const root = setup();
    const first = makeComponent('e1');
    root.appendChild(first);
    await flush();
    playAnimation(first);

    t += 4900;
    const redrawn = makeComponent('e1');
    root.replaceChild(redrawn, first);
    await flush();

    expect(redrawn.classList.contains(NO_ANIM_CLASS)).toBe(true);
  });

  it('allows animation again once the cooldown has passed', async () => {
    const root = setup();
    const first = makeComponent('e1');
    root.appendChild(first);
    await flush();
    playAnimation(first);

    t += 5000;
    const redrawn = makeComponent('e1');
    root.replaceChild(redrawn, first);
    await flush();

    expect(redrawn.classList.contains(NO_ANIM_CLASS)).toBe(false);
  });

  it('does not start a cooldown when the animation never played (hidden)', async () => {
    const root = setup();
    root.appendChild(makeComponent('e1'));
    await flush();

    const redrawn = makeComponent('e1');
    root.replaceChild(redrawn, root.firstChild!);
    await flush();

    expect(redrawn.classList.contains(NO_ANIM_CLASS)).toBe(false);
  });

  it('lets a component suppressed while hidden animate when revealed after cooldown', async () => {
    const root = setup();
    const first = makeComponent('e1');
    root.appendChild(first);
    await flush();
    playAnimation(first);

    const hidden = makeComponent('e1');
    hidden.classList.add('formio-hidden');
    root.replaceChild(hidden, first);
    await flush();
    expect(hidden.classList.contains(NO_ANIM_CLASS)).toBe(true);

    t += 6000;
    hidden.classList.remove('formio-hidden');
    await flush();

    expect(hidden.classList.contains(NO_ANIM_CLASS)).toBe(false);
  });

  it('works inside a shadow root', async () => {
    const host = document.body.appendChild(document.createElement('div'));
    const shadow = setup(host.attachShadow({ mode: 'open' }));
    const first = makeComponent('e1');
    shadow.appendChild(first);
    await flush();
    playAnimation(first);

    t += 1000;
    const redrawn = makeComponent('e1');
    shadow.replaceChild(redrawn, first);
    await flush();

    expect(redrawn.classList.contains(NO_ANIM_CLASS)).toBe(true);
  });
});
