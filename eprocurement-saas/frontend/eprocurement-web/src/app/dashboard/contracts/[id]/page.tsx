import { MvpFlowPage } from "@/components/pages/MvpFlowPage";

export default function ContractDetailPage({ params }: { params: { id: string } }) {
  return <MvpFlowPage mode="contracts" id={params.id} />;
}
