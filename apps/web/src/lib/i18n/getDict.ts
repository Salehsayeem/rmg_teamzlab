import { en } from "./en";
import { bn } from "./bn";

export type Lang = "en" | "bn";
export type Dict = typeof en;

/** Compile-time key-parity check: bn (and any future locale) must implement the exact en key set. */
const dictionaries: Record<Lang, Dict> = { en, bn };

export const langs: readonly Lang[] = ["en", "bn"];

export function isLang(value: string): value is Lang {
  return (langs as readonly string[]).includes(value);
}

export function getDict(lang: Lang): Dict {
  return dictionaries[lang];
}
