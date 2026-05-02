"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { ApprovalQueue } from "@/components/approval-queue";
import { ClaimForm } from "@/components/claim-form";
import { apiJson } from "@/lib/api";
import {
  clearTokens,
  getAccessToken,
  getRefreshToken,
  saveTokens,
} from "@/lib/auth-storage";

const loginSchema = z.object({
  email: z.string().email(),
  password: z.string().min(1),
});

type LoginValues = z.infer<typeof loginSchema>;

type TokenResponse = {
  accessToken: string;
  refreshToken: string;
  expiresAtUnixSeconds: number;
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

export default function HomePage() {
  const queryClient = useQueryClient();
  const [token, setToken] = useState<string | null>(null);

  useEffect(() => {
    setToken(getAccessToken());
  }, []);

  const form = useForm<LoginValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: "admin@demo.local", password: "Demo123!" },
  });

  const loginMutation = useMutation({
    mutationFn: async (values: LoginValues) =>
      apiJson<TokenResponse>("/api/auth/login", {
        method: "POST",
        body: JSON.stringify(values),
      }),
    onSuccess: (data) => {
      saveTokens(data.accessToken, data.refreshToken);
      setToken(data.accessToken);
      queryClient.invalidateQueries({ queryKey: ["claims"] });
    },
  });

  const claimsQuery = useQuery({
    queryKey: ["claims", token],
    enabled: Boolean(token),
    queryFn: () =>
      apiJson<ClaimSummary[]>("/api/claims", {
        accessToken: token!,
      }),
  });

  const logout = () => {
    clearTokens();
    setToken(null);
    queryClient.removeQueries({ queryKey: ["claims"] });
    queryClient.removeQueries({ queryKey: ["catalog"] });
    queryClient.removeQueries({ queryKey: ["approvals"] });
  };

  const apiHint = useMemo(
    () =>
      process.env.NEXT_PUBLIC_API_URL ?? "https://localhost:7272 (default)",
    []
  );

  return (
    <main className="mx-auto flex max-w-6xl flex-col gap-10 px-6 py-12">
      <header className="space-y-2 border-b border-slate-800 pb-8">
        <p className="text-xs uppercase tracking-[0.2em] text-slate-500">
          Claims Management SaaS
        </p>
        <h1 className="text-3xl font-semibold tracking-tight">
          Dashboard preview
        </h1>
        <p className="max-w-2xl text-sm text-slate-400">
          Next.js talks to the ASP.NET Core API over HTTPS. Set{" "}
          <code className="rounded bg-slate-900 px-1.5 py-0.5 text-slate-200">
            NEXT_PUBLIC_API_URL
          </code>{" "}
          to match your API ({apiHint}).
        </p>
      </header>

      <section className="grid gap-8 lg:grid-cols-2">
        <div className="rounded-xl border border-slate-800 bg-slate-900/60 p-6 shadow-lg shadow-slate-950/40">
          <h2 className="text-lg font-medium">Sign in</h2>
          <p className="mt-1 text-sm text-slate-400">
            Seed users (fresh DB):{" "}
            <span className="text-slate-200">admin@demo.local</span> (approves) ·{" "}
            <span className="text-slate-200">submitter@demo.local</span> (submits
            claims for others to approve). Password{" "}
            <span className="text-slate-200">Demo123!</span>
          </p>
          <form
            className="mt-6 space-y-4"
            onSubmit={form.handleSubmit((v) => loginMutation.mutate(v))}
          >
            <div>
              <label className="text-xs font-medium text-slate-400">
                Email
              </label>
              <input
                type="email"
                autoComplete="username"
                className="mt-1 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm outline-none ring-emerald-500/40 focus:ring-2"
                {...form.register("email")}
              />
              {form.formState.errors.email && (
                <p className="mt-1 text-xs text-rose-400">
                  {form.formState.errors.email.message}
                </p>
              )}
            </div>
            <div>
              <label className="text-xs font-medium text-slate-400">
                Password
              </label>
              <input
                type="password"
                autoComplete="current-password"
                className="mt-1 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm outline-none ring-emerald-500/40 focus:ring-2"
                {...form.register("password")}
              />
              {form.formState.errors.password && (
                <p className="mt-1 text-xs text-rose-400">
                  {form.formState.errors.password.message}
                </p>
              )}
            </div>
            {loginMutation.isError && (
              <p className="text-sm text-rose-400">
                {(loginMutation.error as Error).message}
              </p>
            )}
            <div className="flex gap-3">
              <button
                type="submit"
                disabled={loginMutation.isPending}
                className="inline-flex items-center justify-center rounded-lg bg-emerald-500 px-4 py-2 text-sm font-medium text-emerald-950 transition hover:bg-emerald-400 disabled:opacity-60"
              >
                {loginMutation.isPending ? "Signing in…" : "Sign in"}
              </button>
              {token && (
                <button
                  type="button"
                  onClick={logout}
                  className="rounded-lg border border-slate-700 px-4 py-2 text-sm text-slate-200 hover:bg-slate-800"
                >
                  Sign out
                </button>
              )}
            </div>
          </form>
        </div>

        <div className="rounded-xl border border-slate-800 bg-slate-900/60 p-6">
          <h2 className="text-lg font-medium">Your claims</h2>
          <p className="mt-1 text-sm text-slate-400">
            TanStack Query loads{" "}
            <code className="rounded bg-slate-950 px-1">GET /api/claims</code>{" "}
            when a JWT is present.
          </p>
          <div className="mt-4 space-y-3">
            {!token && (
              <p className="text-sm text-slate-500">
                Sign in to load claims from PostgreSQL via the API.
              </p>
            )}
            {token && claimsQuery.isLoading && (
              <p className="text-sm text-slate-400">Loading claims…</p>
            )}
            {token && claimsQuery.isError && (
              <p className="text-sm text-rose-400">
                {(claimsQuery.error as Error).message}
              </p>
            )}
            {token && claimsQuery.data?.length === 0 && (
              <p className="text-sm text-slate-400">
                No claims yet. Use the form below to create one.
              </p>
            )}
            {claimsQuery.data?.map((c) => (
              <div
                key={c.id}
                className="rounded-lg border border-slate-800 bg-slate-950/80 px-4 py-3"
              >
                <div className="flex items-center justify-between gap-3">
                  <div>
                    <p className="font-medium text-slate-100">{c.title}</p>
                    <p className="text-xs text-slate-500">{c.referenceNumber}</p>
                  </div>
                  <span className="rounded-full bg-slate-800 px-2 py-1 text-xs uppercase tracking-wide text-emerald-300">
                    {c.status}
                  </span>
                </div>
                <p className="mt-2 text-sm text-slate-300">
                  {c.currencyCode}{" "}
                  {c.totalAmount.toLocaleString(undefined, {
                    minimumFractionDigits: 2,
                  })}
                </p>
              </div>
            ))}
          </div>
          <p className="mt-6 text-xs text-slate-500">
            Refresh token stored:{" "}
            {typeof window !== "undefined" && getRefreshToken()
              ? "yes"
              : "no"}
          </p>
        </div>
      </section>

      {token && (
        <section className="space-y-8">
          <ApprovalQueue accessToken={token} />
          <ClaimForm accessToken={token} />
        </section>
      )}
    </main>
  );
}
