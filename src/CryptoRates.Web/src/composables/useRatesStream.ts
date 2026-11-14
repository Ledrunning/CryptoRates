import { onBeforeUnmount, onMounted, ref, shallowRef } from "vue";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import type { Flash, Rate, RatesSnapshot, StreamState } from "../types";

// Must match RatesHub.RatesUpdatedMethod on the gateway.
const RATES_UPDATED = "RatesUpdated";
const FLASH_MS = 1800;
const FIRST_CONNECT_RETRY_MS = 3000;

const sleep = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

/**
 * Keeps the latest rates in sync with the gateway. The hub sends the current snapshot
 * right after every (re)connect, so there is nothing to poll or refresh by hand.
 */
export function useRatesStream(hubUrl: string) {
  const rates = shallowRef<Rate[]>([]);
  const fetchedAt = ref<Date | null>(null);
  const state = ref<StreamState>("connecting");
  const flash = ref<Record<string, Flash>>({});

  let flashTimer: ReturnType<typeof setTimeout> | undefined;
  let stopped = false;

  const connection = new HubConnectionBuilder()
    .withUrl(hubUrl)
    .withAutomaticReconnect({
      // Never give up: the default policy stops after four attempts.
      nextRetryDelayInMilliseconds: (context) =>
        Math.min(30_000, 1000 * 2 ** context.previousRetryCount),
    })
    .configureLogging(LogLevel.Warning)
    .build();

  function apply(snapshot: RatesSnapshot) {
    const previous = new Map(
      rates.value.map((rate) => [rate.id, rate.priceUsd]),
    );
    const changes: Record<string, Flash> = {};

    for (const rate of snapshot.rates) {
      const before = previous.get(rate.id);
      if (before !== undefined && before !== rate.priceUsd) {
        changes[rate.id] = rate.priceUsd > before ? "up" : "down";
      }
    }

    rates.value = snapshot.rates;
    fetchedAt.value = new Date(snapshot.fetchedAt);
    flash.value = changes;

    clearTimeout(flashTimer);
    flashTimer = setTimeout(() => (flash.value = {}), FLASH_MS);
  }

  connection.on(RATES_UPDATED, apply);
  connection.onreconnecting(() => (state.value = "reconnecting"));
  connection.onreconnected(() => (state.value = "live"));

  async function start() {
    // withAutomaticReconnect does not cover the first connect, so retry it by hand.
    while (!stopped) {
      try {
        state.value = "connecting";
        await connection.start();
        state.value = "live";
        return;
      } catch {
        state.value = "reconnecting";
        await sleep(FIRST_CONNECT_RETRY_MS);
      }
    }
  }

  onMounted(start);
  onBeforeUnmount(() => {
    stopped = true;
    clearTimeout(flashTimer);
    void connection.stop();
  });

  return { rates, fetchedAt, state, flash };
}
