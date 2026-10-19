export type PdfGenerator = (
  templateKey: string,
  submissionData: unknown
) => Promise<string>;

export interface PdfPreviewBridgeOptions {
  /** Generates a PDF and resolves to a blob URL owned by the bridge. */
  generate: PdfGenerator;
  /** Builds the data model sent to the report API from the form's `data`. */
  resolveSubmissionData: (data: unknown) => unknown;
}

export interface PdfPreviewBridge {
  previewPdf: (
    instance: unknown,
    data: unknown,
    currentStep: string,
    targetStep: string,
    templateKey: string,
    iframeTitle: string
  ) => Promise<void>;
  downloadPdf: (
    instance: unknown,
    data: unknown,
    templateKey: string,
    iframeTitle: string,
    fileName: string
  ) => Promise<void>;
  dispose: () => void;
}

interface PreviewEntry {
  root: ParentNode;
  generation: number;
  url: string | null;
  urlSignature: string | null;
  pending: Promise<void> | null;
  pendingSignature: string | null;
}

function resolveRoot(instance: unknown): ParentNode {
  const element = (instance as any)?.root?.element;
  return element && typeof element.querySelectorAll === 'function'
    ? (element as ParentNode)
    : document;
}

function findFrames(root: ParentNode, title: string): HTMLIFrameElement[] {
  const escaped = title.replace(/["\\]/g, '\\$&');
  return Array.from(
    root.querySelectorAll<HTMLIFrameElement>(`iframe[title="${escaped}"]`)
  );
}

function signatureOf(templateKey: string, submissionData: unknown) {
  try {
    return JSON.stringify([templateKey, submissionData]);
  } catch {
    return null;
  }
}

/**
 * Bridge behind `window.probate.previewPdf` / `downloadPdf` used by CHEFS
 * schema scripts. The generated PDF is cached per iframe title and keyed by
 * the submission data, and is (re)applied to any matching iframe whenever
 * one is rendered - Form.io conditionals and redraws replace the iframe
 * with a fresh `about:blank` one without triggering a data change.
 */
export function createPdfPreviewBridge(
  options: PdfPreviewBridgeOptions
): PdfPreviewBridge {
  const entries = new Map<string, PreviewEntry>();
  const observers = new Map<ParentNode, MutationObserver>();
  let applyScheduled = false;
  let disposed = false;

  function applyEntry(title: string, entry: PreviewEntry) {
    if (!entry.url) return;
    for (const frame of findFrames(entry.root, title)) {
      if (frame.getAttribute('src') !== entry.url) {
        frame.setAttribute('src', entry.url);
      }
      frame.setAttribute('data-pdf-loaded', 'true');
    }
  }

  function applyAll() {
    applyScheduled = false;
    if (disposed) return;
    entries.forEach((entry, title) => applyEntry(title, entry));
  }

  function observe(root: ParentNode) {
    if (observers.has(root) || typeof MutationObserver === 'undefined') return;
    const observer = new MutationObserver((mutations) => {
      if (applyScheduled) return;
      if (!mutations.some((m) => m.addedNodes.length > 0)) return;
      applyScheduled = true;
      queueMicrotask(applyAll);
    });
    observer.observe(root as Node, { childList: true, subtree: true });
    observers.set(root, observer);
  }

  function getEntry(title: string, root: ParentNode): PreviewEntry {
    let entry = entries.get(title);
    if (!entry) {
      entry = {
        root,
        generation: 0,
        url: null,
        urlSignature: null,
        pending: null,
        pendingSignature: null,
      };
      entries.set(title, entry);
    }
    entry.root = root;
    return entry;
  }

  async function previewPdf(
    instance: unknown,
    data: unknown,
    currentStep: string,
    targetStep: string,
    templateKey: string,
    iframeTitle: string
  ): Promise<void> {
    if (disposed || currentStep !== targetStep) return;

    const root = resolveRoot(instance);
    observe(root);
    const entry = getEntry(iframeTitle, root);

    const submissionData = options.resolveSubmissionData(data);
    const signature = signatureOf(templateKey, submissionData);

    if (signature !== null) {
      if (entry.url && entry.urlSignature === signature) {
        if (entry.pending && entry.pendingSignature !== signature) {
          entry.generation += 1;
          entry.pending = null;
          entry.pendingSignature = null;
        }
        applyEntry(iframeTitle, entry);
        return;
      }
      if (entry.pending && entry.pendingSignature === signature) {
        return entry.pending;
      }
    }

    // Generate even if the iframe isn't rendered yet (e.g. hidden behind a
    // declaration conditional) - the observer applies it once it appears.
    const generation = ++entry.generation;
    const run = (async () => {
      try {
        const url = await options.generate(templateKey, submissionData);
        if (disposed || generation !== entry.generation) {
          URL.revokeObjectURL(url);
          return;
        }
        const oldUrl = entry.url;
        entry.url = url;
        entry.urlSignature = signature;
        applyEntry(iframeTitle, entry);
        if (oldUrl && oldUrl !== url) URL.revokeObjectURL(oldUrl);
      } finally {
        if (generation === entry.generation) {
          entry.pending = null;
          entry.pendingSignature = null;
        }
      }
    })();
    entry.pending = run;
    entry.pendingSignature = signature;
    return run;
  }

  async function downloadPdf(
    _instance: unknown,
    data: unknown,
    templateKey: string,
    iframeTitle: string,
    fileName: string
  ): Promise<void> {
    const submissionData = options.resolveSubmissionData(data);
    const signature = signatureOf(templateKey, submissionData);
    const entry = entries.get(iframeTitle);

    if (
      signature !== null &&
      entry?.pending &&
      entry.pendingSignature === signature
    ) {
      await entry.pending.catch(() => undefined);
    }

    let url: string;
    let freshUrl = false;
    if (signature !== null && entry?.url && entry.urlSignature === signature) {
      url = entry.url;
    } else {
      url = await options.generate(templateKey, submissionData);
      freshUrl = true;
    }

    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    a.click();

    if (freshUrl) {
      URL.revokeObjectURL(url);
    }
  }

  function dispose() {
    disposed = true;
    observers.forEach((observer) => observer.disconnect());
    observers.clear();
    entries.forEach((entry) => {
      if (entry.url) URL.revokeObjectURL(entry.url);
    });
    entries.clear();
  }

  return { previewPdf, downloadPdf, dispose };
}
