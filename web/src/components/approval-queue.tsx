"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { apiJson } from "@/lib/api";

type ApprovalQueueItem = {
  claimId: string;
  referenceNumber: string;
  title: string;
  totalAmount: number;
  currencyCode: string;
  submitterName: string;
  pendingStepName: string;
  stepOrder: number;
  submittedAtUtc: string | null;
};

export function ApprovalQueue({ accessToken }: { accessToken: string }) {
  const queryClient = useQueryClient();
  const [rejectingId, setRejectingId] = useState<string | null>(null);
  const [rejectComment, setRejectComment] = useState("");

  const queueQuery = useQuery({
    queryKey: ["approvals", "queue", accessToken],
    queryFn: () =>
      apiJson<ApprovalQueueItem[]>("/api/approvals/queue", {
        accessToken,
      }),
  });

  const approveMutation = useMutation({
    mutationFn: async (claimId: string) =>
      apiJson<unknown>(`/api/claims/${claimId}/approve`, {
        method: "POST",
        accessToken,
        body: JSON.stringify({}),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["approvals", "queue"] });
      queryClient.invalidateQueries({ queryKey: ["claims"] });
    },
  });

  const rejectMutation = useMutation({
    mutationFn: async ({ claimId, comment }: { claimId: string; comment: string }) =>
      apiJson<unknown>(`/api/claims/${claimId}/reject`, {
        method: "POST",
        accessToken,
        body: JSON.stringify({ comment: comment || null }),
      }),
    onSuccess: () => {
      setRejectingId(null);
      setRejectComment("");
      queryClient.invalidateQueries({ queryKey: ["approvals", "queue"] });
      queryClient.invalidateQueries({ queryKey: ["claims"] });
    },
  });

  return (
    <div className="rounded-xl border border-amber-900/40 bg-amber-950/20 p-6">
      <h2 className="text-lg font-medium text-amber-100">Approval queue</h2>
      <p className="mt-1 text-sm text-amber-200/70">
        Claims from <span className="text-amber-100">other users</span> in your
        tenant, at your role&apos;s current workflow step. Admins can act on all
        steps.
      </p>

      {queueQuery.isLoading && (
        <p className="mt-4 text-sm text-slate-400">Loading queue…</p>
      )}
      {queueQuery.isError && (
        <p className="mt-4 text-sm text-rose-400">
          {(queueQuery.error as Error).message}
        </p>
      )}
      {queueQuery.data?.length === 0 && !queueQuery.isLoading && (
        <p className="mt-4 text-sm text-slate-500">
          No items. Submit a claim as another user, or wait for colleagues.
        </p>
      )}

      <ul className="mt-4 space-y-4">
        {queueQuery.data?.map((item) => (
          <li
            key={item.claimId}
            className="rounded-lg border border-slate-800 bg-slate-950/60 p-4"
          >
            <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
              <div>
                <p className="font-medium text-slate-100">{item.title}</p>
                <p className="text-xs text-slate-500">
                  {item.referenceNumber} · {item.submitterName}
                </p>
                <p className="mt-2 text-sm text-slate-300">
                  {item.currencyCode}{" "}
                  {item.totalAmount.toLocaleString(undefined, {
                    minimumFractionDigits: 2,
                  })}
                </p>
                <p className="mt-1 text-xs text-amber-300/90">
                  Awaiting: {item.pendingStepName} (step {item.stepOrder})
                </p>
              </div>
              <div className="flex flex-col gap-2 sm:items-end">
                <div className="flex flex-wrap gap-2">
                  <button
                    type="button"
                    disabled={approveMutation.isPending}
                    onClick={() => approveMutation.mutate(item.claimId)}
                    className="rounded-lg bg-emerald-500 px-3 py-1.5 text-sm font-medium text-emerald-950 hover:bg-emerald-400 disabled:opacity-50"
                  >
                    Approve
                  </button>
                  <button
                    type="button"
                    onClick={() =>
                      setRejectingId((id) =>
                        id === item.claimId ? null : item.claimId
                      )
                    }
                    className="rounded-lg border border-rose-800/60 px-3 py-1.5 text-sm text-rose-200 hover:bg-rose-950/50"
                  >
                    {rejectingId === item.claimId ? "Cancel" : "Reject"}
                  </button>
                </div>
                {rejectingId === item.claimId && (
                  <div className="w-full min-w-[220px] sm:max-w-sm">
                    <label className="text-xs text-slate-500">
                      Comment (optional)
                    </label>
                    <textarea
                      className="mt-1 w-full rounded border border-slate-700 bg-slate-950 px-2 py-1.5 text-sm"
                      rows={2}
                      value={rejectComment}
                      onChange={(e) => setRejectComment(e.target.value)}
                    />
                    <button
                      type="button"
                      disabled={rejectMutation.isPending}
                      onClick={() =>
                        rejectMutation.mutate({
                          claimId: item.claimId,
                          comment: rejectComment,
                        })
                      }
                      className="mt-2 rounded bg-rose-600 px-3 py-1.5 text-sm text-white hover:bg-rose-500 disabled:opacity-50"
                    >
                      Confirm reject
                    </button>
                  </div>
                )}
              </div>
            </div>
          </li>
        ))}
      </ul>

      {(approveMutation.isError || rejectMutation.isError) && (
        <p className="mt-4 text-sm text-rose-400">
          {(approveMutation.error ?? rejectMutation.error as Error).message}
        </p>
      )}
    </div>
  );
}
