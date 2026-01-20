import { defineStore } from "pinia";
import { ref } from "vue";

/**
 * LayoutStore manages the layout state of the application,
 * including the navigation header and other presentation elements.
 * @returns An object containing the layout state and methods to update it.
 */
export const useLayoutStore = defineStore("LayoutStore", () => {
  const navHeader = ref<string>("Probate");

  const setNavHeader = (newHeader: string) => (navHeader.value = newHeader);
  const resetNavHeader = () => (navHeader.value = "Probate");

  return {
    navHeader,
    setNavHeader,
    resetNavHeader,
  };
});
