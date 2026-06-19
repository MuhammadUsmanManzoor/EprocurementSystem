import { MvpFlowPage } from "@/components/pages/MvpFlowPage";

export default function PurchaseRequestDetailPage({ params }: { params: { id: string } }) {
  return <MvpFlowPage mode="pr-detail" id={params.id} />;
}
