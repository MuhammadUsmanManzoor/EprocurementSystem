using System.Text;
using System.Text.Json.Serialization;
using EProcurement.SharedKernel.Audit;
using EProcurement.SharedKernel.Entities;
using EProcurement.SharedKernel.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<DocumentDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHttpClient("audit", client => client.BaseAddress = new Uri(builder.Configuration["Services:Audit"] ?? "http://audit-service:8080"));
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidateAudience = true, ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "eprocurement",
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "eprocurement-web",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"] ?? "development-secret-change-me-development-secret"))
    };
});
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "document-service" }));

var group = app.MapGroup("/api/documents").RequireAuthorization();

group.MapGet("/", async (Guid? entityId, string? entityName, DocumentDbContext db, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    var query = db.Documents.Include(d => d.Versions).AsQueryable();
    if (!user.IsSuperAdmin) query = query.Where(d => d.TenantId == user.RequireTenantId());
    if (user.IsVendor) query = query.Where(d => d.CreatedByEmail == user.Email || d.Versions.Any(v => v.UploadedByEmail == user.Email));
    if (entityId is not null) query = query.Where(d => d.EntityId == entityId);
    if (!string.IsNullOrWhiteSpace(entityName)) query = query.Where(d => d.EntityName == entityName);

    var documents = await query.OrderByDescending(d => d.CreatedAtUtc).ToListAsync();
    return Results.Ok(documents);
});

group.MapGet("/{id:guid}", async (Guid id, DocumentDbContext db, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    var document = await db.Documents.Include(d => d.Versions).SingleOrDefaultAsync(d => d.Id == id);
    if (document is null) return Results.NotFound();
    if (!CanRead(user, document)) return Results.Forbid();
    return Results.Ok(document);
});

group.MapPost("/", async (CreateDocumentRequest request, DocumentDbContext db, IHttpClientFactory httpClientFactory, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    var tenantId = user.IsSuperAdmin ? request.TenantId : user.RequireTenantId();
    if (tenantId == Guid.Empty) return Results.BadRequest("TenantId is required.");
    if (!CanCreate(user, request.EntityName)) return Results.Forbid();

    var documentId = Guid.NewGuid();
    var version = BuildVersion(tenantId, 1, request.File, user.Email);
    version.DocumentId = documentId;
    var document = new DocumentMetadata
    {
        Id = documentId,
        TenantId = tenantId,
        EntityName = request.EntityName.Trim(),
        EntityId = request.EntityId,
        DocumentType = request.DocumentType.Trim(),
        Title = request.Title.Trim(),
        FileName = version.FileName,
        ContentType = version.ContentType,
        StorageBucket = version.StorageBucket,
        StorageObjectKey = version.StorageObjectKey,
        SizeBytes = version.SizeBytes,
        UploadedByEmail = user.Email,
        Status = DocumentStatus.Draft,
        CurrentVersionNumber = 1,
        CreatedByEmail = user.Email,
        Versions = new List<DocumentVersion> { version }
    };

    db.Documents.Add(document);
    await db.SaveChangesAsync();
    await AuditAsync(httpClientFactory, tenantId, "DocumentCreated", "Document", document.Id, user.Email, $"{document.Title} v1");
    return Results.Created($"/api/documents/{document.Id}", document);
});

group.MapPut("/{id:guid}/metadata", async (Guid id, UpdateDocumentMetadataRequest request, DocumentDbContext db, IHttpClientFactory httpClientFactory, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    var document = await db.Documents.Include(d => d.Versions).SingleOrDefaultAsync(d => d.Id == id);
    if (document is null) return Results.NotFound();
    if (!CanEdit(user, document)) return Results.Forbid();

    document.Title = request.Title.Trim();
    document.DocumentType = request.DocumentType.Trim();
    document.Status = request.Status;
    document.UpdatedAtUtc = DateTime.UtcNow;

    await db.SaveChangesAsync();
    await AuditAsync(httpClientFactory, document.TenantId, "DocumentMetadataUpdated", "Document", document.Id, user.Email, document.Title);
    return Results.Ok(document);
});

group.MapPost("/{id:guid}/versions", async (Guid id, CreateDocumentVersionRequest request, DocumentDbContext db, IHttpClientFactory httpClientFactory, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    var document = await db.Documents.Include(d => d.Versions).SingleOrDefaultAsync(d => d.Id == id);
    if (document is null) return Results.NotFound();
    if (!CanEdit(user, document)) return Results.Forbid();

    foreach (var existing in document.Versions) existing.IsCurrent = false;

    var nextVersionNumber = document.CurrentVersionNumber + 1;
    var version = BuildVersion(document.TenantId, nextVersionNumber, request.File, user.Email);
    version.DocumentId = document.Id;
    document.Versions.Add(version);
    db.DocumentVersions.Add(version);
    document.FileName = version.FileName;
    document.ContentType = version.ContentType;
    document.StorageBucket = version.StorageBucket;
    document.StorageObjectKey = version.StorageObjectKey;
    document.SizeBytes = version.SizeBytes;
    document.UploadedByEmail = user.Email;
    document.CurrentVersionNumber = nextVersionNumber;
    document.Status = DocumentStatus.Draft;
    document.UpdatedAtUtc = DateTime.UtcNow;

    await db.SaveChangesAsync();
    await AuditAsync(httpClientFactory, document.TenantId, "DocumentVersionUploaded", "Document", document.Id, user.Email, $"{document.Title} v{nextVersionNumber}");
    return Results.Created($"/api/documents/{document.Id}/versions/{version.Id}", document);
});

group.MapPost("/{id:guid}/lock", async (Guid id, LockDocumentRequest request, DocumentDbContext db, IHttpClientFactory httpClientFactory, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    var document = await db.Documents.Include(d => d.Versions).SingleOrDefaultAsync(d => d.Id == id);
    if (document is null) return Results.NotFound();
    if (!CanLock(user, document)) return Results.Forbid();

    document.Status = DocumentStatus.Locked;
    document.IsLocked = true;
    document.LockedAtUtc = DateTime.UtcNow;
    document.LockedByEmail = user.Email;
    document.LockedReason = string.IsNullOrWhiteSpace(request.LockedReason) ? "ManualLock" : request.LockedReason.Trim();
    document.UpdatedAtUtc = DateTime.UtcNow;

    await db.SaveChangesAsync();
    await AuditAsync(httpClientFactory, document.TenantId, "DocumentLocked", "Document", document.Id, user.Email, document.LockedReason);
    return Results.Ok(document);
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DocumentDbContext>();
    await db.Database.EnsureCreatedAsync();
    await EnsureSchemaAsync(db);
}

app.Run();

static DocumentVersion BuildVersion(Guid tenantId, int versionNumber, DocumentVersionFileRequest file, string uploadedByEmail)
{
    return new DocumentVersion
    {
        TenantId = tenantId,
        VersionNumber = versionNumber,
        FileName = file.FileName.Trim(),
        ContentType = file.ContentType.Trim(),
        StorageBucket = file.StorageBucket.Trim(),
        StorageObjectKey = file.StorageObjectKey.Trim(),
        SizeBytes = file.SizeBytes,
        ChecksumSha256 = file.ChecksumSha256.Trim(),
        ChangeSummary = file.ChangeSummary.Trim(),
        UploadedByEmail = uploadedByEmail,
        UploadedAtUtc = DateTime.UtcNow,
        IsCurrent = true,
        VirusScanStatus = string.IsNullOrWhiteSpace(file.VirusScanStatus) ? "Pending" : file.VirusScanStatus.Trim()
    };
}

static bool CanRead(CurrentUser user, DocumentMetadata document)
{
    if (!user.CanAccessTenant(document.TenantId)) return false;
    if (user.IsVendor) return document.CreatedByEmail == user.Email || document.Versions.Any(v => v.UploadedByEmail == user.Email);
    return true;
}

static bool CanCreate(CurrentUser user, string entityName)
{
    if (user.Role.Equals("Auditor", StringComparison.OrdinalIgnoreCase)) return false;
    if (user.IsVendor) return entityName.Equals("Bid", StringComparison.OrdinalIgnoreCase);
    return user.IsSuperAdmin || user.IsTenantAdmin || user.Role is "Procurement" or "Finance" || user.IsCommittee;
}

static bool CanEdit(CurrentUser user, DocumentMetadata document)
{
    if (!CanRead(user, document) || document.IsLocked || document.Status == DocumentStatus.Locked || document.Status == DocumentStatus.Archived) return false;
    if (user.Role.Equals("Auditor", StringComparison.OrdinalIgnoreCase)) return false;
    if (user.IsVendor) return document.EntityName.Equals("Bid", StringComparison.OrdinalIgnoreCase) && document.CreatedByEmail == user.Email;
    return user.IsSuperAdmin || user.IsTenantAdmin || user.Role is "Procurement" or "Finance" || user.IsCommittee;
}

static bool CanLock(CurrentUser user, DocumentMetadata document)
{
    if (!CanRead(user, document)) return false;
    if (user.Role.Equals("Auditor", StringComparison.OrdinalIgnoreCase) || user.IsVendor) return false;
    return user.IsSuperAdmin || user.IsTenantAdmin || user.Role is "Procurement" or "Finance" || user.IsCommittee;
}

static async Task EnsureSchemaAsync(DocumentDbContext db)
{
    await db.Database.ExecuteSqlRawAsync("""
        ALTER TABLE "Documents" ADD COLUMN IF NOT EXISTS "DocumentType" character varying(120) NOT NULL DEFAULT 'GENERAL';
        ALTER TABLE "Documents" ADD COLUMN IF NOT EXISTS "Title" character varying(260) NOT NULL DEFAULT 'Untitled document';
        ALTER TABLE "Documents" ADD COLUMN IF NOT EXISTS "Status" character varying(32) NOT NULL DEFAULT 'Draft';
        ALTER TABLE "Documents" ADD COLUMN IF NOT EXISTS "CurrentVersionNumber" integer NOT NULL DEFAULT 1;
        ALTER TABLE "Documents" ADD COLUMN IF NOT EXISTS "IsLocked" boolean NOT NULL DEFAULT false;
        ALTER TABLE "Documents" ADD COLUMN IF NOT EXISTS "LockedAtUtc" timestamp with time zone NULL;
        ALTER TABLE "Documents" ADD COLUMN IF NOT EXISTS "LockedByEmail" character varying(256) NULL;
        ALTER TABLE "Documents" ADD COLUMN IF NOT EXISTS "LockedReason" character varying(160) NULL;
        ALTER TABLE "Documents" ADD COLUMN IF NOT EXISTS "CreatedByEmail" character varying(256) NOT NULL DEFAULT 'system';
        CREATE TABLE IF NOT EXISTS "DocumentVersions" (
            "Id" uuid NOT NULL,
            "TenantId" uuid NOT NULL,
            "CreatedAtUtc" timestamp with time zone NOT NULL,
            "UpdatedAtUtc" timestamp with time zone NULL,
            "DocumentId" uuid NOT NULL,
            "VersionNumber" integer NOT NULL,
            "FileName" character varying(260) NOT NULL,
            "ContentType" character varying(120) NOT NULL,
            "StorageBucket" character varying(80) NOT NULL,
            "StorageObjectKey" character varying(500) NOT NULL,
            "SizeBytes" bigint NOT NULL,
            "ChecksumSha256" character varying(128) NOT NULL,
            "ChangeSummary" character varying(500) NOT NULL,
            "UploadedByEmail" character varying(256) NOT NULL,
            "UploadedAtUtc" timestamp with time zone NOT NULL,
            "IsCurrent" boolean NOT NULL,
            "VirusScanStatus" character varying(40) NOT NULL,
            CONSTRAINT "PK_DocumentVersions" PRIMARY KEY ("Id")
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_DocumentVersions_DocumentId_VersionNumber" ON "DocumentVersions" ("DocumentId", "VersionNumber");
        """);
}

static async Task AuditAsync(IHttpClientFactory factory, Guid tenantId, string action, string entity, Guid entityId, string actor, string details)
{
    try { await factory.CreateClient("audit").PostAsJsonAsync("/api/audit-logs", new AuditEventRequest(tenantId, "DocumentService", action, entity, entityId, actor, details)); } catch { }
}

public sealed class DocumentMetadata : TenantEntity
{
    public string EntityName { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string StorageBucket { get; set; } = string.Empty;
    public string StorageObjectKey { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string UploadedByEmail { get; set; } = string.Empty;
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
    public int CurrentVersionNumber { get; set; } = 1;
    public bool IsLocked { get; set; }
    public DateTime? LockedAtUtc { get; set; }
    public string? LockedByEmail { get; set; }
    public string? LockedReason { get; set; }
    public string CreatedByEmail { get; set; } = string.Empty;
    public List<DocumentVersion> Versions { get; set; } = new();
}

public sealed class DocumentVersion : TenantEntity
{
    public Guid DocumentId { get; set; }
    public int VersionNumber { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string StorageBucket { get; set; } = string.Empty;
    public string StorageObjectKey { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string ChecksumSha256 { get; set; } = string.Empty;
    public string ChangeSummary { get; set; } = string.Empty;
    public string UploadedByEmail { get; set; } = string.Empty;
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsCurrent { get; set; }
    public string VirusScanStatus { get; set; } = "Pending";
}

public enum DocumentStatus { Draft, Submitted, Locked, Archived }

public sealed record CreateDocumentRequest(Guid TenantId, string EntityName, Guid EntityId, string DocumentType, string Title, DocumentVersionFileRequest File);
public sealed record UpdateDocumentMetadataRequest(string DocumentType, string Title, DocumentStatus Status);
public sealed record CreateDocumentVersionRequest(DocumentVersionFileRequest File);
public sealed record LockDocumentRequest(string LockedReason);
public sealed record DocumentVersionFileRequest(string FileName, string ContentType, string StorageBucket, string StorageObjectKey, long SizeBytes, string ChecksumSha256, string ChangeSummary, string? VirusScanStatus);

public sealed class DocumentDbContext : DbContext
{
    public DocumentDbContext(DbContextOptions<DocumentDbContext> options) : base(options) { }
    public DbSet<DocumentMetadata> Documents => Set<DocumentMetadata>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DocumentMetadata>(e =>
        {
            e.ToTable("Documents");
            e.HasKey(d => d.Id);
            e.Property(d => d.EntityName).HasMaxLength(80).IsRequired();
            e.Property(d => d.DocumentType).HasMaxLength(120).IsRequired();
            e.Property(d => d.Title).HasMaxLength(260).IsRequired();
            e.Property(d => d.FileName).HasMaxLength(260).IsRequired();
            e.Property(d => d.ContentType).HasMaxLength(120).IsRequired();
            e.Property(d => d.StorageBucket).HasMaxLength(80).IsRequired();
            e.Property(d => d.StorageObjectKey).HasMaxLength(500).IsRequired();
            e.Property(d => d.UploadedByEmail).HasMaxLength(256).IsRequired();
            e.Property(d => d.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(d => d.LockedByEmail).HasMaxLength(256);
            e.Property(d => d.LockedReason).HasMaxLength(160);
            e.Property(d => d.CreatedByEmail).HasMaxLength(256).IsRequired();
            e.HasMany(d => d.Versions).WithOne().HasForeignKey(v => v.DocumentId);
        });

        modelBuilder.Entity<DocumentVersion>(e =>
        {
            e.ToTable("DocumentVersions");
            e.HasKey(v => v.Id);
            e.HasIndex(v => new { v.DocumentId, v.VersionNumber }).IsUnique();
            e.Property(v => v.FileName).HasMaxLength(260).IsRequired();
            e.Property(v => v.ContentType).HasMaxLength(120).IsRequired();
            e.Property(v => v.StorageBucket).HasMaxLength(80).IsRequired();
            e.Property(v => v.StorageObjectKey).HasMaxLength(500).IsRequired();
            e.Property(v => v.ChecksumSha256).HasMaxLength(128).IsRequired();
            e.Property(v => v.ChangeSummary).HasMaxLength(500).IsRequired();
            e.Property(v => v.UploadedByEmail).HasMaxLength(256).IsRequired();
            e.Property(v => v.VirusScanStatus).HasMaxLength(40).IsRequired();
        });
    }
}
