import {
  BaseStepPgTypes,
  stepsAndPagesNumberInfoType,
} from '@/types/StepsAndPages';
import { defineStore } from 'pinia';
import { ref } from 'vue';

export const useApplicationStore = defineStore(
  'PreviousApplicationStore',
  () => {
    const currentApplication = ref<Record<string, any> | null>(null);
    const existingApplication = ref(false);
    const stPgNo = ref<stepsAndPagesNumberInfoType | null>(null);

    function setCurrentApplication(app: Record<string, any>) {
      currentApplication.value = app;
    }

    function setExistingApplication(value: boolean) {
      existingApplication.value = value;
    }

    function updateStPgNo() {
      if (!currentApplication.value?.steps) return;

      const stepsAndPagesNumber = {
        DECEASED: {},
        WILL: {},
        RELATIONS: {},
        APPLICANT: {},
        NOTIFY: {},
        BELONGINGS: {},
        NOWILL: {},
        OVERVIEW: {},
        REVIEW: {},
        SUBMIT: {},
        NEXT: {},
      } as stepsAndPagesNumberInfoType;

      for (const step of currentApplication.value.steps) {
        const stepName = step.name as keyof stepsAndPagesNumberInfoType;
        stepsAndPagesNumber[stepName]._StepNo = Number(step.id);
        for (const page of step.pages) {
          (stepsAndPagesNumber[stepName] as BaseStepPgTypes)[page.name] =
            Number(page.key);
        }
      }

      stPgNo.value = stepsAndPagesNumber;
    }
    return {
      currentApplication,
      existingApplication,
      stPgNo,
      setCurrentApplication,
      setExistingApplication,
      updateStPgNo,
    };
  }
);
