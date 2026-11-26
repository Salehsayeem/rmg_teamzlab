"use client";

import { useActionState, useState, useTransition } from "react";
import Link from "next/link";
import { AlertIcon } from "@/components/icons";
import { cancelOrder, confirmOrder, type ConfirmState, type OrderFormState } from "@/lib/actions/orders";
import type { Dict, Lang } from "@/lib/i18n/getDict";

const BTN =
  "flex h-10 items-center gap-2 rounded-lg px-4 text-sm font-medium transition-opacity focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring";
const GHOST =
  "flex h-10 items-center gap-2 rounded-lg border border-border px-4 text-sm font-medium text-sidebar-foreground transition-colors hover:border-primary hover:text-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring";

export type OrderActionsProps = {
  lang: Lang;
  dict: Dict;
  orderId: string;
  status: string;
  revision: number;
  canConfirm: boolean;
  canEdit: boolean;
};

const INITIAL: OrderFormState = { status: "idle" };

export function OrderActions({ lang, dict, orderId, status, revision, canConfirm, canEdit }: OrderActionsProps) {
  const t = dict.orders;
  const [confirmState, setConfirmState] = useState<ConfirmState>({ status: "idle" });
  const [confirmPending, startConfirm] = useTransition();
  const [cancelOpen, setCancelOpen] = useState(false);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [cancelState, cancelFormAction, cancelPending] = useActionState(cancelOrder.bind(null, lang, orderId), INITIAL);

  const onConfirm = () =>
    startConfirm(async () => {
      setConfirmState({ status: "idle" });
      const result = await confirmOrder(lang, orderId, revision);
      // redirect() on success unwinds with its own sentinel — only failures land here.
      setConfirmState(result);
      setConfirmOpen(false);
    });

  const isDraft = status === "Draft";
  const isActive = status === "Draft" || status === "Confirmed";

  return (
    <section aria-label={t.actions.confirm} className="rounded-lg border border-border bg-card p-6">
      {confirmState.status === "conflict" && (
        <div role="alert" className="mb-4 flex flex-wrap items-center gap-3 rounded-lg bg-lightwarning p-4">
          <span aria-hidden="true" className="text-warning">
            <AlertIcon />
          </span>
          <p className="text-sm font-medium">{t.actions.conflict}</p>
          {confirmState.currentRevision !== undefined && (
            <p className="text-sm text-sidebar-foreground">
              {t.actions.currentRevision}: {confirmState.currentRevision}
            </p>
          )}
          <Link
            href={`/${lang}/orders/${orderId}`}
            className="ms-auto flex h-9 items-center rounded-lg border border-border bg-background px-3 text-sm font-medium transition-colors hover:border-primary hover:text-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
          >
            {t.actions.refresh}
          </Link>
        </div>
      )}
      {confirmState.status === "error" && confirmState.message && (
        <div role="alert" className="mb-4 flex gap-3 rounded-lg bg-lighterror p-4">
          <span aria-hidden="true" className="mt-0.5 text-error">
            <AlertIcon />
          </span>
          <p className="min-w-0 text-sm font-medium">{confirmState.message}</p>
        </div>
      )}
      {cancelState.status === "error" && cancelState.message && (
        <div role="alert" className="mb-4 flex gap-3 rounded-lg bg-lighterror p-4">
          <span aria-hidden="true" className="mt-0.5 text-error">
            <AlertIcon />
          </span>
          <p className="min-w-0 text-sm font-medium">{cancelState.message}</p>
        </div>
      )}
      {cancelState.status === "error" && cancelState.fieldErrors?.reason?.[0] && (
        <p role="alert" className="mb-4 text-xs text-error">{cancelState.fieldErrors.reason[0]}</p>
      )}
      <div className="flex flex-wrap items-center gap-3">
        {isDraft && (
          <div className="flex flex-col gap-1">
            <button
              type="button"
              onClick={() => setConfirmOpen(true)}
              disabled={!canConfirm || confirmPending}
              className={`${BTN} bg-success text-primary-foreground transition-opacity hover:opacity-90 disabled:opacity-60`}
            >
              {confirmPending ? t.actions.confirming : t.actions.confirm}
            </button>
            {!canConfirm && <p className="text-xs text-sidebar-foreground">{t.actions.permissionHint}</p>}
          </div>
        )}
        {isDraft && confirmOpen && (
          <form
            onSubmit={(e) => {
              e.preventDefault();
              onConfirm();
            }}
            className="flex w-full flex-col gap-2 rounded-lg bg-muted p-4"
          >
            <p className="text-sm font-medium">{t.detail.confirmTitle}</p>
            <p className="text-sm text-sidebar-foreground">{t.detail.confirmBody}</p>
            <div className="flex justify-end gap-2">
              <button type="button" onClick={() => setConfirmOpen(false)} className={GHOST}>
                {t.form.cancel}
                </button>
              <button type="submit" disabled={confirmPending} className={`${BTN} bg-success text-primary-foreground disabled:opacity-60`}>
                {confirmPending ? t.actions.confirming : t.actions.confirm}
              </button>
            </div>
          </form>
        )}
        {isActive && canEdit && (
          <button
            type="button"
            onClick={() => setCancelOpen(!cancelOpen)}
            aria-expanded={cancelOpen}
            className={GHOST}
          >
            {t.actions.cancel}
          </button>
        )}
        {isActive && !canEdit && (
          <div className="flex flex-col gap-1">
            <span aria-disabled="true" className={`${GHOST} opacity-50`}>{t.actions.cancel}</span>
            <p className="text-xs text-sidebar-foreground">{t.actions.permissionHint}</p>
          </div>
        )}
      </div>
      {cancelOpen && (
        <form action={cancelFormAction} className="mt-4 flex flex-col gap-3 rounded-lg bg-muted p-4" noValidate>
          <p className="text-sm font-medium">{t.detail.cancelTitle}</p>
          <p className="text-sm text-sidebar-foreground">{t.detail.cancelBody}</p>
          <label htmlFor="order-cancel-reason" className="text-sm font-medium">{t.actions.reason}</label>
          <textarea
            id="order-cancel-reason"
            name="reason"
            rows={2}
            placeholder={t.actions.reasonPlaceholder}
            className="min-h-20 rounded-lg border border-input bg-background p-3 text-sm text-foreground placeholder:text-sidebar-foreground focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
          />
          <div className="flex justify-end gap-2">
            <button
              type="button"
              onClick={() => setCancelOpen(false)}
              className={GHOST}
            >
              {t.form.cancel}
            </button>
            <button
              type="submit"
              disabled={cancelPending}
              className={`${BTN} bg-error text-primary-foreground transition-opacity hover:opacity-90 disabled:opacity-60`}
            >
              {cancelPending ? t.actions.cancelling : t.actions.confirmCancel}
            </button>
          </div>
        </form>
      )}
    </section>
  );
}
