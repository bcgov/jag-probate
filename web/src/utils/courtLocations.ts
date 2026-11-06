import type { CourtLocationModel } from '@/types';

export interface CourtLocationOption {
  label: string;
  value: string;
}

interface FormioComponent {
  component: { data: { values: CourtLocationOption[] } };
  dataValue?: unknown;
  redraw(): void;
  setValue(value: unknown, options?: { modified?: boolean }): void;
  on(event: string, callback: () => void): void;
  _courtLocationChangeListenerAttached?: boolean;
}

interface FormioInstance {
  data: Record<string, unknown>;
  root: {
    getComponent(key: string): FormioComponent | undefined;
  };
}

export function getSupremeCourtOptions(
  courts: CourtLocationModel[]
): CourtLocationOption[] {
  return courts
    .filter(
      (court) =>
        court.isSupremeCourt === true &&
        typeof court.name === 'string' &&
        court.name.trim() !== '' &&
        court.identifierCode !== undefined &&
        court.identifierCode !== null &&
        String(court.identifierCode).trim() !== ''
    )
    .map((court) => ({
      label: court.name?.trim() ?? '',
      value: String(court.identifierCode),
    }));
}

export function applyCourtLocationOptions(
  form: FormioInstance,
  options: CourtLocationOption[]
): void {
  const courthouse = form.root.getComponent('applicantCourthouse');
  if (!courthouse) {
    throw new Error('Courthouse dropdown is not available.');
  }

  // The schema's courthouse-name calculation reads its labels from here, so
  // it must be set before any value change triggers a recalculation.
  form.data._courtLocations = options;

  const applicant = form.data.applicant;
  const savedApplicantValue =
    applicant !== null &&
    typeof applicant === 'object' &&
    'applicantCourthouse' in applicant
      ? applicant.applicantCourthouse
      : undefined;
  const savedValue =
    courthouse.dataValue !== undefined &&
    courthouse.dataValue !== null &&
    courthouse.dataValue !== ''
      ? courthouse.dataValue
      : savedApplicantValue;
  courthouse.component.data.values = options;
  courthouse.redraw();

  if (savedValue !== undefined && savedValue !== null && savedValue !== '') {
    courthouse.setValue(savedValue, { modified: false });
  }

  const nameField = form.root.getComponent('applicantCourthouseName');
  const updateCourthouseName = () => {
    if (!nameField) return;
    const selectedValue = courthouse.dataValue ?? savedValue;
    const selected = options.find(
      (option) => String(option.value) === String(selectedValue ?? '')
    );
    if (selected) {
      nameField.setValue(selected.label, { modified: true });
    }
  };

  updateCourthouseName();
  if (!courthouse._courtLocationChangeListenerAttached) {
    courthouse._courtLocationChangeListenerAttached = true;
    courthouse.on('change', updateCourthouseName);
  }
}
