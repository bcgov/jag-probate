import { App } from 'vue';

import { library } from '@fortawesome/fontawesome-svg-core';
import { FontAwesomeIcon } from '@fortawesome/vue-fontawesome';

// Import specific icons to be used in the application's library
import {
  faAnglesLeft,
  faAnglesRight,
  faArrowLeft,
  faBars,
  faCircleArrowLeft,
  faCircleArrowRight,
  faBook,
  faChevronDown,
  faCircle,
  faCircleCheck,
  faCircleHalfStroke,
  faCircleQuestion,
  faCircleXmark,
  faCoins,
  faEnvelopeOpenText,
  faFileLines,
  faFloppyDisk,
  faList,
  faListCheck,
  faRightFromBracket,
  faSkull,
  faUser,
  faUserTie,
  faUsers,
  faXmark,
} from '@fortawesome/free-solid-svg-icons';

export default function registerFontAwesome(app: App) {
  library.add(
    faAnglesLeft,
    faAnglesRight,
    faArrowLeft,
    faBars,
    faCircleArrowLeft,
    faCircleArrowRight,
    faBook,
    faChevronDown,
    faCircle,
    faCircleCheck,
    faCircleHalfStroke,
    faCircleXmark,
    faCircleQuestion,
    faCoins,
    faEnvelopeOpenText,
    faFileLines,
    faFloppyDisk,
    faList,
    faListCheck,
    faRightFromBracket,
    faSkull,
    faUser,
    faUserTie,
    faUsers,
    faXmark
  );
  app.component('font-awesome-icon', FontAwesomeIcon);
}
