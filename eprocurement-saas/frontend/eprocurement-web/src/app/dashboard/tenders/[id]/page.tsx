import { MvpFlowPage } from "@/components/pages/MvpFlowPage";

export default function TenderDetailPage({ params }: { params: { id: string } }) {
  return <MvpFlowPage mode="tender-detail" id={params.id} />;
}
