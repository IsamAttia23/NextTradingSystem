# مراجعة التصميم والتصحيحات

المستودع: https://github.com/IsamAttia23/NextTradingSystem
النسخة التي تمت مراجعتها: d4c94d02dc4a5bd018891d513d4715d5e212b154 — الفرع master.
تاريخ المراجعة: 27 أكتوبر 2026.

## نتيجة المراجعة

المشروع يحتوي على تقسيم Domain / Application / Infrastructure / Web، لكن أسماء المشاريع وحدها لا تحقق Clean Architecture. نفذت تصحيحات في اتجاه الاعتماديات والحفظ ومعالجة الأخطاء، وأضفت اختبارات قابلة للتشغيل. هذه النسخة لا تعني اكتمال جميع قواعد التداول والمحاسبة أو تحول Domain إلى نموذج مستقل تماماً عن الأطر.

## الإصلاحات المنفذة

| المشكلة السابقة | التصحيح والسبب |
|---|---|
| Application يستعمل EF Core في Pagination ولا يملك مرجع الحزمة، فيفشل البناء | نقل التنفيذ إلى Infrastructure والإبقاء على واجهة الخدمة في Application؛ تطبيق Dependency Inversion |
| Application يعتمد على ASP.NET Core بالكامل | استبدال FrameworkReference بحزم Extensions المحددة؛ اختبار يمنع الرجوع إلى EF أو HTTP أو Infrastructure |
| QueryModels ونتائج الصفحات موجودة في Domain | نقلها إلى Application لأنها عقود استعلام وعرض وليست قواعد أعمال |
| واجهات المستخدم الحالي وبيانات الطلب موجودة في Infrastructure | نقل الواجهات إلى Application/Abstractions/Context مع إبقاء تنفيذ HttpContext في Infrastructure |
| UnitOfWork يحل المستودعات عبر IServiceProvider ويستقبل TKey غير مستخدم | حذف Repository<TEntity,TKey>()؛ حقن المستودع المطلوب مباشرة، وقصر UnitOfWork على الحفظ والمعاملة |
| مستودعات متخصصة موجودة دون تسجيل DI | تسجيل واجهاتها وتنفيذاتها وفحص إمكانية حلها داخل scope |
| تسجيل Identity مكرر، ولا يوجد مسار مصادقة في Web | توحيد تسجيل Identity وإضافة DataProtection وتسجيل Identity cookies وترتيب Authentication قبل Authorization؛ لا يضيف هذا شاشة دخول أو صلاحيات أعمال تلقائياً |
| Exception middleware موجود في Infrastructure وغير مستخدم، ويكشف ex.Message للأخطاء الداخلية | نقله إلى Web وتفعيله، وإرجاع ProblemDetails / ValidationProblemDetails مع traceId وتسجيل الاستثناء الداخلي؛ عدم ابتلاع إلغاء الطلب أو محاولة الكتابة بعد بدء الرد |
| وجود نسختين متنافستين من الاستثناءات | حذف النسخ غير المستخدمة داخل Infrastructure والإبقاء على Application.Exceptions |
| حذف الكيان يغير DeletedAt مباشرة ويتجاوز سلة المحذوفات؛ والكيان غير القابل للحذف المنطقي لا يحذف أصلاً | استعمال DbSet.Remove؛ خط الحفظ المشترك ينفذ الحذف المنطقي ويسجل snapshot، بينما الكيانات الأخرى تحذف فعلياً |
| سلة المحذوفات تستعمل int وتحوّل Guid إلى int، وتقرأ مع تفعيل فلتر إخفاء المحذوفات | خدمة generic للنوع المطلوب، ومعرّف TKey، وIgnoreQueryFilters داخل الاستعادة والسلة؛ إزالة reflection وService Locator |
| HardDelete يعاد تحويله إلى حذف منطقي | مسار داخلي صريح للحذف النهائي يتجاوز التحويل لهذا الكيان فقط، ويتحقق أن الكيان محذوف، ويسجل Purge؛ تفويض المستخدم مسؤولية حالة الاستخدام المستدعية |
| فحص العلاقات يفحص أول عمود فقط ويبحث عن DeletedOn غير الموجود | مطابقة جميع أعمدة المفتاح الخارجي المركب واستخدام فلاتر الحذف القائمة |
| Identity audit يستثني أنواع long رغم استعمال Guid | تصحيح الأنواع المستثناة إلى Guid |
| RowVersion مهيأ كقيمة يولدها bytea تلقائياً | رمز byte[] جديد يولده خط الحفظ، مع الحفاظ على OriginalValue للمقارنة؛ ترحيل مستقل يحدّث metadata دون تغيير الأعمدة أو البيانات |
| التحقق في PermissionService يعتمد على المرور عبر Handler | التحقق أيضاً عند استدعاء الخدمة مباشرة قبل Trim أو الوصول لقاعدة البيانات |
| PaymentRepository يلغي ترتيب المستخدم في return | الحفاظ على الترتيب المختار وربطه بالمعرّف؛ إصلاح فحوص null في استعلامات العلاقات |
| الصفحات العامة غير مرتبة | ترتيب الكيانات بالمعرّف قبل Pagination وفحص تجاوز إزاحة الصفحات |
| تحذيرات null ومجموعات غير مهيأة ومسارات جهاز داخل csproj | تهيئة القيم والمجموعات وإزالة المسارات المطلقة وتكرار تخزين DbContext في الوراثة |

## اتجاه الاعتماديات بعد التعديل

- Domain: الكيانات وقيم الأعمال والتعدادات والعقود المشتركة للكيانات.
- Application → Domain: حالات الاستخدام، DTOs، التحقق، QueryModels، واجهات التخزين والسياق.
- Infrastructure → Application / Domain: EF Core وPostgreSQL وIdentity والتدقيق وسلة المحذوفات.
- Web → Application / Infrastructure: composition root، HTTP، middleware والمصادقة.
- Tests: اختبارات حدود الطبقات وDI والسلوك والحفظ.

## التحقق المنفذ

- `dotnet build NextTradingSystem.slnx --no-restore`: ناجح، 0 أخطاء، 0 تحذيرات.
- `dotnet test NEXT.Tests/NEXT.Tests.csproj --no-build --no-restore`: 10 اختبارات ناجحة.
- EF Core: لا توجد تغييرات نموذج معلقة بعد الترحيل الجديد.
- توليد SQL لجميع الترحيلات: ناجح؛ لم ينفذ على أي قاعدة بيانات.
- الاختبارات تغطي: حدود Application، حل المستودعات وDI، تحويل الحذف، حذف الكيان غير المنطقي، التحقق المباشر، عدم تسريب الأخطاء، تفاصيل أخطاء الحقول، الحذف/الاستعادة/Purge والتدقيق، منع Purge للكيان النشط، ورفض تحديث برمز تزامن قديم.
- اختبارات التخزين تستخدم EF InMemory، ولا تثبت معاملات PostgreSQL أو قيوده أو ترجمة كل LINQ. يلزم تشغيل اختبارات تكامل على PostgreSQL منفصل قبل اعتماد النسخة للإنتاج.
- واجهت البيئة مشكلة TLS في NuGet؛ تم تنزيل MediatR 12.4.1 كما هو، واستعمال الحزم المتاحة محلياً. لم تتم ترقية الحزم تعويضاً عن هذا العائق. لم يجر تدقيق ثغرات الحزم.

## تغييرات تستلزم تحديث المستدعين

1. استبدال namespaces الخاصة بالاستعلامات إلى NEXT.Application.QueryModels وNEXT.Application.Common.QueryModels.
2. استعمال NEXT.Application.Abstractions.Context لواجهات السياق.
3. حذف استدعاءات unitOfWork.Repository واستعمال constructor injection.
4. سلة المحذوفات: IRecycleBinService<TEntity,TKey>، وList(query)، وRestore(id)، وHardDelete(id)، وRecycleBinItemDto<TKey>؛ النوع يحدد عند إغلاق generic بدلاً من string runtime.
5. معالجة الاستثناءات: NEXT.web.Middleware؛ الأخطاء تصبح ProblemDetails وقد يحتاج العميل لتحديث قراءة الرد.
6. جميع الكتابات إلى كيانات RowVersion يجب أن تمر عبر SaveChanges؛ ExecuteUpdate والكتابة المباشرة إلى SQL تحتاج تحديث رمز التزامن صراحة. عند تحرير DTO عبر طلبين منفصلين يجب أن يحمل DTO الرمز الأصلي ويطبقه المستودع على OriginalValue؛ الاختبار الحالي يثبت التزامن بين سياقين ولا يضيف شاشات تعديل للمنتجات.

## تحسينات معمارية متبقية تحتاج قراراً مستقلاً

- Domain ما زال يرث IdentityUser / IdentityRole / IdentityUserRole ويعتمد على Identity.Stores. الفصل الصارم يحتاج نماذج Identity داخل Infrastructure وعقود مستخدمين مستقلة في Application، مع الحفاظ على العلاقات وخرائط EF والترحيلات. لم أبدل هوية المستخدمين أو مخططهم ضمن هذه الإصلاحات.
- كيانات الفواتير والمخزون والمحاسبة ما زالت أقرب إلى بيانات عامة. يلزم تحديد invariants والأحداث والانتقالات المسموحة ثم تحويل العمليات إلى aggregates وسلوك أعمال، مثل PostInvoice وReserveStock. لا يمكن اختراع قواعد الضريبة أو المخزون السالب دون متطلبات.
- IRepository يعرض IQueryable، وبعض العقود واسعة. الأفضل فصل واجهات read/write وإضافة عقود استعلام مخصصة تعيد DTOs، بدلاً من توسيع generic CRUD لكل حالة.
- إنشاء مستخدم ثم إسناد دور يستخدم تعويضاً بالحذف عند الفشل. الأفضل حالة استخدام ذرية عبر معاملة واحدة؛ توجد أيضاً سباقات فحص التفرد قبل الإدراج يجب أن تضبطها قيود القاعدة وترجمة تعارضاتها.
- CancellationToken لا يصل إلى جميع عمليات CRUD؛ يستحسن تمريره عبر العقود والخدمات حتى آخر عملية I/O.
- يلزم ربط صلاحيات الأدوار بنظام Authorization فعلي، واختبار الوصول إلى الاستعادة وPurge، وإضافة قيود وفحوص محاسبية ومخزنية حسب حالات الاستخدام. تسجيل RolePermission وحده لا يفرض الصلاحيات.
- يلزم توحيد نتيجة العمليات: بعض المسارات ترجع Result وبعضها يرمي استثناء، والتحقق موزع بين DTOs وHandlers وخدمات. يمكن توحيده تدريجياً دون إخفاء قواعد الأعمال داخل generic handler.

## التشغيل

يتطلب .NET SDK 10 وPostgreSQL. اضبط `ConnectionStrings__NEXTDatabase` أو User Secrets؛ لا تضع كلمة مرور في Git.

```powershell
dotnet restore NextTradingSystem.slnx
dotnet build NextTradingSystem.slnx
dotnet test NEXT.Tests/NEXT.Tests.csproj
dotnet ef migrations list --project NEXT.Infrastructure --startup-project NEXT.web
dotnet ef database update --project NEXT.Infrastructure --startup-project NEXT.web
dotnet run --project NEXT.web
```

راجع نسخة احتياطية وقاعدة اختبار قبل تشغيل database update. لم أطبق الترحيلات أو أرفع تعديلات إلى GitHub.
