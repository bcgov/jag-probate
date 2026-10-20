import { createPinia } from 'pinia';

/**
 * The app-wide Pinia instance. Exported on its own so non-component code
 * (e.g. the window.wizard* bridge used by CHEFS schema scripts) can reach
 * stores before - or entirely without - any component being mounted.
 */
export const pinia = createPinia();
