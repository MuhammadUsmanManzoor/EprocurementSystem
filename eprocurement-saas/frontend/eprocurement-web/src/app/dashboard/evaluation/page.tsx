import { WizardPage } from "@/components/pages/WizardPage";

export default function EvaluationPage() {
  return <WizardPage title="Evaluation" description="Committee scoring workflow for technical, commercial, compliance, and final recommendation review." steps={["Compliance", "Technical Score", "Financial Score", "Recommendation"]} />;
}
