import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createPdfPreviewBridge } from './pdfPreviewBridge';

const TITLE = 'Form P10 PDF Preview';
const iframeHtml = `<iframe src="about:blank" title="${TITLE}"></iframe>`;

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

const flush = () => new Promise((r) => setTimeout(r, 0));

describe('createPdfPreviewBridge', () => {
  let root: HTMLDivElement;
  let instance: { root: { element: HTMLElement } };
  let urlCounter: number;
  let generate: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    root = document.createElement('div');
    document.body.appendChild(root);
    instance = { root: { element: root } };
    urlCounter = 0;
    generate = vi.fn(async () => `blob:pdf-${++urlCounter}`);
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
  });

  afterEach(() => {
    root.remove();
    vi.restoreAllMocks();
  });

  function makeBridge() {
    return createPdfPreviewBridge({
      generate,
      resolveSubmissionData: (data) => data,
    });
  }

  const frame = () =>
    root.querySelector<HTMLIFrameElement>(`iframe[title="${TITLE}"]`);

  it('skips generation when not on the target substep', async () => {
    const bridge = makeBridge();
    await bridge.previewPdf(
      instance,
      {},
      'other',
      'form-preview-p10',
      'P10',
      TITLE
    );
    expect(generate).not.toHaveBeenCalled();
  });

  it('applies the PDF to an iframe rendered after generation (conditional show)', async () => {
    const bridge = makeBridge();
    await bridge.previewPdf(instance, { a: 1 }, 's', 's', 'P10', TITLE);
    expect(generate).toHaveBeenCalledTimes(1);

    root.innerHTML = iframeHtml;
    await flush();

    expect(frame()?.getAttribute('src')).toBe('blob:pdf-1');
    expect(frame()?.getAttribute('data-pdf-loaded')).toBe('true');
  });

  it('re-applies the PDF when the iframe is replaced by a redraw', async () => {
    root.innerHTML = iframeHtml;
    const bridge = makeBridge();
    await bridge.previewPdf(instance, { a: 1 }, 's', 's', 'P10', TITLE);
    expect(frame()?.getAttribute('src')).toBe('blob:pdf-1');

    root.innerHTML = iframeHtml;
    await flush();
    expect(frame()?.getAttribute('src')).toBe('blob:pdf-1');
  });

  it('sets the src on the current iframe when it is replaced mid-generation', async () => {
    root.innerHTML = iframeHtml;
    const pending = deferred<string>();
    generate.mockImplementationOnce(() => pending.promise);
    const bridge = makeBridge();

    const call = bridge.previewPdf(instance, { a: 1 }, 's', 's', 'P10', TITLE);
    root.innerHTML = iframeHtml;
    pending.resolve('blob:late');
    await call;

    expect(frame()?.getAttribute('src')).toBe('blob:late');
  });

  it('does not regenerate for unchanged data but does for changed data', async () => {
    root.innerHTML = iframeHtml;
    const bridge = makeBridge();
    await bridge.previewPdf(instance, { a: 1 }, 's', 's', 'P10', TITLE);
    await bridge.previewPdf(instance, { a: 1 }, 's', 's', 'P10', TITLE);
    expect(generate).toHaveBeenCalledTimes(1);

    await bridge.previewPdf(instance, { a: 2 }, 's', 's', 'P10', TITLE);
    expect(generate).toHaveBeenCalledTimes(2);
    expect(frame()?.getAttribute('src')).toBe('blob:pdf-2');
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:pdf-1');
  });

  it.each([false, true])(
    'keeps cached A while B is pending (dispose: %s)',
    async (dispose) => {
      root.innerHTML = iframeHtml;
      const pending = deferred<string>();
      const bridge = makeBridge();
      await bridge.previewPdf(instance, { a: 1 }, 's', 's', 'P10', TITLE);
      generate.mockImplementationOnce(() => pending.promise);

      const newer = bridge.previewPdf(
        instance,
        { a: 2 },
        's',
        's',
        'P10',
        TITLE
      );
      await bridge.previewPdf(instance, { a: 1 }, 's', 's', 'P10', TITLE);
      expect(URL.revokeObjectURL).not.toHaveBeenCalled();
      if (dispose) bridge.dispose();
      pending.resolve('blob:pdf-2');
      await newer;

      expect(frame()?.getAttribute('src')).toBe('blob:pdf-1');
      expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:pdf-2');
      if (dispose)
        expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:pdf-1');
      else expect(URL.revokeObjectURL).not.toHaveBeenCalledWith('blob:pdf-1');
    }
  );

  it.each([
    { completion: 'resolve', nextValue: 3 },
    { completion: 'reject', nextValue: 3 },
    { completion: 'resolve', nextValue: 2 },
  ])(
    'keeps the next request after stale B $completion (data: $nextValue)',
    async ({ completion, nextValue }) => {
      root.innerHTML = iframeHtml;
      const pendingB = deferred<string>();
      const pendingNext = deferred<string>();
      const bridge = makeBridge();
      const preview = (value: number) =>
        bridge.previewPdf(instance, { a: value }, 's', 's', 'P10', TITLE);
      await preview(1);
      generate.mockImplementationOnce(() => pendingB.promise);
      const requestB = preview(2);
      await preview(1);
      generate.mockImplementationOnce(() => pendingNext.promise);
      const requestNext = preview(nextValue);

      expect(generate).toHaveBeenCalledTimes(3);
      if (completion === 'reject') {
        pendingB.reject(new Error('stale B failed'));
        await expect(requestB).rejects.toThrow('stale B failed');
      } else {
        pendingB.resolve('blob:stale-b');
        await requestB;
        expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:stale-b');
      }
      expect(frame()?.getAttribute('src')).toBe('blob:pdf-1');
      const sharedNext = preview(nextValue);
      expect(generate).toHaveBeenCalledTimes(3);
      pendingNext.resolve('blob:current');
      await Promise.all([requestNext, sharedNext]);
      expect(frame()?.getAttribute('src')).toBe('blob:current');
      expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:pdf-1');
    }
  );

  it('discards a stale result when newer data was requested', async () => {
    root.innerHTML = iframeHtml;
    const first = deferred<string>();
    generate.mockImplementationOnce(() => first.promise);
    const bridge = makeBridge();

    const older = bridge.previewPdf(instance, { a: 1 }, 's', 's', 'P10', TITLE);
    await bridge.previewPdf(instance, { a: 2 }, 's', 's', 'P10', TITLE);
    first.resolve('blob:stale');
    await older;

    expect(frame()?.getAttribute('src')).toBe('blob:pdf-1');
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:stale');
  });

  it('downloads the cached preview when data is unchanged', async () => {
    root.innerHTML = iframeHtml;
    const click = vi
      .spyOn(HTMLAnchorElement.prototype, 'click')
      .mockImplementation(() => undefined);
    const bridge = makeBridge();
    await bridge.previewPdf(instance, { a: 1 }, 's', 's', 'P10', TITLE);

    await bridge.downloadPdf(instance, { a: 1 }, 'P10', TITLE, 'FormP10.pdf');
    expect(generate).toHaveBeenCalledTimes(1);
    expect(click).toHaveBeenCalledTimes(1);

    await bridge.downloadPdf(instance, { a: 3 }, 'P10', TITLE, 'FormP10.pdf');
    expect(generate).toHaveBeenCalledTimes(2);
    expect(generate).toHaveBeenLastCalledWith('P10', { a: 3 });
  });
});
