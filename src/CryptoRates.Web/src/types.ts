/** Mirrors the gateway contract: GET /api/v1/rates and the RatesUpdated hub event. */
export interface Rate {
  id: string;
  symbol: string;
  name: string;
  rank: number | null;
  priceUsd: number;
  updatedAt: string;
}

export interface RatesSnapshot {
  rates: Rate[];
  fetchedAt: string;
}

export type StreamState = "connecting" | "live" | "reconnecting";
export type Flash = "up" | "down";
