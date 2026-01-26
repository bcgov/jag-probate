import { App } from 'vue';

import { library } from '@fortawesome/fontawesome-svg-core';
import { FontAwesomeIcon } from '@fortawesome/vue-fontawesome';

// Import specific icons to be used in the application's library
import {
  faCircleCheck,
  faCircleQuestion,
  faCircleXmark,
} from '@fortawesome/free-solid-svg-icons';

export default function registerFontAwesome(app: App) {
  library.add(faCircleCheck, faCircleXmark, faCircleQuestion);
  app.component('font-awesome-icon', FontAwesomeIcon);
}
