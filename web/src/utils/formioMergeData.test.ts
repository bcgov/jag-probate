import { describe, expect, it } from 'vitest';
import { mergeIntoFormData } from './formioMergeData';

describe('mergeIntoFormData', () => {
  it('keeps container object identity so bound child components see persisted values', () => {
    const applicant = { applicantOrgAddressCity: '', applicantOrgTitle: '' };
    const formData: Record<string, any> = { applicant };
    // Child component bound to the container object Form.io built at load.
    const cityComponent = { data: applicant };

    const changed = mergeIntoFormData(formData, {
      applicant: {
        applicantOrgAddressCity: 'Seattle',
        applicantOrgTitle: 'Director',
      },
      deceasedName: 'Alex OrgFixture',
    });

    expect(changed).toBe(true);
    expect(formData.applicant).toBe(applicant);
    expect(cityComponent.data.applicantOrgAddressCity).toBe('Seattle');
    expect(formData.deceasedName).toBe('Alex OrgFixture');
  });

  it('replaces arrays and reports no change when values already match', () => {
    const formData: Record<string, any> = { list: [1], nested: { a: 1 } };

    expect(mergeIntoFormData(formData, { list: [2] })).toBe(true);
    expect(formData.list).toEqual([2]);
    expect(mergeIntoFormData(formData, { nested: { a: 1 } })).toBe(false);
  });

  it('ignores nested __proto__ properties without polluting Object.prototype', () => {
    const marker = 'formioMergePollutionTest';
    const applicant = { city: '' };
    const formData = { applicant };
    const source = JSON.parse(
      '{"applicant":{"__proto__":{"formioMergePollutionTest":"polluted"},"city":"Seattle"}}'
    );

    try {
      expect(mergeIntoFormData(formData, source)).toBe(true);
      expect(applicant.city).toBe('Seattle');
      expect(Object.hasOwn(Object.prototype, marker)).toBe(false);
    } finally {
      Reflect.deleteProperty(Object.prototype, marker);
    }
  });
});
