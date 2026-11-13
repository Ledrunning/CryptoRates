<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from "vue";
import Pagination from "./components/Pagination.vue";
import { useRatesStream } from "./composables/useRatesStream";
import { formatAgo, formatUsd } from "./format";

const PAGE_SIZE = 25;
const hubUrl = `${import.meta.env.VITE_GATEWAY_URL ?? "https://localhost:7208"}/hubs/rates`;

const { rates, fetchedAt, state, flash } = useRatesStream(hubUrl);

// The "updated … ago" text ticks every second.
const now = ref(Date.now());
let ticker: ReturnType<typeof setInterval> | undefined;
onMounted(() => (ticker = setInterval(() => (now.value = Date.now()), 1000)));
onBeforeUnmount(() => clearInterval(ticker));

const statusText = computed(() => {
  switch (state.value) {
    case "live":
      return "Live";
    case "reconnecting":
      return "Connection lost, retrying…";
    default:
      return "Connecting…";
  }
});
const updatedText = computed(() =>
  fetchedAt.value ? `Updated ${formatAgo(fetchedAt.value, now.value)}` : "",
);

// Search and pagination run in the browser: the hub always delivers the whole snapshot.
const query = ref("");
const page = ref(readPageFromUrl());

const filtered = computed(() => {
  const text = query.value.trim().toLowerCase();
  if (!text) return rates.value;
  return rates.value.filter(
    (r) =>
      r.name.toLowerCase().includes(text) ||
      r.symbol.toLowerCase().includes(text),
  );
});
const pageCount = computed(() =>
  Math.max(1, Math.ceil(filtered.value.length / PAGE_SIZE)),
);
const currentPage = computed(() => Math.min(page.value, pageCount.value));
const pageRows = computed(() =>
  filtered.value.slice(
    (currentPage.value - 1) * PAGE_SIZE,
    currentPage.value * PAGE_SIZE,
  ),
);

const loading = computed(() => rates.value.length === 0);
const countText = computed(() => {
  const n = filtered.value.length;
  return `${n} ${n === 1 ? "coin" : "coins"}`;
});

watch(query, () => (page.value = 1));
watch(page, (value) => {
  const url = new URL(location.href);
  if (value === 1) url.searchParams.delete("page");
  else url.searchParams.set("page", String(value));
  history.replaceState(null, "", url);
});

function readPageFromUrl(): number {
  const value = Number(new URLSearchParams(location.search).get("page"));
  return Number.isInteger(value) && value >= 1 ? value : 1;
}
</script>

<template>
  <main class="page">
    <header class="hero">
      <h1 class="hero__title">Crypto rates</h1>
      <p class="hero__sub">
        Prices in US dollars for the top coins by market cap. This page updates
        on its own.
      </p>

      <p class="live" :data-state="state">
        <span class="live__dot" aria-hidden="true"></span>
        <span class="live__status" role="status">{{ statusText }}</span>
        <span v-if="updatedText" class="live__updated">{{ updatedText }}</span>
      </p>
    </header>

    <div class="toolbar">
      <input
        v-model="query"
        class="search"
        type="search"
        placeholder="Search by name or symbol"
        aria-label="Search coins"
        autocomplete="off"
      />
      <span class="toolbar__count">{{ countText }}</span>
    </div>

    <section class="panel">
      <table class="rates">
        <caption class="sr-only">
          Cryptocurrency prices in US dollars
        </caption>
        <thead>
          <tr>
            <th class="col-rank" scope="col">#</th>
            <th scope="col">Coin</th>
            <th class="col-price" scope="col">Price (USD)</th>
          </tr>
        </thead>
        <tbody>
          <template v-if="loading">
            <tr v-for="n in 8" :key="n" class="skeleton" aria-hidden="true">
              <td class="col-rank"><span class="bar bar--short"></span></td>
              <td><span class="bar"></span></td>
              <td class="col-price"><span class="bar bar--price"></span></td>
            </tr>
          </template>

          <template v-else>
            <tr v-for="rate in pageRows" :key="rate.id">
              <td class="col-rank">{{ rate.rank ?? "–" }}</td>
              <td>
                <span class="coin__name">{{ rate.name }}</span>
                <span class="coin__symbol">{{ rate.symbol }}</span>
              </td>
              <td class="col-price">
                <span class="price" :class="flash[rate.id]">{{
                  formatUsd(rate.priceUsd)
                }}</span>
              </td>
            </tr>
            <tr v-if="pageRows.length === 0">
              <td class="empty" colspan="3">
                No coin matches “{{ query }}”. Try a name or a symbol like BTC.
              </td>
            </tr>
          </template>
        </tbody>
      </table>
    </section>

    <Pagination
      v-if="pageCount > 1"
      :page="currentPage"
      :page-count="pageCount"
      @update:page="page = $event"
    />
  </main>
</template>
