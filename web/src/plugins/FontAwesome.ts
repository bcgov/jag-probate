import { App } from 'vue';

import { library } from '@fortawesome/fontawesome-svg-core';
import { FontAwesomeIcon } from '@fortawesome/vue-fontawesome';

// Import specific icons to be used in the application's library
import {
  faArrowLeft,
  faChevronDown,
  faCircleCheck,
  faCircleQuestion,
  faCircleXmark,
  faList,
  faRightFromBracket,
  faUser,
} from '@fortawesome/free-solid-svg-icons';

export default function registerFontAwesome(app: App) {
  library.add(
    faArrowLeft,
    faChevronDown,
    faCircleCheck,
    faCircleXmark,
    faCircleQuestion,
    faList,
    faRightFromBracket,
    faUser
  );
  app.component('font-awesome-icon', FontAwesomeIcon);
}
