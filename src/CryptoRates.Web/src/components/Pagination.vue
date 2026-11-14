<script setup lang="ts">
import { computed } from "vue";

const props = defineProps<{ page: number; pageCount: number }>();
const emit = defineEmits<{ "update:page": [value: number] }>();

// 1 … 4 5 6 … 10: first, last and the neighbours of the current page.
const items = computed<(number | "gap")[]>(() => {
  const wanted = new Set([
    1,
    props.pageCount,
    props.page - 1,
    props.page,
    props.page + 1,
  ]);
  const numbers = [...wanted]
    .filter((n) => n >= 1 && n <= props.pageCount)
    .sort((a, b) => a - b);

  const result: (number | "gap")[] = [];
  numbers.forEach((n, i) => {
    if (i > 0 && n - numbers[i - 1]! > 1) result.push("gap");
    result.push(n);
  });
  return result;
});

const go = (n: number) =>
  emit("update:page", Math.min(Math.max(1, n), props.pageCount));
</script>

<template>
  <nav class="pager" aria-label="Pages">
    <button
      class="pager__item"
      type="button"
      aria-label="Previous page"
      :disabled="page <= 1"
      @click="go(page - 1)"
    >
      <svg viewBox="0 0 16 16" width="16" height="16" aria-hidden="true">
        <path
          d="M10 3 5 8l5 5"
          fill="none"
          stroke="currentColor"
          stroke-width="1.8"
          stroke-linecap="round"
          stroke-linejoin="round"
        />
      </svg>
    </button>

    <template v-for="(item, i) in items" :key="`${item}-${i}`">
      <span v-if="item === 'gap'" class="pager__gap" aria-hidden="true">…</span>
      <button
        v-else
        class="pager__item"
        type="button"
        :aria-label="`Page ${item}`"
        :aria-current="item === page ? 'page' : undefined"
        @click="go(item)"
      >
        {{ item }}
      </button>
    </template>

    <button
      class="pager__item"
      type="button"
      aria-label="Next page"
      :disabled="page >= pageCount"
      @click="go(page + 1)"
    >
      <svg viewBox="0 0 16 16" width="16" height="16" aria-hidden="true">
        <path
          d="m6 3 5 5-5 5"
          fill="none"
          stroke="currentColor"
          stroke-width="1.8"
          stroke-linecap="round"
          stroke-linejoin="round"
        />
      </svg>
    </button>
  </nav>
</template>
