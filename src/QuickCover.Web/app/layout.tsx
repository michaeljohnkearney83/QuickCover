import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import Link from "next/link";
import { CustomerSwitcher } from "@/components/CustomerSwitcher";
import "./globals.css";

const geistSans = Geist({ variable: "--font-geist-sans", subsets: ["latin"] });
const geistMono = Geist_Mono({ variable: "--font-geist-mono", subsets: ["latin"] });

export const metadata: Metadata = {
  title: "QuickCover: taxi insurance",
  description: "Quote and buy taxi insurance in a couple of minutes.",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="en-GB" className={`${geistSans.variable} ${geistMono.variable} h-full antialiased`}>
      <body className="flex min-h-full flex-col font-sans">
        <header className="border-b border-border bg-surface">
          <nav className="mx-auto flex max-w-3xl flex-wrap items-center gap-x-6 gap-y-2 px-4 py-3">
            <Link href="/" className="text-lg font-semibold tracking-tight">
              Quick<span className="text-accent">Cover</span>
            </Link>
            <Link href="/" className="text-sm text-muted hover:text-foreground">
              Get a quote
            </Link>
            <Link href="/policies" className="text-sm text-muted hover:text-foreground">
              My policies
            </Link>
            <div className="ml-auto">
              <CustomerSwitcher />
            </div>
          </nav>
        </header>
        <main className="mx-auto w-full max-w-3xl flex-1 px-4 py-8">{children}</main>
        <footer className="mx-auto w-full max-w-3xl px-4 py-6 text-xs text-muted">
          Practice project. Payments are simulated and no real cover is provided.
        </footer>
      </body>
    </html>
  );
}
