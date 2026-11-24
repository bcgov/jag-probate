/**
 * Who owns step/substep availability (visibility + bans):
 * - 'off':     legacy — CHEFS scripts mutate the sidebar through the bridge.
 * - 'observe': legacy still mutates; the pathway machine computes in parallel
 *              and logs differences (dev only). Shadow-mode rollout.
 * - 'on':      the pathway machine is the sole writer; the bridge's
 *              availability setters are no-ops.
 *
 * Set with VITE_PATHWAY_MACHINE; defaults to 'on'.
 */
export type PathwayMachineMode = 'off' | 'observe' | 'on';

function readEnvMode(): PathwayMachineMode {
  const raw = String(import.meta.env?.VITE_PATHWAY_MACHINE ?? '')
    .trim()
    .toLowerCase();
  return raw === 'off' || raw === 'observe' ? raw : 'on';
}

let mode: PathwayMachineMode = readEnvMode();

export function getPathwayMachineMode(): PathwayMachineMode {
  return mode;
}

/** Tests / dev tooling. */
export function setPathwayMachineMode(next: PathwayMachineMode): void {
  mode = next;
}

export function resetPathwayMachineModeForTests(): void {
  mode = readEnvMode();
}
