namespace EProcurement.SharedKernel.Audit;

public sealed record AuditEventRequest(
    Guid TenantId,
    string ServiceName,
    string Action,
    string EntityName,
    Guid? EntityId,
    string ActorEmail,
    string Details);
