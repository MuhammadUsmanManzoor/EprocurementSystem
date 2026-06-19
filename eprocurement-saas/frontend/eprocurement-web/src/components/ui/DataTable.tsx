"use client";

import { ArrowDownUp } from "lucide-react";
import { ReactNode, useMemo, useState } from "react";
import { Input } from "./Input";

export type Column<T> = {
  key: keyof T & string;
  label: string;
  render?: (item: T) => ReactNode;
};

export function DataTable<T extends Record<string, unknown>>({ data, columns, searchPlaceholder = "Search records" }: { data: T[]; columns: Column<T>[]; searchPlaceholder?: string }) {
  const [search, setSearch] = useState("");
  const [sortKey, setSortKey] = useState<string>(columns[0]?.key ?? "");
  const [sortAsc, setSortAsc] = useState(true);

  const filtered = useMemo(() => {
    return data
      .filter((item) => JSON.stringify(item).toLowerCase().includes(search.toLowerCase()))
      .sort((a, b) => {
        const av = String(a[sortKey] ?? "");
        const bv = String(b[sortKey] ?? "");
        return sortAsc ? av.localeCompare(bv) : bv.localeCompare(av);
      });
  }, [data, search, sortAsc, sortKey]);

  function onSort(key: string) {
    if (sortKey === key) {
      setSortAsc((value) => !value);
      return;
    }
    setSortKey(key);
    setSortAsc(true);
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <Input className="max-w-sm" placeholder={searchPlaceholder} value={search} onChange={(event) => setSearch(event.target.value)} />
        <span className="text-sm text-slate-500">{filtered.length} records</span>
      </div>
      <div className="overflow-hidden rounded-lg border border-line bg-white">
        <div className="overflow-x-auto">
          <table className="min-w-full text-left text-sm">
            <thead className="bg-slate-50 text-xs uppercase text-slate-500">
              <tr>
                {columns.map((column) => (
                  <th key={column.key} className="whitespace-nowrap px-4 py-3 font-semibold">
                    <button className="flex items-center gap-2" onClick={() => onSort(column.key)} type="button">
                      {column.label}
                      <ArrowDownUp size={13} />
                    </button>
                  </th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-line">
              {filtered.map((item, index) => (
                <tr key={String(item.id ?? index)} className="hover:bg-slate-50">
                  {columns.map((column) => (
                    <td key={column.key} className="whitespace-nowrap px-4 py-3 text-slate-700">
                      {column.render ? column.render(item) : String(item[column.key] ?? "")}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
