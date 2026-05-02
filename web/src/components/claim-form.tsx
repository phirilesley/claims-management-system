"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useFieldArray, useForm, type Resolver } from "react-hook-form";
import { z } from "zod";
import { apiJson } from "@/lib/api";

const lineSchema = z.object({
  description: z.string().min(1, "Description is required"),
  quantity: z.preprocess(
    (v) => (typeof v === "string" ? Number.parseFloat(v) : v),
    z.number().positive()
  ),
  unitAmount: z.preprocess(
    (v) => (typeof v === "string" ? Number.parseFloat(v) : v),
    z.number().min(0)
  ),
  category: z.string().optional(),
});

const claimFormSchema = z.object({
  claimTypeId: z.string().uuid("Pick a claim type"),
  currencyId: z.string().uuid("Pick a currency"),
  title: z.string().min(1).max(512),
  lines: z.array(lineSchema).min(1, "Add at least one line"),
});

export type ClaimFormValues = {
  claimTypeId: string;
  currencyId: string;
  title: string;
  lines: {
    description: string;
    quantity: number;
    unitAmount: number;
    category?: string;
  }[];
};

type ClaimTypeOption = { id: string; code: string; name: string };
type CurrencyOption = { id: string; code: string; name: string };

type CreateClaimPayload = {
  claimTypeId: string;
  title: string;
  currencyId: string;
  submit: boolean;
  dynamicDataJson: string | null;
  bankDetailsJson: string | null;
  lines: {
    lineNumber: number;
    description: string;
    quantity: number;
    unitAmount: number;
    category: string | null;
    mileageKm: number | null;
    perDiemDays: number | null;
    metadataJson: string | null;
  }[];
};

type ClaimSummary = {
  id: string;
  referenceNumber: string;
  title: string;
  status: string;
  currencyCode: string;
  totalAmount: number;
  createdAtUtc: string;
  submittedAtUtc: string | null;
};

export function ClaimForm({
  accessToken,
}: {
  accessToken: string;
}) {
  const queryClient = useQueryClient();

  const claimTypesQuery = useQuery({
    queryKey: ["catalog", "claim-types", accessToken],
    queryFn: () =>
      apiJson<ClaimTypeOption[]>("/api/catalog/claim-types", {
        accessToken,
      }),
  });

  const currenciesQuery = useQuery({
    queryKey: ["catalog", "currencies", accessToken],
    queryFn: () =>
      apiJson<CurrencyOption[]>("/api/catalog/currencies", {
        accessToken,
      }),
  });

  const form = useForm<ClaimFormValues>({
    resolver: zodResolver(claimFormSchema) as Resolver<ClaimFormValues>,
    defaultValues: {
      title: "",
      claimTypeId: "",
      currencyId: "",
      lines: [{ description: "", quantity: 1, unitAmount: 0, category: "" }],
    },
  });

  const { fields, append, remove } = useFieldArray({
    control: form.control,
    name: "lines",
  });

  const createMutation = useMutation({
    mutationFn: async (payload: { values: ClaimFormValues; submit: boolean }) => {
      const { values, submit } = payload;
      const body: CreateClaimPayload = {
        claimTypeId: values.claimTypeId,
        title: values.title,
        currencyId: values.currencyId,
        submit,
        dynamicDataJson: null,
        bankDetailsJson: null,
        lines: values.lines.map((line, index) => ({
          lineNumber: index + 1,
          description: line.description,
          quantity: line.quantity,
          unitAmount: line.unitAmount,
          category: line.category?.trim() ? line.category : null,
          mileageKm: null,
          perDiemDays: null,
          metadataJson: null,
        })),
      };
      return apiJson<ClaimSummary>("/api/claims", {
        method: "POST",
        accessToken,
        body: JSON.stringify(body),
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["claims"] });
      queryClient.invalidateQueries({ queryKey: ["approvals", "queue"] });
      form.reset({
        title: "",
        claimTypeId: form.getValues("claimTypeId"),
        currencyId: form.getValues("currencyId"),
        lines: [{ description: "", quantity: 1, unitAmount: 0, category: "" }],
      });
    },
  });

  const catalogLoading = claimTypesQuery.isLoading || currenciesQuery.isLoading;
  const catalogError =
    claimTypesQuery.error ?? currenciesQuery.error ?? createMutation.error;

  return (
    <div className="rounded-xl border border-slate-800 bg-slate-900/60 p-6 shadow-lg shadow-slate-950/40">
      <h2 className="text-lg font-medium">New claim</h2>
      <p className="mt-1 text-sm text-slate-400">
        Multi-line items; save as draft or submit into the approval workflow.
      </p>

      {catalogLoading && (
        <p className="mt-4 text-sm text-slate-400">Loading catalog…</p>
      )}

      {(claimTypesQuery.isError || currenciesQuery.isError) && (
        <p className="mt-4 text-sm text-rose-400">
          {(catalogError as Error).message}
        </p>
      )}

      {claimTypesQuery.data &&
        currenciesQuery.data &&
        claimTypesQuery.data.length > 0 &&
        currenciesQuery.data.length > 0 && (
          <form
            className="mt-6 space-y-6"
            onSubmit={form.handleSubmit((values) =>
              createMutation.mutate({ values, submit: true })
            )}
          >
            <div className="grid gap-4 sm:grid-cols-2">
              <div>
                <label className="text-xs font-medium text-slate-400">
                  Claim type
                </label>
                <select
                  className="mt-1 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm outline-none ring-emerald-500/40 focus:ring-2"
                  {...form.register("claimTypeId")}
                >
                  <option value="">Select…</option>
                  {claimTypesQuery.data.map((t) => (
                    <option key={t.id} value={t.id}>
                      {t.name} ({t.code})
                    </option>
                  ))}
                </select>
                {form.formState.errors.claimTypeId && (
                  <p className="mt-1 text-xs text-rose-400">
                    {form.formState.errors.claimTypeId.message}
                  </p>
                )}
              </div>
              <div>
                <label className="text-xs font-medium text-slate-400">
                  Currency
                </label>
                <select
                  className="mt-1 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm outline-none ring-emerald-500/40 focus:ring-2"
                  {...form.register("currencyId")}
                >
                  <option value="">Select…</option>
                  {currenciesQuery.data.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.code} — {c.name}
                    </option>
                  ))}
                </select>
                {form.formState.errors.currencyId && (
                  <p className="mt-1 text-xs text-rose-400">
                    {form.formState.errors.currencyId.message}
                  </p>
                )}
              </div>
            </div>

            <div>
              <label className="text-xs font-medium text-slate-400">Title</label>
              <input
                className="mt-1 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm outline-none ring-emerald-500/40 focus:ring-2"
                placeholder="e.g. Field travel — March"
                {...form.register("title")}
              />
              {form.formState.errors.title && (
                <p className="mt-1 text-xs text-rose-400">
                  {form.formState.errors.title.message}
                </p>
              )}
            </div>

            <div className="space-y-4">
              <div className="flex items-center justify-between">
                <span className="text-sm font-medium text-slate-200">
                  Line items
                </span>
                <button
                  type="button"
                  onClick={() =>
                    append({ description: "", quantity: 1, unitAmount: 0, category: "" })
                  }
                  className="text-xs font-medium text-emerald-400 hover:text-emerald-300"
                >
                  + Add line
                </button>
              </div>

              {fields.map((field, index) => (
                <div
                  key={field.id}
                  className="rounded-lg border border-slate-800 bg-slate-950/80 p-4"
                >
                  <div className="mb-3 flex items-center justify-between">
                    <span className="text-xs uppercase tracking-wide text-slate-500">
                      Line {index + 1}
                    </span>
                    {fields.length > 1 && (
                      <button
                        type="button"
                        onClick={() => remove(index)}
                        className="text-xs text-rose-400 hover:text-rose-300"
                      >
                        Remove
                      </button>
                    )}
                  </div>
                  <div className="grid gap-3 sm:grid-cols-2">
                    <div className="sm:col-span-2">
                      <label className="text-xs text-slate-400">Description</label>
                      <input
                        className="mt-1 w-full rounded border border-slate-700 bg-slate-950 px-2 py-1.5 text-sm"
                        {...form.register(`lines.${index}.description`)}
                      />
                      {form.formState.errors.lines?.[index]?.description && (
                        <p className="mt-1 text-xs text-rose-400">
                          {form.formState.errors.lines[index]?.description?.message}
                        </p>
                      )}
                    </div>
                    <div>
                      <label className="text-xs text-slate-400">Qty</label>
                      <input
                        type="number"
                        step="any"
                        className="mt-1 w-full rounded border border-slate-700 bg-slate-950 px-2 py-1.5 text-sm"
                        {...form.register(`lines.${index}.quantity`, {
                          valueAsNumber: true,
                        })}
                      />
                    </div>
                    <div>
                      <label className="text-xs text-slate-400">Unit amount</label>
                      <input
                        type="number"
                        step="any"
                        className="mt-1 w-full rounded border border-slate-700 bg-slate-950 px-2 py-1.5 text-sm"
                        {...form.register(`lines.${index}.unitAmount`, {
                          valueAsNumber: true,
                        })}
                      />
                    </div>
                    <div className="sm:col-span-2">
                      <label className="text-xs text-slate-400">
                        Category (optional)
                      </label>
                      <input
                        className="mt-1 w-full rounded border border-slate-700 bg-slate-950 px-2 py-1.5 text-sm"
                        {...form.register(`lines.${index}.category`)}
                      />
                    </div>
                  </div>
                </div>
              ))}
              {form.formState.errors.lines &&
                typeof form.formState.errors.lines.message === "string" && (
                  <p className="text-xs text-rose-400">
                    {form.formState.errors.lines.message}
                  </p>
                )}
            </div>

            {createMutation.isError && (
              <p className="text-sm text-rose-400">
                {(createMutation.error as Error).message}
              </p>
            )}

            <div className="flex flex-wrap gap-3">
              <button
                type="button"
                disabled={createMutation.isPending}
                onClick={form.handleSubmit((values) =>
                  createMutation.mutate({ values, submit: false })
                )}
                className="rounded-lg border border-slate-600 px-4 py-2 text-sm text-slate-200 hover:bg-slate-800 disabled:opacity-60"
              >
                {createMutation.isPending ? "Saving…" : "Save draft"}
              </button>
              <button
                type="submit"
                disabled={createMutation.isPending}
                className="rounded-lg bg-emerald-500 px-4 py-2 text-sm font-medium text-emerald-950 hover:bg-emerald-400 disabled:opacity-60"
              >
                {createMutation.isPending ? "Submitting…" : "Submit claim"}
              </button>
            </div>
          </form>
        )}
    </div>
  );
}
