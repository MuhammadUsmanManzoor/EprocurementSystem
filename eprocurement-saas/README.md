# E-Procurement SaaS

SaaS-based E-Procurement and Tender Management System foundation.

Project status: active MVP build and local Docker Compose testing.

## Current Scope

- Next.js, React, TypeScript, and Tailwind CSS frontend
- .NET 7 API Gateway with YARP
- AuthService with JWT login and seeded demo users
- TenantService with basic tenant CRUD and seeded AKPK Demo tenant
- VendorService for vendor registration and approval
- ProcurementService for purchase requests and approvals
- TenderService for tender creation, publishing, and invited vendor visibility
- BiddingService for bid submission, opening, comparison, and evaluation
- ContractService for award decisions, PO generation, and contracts
- DocumentService for upload metadata linked to business records
- AuditService for tenant-aware audit logs
- NotificationService for email/in-app notification records
- SapIntegrationService placeholder for future SAP Business One export
- PostgreSQL with EF Core setup
- Docker Compose for PostgreSQL, MinIO, MailHog, RabbitMQ, gateway, services, and frontend

## Business Workflow

Purchase Request -> Approval -> Tender/RFQ/RFP -> Vendor Bidding -> Bid Opening -> Evaluation -> Award Decision -> Purchase Order -> Contract

## Demo Tenant

- AKPK Demo

## Demo Users

All demo users use password `Password123!`.

- `superadmin@demo.com`
- `tenantadmin@akpk.com`
- `procurement@akpk.com`
- `approver@akpk.com`
- `committee@akpk.com`
- `finance@akpk.com`
- `vendor@demo.com`
- `auditor@akpk.com`

## Run With Docker Compose

1. Copy `.env.example` to `.env`.
2. From this folder, run:

```bash
docker compose up --build
```

3. Open:

- Frontend: `http://localhost:3000`
- API Gateway: `http://localhost:8088/health`
- pgAdmin: `http://localhost:5050`
- AuthService: `http://localhost:5101/health`
- TenantService: `http://localhost:5102/health`
- VendorService: `http://localhost:5103/health`
- ProcurementService: `http://localhost:5104/health`
- TenderService: `http://localhost:5105/health`
- BiddingService: `http://localhost:5106/health`
- ContractService: `http://localhost:5107/health`
- DocumentService: `http://localhost:5108/health`
- AuditService: `http://localhost:5109/health`
- NotificationService: `http://localhost:5110/health`
- SapIntegrationService: `http://localhost:5111/health`
- MinIO Console: `http://localhost:9001`
- MailHog: `http://localhost:8025`
- RabbitMQ Management: `http://localhost:15672`

pgAdmin login:

- Email: `admin@demo.com`
- Password: `Password123!`

Register the PostgreSQL server in pgAdmin with:

- Host: `postgres`
- Port: `5432`
- Username: `postgres`
- Password: `postgres`

If you already ran the phase-one stack before these databases were added, recreate the local PostgreSQL volume:

```bash
docker compose down -v
docker compose up --build
```

This removes local container data. Use it only for the demo/dev stack.

## Useful API Calls

Login through the gateway:

```bash
curl -X POST http://localhost:8088/auth/login \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"tenantadmin@akpk.com\",\"password\":\"Password123!\"}"
```

List tenants:

```bash
curl http://localhost:8088/tenants
```

Gateway routes:

- `POST /vendors/register`, `POST /vendors/{id}/approve`
- `POST /purchase-requests`, `POST /purchase-requests/{id}/submit`, `POST /purchase-requests/{id}/approve`
- `POST /tenders`, `POST /tenders/{id}/publish`, `POST /tenders/{id}/invite`
- `POST /bids`, `POST /bids/tenders/{tenderId}/open`, `GET /bids/tenders/{tenderId}/comparison`, `POST /bids/{id}/evaluate`
- `POST /awards`, `POST /awards/{id}/approve`
- `POST /purchase-orders/generate`
- `POST /contracts`
- `POST /documents`
- `GET /audit-logs`
- `POST /notifications`
- `POST /sap/export-purchase-order`

## Local Development

Backend services can be run individually with `dotnet run` from each service folder. For local non-Docker runs, make sure PostgreSQL has these databases:

- `eprocurement_auth`
- `eprocurement_tenants`
- `eprocurement_vendors`
- `eprocurement_procurement`
- `eprocurement_tenders`
- `eprocurement_bidding`
- `eprocurement_contracts`
- `eprocurement_documents`
- `eprocurement_audit`
- `eprocurement_notifications`

The frontend expects `NEXT_PUBLIC_API_BASE_URL=http://localhost:8088`.
