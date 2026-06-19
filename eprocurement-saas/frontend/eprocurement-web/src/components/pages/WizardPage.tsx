"use client";

import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { Card, CardBody } from "@/components/ui/Card";
import { FormSection } from "@/components/ui/FormSection";
import { Input } from "@/components/ui/Input";
import { PageHeader } from "@/components/ui/PageHeader";
import { Select } from "@/components/ui/Select";
import { Textarea } from "@/components/ui/Textarea";
import { Wizard } from "@/components/ui/Wizard";
import { useToast } from "@/components/ui/ToastProvider";

export function WizardPage({ title, description, steps }: { title: string; description: string; steps: string[] }) {
  const [step, setStep] = useState(0);
  const [error, setError] = useState("");
  const { showToast } = useToast();

  function next() {
    setError("");
    if (step === 0) {
      const value = (document.getElementById("wizard-title") as HTMLInputElement | null)?.value;
      if (!value?.trim()) {
        setError("Title is required before continuing.");
        return;
      }
    }
    if (step < steps.length - 1) {
      setStep((value) => value + 1);
      return;
    }
    showToast("Submitted", `${title} has been validated and is ready for API submission.`);
  }

  return (
    <>
      <PageHeader title={title} description={description} />
      <Card className="mb-5">
        <CardBody>
          <Wizard steps={steps} currentStep={step} />
        </CardBody>
      </Card>
      <FormSection title={steps[step]} description="Complete the required fields and continue through the workflow.">
        <div className="grid gap-4 md:grid-cols-2">
          <div>
            <label className="mb-2 block text-sm font-medium text-slate-700">Title</label>
            <Input id="wizard-title" placeholder="Enter a concise business title" />
          </div>
          <div>
            <label className="mb-2 block text-sm font-medium text-slate-700">Category</label>
            <Select>
              <option>IT Services</option>
              <option>Facilities</option>
              <option>Consulting</option>
              <option>Office Supplies</option>
            </Select>
          </div>
          <div className="md:col-span-2">
            <label className="mb-2 block text-sm font-medium text-slate-700">Notes</label>
            <Textarea placeholder="Add scope, justification, commercial terms, or committee notes" />
          </div>
        </div>
        {error ? <p className="mt-4 rounded-md bg-red-50 p-3 text-sm text-red-700">{error}</p> : null}
        <div className="mt-6 flex justify-between">
          <Button variant="secondary" disabled={step === 0} onClick={() => setStep((value) => Math.max(0, value - 1))}>Back</Button>
          <Button onClick={next}>{step === steps.length - 1 ? "Submit" : "Next"}</Button>
        </div>
      </FormSection>
    </>
  );
}
