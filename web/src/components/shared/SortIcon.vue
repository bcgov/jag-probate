<template>
  <svg
    width="12"
    height="20"
    viewBox="0 0 8 16"
    xmlns="http://www.w3.org/2000/svg"
    class="ms-1"
    style="vertical-align: middle"
  >
    <!-- Up arrow -->
    <path
      d="M4 1 L7.5 6 L0.5 6 Z"
      :fill="isActive && !sortDesc ? 'white' : 'rgba(255,255,255,0.4)'"
    />
    <!-- Down arrow -->
    <path
      d="M4 15 L7.5 10 L0.5 10 Z"
      :fill="isActive && sortDesc ? 'white' : 'rgba(255,255,255,0.4)'"
    />
  </svg>
</template>

<script setup lang="ts">
  import { computed } from 'vue';

  const props = defineProps<{
    column: string;
    sortKey: string;
    sortDesc: boolean;
  }>();

  const isActive = computed(() => props.sortKey === props.column);
</script>
