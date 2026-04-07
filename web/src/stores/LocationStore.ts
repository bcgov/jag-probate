import { defineStore } from 'pinia';
import { ref } from 'vue';

export interface LocationInfo {
  id: string;
  name: string;
  address: string;
  city: string;
  postalCode: string;
  province: string;
}

export const useLocationStore = defineStore('LocationStore', () => {
  const locationsInfo = ref<LocationInfo[]>([]);

  function setLocationsInfo(locations: LocationInfo[]) {
    locationsInfo.value = locations;
  }

  return {
    locationsInfo,
    setLocationsInfo,
  };
});
