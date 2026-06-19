import { Card, CardBody, CardHeader } from "./Card";

export function FormSection({ title, description, children }: { title: string; description?: string; children: React.ReactNode }) {
  return (
    <Card>
      <CardHeader>
        <h2 className="text-base font-semibold text-ink">{title}</h2>
        {description ? <p className="mt-1 text-sm text-slate-600">{description}</p> : null}
      </CardHeader>
      <CardBody>{children}</CardBody>
    </Card>
  );
}
