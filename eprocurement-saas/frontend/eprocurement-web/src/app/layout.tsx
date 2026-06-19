import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "E-Procurement SaaS",
  description: "SaaS-based e-procurement and tender management platform"
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
