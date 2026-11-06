using System.Text.RegularExpressions;
using AnalysisEngine.Core.DiffAnalysis;

namespace AnalysisEngine.Core.Rules.Technical;

/// <summary>
/// Sirket ici ADR (Architecture Decision Record) standartlarina uyumlulugu kontrol eder.
/// /teknik/ klasorundeki 10 ADR'den cikarilan regex-bazli kurallarla
/// diff satirlarini tarar ve ihlalleri tespit eder.
///
/// 10 ADR → 75 kural:
///   ADR-0011 DateTime (8), ADR-0022 API Versioning (6), ADR-0010 Audit (7),
///   ADR-0003 Health Check (4), ADR-0002 HTTP Status (5), ADR-0023 Idempotency (5),
///   ADR-0038 .NET 10 (25), ADR-0019 RabbitMQ (7), ADR-0008 DB ID (5),
///   ADR-0050 Deprecation (3)
/// </summary>
public class AdrComplianceChecker
{
    private static readonly List<AdrRule> Rules = new()
    {
        // ══════════════════════════════════════════════════════════════
        // ADR-0011: DateTime ve Timezone Standardi (8 kural)
        // ══════════════════════════════════════════════════════════════

        new("ADR-DT-01", "DateTime Tipi Kullanimi",
            AdrSeverity.Critical, "ADR-0011: DateTime tipi zaman dilimi bilgisi tasimaz — DateTimeOffset kullanilmali",
            new[] { @"\bDateTime\b(?!\s*Offset)" },
            FileScope.CSharp),

        new("ADR-DT-02", "DateTime.Now Kullanimi",
            AdrSeverity.Critical, "ADR-0011: DateTime.Now local zaman doner — DateTimeOffset.UtcNow kullanilmali",
            new[] { @"DateTime\.Now\b" },
            FileScope.CSharp),

        new("ADR-DT-03", "DateTime.UtcNow Kullanimi",
            AdrSeverity.High, "ADR-0011: DateTime.UtcNow offset bilgisi icermez — DateTimeOffset.UtcNow kullanilmali",
            new[] { @"DateTime\.UtcNow\b" },
            FileScope.CSharp),

        new("ADR-DT-04", "Async Deadlock (.Result/.Wait)",
            AdrSeverity.Critical, "ADR-0011/0038: .Result veya .Wait() async deadlock yaratir — await kullanilmali",
            new[] { @"\.Result\b(?!\s*\{)", @"\.Wait\(\)" },
            FileScope.CSharp),

        new("ADR-DT-05", "new DateTime() Kullanimi",
            AdrSeverity.High, "ADR-0011: new DateTime() yerine DateTimeOffset kullanilmali",
            new[] { @"new\s+DateTime\s*\(" },
            FileScope.CSharp),

        new("ADR-DT-06", "TIMESTAMP (TZ'siz) Kolon",
            AdrSeverity.Critical, "ADR-0011: PostgreSQL'de TIMESTAMPTZ kullanilmali — TIMESTAMP timezone kaybeder",
            new[] { @"\bTIMESTAMP\b(?!TZ)", @"HasColumnType\(\s*""timestamp""\s*\)" },
            FileScope.All),

        new("ADR-DT-07", "Unix Epoch (long) Timestamp",
            AdrSeverity.High, "ADR-0011: Unix epoch (long) formati yeni gelistirmelerde kullanilmaz",
            new[] { @"\blong\b\s+\w*(?:[Tt]imestamp|[Tt]ime|[Cc]reated|[Uu]pdated)\b" },
            FileScope.CSharpModels),

        new("ADR-DT-08", "public DateTime Property (DateTimeOffset Olmali)",
            AdrSeverity.High, "ADR-0011: Entity/DTO'larda DateTimeOffset zorunlu — public DateTime property YASAK",
            new[] { @"public\s+DateTime\s+\w+" },
            FileScope.CSharpModels),

        // ══════════════════════════════════════════════════════════════
        // ADR-0010: Audit Trail Standardi (7 kural)
        // ══════════════════════════════════════════════════════════════

        new("ADR-AU-01", "Hatali Audit Alan Isimlendirme",
            AdrSeverity.High, "ADR-0010: Audit alanlari snake_case olmali — createdAt, CreatedDate, ins_date YASAK",
            new[] { @"\bcreatedAt\b", @"\bCreatedDate\b", @"\bcreate_time\b", @"\bins_date\b",
                    @"\bupdatedAt\b", @"\bUpdatedDate\b", @"\bupdate_time\b", @"\bmod_date\b",
                    @"\bInsertDate\b", @"\bModifyDate\b", @"\bCreateDate\b", @"\bUpdateDate\b" },
            FileScope.All),

        new("ADR-AU-02", "created_at/created_by Immutability Ihlali",
            AdrSeverity.Critical, "ADR-0010: CreatedAt/CreatedBy private set olmali — public set YASAK (degistirilemez)",
            new[] { @"Created(?:At|By)\s*\{\s*get;\s*set;" },
            FileScope.CSharp),

        new("ADR-AU-03", "Audit Alan DB Default Kullanimi",
            AdrSeverity.High, "ADR-0010: Audit alanlari uygulama katmaninda doldurulmali — DB DEFAULT/trigger YASAK",
            new[] { @"HasDefaultValue.*(?:DateTime|CURRENT_TIMESTAMP|NOW|GETDATE)",
                    @"DEFAULT\s+(?:CURRENT_TIMESTAMP|NOW\(\)|GETDATE\(\))" },
            FileScope.All),

        new("ADR-AU-04", "created_by Null/Empty Atama",
            AdrSeverity.High, "ADR-0010: created_by bos birakilamaz — bilinmiyorsa 'anonymous' yazilmali",
            new[] { @"[Cc]reated[Bb]y\s*=\s*null", @"[Cc]reated[Bb]y\s*=\s*""""",
                    @"[Cc]reated[Bb]y\s*=\s*string\.Empty" },
            FileScope.CSharp),

        new("ADR-AU-05", "Request Body'de Audit Alan Kabulu",
            AdrSeverity.High, "ADR-0010: Request body'de created_at/updated_at/created_by KABUL EDILMEMELI",
            new[] { @"class\s+\w*Request\w*[^}]*(?:CreatedAt|UpdatedAt|CreatedBy|UpdatedBy)" },
            FileScope.CSharp),

        new("ADR-AU-06", "Audit Alani Eksik (Yeni Entity)",
            AdrSeverity.High, "ADR-0010: Her entity created_at, updated_at, created_by, updated_by icermeli",
            new[] { @"class\s+\w+\s*:\s*(?:Entity|BaseEntity|AggregateRoot)" },
            FileScope.CSharpModels,
            RequiresAbsence: new[] { @"[Cc]reated[Aa]t|created_at" }),

        new("ADR-AU-07", "Audit Timestamp Local Time",
            AdrSeverity.Critical, "ADR-0010: Tum audit timestamp'leri UTC olmali — local time YASAK",
            new[] { @"CreatedAt\s*=\s*DateTime\.Now", @"UpdatedAt\s*=\s*DateTime\.Now" },
            FileScope.CSharp),

        // ══════════════════════════════════════════════════════════════
        // ADR-0038: .NET 10 Clean Architecture Standartlari (25 kural)
        // ══════════════════════════════════════════════════════════════

        new("ADR-NET-01", "Yanlis Target Framework",
            AdrSeverity.Critical, "ADR-0038: Tum projeler .NET 10 hedeflemeli",
            new[] { @"<TargetFramework>net(?!10)[^<]*</TargetFramework>" },
            FileScope.All),

        new("ADR-NET-02", "Nullable Devre Disi",
            AdrSeverity.High, "ADR-0038: <Nullable>enable</Nullable> zorunlu",
            new[] { @"<Nullable>disable</Nullable>" },
            FileScope.All),

        new("ADR-NET-03", "new HttpClient() Kullanimi",
            AdrSeverity.Critical, "ADR-0038: new HttpClient() YASAK — AddHttpClient<T> ile DI factory kullanilmali (socket exhaustion)",
            new[] { @"new\s+HttpClient\s*\(" },
            FileScope.CSharp),

        new("ADR-NET-04", "Entity Public Set",
            AdrSeverity.High, "ADR-0038: Entity property'leri private set olmali — is kurallarini korumak icin public set YASAK",
            new[] { @"public\s+\w+\s+\w+\s*\{\s*get;\s*set;\s*\}" },
            FileScope.CSharpModels),

        new("ADR-NET-05", "Generic Repository Pattern",
            AdrSeverity.High, "ADR-0038: IRepository<T> YASAK — spesifik Aggregate repository kullanilmali",
            new[] { @"IRepository<\w*>", @"IGenericRepository", @"interface\s+IRepository\s*<" },
            FileScope.CSharp),

        new("ADR-NET-06", "Service Locator Anti-Pattern",
            AdrSeverity.High, "ADR-0038: Service Locator YASAK — constructor injection kullanilmali",
            new[] { @"\.GetService\s*<", @"\.GetRequiredService\s*<",
                    @"ServiceLocator", @"IServiceProvider\s+\w+\s*[;=]" },
            FileScope.CSharp),

        new("ADR-NET-07", "UPPER_SNAKE_CASE Sabit",
            AdrSeverity.Low, "ADR-0038: Sabitler PascalCase olmali — UPPER_SNAKE_CASE YASAK",
            new[] { @"const\s+\w+\s+[A-Z]{2}[A-Z_]+\s*=" },
            FileScope.CSharp),

        new("ADR-NET-08", "Async Metod Isimlendirme (Async Suffix Eksik)",
            AdrSeverity.Medium, "ADR-0038: Async metotlar 'Async' suffix'i ile bitmeli",
            new[] { @"public\s+(?:async\s+)?Task<?\w*>?\s+(?!.*Async)\w+\s*\([^)]*\)\s*\{" },
            FileScope.CSharp),

        new("ADR-NET-09", "CancellationToken Eksik",
            AdrSeverity.Medium, "ADR-0038: Tum async metotlarda CancellationToken parametresi olmali",
            new[] { @"public\s+(?:async\s+)?Task.*Async\s*\([^)]*\)(?![^)]*[Cc]ancellation[Tt]oken)" },
            FileScope.CSharp),

        new("ADR-NET-10", "Fiziksel DELETE (Hard Delete)",
            AdrSeverity.Critical, "ADR-0038: Fiziksel DELETE YASAK — soft delete (IsDeleted/deleted_at) kullanilmali",
            new[] { @"\.Remove\s*\(", @"\.RemoveRange\s*\(",
                    @"ExecuteSqlRaw.*DELETE", @"DELETE\s+FROM\b", @"ExecuteDelete" },
            FileScope.CSharp),

        new("ADR-NET-11", "PII Loglama (Maskelenmemis)",
            AdrSeverity.Critical, "ADR-0038: TCKN, email, telefon, kart no loglarda MASKELENMELI — KVKK/PCI-DSS ihlal riski",
            new[] { @"[Ll]og\w*\(.*\{.*[Tt]ckn", @"[Ll]og\w*\(.*\{.*[Ee]mail",
                    @"[Ll]og\w*\(.*\{.*[Pp]hone", @"[Ll]og\w*\(.*\{.*[Pp]assword",
                    @"_logger\.\w+\(.*[Tt]ckn", @"Console\.Write.*[Tt]ckn" },
            FileScope.CSharp),

        new("ADR-NET-12", "Domain Katmaninda Infrastructure Referansi",
            AdrSeverity.Critical, "ADR-0038: Domain katmani SIFIR dis bagimlilik — EF Core, HttpClient, Infrastructure namespace YASAK",
            new[] { @"using\s+Microsoft\.EntityFrameworkCore",
                    @"using\s+System\.Net\.Http",
                    @"using\s+\w+\.Infrastructure" },
            FileScope.CSharpDomain),

        new("ADR-NET-13", "Application Katmaninda Infrastructure Referansi",
            AdrSeverity.Critical, "ADR-0038: Application katmani Infrastructure'a referans veremez",
            new[] { @"using\s+\w+\.Infrastructure" },
            FileScope.CSharpApplication),

        new("ADR-NET-14", "Controller'da Is Mantigi / Try-Catch",
            AdrSeverity.Medium, "ADR-0038: Controller INCE olmali — try-catch ve karmasik if/else YASAK, GlobalExceptionMiddleware kullanilmali",
            new[] { @"try\s*\{" },
            FileScope.CSharpControllers),

        new("ADR-NET-15", "Controller'da Manuel Validation",
            AdrSeverity.High, "ADR-0038: Controller'da if(x == null) gibi manuel validation YASAK — FluentValidation kullanilmali",
            new[] { @"if\s*\(\s*\w+\.\w+\s*==\s*null", @"if\s*\(\s*string\.IsNullOrEmpty\s*\(\s*\w+\." },
            FileScope.CSharpControllers),

        new("ADR-NET-16", "Entity Dogrudan Controller'dan Donme",
            AdrSeverity.High, "ADR-0038: Entity dogrudan Controller'dan dondurulemez — DTO/Response kullanilmali",
            new[] { @"return\s+Ok\(\s*(?:entity|await\s+_\w+\.(?:Get|Find)\w*Async)" },
            FileScope.CSharpControllers),

        new("ADR-NET-17", "throw new Exception() (Generic)",
            AdrSeverity.Medium, "ADR-0038: Generic Exception YASAK — spesifik exception (InvalidOperationException vb.) kullanilmali",
            new[] { @"throw\s+new\s+Exception\s*\(" },
            FileScope.CSharp),

        new("ADR-NET-18", "Controller ControllerBase Degil Controller'dan Turuyor",
            AdrSeverity.Medium, "ADR-0038: ControllerBase sinifinden miras alinmali — Controller (MVC) YASAK",
            new[] { @":\s*Controller\b(?!Base)" },
            FileScope.CSharpControllers),

        new("ADR-NET-19", "Private Field Underscore Prefix Eksik",
            AdrSeverity.Low, "ADR-0038: Private field'lar _camelCase olmali — underscore prefix zorunlu",
            new[] { @"private\s+(?:readonly\s+)?(?!static)\w+\s+(?!_)[a-z]\w+\s*[;=]" },
            FileScope.CSharp),

        new("ADR-NET-20", "JsonPropertyName Buyuk Harf Basliyor",
            AdrSeverity.Low, "ADR-0038: JsonPropertyName camelCase olmali — buyuk harfle baslamak YASAK",
            new[] { @"\[JsonPropertyName\(\s*""[A-Z]" },
            FileScope.CSharp),

        new("ADR-NET-21", "DI Kaydinda Interface Eksik",
            AdrSeverity.High, "ADR-0038: Siniflar interface uzerinden DI'a kayit edilmeli — concrete class kaydı YASAK",
            new[] { @"Add(?:Scoped|Transient|Singleton)<(?!I)[A-Z]\w+>\s*\(\)" },
            FileScope.CSharp),

        new("ADR-NET-22", "Request DTO 'Request' Suffix Eksik",
            AdrSeverity.Low, "ADR-0038: Istek modelleri Request suffix'i almali",
            new[] { @"\[FromBody\]\s+(?!.*Request)\w+\s+\w+" },
            FileScope.CSharpControllers),

        new("ADR-NET-23", "POST 201 Yerine 200 Donuyor",
            AdrSeverity.Medium, "ADR-0038/0002: POST basarili islemde 201 Created donmeli — 200 OK degil",
            new[] { @"\[HttpPost\][^}]*return\s+Ok\(" },
            FileScope.CSharpControllers),

        new("ADR-NET-24", "DELETE 204 Yerine 200 Donuyor",
            AdrSeverity.Medium, "ADR-0038/0002: DELETE islemde 204 NoContent donmeli — 200 OK degil",
            new[] { @"\[HttpDelete\][^}]*return\s+Ok\(" },
            FileScope.CSharpControllers),

        new("ADR-NET-25", "EF Attribute Domain Katmaninda",
            AdrSeverity.Critical, "ADR-0038: Domain katmaninda EF attribute'leri ([Key], [Table], [Column]) YASAK",
            new[] { @"\[(?:Key|Table|Column|ForeignKey|DatabaseGenerated|Index)\]" },
            FileScope.CSharpDomain),

        // ══════════════════════════════════════════════════════════════
        // ADR-0008: Veritabani ID Standardi (5 kural)
        // ══════════════════════════════════════════════════════════════

        new("ADR-ID-01", "Auto Increment ID",
            AdrSeverity.Critical, "ADR-0008: SERIAL/AUTO_INCREMENT YASAK — UUID v7 kullanilmali",
            new[] { @"\bSERIAL\b", @"\bAUTO_INCREMENT\b", @"\bBIGSERIAL\b",
                    @"\bIDENTITY\s*\(", @"\.UseIdentityColumn", @"\.UseSerialColumn",
                    @"DatabaseGeneratedOption\.Identity" },
            FileScope.All),

        new("ADR-ID-02", "DB Tarafli UUID Uretimi",
            AdrSeverity.High, "ADR-0008: UUID veritabaninda degil uygulama katmaninda uretilmeli",
            new[] { @"gen_random_uuid\(\)", @"uuid_generate_v4\(\)", @"NEWID\(\)",
                    @"HasDefaultValueSql.*uuid", @"DEFAULT\s+.*uuid" },
            FileScope.All),

        new("ADR-ID-03", "UUID v4 (Random) Kullanimi",
            AdrSeverity.Medium, "ADR-0008: UUID v4 (random, siralanaMAZ) yerine UUID v7 (time-sortable) tercih edilmeli",
            new[] { @"Guid\.NewGuid\(\)", @"UUID\.randomUUID\(\)" },
            FileScope.CSharp),

        new("ADR-ID-04", "Sayisal Primary Key (int/long ID)",
            AdrSeverity.High, "ADR-0008: Yeni tablolarda int/long PK YASAK — UUID v7 kullanilmali",
            new[] { @"public\s+(?:int|long)\s+Id\s*\{",
                    @"PRIMARY\s+KEY.*\b(?:INT|BIGINT|INTEGER)\b" },
            FileScope.All),

        new("ADR-ID-05", "ValueGeneratedOnAdd (DB ID Uretimi)",
            AdrSeverity.High, "ADR-0008: .ValueGeneratedOnAdd() ID'yi DB'ye birakir — uygulama katmaninda uretilmeli",
            new[] { @"\.ValueGeneratedOnAdd\(\)" },
            FileScope.CSharp),

        // ══════════════════════════════════════════════════════════════
        // ADR-0002: HTTP Status Code Standardi (5 kural)
        // ══════════════════════════════════════════════════════════════

        new("ADR-HTTP-01", "Dis Bagimlilik Hatasi 500 Olarak Doner",
            AdrSeverity.Critical, "ADR-0002: Dis bagimlilik hatalari 502/503/504 olmali — 500 SADECE bizim hatamiz icin",
            new[] { @"catch\s*\(\s*HttpRequestException.*(?:500|InternalServerError)",
                    @"catch\s*\(\s*TimeoutException.*(?:500|InternalServerError)" },
            FileScope.CSharp),

        new("ADR-HTTP-02", "Validation Hatasi 500 Donuyor",
            AdrSeverity.High, "ADR-0002: Validation hatalari 400 Bad Request olmali — 500 degil",
            new[] { @"[Vv]alidat.*(?:500|InternalServerError)" },
            FileScope.CSharp),

        new("ADR-HTTP-03", "Her Hata 200 OK + Error Body",
            AdrSeverity.High, "ADR-0002: Hata durumlarinda 200 OK + error body anti-pattern — dogru HTTP status kodu kullanilmali",
            new[] { @"return\s+Ok\(.*[Ee]rror", @"return\s+Ok\(.*[Ff]ail",
                    @"StatusCode\s*=\s*200.*[Ee]rror" },
            FileScope.CSharp),

        new("ADR-HTTP-04", "404 Validation Icin Kullaniliyor",
            AdrSeverity.Medium, "ADR-0002: 404 = resource bulunamadi. Validation icin 400 kullanilmali",
            new[] { @"NotFound\(.*[Vv]alidat" },
            FileScope.CSharp),

        new("ADR-HTTP-05", "Tutarsiz Status Code (POST→200, DELETE→200)",
            AdrSeverity.Medium, "ADR-0002: POST→201, DELETE→204, GET→200 olmali",
            new[] { @"\[HttpPost\][^}]*return\s+Ok\(",
                    @"\[HttpDelete\][^}]*return\s+Ok\(" },
            FileScope.CSharpControllers),

        // ══════════════════════════════════════════════════════════════
        // ADR-0023: Idempotency Standardi (5 kural)
        // ══════════════════════════════════════════════════════════════

        new("ADR-IDEM-01", "POST Mutation Idempotency Eksik",
            AdrSeverity.High, "ADR-0023: Veritabaninda mutasyon yapan POST endpoint'lerinde idempotency ZORUNLU — cift islem riski",
            new[] { @"\[HttpPost\]" },
            FileScope.CSharpControllers,
            RequiresAbsence: new[] { @"[Ii]dempoten|SETNX|idem:|IdempotencyKey|idempotency" }),

        new("ADR-IDEM-02", "GET/DELETE Icin Gereksiz Idempotency",
            AdrSeverity.Low, "ADR-0023: GET/PUT/DELETE dogal idempotent — ek kilit kodu gereksiz",
            new[] { @"\[Http(?:Get|Delete)\].*[Ii]dempoten" },
            FileScope.CSharpControllers),

        new("ADR-IDEM-03", "409 Conflict Response Eksik",
            AdrSeverity.High, "ADR-0023: Tekrarlanan istekler icin 409 Conflict donulmeli",
            new[] { @"[Ii]dempoten|SETNX" },
            FileScope.CSharp,
            RequiresAbsence: new[] { @"409|Conflict|DuplicateRequest|AlreadyProcessed" }),

        new("ADR-IDEM-04", "Redis TTL Eksik (Idempotency Key)",
            AdrSeverity.High, "ADR-0023: Idempotency key'de TTL ZORUNLU — POS 5-15dk, Fatura 24 saat",
            new[] { @"(?:SETNX|SetAsync|StringSetAsync)\s*\(" },
            FileScope.CSharp,
            RequiresAbsence: new[] { @"[Ee]xpir|[Tt][Tt][Ll]|TimeSpan" }),

        new("ADR-IDEM-05", "Gateway Seviyesi Idempotency",
            AdrSeverity.Medium, "ADR-0023: Idempotency uygulama katmaninda olmali — gateway/client UUID header'i bekleme",
            new[] { @"X-Idempotency-Key|X-Request-Id.*required|[Hh]eader.*[Ii]dempotency" },
            FileScope.CSharp),

        // ══════════════════════════════════════════════════════════════
        // ADR-0022: API Versiyonlama Standardi (6 kural)
        // ══════════════════════════════════════════════════════════════

        new("ADR-VER-01", "URL'de Fiil Kullanimi",
            AdrSeverity.High, "ADR-0022: URL'lerde fiil YASAK — resource-based naming: GET /customers (fiil=HTTP method)",
            new[] { @"\[Http\w+\(\s*""(?:get|create|update|delete|remove|fetch|list|find|search|send|process)",
                    @"""/api/(?:get|create|update|delete|fetch|list|send|process)[A-Z]" },
            FileScope.CSharp),

        new("ADR-VER-02", "Hatali Versiyonlama (Query/Header)",
            AdrSeverity.High, "ADR-0022: Sadece URL path versioning (/v1/) — query (?v=) ve header (X-API-Version) YASAK",
            new[] { @"\?v=\d+", @"X-API-Version", @"api-version=\d+" },
            FileScope.All),

        new("ADR-VER-03", "Tekil Resource Adi",
            AdrSeverity.Medium, "ADR-0022: Resource isimleri COGUL olmali — /customer degil /customers",
            new[] { @"""/api/v\d+/(?:user|customer|payment|invoice|order|product|account|merchant|transaction|refund|notification)(?:/|"")" },
            FileScope.CSharp),

        new("ADR-VER-04", "CamelCase/snake_case URL",
            AdrSeverity.Medium, "ADR-0022: URL'lerde kebab-case kullanilmali — camelCase ve underscore YASAK",
            new[] { @"\[Route\(""[^""]*[a-z][A-Z]", @"\[Route\(""[^""]*_" },
            FileScope.CSharpControllers),

        new("ADR-VER-05", "Minor Version URL'de (v1.1)",
            AdrSeverity.Medium, "ADR-0022: URL'de minor versiyon (v1.1, v1.2) YASAK — sadece major (v1, v2)",
            new[] { @"/v\d+\.\d+/" },
            FileScope.All),

        new("ADR-VER-06", "3+ Seviye Hiyerarsi",
            AdrSeverity.Low, "ADR-0022: Maksimum 3 seviye hiyerarsi — daha derin URL kacinilmali",
            new[] { @"""/api/v\d+/\w+/\{[^}]+\}/\w+/\{[^}]+\}/\w+/\{" },
            FileScope.CSharp),

        // ══════════════════════════════════════════════════════════════
        // ADR-0003: Health Check Standardi (4 kural)
        // ══════════════════════════════════════════════════════════════

        new("ADR-HC-01", "Yanlis Health Check Endpoint Path",
            AdrSeverity.High, "ADR-0003: /hc/live (liveness), /hc/ready (readiness) kullanilmali — /health, /healthz YASAK",
            new[] { @"""/health""", @"""/healthz""", @"""/actuator/health""",
                    @"MapHealthChecks\(""/health""", @"MapGet\(""/health""" },
            FileScope.CSharp),

        new("ADR-HC-02", "Kritik Olmayan Servis critical=true",
            AdrSeverity.Medium, "ADR-0003: Redis/cache critical: true OLMAMALI — sadece primary DB ve RabbitMQ critical olabilir",
            new[] { @"[Rr]edis.*critical.*true", @"[Cc]ache.*critical.*true" },
            FileScope.CSharp),

        new("ADR-HC-03", "Health Check Timeout 5sn Ustu",
            AdrSeverity.Medium, "ADR-0003: Tek bir check maksimum 5 saniye — uzun timeout servisi bloklar",
            new[] { @"[Tt]imeout\s*=\s*TimeSpan\.From(?:Seconds|Minutes)\(\s*(?:[6-9]|\d{2,})" },
            FileScope.CSharp),

        new("ADR-HC-04", "Tekil Health Check Endpoint",
            AdrSeverity.Medium, "ADR-0003: Liveness ve readiness AYRI endpoint olmali — tek /health yetersiz",
            new[] { @"MapHealthChecks\s*\(\s*""/health""\s*\)" },
            FileScope.CSharp,
            RequiresAbsence: new[] { @"/hc/live|/hc/ready|liveness|readiness" }),

        // ══════════════════════════════════════════════════════════════
        // ADR-0019: RabbitMQ Queue/Exchange Naming (7 kural)
        // ══════════════════════════════════════════════════════════════

        new("ADR-Q-01", "Queue/Exchange Buyuk Harf",
            AdrSeverity.High, "ADR-0019: Queue/exchange isimleri tamamen LOWERCASE olmali — PascalCase YASAK",
            new[] { @"(?:queue|exchange)\s*[=:]\s*""[^""]*[A-Z]" },
            FileScope.All),

        new("ADR-Q-02", "Hatali DLQ Isimlendirme",
            AdrSeverity.High, "ADR-0019: DLQ formati <queue>.dlq olmali — _error, _skipped, dead-letter.queue YASAK",
            new[] { @"""[^""]*_error""", @"""[^""]*_skipped""", @"""[^""]*dead-letter\.queue""",
                    @"""[^""]*\.dlq\.[a-z]+""" },
            FileScope.All),

        new("ADR-Q-03", "Environment Adi Queue/Exchange'de",
            AdrSeverity.High, "ADR-0019: Queue/exchange adinda ortam bilgisi YASAK — vhost ile ayrilmali",
            new[] { @"(?:queue|exchange).*""[^""]*-(?:dev|staging|prod|test|uat)""" },
            FileScope.All),

        new("ADR-Q-04", "MassTransit Namespace Isimlendirme",
            AdrSeverity.Medium, "ADR-0019: MassTransit namespace-bazli isimlendirme YASAK — domain.entity.action kullanilmali",
            new[] { @"[A-Z]\w+\.[A-Z]\w+\.(?:Messaging|Events|Commands)\.[A-Z]" },
            FileScope.All),

        new("ADR-Q-05", "Exchange Tip Belirtilmemis",
            AdrSeverity.Medium, "ADR-0019: Exchange adi format: <domain>.<tip> — tip (topic/direct/fanout/hashed) belirtilmeli",
            new[] { @"exchange\s*[=:]\s*""[a-z][a-z0-9-]*""(?!.*\.(topic|direct|fanout|hashed|headers))" },
            FileScope.All),

        new("ADR-Q-06", "Queue Adinda Underscore",
            AdrSeverity.Medium, "ADR-0019: Queue/exchange adinda alt cizgi (_) YASAK — nokta (.) ayirici kullanilmali",
            new[] { @"(?:queue|exchange)\s*[=:]\s*""[^""]*_[^""]*""" },
            FileScope.All),

        new("ADR-Q-07", "Headers Exchange (Mimari Onay Gerekli)",
            AdrSeverity.High, "ADR-0019: Headers exchange kullanimi mimari ekip onayi gerektirir — onay almadan kullanma",
            new[] { @"ExchangeType\.Headers|""headers"".*exchange" },
            FileScope.All),

        // ══════════════════════════════════════════════════════════════
        // ADR-0050: API Deprecation Politikasi (3 kural)
        // ══════════════════════════════════════════════════════════════

        new("ADR-DEP-01", "Deprecated Endpoint Sunset/Deprecation Header Eksik",
            AdrSeverity.High, "ADR-0050: Deprecated endpoint'lerde Sunset ve Deprecation HTTP header'lari ZORUNLU",
            new[] { @"\[Obsolete\b", @"deprecated.*true" },
            FileScope.CSharp,
            RequiresAbsence: new[] { @"Sunset|Deprecation.*header|sunset" }),

        new("ADR-DEP-02", "OpenAPI deprecated Isaretleme Eksik",
            AdrSeverity.Medium, "ADR-0050: Deprecated endpoint OpenAPI/Swagger'da deprecated: true ile isaretlenmeli",
            new[] { @"\[Obsolete\]" },
            FileScope.CSharp,
            RequiresAbsence: new[] { @"SwaggerOperation.*deprecated|deprecated.*true.*openapi" }),

        new("ADR-DEP-03", "Endpoint Habersiz Silme",
            AdrSeverity.High, "ADR-0050: Endpoint silmeden once minimum 6 ay deprecation suresi olmali — anlik silme YASAK",
            new[] { @"\[Http(?:Get|Post|Put|Delete|Patch)\]" },
            FileScope.CSharpControllers),
            // NOT: Bu kural sadece SILINEN satirlarda kontrol edilmeli — eklenen satirlarda degil

        // ══════════════════════════════════════════════════════════════
        // JAVA/SPRING ADR KURALLARI (40+ kural)
        // ══════════════════════════════════════════════════════════════

        // ADR-0011 Java DateTime Esdegerleri
        new("ADR-JDT-01", "Java Date Kullanimi",
            AdrSeverity.Critical, "ADR-0011: java.util.Date YASAK — java.time (LocalDateTime/ZonedDateTime/OffsetDateTime) kullanilmali",
            new[] { @"\bnew\s+Date\s*\(", @"\bjava\.util\.Date\b", @"\bimport\s+java\.util\.Date\b" },
            FileScope.Java),

        new("ADR-JDT-02", "Calendar Kullanimi",
            AdrSeverity.Critical, "ADR-0011: java.util.Calendar YASAK — java.time API kullanilmali",
            new[] { @"Calendar\.getInstance\(\)", @"\bjava\.util\.Calendar\b" },
            FileScope.Java),

        new("ADR-JDT-03", "LocalDateTime Timezone Kaybeder",
            AdrSeverity.High, "ADR-0011: LocalDateTime timezone bilgisi TASIMAZ — OffsetDateTime veya ZonedDateTime kullanilmali",
            new[] { @"LocalDateTime\.now\(\)", @"LocalDateTime\.of\(" },
            FileScope.JavaDomain),

        new("ADR-JDT-04", "SimpleDateFormat (Thread-Unsafe)",
            AdrSeverity.High, "ADR-0011: SimpleDateFormat thread-safe DEGIL — DateTimeFormatter kullanilmali",
            new[] { @"new\s+SimpleDateFormat\s*\(", @"\bSimpleDateFormat\b" },
            FileScope.Java),

        // ADR-0038 Java Clean Architecture Esdegerleri
        new("ADR-JNET-01", "Domain Katmaninda Spring/JPA Bagimliligi",
            AdrSeverity.Critical, "ADR-0038: Domain katmani framework-agnostik olmali — Spring annotation ve JPA YASAK",
            new[] { @"import\s+org\.springframework\.", @"import\s+javax\.persistence\.",
                    @"import\s+jakarta\.persistence\.", @"@Entity\b", @"@Table\b",
                    @"@Column\b", @"@Autowired\b" },
            FileScope.JavaDomain),

        new("ADR-JNET-02", "new RestTemplate() Kullanimi",
            AdrSeverity.Critical, "ADR-0038: new RestTemplate() YASAK — Bean olarak DI ile inject edilmeli (connection pool)",
            new[] { @"new\s+RestTemplate\s*\(" },
            FileScope.Java),

        new("ADR-JNET-03", "Field Injection (@Autowired)",
            AdrSeverity.High, "ADR-0038: @Autowired field injection YASAK — constructor injection kullanilmali (@RequiredArgsConstructor)",
            new[] { @"@Autowired\s+(?:private|protected)\s+" },
            FileScope.Java),

        new("ADR-JNET-04", "Generic Exception Throw",
            AdrSeverity.Medium, "ADR-0038: throw new Exception() YASAK — spesifik exception kullanilmali",
            new[] { @"throw\s+new\s+(?:Exception|RuntimeException)\s*\(" },
            FileScope.Java),

        new("ADR-JNET-05", "Catch Generic Exception",
            AdrSeverity.Medium, "ADR-0038: catch(Exception e) YASAK — spesifik exception catch edilmeli",
            new[] { @"catch\s*\(\s*Exception\s+\w+\s*\)" },
            FileScope.Java),

        new("ADR-JNET-06", "System.out.println Loglama",
            AdrSeverity.High, "ADR-0038: System.out.println YASAK — SLF4J/Logback kullanilmali",
            new[] { @"System\.out\.print", @"System\.err\.print", @"\.printStackTrace\(\)" },
            FileScope.Java),

        new("ADR-JNET-07", "PII Loglama (Java)",
            AdrSeverity.Critical, "ADR-0038: TCKN, email, telefon, kart no loglarda MASKELENMELI — KVKK/PCI-DSS ihlal riski",
            new[] { @"log\.\w+\(.*[Tt]ckn", @"log\.\w+\(.*[Ee]mail",
                    @"log\.\w+\(.*[Pp]hone", @"log\.\w+\(.*[Pp]assword",
                    @"log\.\w+\(.*[Cc]ardNumber" },
            FileScope.Java),

        new("ADR-JNET-08", "Controller'da Try-Catch",
            AdrSeverity.Medium, "ADR-0038: Controller ince olmali — try-catch YASAK, @ControllerAdvice/GlobalExceptionHandler kullanilmali",
            new[] { @"try\s*\{" },
            FileScope.JavaControllers),

        new("ADR-JNET-09", "Controller'da Is Mantigi",
            AdrSeverity.High, "ADR-0038: Controller'da is mantigi YASAK — UseCase/Service'e delege edilmeli",
            new[] { @"@(?:Service|Repository|Component)\b" },
            FileScope.JavaControllers),

        new("ADR-JNET-10", "Entity Dogrudan Controller'dan Donme",
            AdrSeverity.High, "ADR-0038: Entity dogrudan dondurulemez — DTO/Response kullanilmali",
            new[] { @"ResponseEntity\.ok\(\s*(?:entity|repository\.find)" },
            FileScope.JavaControllers),

        // ADR-0008 Java UUID Esdegerleri
        new("ADR-JID-01", "UUID v4 (Random) Kullanimi",
            AdrSeverity.Medium, "ADR-0008: UUID.randomUUID() (v4) yerine UUID v7 (time-sortable) tercih edilmeli",
            new[] { @"UUID\.randomUUID\(\)" },
            FileScope.Java),

        new("ADR-JID-02", "Long/Integer ID (Auto Increment)",
            AdrSeverity.High, "ADR-0008: @GeneratedValue(strategy = IDENTITY/AUTO) YASAK — UUID v7 kullanilmali",
            new[] { @"@GeneratedValue\s*\(\s*strategy\s*=\s*GenerationType\.(IDENTITY|AUTO)\s*\)",
                    @"@Id\s+private\s+(?:Long|Integer|long|int)\s+" },
            FileScope.Java),

        // ADR-0002 Java HTTP Status Esdegerleri
        new("ADR-JHTTP-01", "Validation Hatasi 500 Donuyor (Java)",
            AdrSeverity.High, "ADR-0002: Validation hatalari 400 Bad Request olmali — 500 degil",
            new[] { @"HttpStatus\.INTERNAL_SERVER_ERROR.*[Vv]alidat" },
            FileScope.Java),

        new("ADR-JHTTP-02", "Her Hata 200 OK + Error Body (Java)",
            AdrSeverity.High, "ADR-0002: 200 OK + error body anti-pattern — dogru HTTP status kullanilmali",
            new[] { @"ResponseEntity\.ok\(.*[Ee]rror", @"HttpStatus\.OK.*[Ff]ail" },
            FileScope.Java),

        // ADR-0023 Java Idempotency Esdegerleri
        new("ADR-JIDEM-01", "POST Mutation Idempotency Eksik (Java)",
            AdrSeverity.High, "ADR-0023: POST endpoint'lerinde idempotency kontrolu ZORUNLU",
            new[] { @"@PostMapping\b" },
            FileScope.JavaControllers,
            RequiresAbsence: new[] { @"[Ii]dempoten|SETNX|idempotency|IdempotencyKey" }),

        // ADR-0003 Java Health Check
        new("ADR-JHC-01", "Spring Actuator Default Health",
            AdrSeverity.High, "ADR-0003: /actuator/health YASAK — /hc/live ve /hc/ready olmali",
            new[] { @"/actuator/health" },
            FileScope.Java),

        // ADR-0019 RabbitMQ (Java icin de gecerli — zaten FileScope.All)

        // ADR-0050 Java Deprecation
        new("ADR-JDEP-01", "@Deprecated Sunset Header Eksik",
            AdrSeverity.High, "ADR-0050: @Deprecated endpoint'lerde Sunset HTTP header'i ZORUNLU",
            new[] { @"@Deprecated\b" },
            FileScope.Java,
            RequiresAbsence: new[] { @"Sunset|sunset|deprecation.*header" }),

        // Java-Specific Best Practices
        new("ADR-JBP-01", "Optional.get() Null Check Olmadan",
            AdrSeverity.High, "ADR-0038: Optional.get() NullPointerException riski — orElseThrow/orElse kullanilmali",
            new[] { @"\.get\(\)\s*(?!;)" },
            FileScope.Java,
            RequiresAbsence: new[] { @"\.isPresent\(\)|\.isEmpty\(\)" }),

        new("ADR-JBP-02", "Raw Type Kullanimi",
            AdrSeverity.Medium, "ADR-0038: Raw type (List, Map, Set) YASAK — generic type (List<T>, Map<K,V>) kullanilmali",
            new[] { @"(?:List|Map|Set|Collection)\s+\w+\s*[;=](?!.*<)" },
            FileScope.Java),

        new("ADR-JBP-03", "Mutable Entity Field (Public Setter)",
            AdrSeverity.High, "ADR-0038: Entity field'lari immutable olmali — public setter YASAK (Lombok @Setter domain'da YASAK)",
            new[] { @"@Setter\b" },
            FileScope.JavaDomain),

        new("ADR-JBP-04", "@Transactional Propagation REQUIRES_NEW",
            AdrSeverity.High, "ADR-0038: REQUIRES_NEW yeni bağımsız TX açar, nested deadlock riski — dikkatli kullanılmalı",
            new[] { @"propagation\s*=\s*Propagation\.REQUIRES_NEW" },
            FileScope.Java),
    };

    /// <summary>
    /// Diff dosyalarini ADR kurallarina karsi kontrol eder.
    /// </summary>
    public List<AdrViolation> Check(List<DiffParser.DiffFile> diffFiles)
    {
        var violations = new List<AdrViolation>();

        foreach (var file in diffFiles)
        {
            var addedWithLines = GetAddedLinesWithNumbers(file);

            foreach (var rule in Rules)
            {
                if (!IsFileInScope(file.FilePath, rule.Scope)) continue;

                foreach (var (lineNumber, lineText) in addedWithLines)
                {
                    var trimmed = lineText.Trim();
                    if (trimmed.StartsWith("//") || trimmed.StartsWith("/*") || trimmed.StartsWith("*"))
                        continue;

                    bool hasViolation = false;
                    foreach (var pattern in rule.ViolationPatterns)
                    {
                        if (Regex.IsMatch(lineText, pattern, RegexOptions.IgnoreCase))
                        {
                            hasViolation = true;
                            break;
                        }
                    }

                    if (!hasViolation) continue;

                    if (rule.RequiresAbsence != null)
                    {
                        var allAdded = string.Join("\n", file.AddedLines);
                        bool absenceFound = rule.RequiresAbsence.All(p =>
                            !Regex.IsMatch(allAdded, p, RegexOptions.IgnoreCase));
                        if (!absenceFound) continue;
                    }

                    violations.Add(new AdrViolation(
                        RuleId: rule.Id,
                        RuleName: rule.Name,
                        FilePath: file.FilePath,
                        LineNumber: lineNumber,
                        Code: trimmed.Length > 120 ? trimmed[..120] + "..." : trimmed,
                        Description: rule.Description,
                        Severity: rule.Severity));
                }
            }
        }

        return violations
            .GroupBy(v => $"{v.RuleId}:{v.FilePath}")
            .SelectMany(g => g.Take(3))
            .ToList();
    }

    private static List<(int LineNumber, string Text)> GetAddedLinesWithNumbers(DiffParser.DiffFile file)
    {
        var result = new List<(int, string)>();
        int lineNumber = 0;

        foreach (var hunk in file.Hunks)
        {
            lineNumber = hunk.NewStart;
            foreach (var line in hunk.Lines)
            {
                if (line.StartsWith("+") && !line.StartsWith("+++"))
                    result.Add((lineNumber, line[1..]));
                if (!line.StartsWith("-"))
                    lineNumber++;
            }
        }

        if (result.Count == 0 && file.AddedLines.Count > 0)
        {
            for (int i = 0; i < file.AddedLines.Count; i++)
                result.Add((i + 1, file.AddedLines[i]));
        }

        return result;
    }

    private static bool IsFileInScope(string filePath, FileScope scope)
    {
        var normalized = filePath.Replace('\\', '/').ToLowerInvariant();

        if (normalized.Contains(".tests/") || normalized.Contains("/tests/") ||
            normalized.Contains("/test/") || normalized.Contains("/obj/") || normalized.Contains("/bin/"))
            return false;

        return scope switch
        {
            FileScope.All => true,
            FileScope.CSharp => normalized.EndsWith(".cs"),
            FileScope.CSharpModels => normalized.EndsWith(".cs") &&
                (normalized.Contains("/models/") || normalized.Contains("/entities/") ||
                 normalized.Contains(".domain/")),
            FileScope.CSharpDomain => normalized.EndsWith(".cs") &&
                normalized.Contains(".domain/"),
            FileScope.CSharpApplication => normalized.EndsWith(".cs") &&
                normalized.Contains(".application/"),
            FileScope.CSharpControllers => normalized.EndsWith(".cs") &&
                normalized.Contains("/controllers/"),
            FileScope.Java => normalized.EndsWith(".java"),
            FileScope.JavaDomain => normalized.EndsWith(".java") &&
                (normalized.Contains("/domain/") || normalized.Contains("/usecase/") ||
                 normalized.Contains("/model/") || normalized.Contains("/port/")),
            FileScope.JavaControllers => normalized.EndsWith(".java") &&
                (normalized.Contains("/controller/") || normalized.Contains("/rest/")),
            _ => true
        };
    }
}

// ═══ MODELLER ═══

public record AdrRule(
    string Id,
    string Name,
    AdrSeverity Severity,
    string Description,
    string[] ViolationPatterns,
    FileScope Scope,
    string[]? RequiresAbsence = null
);

public record AdrViolation(
    string RuleId,
    string RuleName,
    string FilePath,
    int LineNumber,
    string Code,
    string Description,
    AdrSeverity Severity
);

public enum AdrSeverity
{
    Low,
    Medium,
    High,
    Critical
}

public enum FileScope
{
    All,
    CSharp,
    CSharpModels,
    CSharpDomain,
    CSharpApplication,
    CSharpControllers,
    Java,
    JavaDomain,
    JavaControllers
}
