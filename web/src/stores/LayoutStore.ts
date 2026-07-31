import { defineStore } from 'pinia';
import { ref } from 'vue';

/**
 * LayoutStore manages the layout state of the application,
 * including the navigation header and other presentation elements.
 * @returns An object containing the layout state and methods to update it.
 */
export type backdropClasses = '' | 'bd-legislature-dome';

export const useLayoutStore = defineStore('LayoutStore', () => {
  // Navigation header state
  const navHeader = ref<string>('Probate Application');
  const setNavHeader = (newHeader: string) => (navHeader.value = newHeader);
  const resetNavHeader = () => (navHeader.value = 'Probate Application');

  // Backdrop class state
  const backdropClass = ref<backdropClasses>('');
  const setBackdropClass = (newClass: backdropClasses) =>
    (backdropClass.value = newClass);
  const resetBackdropClass = () => (backdropClass.value = '');

  // Navigation subtitle state
  const navSubtitle = ref<string | undefined>();
  const setNavSubtitle = (newSubtitle: string | undefined) =>
    (navSubtitle.value = newSubtitle);
  const resetNavSubtitle = () => (navSubtitle.value = undefined);

  // Mobile nav drawer state
  // Pages that include the step sidebar set hasMobileNav = true on mount.
  const hasMobileNav = ref(false);
  const mobileNavOpen = ref(false);
  const setHasMobileNav = (value: boolean) => (hasMobileNav.value = value);
  const openMobileNav = () => (mobileNavOpen.value = true);
  const closeMobileNav = () => (mobileNavOpen.value = false);

  return {
    navHeader,
    setNavHeader,
    resetNavHeader,
    navSubtitle,
    setNavSubtitle,
    resetNavSubtitle,
    backdropClass,
    setBackdropClass,
    resetBackdropClass,
    hasMobileNav,
    mobileNavOpen,
    setHasMobileNav,
    openMobileNav,
    closeMobileNav,
  };
});
