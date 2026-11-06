import { describe, expect, it, vi } from 'vitest';
import {
  applyCourtLocationOptions,
  getSupremeCourtOptions,
  type CourtLocationOption,
} from './courtLocations';

describe('court location helpers', () => {
  it('returns only valid Supreme Court dropdown options', () => {
    expect(
      getSupremeCourtOptions([
        {
          name: ' Vancouver Law Courts ',
          identifierCode: '6011',
          isSupremeCourt: true,
        },
        {
          name: 'Provincial Court',
          identifierCode: '1',
          isSupremeCourt: false,
        },
        {
          name: 'Missing code',
          isSupremeCourt: true,
        },
      ])
    ).toEqual([{ label: 'Vancouver Law Courts', value: '6011' }]);
  });

  it('updates options while preserving the saved selection and name', () => {
    const options: CourtLocationOption[] = [
      { label: 'Vancouver Law Courts', value: '6011' },
    ];
    const courthouse = {
      component: { data: { values: [] as CourtLocationOption[] } },
      dataValue: '',
      redraw: vi.fn(),
      setValue: vi.fn(function (this: { dataValue: unknown }, value: unknown) {
        this.dataValue = value;
      }),
      on: vi.fn(),
    };
    const nameField = { setValue: vi.fn() };
    const form: { data: Record<string, unknown>; root: unknown } = {
      data: { applicant: { applicantCourthouse: 6011 } },
      root: {
        getComponent: (key: string) =>
          key === 'applicantCourthouse' ? courthouse : nameField,
      },
    };
    // Form.io recalculates the name field on every change, so the labels it
    // reads must already be present when the first value is set.
    const labelsSeenBySetValue: unknown[] = [];
    courthouse.setValue.mockImplementation(function (
      this: { dataValue: unknown },
      value: unknown
    ) {
      labelsSeenBySetValue.push(form.data._courtLocations);
      this.dataValue = value;
    });

    applyCourtLocationOptions(
      form as Parameters<typeof applyCourtLocationOptions>[0],
      options
    );

    expect(labelsSeenBySetValue).toEqual([options]);
    expect(form.data._courtLocations).toEqual(options);
    expect(courthouse.component.data.values).toEqual(options);
    expect(courthouse.redraw).toHaveBeenCalledOnce();
    expect(courthouse.setValue).toHaveBeenCalledWith(6011, {
      modified: false,
    });
    expect(nameField.setValue).toHaveBeenCalledWith('Vancouver Law Courts', {
      modified: true,
    });
    expect(courthouse.on).toHaveBeenCalledOnce();
  });
});
