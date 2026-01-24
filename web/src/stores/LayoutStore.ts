import { defineStore } from "pinia";
import { ref } from "vue";

/**
 * LayoutStore manages the layout state of the application,
 * including the navigation header and other presentation elements.
 * @returns An object containing the layout state and methods to update it.
 */
export type backdropClasses = "" | "bd-legislature-dome";

export const useLayoutStore = defineStore("LayoutStore", () => {
  const navHeader = ref<string>("Probate");
  const backdropClass = ref<backdropClasses>("");

  const setNavHeader = (newHeader: string) => (navHeader.value = newHeader);
  const resetNavHeader = () => (navHeader.value = "Probate");
  const setBackdropClass = (newClass: backdropClasses) =>
    (backdropClass.value = newClass);
  const resetBackdropClass = () => (backdropClass.value = "");
  return {
    navHeader,
    setNavHeader,
    resetNavHeader,
    backdropClass,
    setBackdropClass,
    resetBackdropClass,
  };
});
