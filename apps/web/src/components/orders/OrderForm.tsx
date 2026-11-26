"use client";

import { useActionState, useMemo, useState } from "react";
import Link from "next/link";
import { AlertIcon, PlusIcon } from "@/components/icons";
import { createOrder, type OrderFormState } from "@/lib/actions/orders";
import type { Dict, Lang } from "@/lib/i18n/getDict";

const CONTROL =
  "h-10 w-full rounded-lg border border-input bg-background px-3 text-sm text-foreground placeholder:text-sidebar-foreground focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring";
const SMALL_CONTROL =
  "h-10 w-full rounded-lg border border-input bg-background px-3 text-sm text-foreground placeholder:text-sidebar-foreground focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring";

/** One selectable approved style version: its variant matrix as colors × sizes. */
export type VersionOption = {
  id: string;
  label: string;
  colors: string[];
  sizes: string[];
  cells: string[]; // `${color}|${size}`
};

export type BuyerOption = { id: string; label: string; currency: string };

type LineState = {
  styleVersionId: string;
  price: string;
  qtys: Record<string, string>; // `${color}|${size}` -> qty text
  schedules: { shipDate: string; qty: string }[];
};

const INITIAL: OrderFormState = { status: "idle" };

export function OrderForm({
  lang,
  dict,
  buyers,
  versions,
  defaultDate,
}: {
  lang: Lang;
  dict: Dict;
  buyers: BuyerOption[];
  versions: VersionOption[];
  defaultDate: string;
}) {
  const t = dict.orders;
  const [state, formAction, pending] = useActionState(createOrder.bind(null, lang), INITIAL);
  const [code, setCode] = useState("");
  const [buyerId, setBuyerId] = useState("");
  const [buyerPo, setBuyerPo] = useState("");
  const [currency, setCurrency] = useState("USD");
  const [orderDate, setOrderDate] = useState(defaultDate);
  const [lines, setLines] = useState<LineState[]>([
    { styleVersionId: "", price: "", qtys: {}, schedules: [{ shipDate: "", qty: "" }] },
  ]);

  const setLine = (i: number, patch: Partial<LineState>) =>
    setLines(lines.map((line, j) => (j === i ? { ...line, ...patch } : line)));
  const setQty = (i: number, cell: string, value: string) =>
    setLine(i, { qtys: { ...lines[i].qtys, [cell]: value } });

  const optionFor = (versionId: string) => versions.find((v) => v.id === versionId);

  // Server rules mirrored client-side: header completeness, per-line version/price/
  // qty>0, cell membership in the selected version's variant matrix, schedule pairs.
  const problems: { scope: string; msg: string }[] = [];
  if (!code.trim()) problems.push({ scope: "code", msg: t.client.code });
  if (!buyerId) problems.push({ scope: "buyerId", msg: t.client.buyer });
  if (!buyerPo.trim()) problems.push({ scope: "buyerPo", msg: t.client.buyerPo });
  if (currency.trim().length !== 3) problems.push({ scope: "currency", msg: t.client.currency });
  if (!orderDate) problems.push({ scope: "orderDate", msg: t.client.orderDate });
  lines.forEach((line, i) => {
    const option = optionFor(line.styleVersionId);
    if (!option) {
      problems.push({ scope: `line${i}`, msg: t.client.version });
      return;
    }
    const price = Number(line.price);
    if (line.price === "" || Number.isNaN(price) || price < 0) {
      problems.push({ scope: `line${i}`, msg: t.client.price });
    }
    const filled = Object.entries(line.qtys).filter(([, v]) => v !== "");
    if (filled.length === 0) {
      problems.push({ scope: `line${i}`, msg: t.client.noQty });
    }
    for (const [cell, value] of filled) {
      const qty = Number(value);
      if (!option.cells.includes(cell)) {
        problems.push({ scope: `line${i}`, msg: t.client.cellMismatch });
      } else if (Number.isNaN(qty) || qty <= 0) {
        problems.push({ scope: `line${i}`, msg: t.client.qtyPositive });
      }
    }
    line.schedules.forEach((schedule) => {
      if ((schedule.shipDate === "") !== (schedule.qty === "")) {
        problems.push({ scope: `line${i}`, msg: t.client.scheduleHalf });
      }
    });
  });
  const problemFor = (scope: string) => problems.find((p) => p.scope === scope)?.msg;
  const lineProblems = (i: number) => problems.filter((p) => p.scope === `line${i}`).map((p) => p.msg);

  // Serialize the controlled state into the wire shape (OrderCreateRequest):
  // only complete schedule rows and filled qty cells are sent.
  const payload = JSON.stringify({
    code: code.trim(),
    buyerId,
    buyerPo: buyerPo.trim(),
    currency: currency.trim().toUpperCase(),
    orderDate,
    lines: lines.map((line) => ({
      styleVersionId: line.styleVersionId,
      price: Number(line.price || 0),
      sizes: Object.entries(line.qtys)
        .filter(([, value]) => value !== "")
        .map(([cell, value]) => {
          const [colorCode, sizeCode] = cell.split("|");
          return { colorCode, sizeCode, qty: Number(value) };
        }),
      schedules: line.schedules
        .filter((schedule) => schedule.shipDate !== "" && schedule.qty !== "")
        .map((schedule) => ({ shipDate: schedule.shipDate, qty: Number(schedule.qty) })),
    })),
  });

  // One Idempotency-Key per logical submission: stable while the payload is
  // unchanged (a retry after a lost response replays), regenerated whenever any
  // field edit changes the payload (that is a new logical attempt).
  // The payload IS the invalidation key (regenerate when it changes), not data the uuid reads.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const idempotencyKey = useMemo(() => crypto.randomUUID(), [payload]);

  const fieldError = (field: string, clientMsg?: string) => state.fieldErrors?.[field]?.[0] ?? clientMsg;
  // Server errors keyed under `lines[i]...` (VALIDATION_FAILED shape / ORDER_VARIANT_MISMATCH
  // cells) render inside the matching line block — house pattern: nested-key collection.
  const lineServerErrors = (i: number) =>
    Object.entries(state.fieldErrors ?? {})
      .filter(([key]) => key === `lines[${i}]` || key.startsWith(`lines[${i}].`))
      .flatMap(([, messages]) => messages);

  return (
    <section className="rounded-lg border border-border bg-card p-6">
      {state.status === "error" && state.message && (
        <div role="alert" className="mb-4 flex gap-3 rounded-lg bg-lighterror p-4">
          <span aria-hidden="true" className="mt-0.5 text-error">
            <AlertIcon />
          </span>
          <div className="min-w-0 text-sm">
            <p className="font-medium">{state.message}</p>
          </div>
        </div>
      )}
      <form action={formAction} className="flex flex-col gap-4" noValidate>
        <input type="hidden" name="payload" value={payload} />
        <input type="hidden" name="idempotencyKey" value={idempotencyKey} />
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <div>
            <label htmlFor="order-code" className="text-sm font-medium">{t.form.code}</label>
            <input
              id="order-code"
              type="text"
              required
              autoComplete="off"
              placeholder={t.form.codePlaceholder}
              value={code}
              onChange={(e) => setCode(e.target.value)}
              aria-invalid={fieldError("code", problemFor("code")) ? true : undefined}
              aria-describedby={fieldError("code", problemFor("code")) ? "order-code-error" : undefined}
              className={`mt-1.5 ${CONTROL}`}
            />
            {fieldError("code", problemFor("code")) && (
              <p id="order-code-error" className="mt-1 text-xs text-error">
                {fieldError("code", problemFor("code"))}
              </p>
            )}
          </div>
          <div>
            <label htmlFor="order-buyer" className="text-sm font-medium">{t.form.buyer}</label>
            <select
              id="order-buyer"
              value={buyerId}
              onChange={(e) => {
                setBuyerId(e.target.value);
                const buyer = buyers.find((b) => b.id === e.target.value);
                if (buyer?.currency) setCurrency(buyer.currency);
              }}
              aria-invalid={fieldError("buyerId", problemFor("buyerId")) ? true : undefined}
              aria-describedby={fieldError("buyerId", problemFor("buyerId")) ? "order-buyer-error" : undefined}
              className={`mt-1.5 ${CONTROL}`}
            >
              <option value="">{t.form.buyerPlaceholder}</option>
              {buyers.map((buyer) => (
                <option key={buyer.id} value={buyer.id}>
                  {buyer.label}
                </option>
                ))}
            </select>
            {fieldError("buyerId", problemFor("buyerId")) && (
              <p id="order-buyer-error" className="mt-1 text-xs text-error">
                {fieldError("buyerId", problemFor("buyerId"))}
              </p>
            )}
          </div>
          <div>
            <label htmlFor="order-po" className="text-sm font-medium">{t.form.buyerPo}</label>
            <input
              id="order-po"
              type="text"
              required
              autoComplete="off"
              placeholder={t.form.buyerPoPlaceholder}
              value={buyerPo}
              onChange={(e) => setBuyerPo(e.target.value)}
              aria-invalid={fieldError("buyerPo", problemFor("buyerPo")) ? true : undefined}
              aria-describedby={fieldError("buyerPo", problemFor("buyerPo")) ? "order-po-error" : undefined}
              className={`mt-1.5 ${CONTROL}`}
            />
            {fieldError("buyerPo", problemFor("buyerPo")) && (
              <p id="order-po-error" className="mt-1 text-xs text-error">
                {fieldError("buyerPo", problemFor("buyerPo"))}
              </p>
            )}
          </div>
          <div>
            <label htmlFor="order-currency" className="text-sm font-medium">{t.form.currency}</label>
            <input
              id="order-currency"
              type="text"
              required
              autoComplete="off"
              value={currency}
              onChange={(e) => setCurrency(e.target.value)}
              aria-invalid={fieldError("currency", problemFor("currency")) ? true : undefined}
              aria-describedby={fieldError("currency", problemFor("currency")) ? "order-currency-error" : undefined}
              className={`mt-1.5 ${CONTROL}`}
            />
            {fieldError("currency", problemFor("currency")) && (
              <p id="order-currency-error" className="mt-1 text-xs text-error">
                {fieldError("currency", problemFor("currency"))}
              </p>
            )}
          </div>
          <div>
            <label htmlFor="order-date" className="text-sm font-medium">{t.form.orderDate}</label>
            <input
              id="order-date"
              type="date"
              required
              value={orderDate}
              onChange={(e) => setOrderDate(e.target.value)}
              className={`mt-1.5 ${CONTROL}`}
            />
            {fieldError("orderDate") && (
              <p id="order-date-error" className="mt-1 text-xs text-error">
                {fieldError("orderDate")}
              </p>
            )}
          </div>
        </div>
        <div>
          <p className="text-sm font-medium">{t.form.lines}</p>
          {state.fieldErrors?.lines && (
            <ul role="alert" className="mt-2 list-inside list-disc text-xs text-error">
              {state.fieldErrors.lines.map((msg) => (
                <li key={msg}>{msg}</li>
              ))}
            </ul>
          )}
          <div className="mt-3 flex flex-col gap-4">
            {lines.map((line, i) => (
              <div key={i} className="rounded-lg border border-border p-4">
                <div className="flex items-center justify-between">
                  <p className="text-sm font-medium">
                    {t.form.line} {i + 1}
                  </p>
                  {lines.length > 1 && (
                    <button
                      type="button"
                      onClick={() => setLines(lines.filter((_, j) => j !== i))}
                      className="flex h-8 items-center rounded-lg border border-border px-2.5 text-xs font-medium text-sidebar-foreground transition-colors hover:border-primary hover:text-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
                    >
                      {t.form.removeLine}
                    </button>
                  )}
                </div>
                <div className="mt-3 grid grid-cols-1 gap-4 sm:grid-cols-2">
                  <div>
                    <label htmlFor={`order-line-${i}-version`} className="text-sm font-medium">
                      {t.form.styleVersion}
                    </label>
                    <select
                      id={`order-line-${i}-version`}
                      value={line.styleVersionId}
                      onChange={(e) => {
                        const option = optionFor(e.target.value);
                        const cells = option?.cells ?? [];
                        setLine(i, {
                          styleVersionId: e.target.value,
                          // a version switch drops cells outside the new matrix
                          qtys: Object.fromEntries(
                            Object.entries(line.qtys).filter(([cell]) => cells.includes(cell)),
                          ),
                        });
                      }}
                      className={`mt-1.5 ${CONTROL}`}
                    >
                      <option value="">{t.form.styleVersionPlaceholder}</option>
                      {versions.map((option) => (
                        <option key={option.id} value={option.id}>
                          {option.label}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label htmlFor={`order-line-${i}-price`} className="text-sm font-medium">
                      {t.form.price}
                    </label>
                    <input
                      id={`order-line-${i}-price`}
                      type="number"
                      min="0"
                      step="any"
                      value={line.price}
                      onChange={(e) => setLine(i, { price: e.target.value })}
                      className={`mt-1.5 ${CONTROL}`}
                    />
                  </div>
                </div>
                <div className="mt-4">
                  <p className="text-sm font-medium">{t.form.sizes}</p>
                  <p className="mt-0.5 text-xs text-sidebar-foreground">{t.form.sizesHint}</p>
                  {(() => {
                    const lineErrors = [...lineProblems(i), ...lineServerErrors(i)];
                    return lineErrors.length > 0 ? (
                      <ul role="alert" className="mt-2 list-inside list-disc text-xs text-error">
                        {lineErrors.map((msg) => (
                          <li key={msg}>{msg}</li>
                        ))}
                      </ul>
                    ) : null;
                  })()}
                  {optionFor(line.styleVersionId) && (
                    <div className="mt-3 overflow-x-auto">
                      <table className="w-full min-w-[480px] border-collapse text-sm">
                        <thead>
                          <tr className="border-b border-border">
                            <th scope="col" className="px-2 py-2 text-start text-xs font-medium text-sidebar-foreground">
                              {t.form.qty}
                            </th>
                            {optionFor(line.styleVersionId)!.sizes.map((size) => (
                              <th key={size} scope="col" className="px-2 py-2 text-start text-xs font-medium text-sidebar-foreground">
                                {size}
                              </th>
                            ))}
                            <th scope="col" className="px-2 py-2 text-end text-xs font-medium text-sidebar-foreground">
                              {t.form.totalQty}
                            </th>
                          </tr>
                        </thead>
                        <tbody>
                          {optionFor(line.styleVersionId)!.colors.map((color) => (
                            <tr key={color} className="border-b border-border last:border-b-0">
                              <td dir="auto" className="px-2 py-2 text-sm font-medium">{color}</td>
                              {optionFor(line.styleVersionId)!.sizes.map((size) => {
                                const cell = `${color}|${size}`;
                                return (
                                  <td key={cell} className="px-1.5 py-1.5">
                                    <input
                                      type="number"
                                      min="0"
                                      step="any"
                                      aria-label={`${color} / ${size} ${t.form.qty}`}
                                      value={line.qtys[cell] ?? ""}
                                      onChange={(e) => setQty(i, cell, e.target.value)}
                                      className={SMALL_CONTROL}
                                    />
                                  </td>
                                );
                              })}
                              <td className="px-2 py-2 text-end text-sm text-sidebar-foreground">
                                {optionFor(line.styleVersionId)!.cells
                                  .filter((cell) => line.qtys[cell])
                                  .reduce((sum, cell) => sum + (Number(line.qtys[cell]) || 0), 0)}
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  )}
                </div>
                <div className="mt-4">
                  <p className="text-sm font-medium">{t.form.schedules}</p>
                  <div className="mt-3 flex flex-col gap-3">
                    {line.schedules.map((schedule, j) => (
                      <div key={j} className="grid grid-cols-1 gap-3 sm:grid-cols-[1fr_1fr_auto]">
                        <div>
                          <label htmlFor={`order-line-${i}-sched-${j}-date`} className="sr-only">
                            {t.form.shipDate}
                          </label>
                          <input
                            id={`order-line-${i}-sched-${j}-date`}
                            type="date"
                            aria-label={`${t.form.line} ${i + 1} ${t.form.shipDate} ${j + 1}`}
                            value={schedule.shipDate}
                            onChange={(e) =>
                              setLine(i, {
                                schedules: line.schedules.map((s, k) =>
                                  k === j ? { ...s, shipDate: e.target.value } : s,
                                ),
                              })
                            }
                            className={CONTROL}
                          />
                        </div>
                        <div>
                          <label htmlFor={`order-line-${i}-sched-${j}-qty`} className="sr-only">
                            {t.form.qty}
                          </label>
                          <input
                            id={`order-line-${i}-sched-${j}-qty`}
                            type="number"
                            min="0"
                            step="any"
                            aria-label={`${t.form.line} ${i + 1} ${t.form.qty} ${j + 1}`}
                            value={schedule.qty}
                            onChange={(e) =>
                              setLine(i, {
                                schedules: line.schedules.map((s, k) =>
                                  k === j ? { ...s, qty: e.target.value } : s,
                                ),
                              })
                            }
                            className={CONTROL}
                          />
                        </div>
                        <button
                          type="button"
                          onClick={() =>
                            setLine(i, {
                              schedules: line.schedules.filter((_, k) => k !== j),
                            })
                          }
                          aria-label={`${t.form.removeSchedule} ${j + 1}`}
                          className="flex h-10 items-center justify-center rounded-lg border border-border px-3 text-sm font-medium text-sidebar-foreground transition-colors hover:border-primary hover:text-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
                        >
                          {t.form.removeSchedule}
                        </button>
                      </div>
                    ))}
                  </div>
                  <button
                    type="button"
                    onClick={() =>
                      setLine(i, {
                        schedules: [...line.schedules, { shipDate: "", qty: "" }],
                      })
                    }
                    className="mt-3 inline-flex h-10 items-center gap-2 rounded-lg border border-border px-3 text-sm font-medium text-sidebar-foreground transition-colors hover:border-primary hover:text-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
                  >
                    <PlusIcon />
                    {t.form.addSchedule}
                  </button>
                </div>
              </div>
            ))}
          </div>
          <button
            type="button"
            onClick={() =>
              setLines([...lines, { styleVersionId: "", price: "", qtys: {}, schedules: [{ shipDate: "", qty: "" }] }])
            }
            className="mt-3 inline-flex h-10 items-center gap-2 rounded-lg border border-border px-3 text-sm font-medium text-sidebar-foreground transition-colors hover:border-primary hover:text-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
          >
            <PlusIcon />
            {t.form.addLine}
          </button>
        </div>
        <div className="mt-2 flex items-center justify-end gap-2">
          <Link
            href={`/${lang}/orders`}
            className="flex h-10 items-center rounded-lg border border-border px-4 text-sm font-medium text-sidebar-foreground transition-colors hover:border-primary hover:text-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
          >
            {t.form.cancel}
          </Link>
          <button
            type="submit"
            disabled={pending || problems.length > 0}
            className="flex h-10 items-center justify-center rounded-lg bg-primary px-4 text-sm font-medium text-primary-foreground transition-opacity hover:opacity-90 disabled:opacity-60 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
          >
            {pending ? t.form.submitting : t.form.submit}
          </button>
        </div>
      </form>
    </section>
  );
}
