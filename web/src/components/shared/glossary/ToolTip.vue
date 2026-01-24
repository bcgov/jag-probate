<template>
    <BPopover title-class="text-bg-primary">
        <template #target>
            <a class="fw-semibold border-bottom border-primary" style="text-decoration: none;" ref="reference">
                {{ term }}
                <font-awesome-icon icon="fas-solid fa-circle-question" class="text-primary" />
            </a>
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