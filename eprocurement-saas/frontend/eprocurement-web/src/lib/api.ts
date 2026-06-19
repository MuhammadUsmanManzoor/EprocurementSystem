export const apiBaseUrl = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8088";

export const demoTenantId = "11111111-1111-1111-1111-111111111111";
export const demoTenderId = "33333333-3333-3333-3333-333333333333";

export type AuthenticatedUser = {
  id: string;
  tenantId: string | null;
  email: string;
  fullName: string;
  role: string;
};

export type LoginResponse = {
  accessToken: string;
  user: AuthenticatedUser;
};

export type PurchaseRequest = {
  id: string;
  tenantId: string;
  title: string;
  department: string;
  costCenter?: string;
  category?: string;
  currency: string;
  requestedByEmail: string;
  justification: string;
  status: string;
  approvalRemarks?: string;
  approvedByEmail?: string;
  createdAtUtc: string;
  items: Array<{ id: string; itemCode?: string; description: string; unitOfMeasure?: string; quantity: number; estimatedUnitPrice: number }>;
};

export type Tender = {
  id: string;
  tenantId: string;
  purchaseRequestId?: string;
  title: string;
  description: string;
  method: string;
  visibility: string;
  evaluationCriteria?: string;
  committeeMember?: string;
  documentType?: string;
  status: string;
  closingDateUtc: string;
  publishedAtUtc?: string;
};

export type Vendor = {
  id: string;
  tenantId: string;
  name: string;
  registrationNumber: string;
  contactEmail: string;
  contactPhone: string;
  status: string;
};

export type Bid = {
  id: string;
  tenantId: string;
  tenderId: string;
  tenderClosingDateUtc: string;
  vendorId: string;
  vendorEmail: string;
  currency: string;
  totalAmount: number;
  status: string;
  openedAtUtc?: string;
};

export type BidComparison = {
  id: string;
  vendorId: string;
  vendorEmail: string;
  status: string;
  currency: string;
  totalAmount: number | null;
  isPriceVisible: boolean;
  isLowest: boolean;
};

export type AwardDecision = {
  id: string;
  tenantId: string;
  tenderId: string;
  bidId: string;
  vendorId: string;
  amount: number;
  currency: string;
  justification: string;
  status: string;
};

export type PurchaseOrder = {
  id: string;
  tenantId: string;
  awardDecisionId: string;
  tenderId: string;
  vendorId: string;
  poNumber: string;
  amount: number;
  currency: string;
  paymentTerm?: string;
  taxCode?: string;
  deliveryLocation?: string;
  status: string;
};

export type Contract = {
  id: string;
  tenantId: string;
  purchaseOrderId: string;
  tenderId: string;
  vendorId: string;
  contractNumber: string;
  title: string;
  documentType?: string;
  startDateUtc: string;
  endDateUtc: string;
  value: number;
  currency: string;
  status: string;
};

export type MasterDataItem = {
  id: string;
  tenantId: string;
  type: string;
  code: string;
  name: string;
  description?: string;
  isActive: boolean;
};

export type DocumentVersion = {
  id: string;
  tenantId: string;
  documentId: string;
  versionNumber: number;
  fileName: string;
  contentType: string;
  storageBucket: string;
  storageObjectKey: string;
  sizeBytes: number;
  checksumSha256: string;
  changeSummary: string;
  uploadedByEmail: string;
  uploadedAtUtc: string;
  isCurrent: boolean;
  virusScanStatus: string;
};

export type DocumentMetadata = {
  id: string;
  tenantId: string;
  entityName: string;
  entityId: string;
  documentType: string;
  title: string;
  status: string;
  currentVersionNumber: number;
  isLocked: boolean;
  lockedAtUtc?: string;
  lockedByEmail?: string;
  lockedReason?: string;
  createdByEmail: string;
  createdAtUtc: string;
  versions: DocumentVersion[];
};

export type DocumentVersionFilePayload = {
  fileName: string;
  contentType: string;
  storageBucket: string;
  storageObjectKey: string;
  sizeBytes: number;
  checksumSha256: string;
  changeSummary: string;
  virusScanStatus?: string;
};

export async function login(email: string, password: string): Promise<LoginResponse> {
  let response: Response;

  try {
    response = await fetch(`${apiBaseUrl}/auth/login`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email, password })
    });
  } catch {
    throw new Error("API Gateway is not reachable. Start Docker Compose and try again.");
  }

  if (!response.ok) {
    throw new Error("Invalid email or password.");
  }

  return response.json();
}

export function getAccessToken() {
  if (typeof window === "undefined") return null;
  return localStorage.getItem("eprocurement.token");
}

async function apiRequest<T>(path: string, options: RequestInit = {}): Promise<T> {
  const token = getAccessToken();
  const headers = new Headers(options.headers);
  headers.set("Content-Type", "application/json");
  if (token) headers.set("Authorization", `Bearer ${token}`);

  let response: Response;
  try {
    response = await fetch(`${apiBaseUrl}${path}`, { ...options, headers });
  } catch {
    throw new Error("API Gateway is not reachable. Start Docker Compose and try again.");
  }

  if (!response.ok) {
    const message = await response.text();
    throw new Error(message || `Request failed with status ${response.status}.`);
  }

  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export const api = {
  purchaseRequests: {
    list: () => apiRequest<PurchaseRequest[]>("/purchase-requests"),
    get: (id: string) => apiRequest<PurchaseRequest>(`/purchase-requests/${id}`),
    create: (payload: { tenantId: string; title: string; department: string; costCenter?: string; category?: string; currency?: string; justification: string; items: Array<{ itemCode?: string; description: string; unitOfMeasure?: string; quantity: number; estimatedUnitPrice: number }> }) =>
      apiRequest<PurchaseRequest>("/purchase-requests", { method: "POST", body: JSON.stringify(payload) }),
    submit: (id: string) => apiRequest<PurchaseRequest>(`/purchase-requests/${id}/submit`, { method: "POST", body: "{}" }),
    approve: (id: string, approved: boolean, remarks: string) =>
      apiRequest<PurchaseRequest>(`/purchase-requests/${id}/approve`, { method: "POST", body: JSON.stringify({ approved, remarks }) })
  },
  tenders: {
    list: () => apiRequest<Tender[]>("/tenders"),
    get: (id: string) => apiRequest<Tender>(`/tenders/${id}`),
    create: (payload: { tenantId: string; purchaseRequestId?: string; title: string; description: string; method: string; visibility: string; evaluationCriteria?: string; committeeMember?: string; documentType?: string; closingDateUtc: string }) =>
      apiRequest<Tender>("/tenders", { method: "POST", body: JSON.stringify(payload) }),
    publish: (id: string) => apiRequest<Tender>(`/tenders/${id}/publish`, { method: "POST", body: "{}" })
  },
  vendors: {
    list: () => apiRequest<Vendor[]>("/vendors")
  },
  bids: {
    list: (tenderId?: string) => apiRequest<Bid[]>(`/bids${tenderId ? `?tenderId=${tenderId}` : ""}`),
    submit: (payload: { tenderId: string; vendorId?: string; tenderClosingDateUtc: string; currency: string; items: Array<{ description: string; quantity: number; unitPrice: number }> }) =>
      apiRequest<{ id: string; status: string; revisionNumber: number; totalAmount: number; currency: string }>("/bids", { method: "POST", body: JSON.stringify(payload) }),
    open: (tenderId: string, tenderClosingDateUtc: string) =>
      apiRequest<{ tenderId: string; openedCount: number }>(`/bids/tenders/${tenderId}/open`, { method: "POST", body: JSON.stringify({ tenderClosingDateUtc }) }),
    comparison: (tenderId: string, tenderClosingDateUtc: string) =>
      apiRequest<BidComparison[]>(`/bids/tenders/${tenderId}/comparison?tenderClosingDateUtc=${encodeURIComponent(tenderClosingDateUtc)}`)
  },
  awards: {
    list: () => apiRequest<AwardDecision[]>("/awards"),
    create: (payload: { tenantId: string; tenderId: string; bidId: string; vendorId: string; amount: number; currency: string; justification: string }) =>
      apiRequest<AwardDecision>("/awards", { method: "POST", body: JSON.stringify(payload) }),
    approve: (id: string, approved: boolean, remarks: string) =>
      apiRequest<AwardDecision>(`/awards/${id}/approve`, { method: "POST", body: JSON.stringify({ approved, remarks }) })
  },
  purchaseOrders: {
    list: () => apiRequest<PurchaseOrder[]>("/purchase-orders"),
    generate: (awardDecisionId: string, payload?: { paymentTerm?: string; taxCode?: string; deliveryLocation?: string }) =>
      apiRequest<PurchaseOrder>("/purchase-orders/generate", { method: "POST", body: JSON.stringify({ awardDecisionId, ...payload }) })
  },
  contracts: {
    list: () => apiRequest<Contract[]>("/contracts"),
    create: (payload: { purchaseOrderId: string; contractNumber: string; title: string; documentType?: string; startDateUtc: string; endDateUtc: string }) =>
      apiRequest<Contract>("/contracts", { method: "POST", body: JSON.stringify(payload) })
  },
  masterData: {
    list: (type?: string) => apiRequest<MasterDataItem[]>(`/master-data${type ? `?type=${encodeURIComponent(type)}` : ""}`),
    create: (payload: { tenantId: string; type: string; code: string; name: string; description?: string }) =>
      apiRequest<MasterDataItem>("/master-data", { method: "POST", body: JSON.stringify(payload) }),
    update: (id: string, payload: { code: string; name: string; description?: string; isActive: boolean }) =>
      apiRequest<MasterDataItem>(`/master-data/${id}`, { method: "PUT", body: JSON.stringify(payload) }),
    remove: (id: string) => apiRequest<void>(`/master-data/${id}`, { method: "DELETE" })
  },
  documents: {
    list: (filters?: { entityName?: string; entityId?: string }) => {
      const search = new URLSearchParams();
      if (filters?.entityName) search.set("entityName", filters.entityName);
      if (filters?.entityId) search.set("entityId", filters.entityId);
      return apiRequest<DocumentMetadata[]>(`/documents${search.toString() ? `?${search}` : ""}`);
    },
    create: (payload: { tenantId: string; entityName: string; entityId: string; documentType: string; title: string; file: DocumentVersionFilePayload }) =>
      apiRequest<DocumentMetadata>("/documents", { method: "POST", body: JSON.stringify(payload) }),
    updateMetadata: (id: string, payload: { documentType: string; title: string; status: string }) =>
      apiRequest<DocumentMetadata>(`/documents/${id}/metadata`, { method: "PUT", body: JSON.stringify(payload) }),
    addVersion: (id: string, file: DocumentVersionFilePayload) =>
      apiRequest<DocumentMetadata>(`/documents/${id}/versions`, { method: "POST", body: JSON.stringify({ file }) }),
    lock: (id: string, lockedReason: string) =>
      apiRequest<DocumentMetadata>(`/documents/${id}/lock`, { method: "POST", body: JSON.stringify({ lockedReason }) })
  }
};
