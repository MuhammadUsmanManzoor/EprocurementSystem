"use client";

import { AlertCircle } from "lucide-react";
import { Button } from "./Button";
import { Modal } from "./Modal";

export function ConfirmationDialog({
  open,
  title,
  description,
  confirmLabel = "Confirm",
  onConfirm,
  onCancel
}: {
  open: boolean;
  title: string;
  description: string;
  confirmLabel?: string;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  return (
    <Modal open={open} title={title} onClose={onCancel}>
      <div className="flex gap-3">
        <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-md bg-amber-50 text-amber-700">
          <AlertCircle size={20} />
        </div>
        <p className="text-sm leading-6 text-slate-600">{description}</p>
      </div>
      <div className="mt-6 flex justify-end gap-3">
        <Button variant="secondary" onClick={onCancel} type="button">Cancel</Button>
        <Button onClick={onConfirm} type="button">{confirmLabel}</Button>
      </div>
    </Modal>
  );
}
