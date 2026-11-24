# SkyMate Authentication — شرح تدريجي للكود

الشرح ده مبني على مراجعة الملفات الحالية داخل `D:\Skymate v1`، وليس على مستند قديم. هنضيف الأجزاء التالية إلى الملف مع تقدم الشرح؛ الإصدار الحالي يحتوي على الجزء الأول فقط.

## الجزء الأول: خريطة الـAuthentication وملف `Program.cs`

### 1. الصورة العامة

المشروع يستخدم ASP.NET Core Identity لإدارة الحسابات وكلمات المرور والأدوار، ويستخدم JWT access token للتحقق من الطلبات بعد تسجيل الدخول.

فكري في المسار العام هكذا:

```text
HTTP Request
    ↓
SkyMate.API (Controller + Middleware)
    ↓
Application contract (IJwtTokenService)
    ↓
Infrastructure (Identity + JWT implementation + EF Core)
    ↓
SQL Server (AppDbContext)
```

في التسجيل، الـAPI يطلب من `UserManager<ApplicationUser>` إنشاء المستخدم. في الدخول، يتحقق `UserManager` من كلمة المرور والأدوار، ثم يستدعي الـAPI واجهة `IJwtTokenService` لإصدار الرمز. عند طلب endpoint محمي، JWT Bearer Middleware يتحقق من الرمز قبل أن يصل التنفيذ إلى الـController.

### 2. أين يوجد كل جزء؟

الحل يحتوي على أربع طبقات أساسية، بالإضافة إلى مشروع الاختبارات:

| الطبقة | دورها في Authentication | ملفات فعلية مرتبطة |
|---|---|---|
| `SkyMate.API` | استقبال HTTP requests، إعداد middleware وJWT validation وSwagger، وتحويل النتائج إلى HTTP responses | `Program.cs`, `Controllers/AuthController.cs`, `DTOs/Auth/*`, `Swagger/AuthorizeOperationFilter.cs` |
| `SkyMate.Application` | تعريف العقد الذي يحتاجه الـAPI من خدمة إنشاء JWT، من غير ربط الـAPI بتفاصيل التوقيع | `Interfaces/Authentication/IJwtTokenService.cs`, `DTOs/Auth/AccessTokenResult.cs` |
| `SkyMate.Domain` | كيانات وقواعد مجال السفر. لا يحتوي `ApplicationUser` ولا يعتمد على ASP.NET Identity | `Entities/*`, `Enums/*` |
| `SkyMate.Infrastructure` | تنفيذ التخزين وIdentity وJWT وربط الخدمات بـEF Core وSQL Server | `Identity/ApplicationUser.cs`, `Authentication/JwtOptions.cs`, `Authentication/JwtTokenService.cs`, `Identity/IdentityRoleSeeder.cs`, `Persistence/AppDbContext.cs`, `DependencyInjection.cs` |
| `tests/SkyMate.UnitTests` | اختبارات persistence واختبارات HTTP للتسجيل والدخول والأدوار والرموز | `Authentication/AuthenticationEndpointTests.cs`, `Authentication/AuthApiFactory.cs`, `Authentication/AuthTestController.cs` |

روابط الملفات الرئيسية:

- [Program.cs](<D:/Skymate v1/SkyMate.API/Program.cs>)
- [AuthController.cs](<D:/Skymate v1/SkyMate.API/Controllers/AuthController.cs>)
- [JwtOptions.cs](<D:/Skymate v1/SkyMate.Infrastructure/Authentication/JwtOptions.cs>)
- [JwtTokenService.cs](<D:/Skymate v1/SkyMate.Infrastructure/Authentication/JwtTokenService.cs>)
- [ApplicationUser.cs](<D:/Skymate v1/SkyMate.Infrastructure/Identity/ApplicationUser.cs>)
- [AppDbContext.cs](<D:/Skymate v1/SkyMate.Infrastructure/Persistence/AppDbContext.cs>)
- [DependencyInjection.cs](<D:/Skymate v1/SkyMate.Infrastructure/DependencyInjection.cs>)

### 3. كيف ترتبط الطبقات ببعض؟

- `AuthController` موجود في API؛ لأنه يتعامل مع HTTP status codes وrequest/response DTOs.
- `UserManager<ApplicationUser>` هو أداة Identity الجاهزة لإدارة المستخدمين. عمليات Identity لا تمر عبر Generic Repository.
- `IJwtTokenService` موجود في Application كـinterface؛ والـAPI يعرف ما الخدمة التي يحتاجها من غير أن يعرف طريقة بناء JWT بالتفصيل.
- `JwtTokenService` موجود في Infrastructure، وهو التنفيذ الذي ينشئ claims ويوقّع JWT.
- `DependencyInjection.AddInfrastructure(...)` يسجل `ApplicationUser` وEF Core store و`IJwtTokenService` في DI container.
- `AppDbContext` يرث `IdentityDbContext<ApplicationUser, IdentityRole<int>, int>`؛ لذلك Identity والجداول الخاصة بالمشروع تستخدم سياق قاعدة بيانات واحدًا، مع مفاتيح مستخدمين وأدوار من نوع `int`.
- Generic Repository وUnit of Work موجودان للكيانات العادية الخاصة بالمشروع. لا نستخدمهما لعمليات Identity؛ لهذا توجد `UserManager` و`RoleManager`.

### 4. أول ملف بالتفصيل: `Program.cs`

هذا هو **Composition Root** ونقطة تشغيل الـAPI. وظيفته تجميع الخدمات، إعداد authentication وauthorization، ترتيب middleware، ثم تشغيل التطبيق. لا يحتوي منطق تسجيل مستخدم أو التحقق من كلمة مروره؛ هذا موجود في `AuthController` وIdentity services.

#### تعريف نقطة الدخول

```csharp
public partial class Program
{
    public static async Task Main(string[] args)
```

- `Program` هو class نقطة دخول التطبيق.
- `partial` يسمح بتعريف أجزاء من class نفسه في ملفات أخرى عند الحاجة. كما يجعل النوع متاحًا للاستخدام مع `WebApplicationFactory<Program>` في اختبارات التكامل.
- `Main` هي نقطة بدء تشغيل التطبيق.
- `async Task` مستخدمة لأن تهيئة الأدوار وتشغيل التطبيق عمليتان asynchronous.
- `args` هي arguments التي قد يمررها المشغل إلى التطبيق.

#### إنشاء WebApplicationBuilder وتحميل الإعدادات المحلية

```csharp
var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment() &&
    string.IsNullOrWhiteSpace(builder.Configuration["Jwt:SigningKey"]))
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}
```

`WebApplication.CreateBuilder` ينشئ builder ويحمّل إعدادات التطبيق، مثل `appsettings.json` وenvironment variables، وUser Secrets في Development.

الشرط يضيف User Secrets كـfallback فقط إذا:

1. البيئة الحالية `Development`.
2. مفتاح `Jwt:SigningKey` غير موجود بعد في الإعدادات.

بهذا الشكل لا نحمل User Secrets في Production، ولا نستبدل مفتاحًا قادمًا من test configuration أو environment variable. `optional: true` معناها أن غياب ملف secrets لا يسبب استثناءً هنا؛ التحقق اللاحق سيعطي رسالة أوضح عن الإعداد الناقص.

#### قراءة إعدادات JWT والتحقق منها

```csharp
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT configuration section 'Jwt' is missing.");
```

- `GetSection("Jwt")` يقرأ مجموعة إعدادات JWT.
- `Get<JwtOptions>()` يحول القيم إلى object من نوع `JwtOptions`.
- `JwtOptions.SectionName` ثابت قيمته `"Jwt"` لتجنب تكرار اسم القسم كنصوص متفرقة.
- إذا لم يوجد القسم أصلًا، يتوقف التطبيق برسالة واضحة.

بعدها:

```csharp
jwtOptions.Validate();
```

يتأكد من وجود `Issuer` و`Audience` و`SigningKey` وطول المفتاح، وأن `ExpiryMinutes` موجب. الاستثناء هنا مقصود: تشغيل API بمفتاح JWT مفقود أو ضعيف أخطر من بدء التطبيق بإعداد غير صالح.

يوجد `try/catch` حول التحقق ليضيف اسم البيئة الحالية وإرشادًا: استخدمي launch profile `http` أو `https` في التطوير، أو مرري `Jwt__SigningKey` كـenvironment variable في البيئات الأخرى. لا يطبع المفتاح نفسه.

#### تسجيل MVC وإعداد JSON

```csharp
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
```

- `builder.Services` هي DI container registrations التي سيحتاجها التطبيق.
- `AddControllers()` يسجل دعم MVC Controllers.
- `PropertyNamingPolicy = CamelCase` يجعل JSON properties مثل `AccessToken` تظهر للعميل باسم `accessToken`.
- `JsonStringEnumConverter` يجعل enum values تُرسل كأسماء نصية بدل أرقام. في الإعداد الحالي تُحافظ القيم على الاسم مثل `Balanced`.

#### ProblemDetails وCORS

```csharp
builder.Services.AddProblemDetails();
```

يسجل خدمة تنسيق أخطاء HTTP في شكل Problem Details، بدل كشف exception أو تفاصيل داخلية للعميل.

```csharp
builder.Services.AddCors(options => options.AddPolicy("ConfiguredCors", policy =>
{
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    if (allowedOrigins.Length > 0)
    {
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    }
}));
```

يقرأ قائمة origins المسموح لها من إعداد `Cors:AllowedOrigins`. الإعداد الحالي في `appsettings.json` قائمة فارغة؛ لذلك لا يُسمح لأي browser origin عبر CORS إلى أن تضيف الواجهة الأمامية إلى القائمة.

#### تسجيل Infrastructure ثم إعداد JWT Bearer

```csharp
builder.Services.AddInfrastructure(builder.Configuration);
```

هذا يستدعي extension method موجودة في Infrastructure. هي تسجل `AppDbContext` وSQL Server وIdentity وrepositories وUnit of Work و`IJwtTokenService`.

```csharp
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => { ... });
```

- `AddAuthentication(...)` يحدد أن scheme الافتراضي للتحقق هو JWT Bearer.
- `AddJwtBearer(...)` يضيف handler يقرأ access token من HTTP `Authorization` header ويتحقق منه.
- `MapInboundClaims = false` يحافظ على أسماء JWT claims كما صدرت، مثل `sub` و`email` و`role`.
- `SaveToken = false` لأن التطبيق لا يحتاج تخزين bearer token داخل authentication properties.

داخل `TokenValidationParameters`:

- `ValidateIssuer` و`ValidIssuer` يتحققان من مصدر الرمز.
- `ValidateAudience` و`ValidAudience` يتحققان أن الرمز موجه للـAPI/Client المتوقع.
- `ValidateIssuerSigningKey` و`IssuerSigningKey` يتحققان من التوقيع بمفتاح الإعدادات.
- `ValidateLifetime` يرفض الرمز المنتهي أو الذي لم يبدأ وقت صلاحيته.
- `ClockSkew = 30 seconds` يسمح بفارق ساعة بسيط بين الأجهزة.
- `NameClaimType = "sub"` يجعل معرف المستخدم هو identity name claim.
- `RoleClaimType = "role"` يجعل `[Authorize(Roles = "Admin")]` يقرأ role claims الصحيحة.

بعد إعداد Authentication، يسجل:

```csharp
builder.Services.AddAuthorization();
```

Authentication يثبت **من المستخدم؟** أما Authorization فيقرر **هل هذا المستخدم مسموح له بهذا المورد؟**

#### إعداد Swagger

```csharp
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "SkyMate API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { ... });
    options.OperationFilter<AuthorizeOperationFilter>();
});
```

- `AddEndpointsApiExplorer` يجهز metadata التي يقرأها Swagger.
- `SwaggerDoc` يحدد اسم ووصف إصدار الوثيقة.
- `AddSecurityDefinition` يضيف Bearer/JWT scheme إلى واجهة Swagger.
- `AuthorizeOperationFilter` يضع security requirement على العمليات التي عليها `[Authorize]`، ولا يضعه على Register/Login اللذين عليهما `[AllowAnonymous]`.

#### بناء التطبيق وترتيب middleware

```csharp
var app = builder.Build();

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors("ConfiguredCors");
app.UseAuthentication();
app.UseAuthorization();
```

`builder.Build()` ينشئ التطبيق النهائي من التسجيلات. ترتيب middleware مهم:

1. `UseExceptionHandler` يحول الأخطاء غير المعالجة إلى استجابة آمنة.
2. `UseHttpsRedirection` يوجه HTTP إلى HTTPS عندما يتوفر منفذ HTTPS.
3. `UseCors` يطبق سياسة browser origins.
4. `UseAuthentication` يفحص credentials/token ويملأ `HttpContext.User`.
5. `UseAuthorization` يقرأ ذلك المستخدم ويفحص `[Authorize]` أو الدور المطلوب.

لو عكسنا آخر خطوتين، Authorization سيحاول اتخاذ قرار قبل أن يحدد Authentication هوية المستخدم.

#### Swagger endpoints، controller routes، وزرع الأدوار

```csharp
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
await IdentityRoleSeeder.SeedAsync(app.Services);
await app.RunAsync();
```

- Swagger UI وملف JSON متاحان في Development فقط.
- `MapControllers()` يجعل attribute-routed controllers مثل `/api/v1/auth/login` قابلة للوصول.
- `IdentityRoleSeeder.SeedAsync` ينشئ `User` و`Admin` إن لم يكونا موجودين. يحتاج اتصال SQL Server وجداول Identity موجودة؛ ولا يطبق migrations.
- `RunAsync()` يبدأ استقبال HTTP requests ويبقي التطبيق شغالًا.

### 5. أشياء مهمة لاحظناها في الكود الحالي

- إعداد JWT الافتراضي في `appsettings.json` يحتوي `Issuer` و`Audience` و`ExpiryMinutes=60`، ولا يحتوي المفتاح السري.
- `launchSettings.json` يحدد `DOTNET_ENVIRONMENT` و`ASPNETCORE_ENVIRONMENT` إلى `Development` لملفي `http` و`https`.
- `appsettings.json` يحتوي `Cors:AllowedOrigins` كقائمة فارغة؛ لازم إعداد أصل الواجهة قبل browser integration.
- Swagger مفعّل في Development فقط.
- تم حذف `WeatherForecastController` وملف `WeatherForecast` وطلب `/weatherforecast` التجريبي الموروث من قالب المشروع.
- التشغيل يعتمد على قاعدة البيانات الموجودة؛ `Program.cs` يزرع الأدوار عند بدء التشغيل لكنه لا ينشئ المخطط ولا يشغل migration.

### أسئلة مقابلة بسيطة — الجزء الأول

1. إيه الفرق بين تسجيل خدمة داخل `builder.Services` واستخدام middleware مثل `app.UseAuthentication()`؟
2. ليه لازم `UseAuthentication()` يسبق `UseAuthorization()`؟
3. ما وظيفة `RoleClaimType = "role"` عند استخدام `[Authorize(Roles = "Admin")]`؟

جاوبي على الأسئلة، وبعدها اكتبي `Next` لما تكوني جاهزة للجزء التالي. الجزء القادم هيكون `DependencyInjection.cs` و`JwtOptions.cs` وكيف تتحول الإعدادات إلى خدمات قابلة للاستخدام.
