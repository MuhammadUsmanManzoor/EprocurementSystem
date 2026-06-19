import { MvpFlowPage } from "@/components/pages/MvpFlowPage";

export default function PurchaseOrderDetailPage({ params }: { params: { id: string } }) {
  return <MvpFlowPage mode="purchase-orders" id={params.id} />;
}
