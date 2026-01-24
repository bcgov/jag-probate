<template>
    <BPopover title-class="text-bg-primary" :triggers="['hover', 'click', 'focus']" :delay="{ show: 0, hide: 300 }"
        tabindex="0">
        <template #target="{ visible }">
            <button type="button" class="btn-link-glossary text-primary fw-semibold p-0" :aria-expanded="visible"
                aria-haspopup="true" :aria-label="`Show definition for ${term}`">
                {{ term }}
                <font-awesome-icon icon="fas-solid fa-circle-question" class="text-primary" aria-hidden="true" />
            </button>
        </template>
        <template #title>{{ term }}</template>
        <div v-html="termDefinition" />
    </BPopover>
</template>
<script setup lang="ts">
import { BPopover } from 'bootstrap-vue-next';
import { computed } from 'vue';
import glossaryJson from './glossary.json';
const glossary = glossaryJson as Record<string, string>;

const props = defineProps<{
    term: string;
    trigger?: string;
    size?: 'small' | 'medium' | 'large';
}>();

const termDefinition = computed(() => glossary[props.term.toLowerCase()] || "Definition not found.");
</script>
<style scoped>
/* Accessible button styled as inline link for glossary terms */
.btn-link-glossary {
    background: none;
    border: none;
    cursor: pointer;
    font-size: inherit;
    line-height: inherit;
    vertical-align: baseline;
    border-bottom: 2px dotted var(--bs-primary);
}

.btn-link-glossary:hover,
.btn-link-glossary:focus {
    color: var(--bs-primary);
}

.btn-link-glossary:focus-visible {
    outline: 2px solid var(--bs-primary);
    outline-offset: 2px;
}
</style>