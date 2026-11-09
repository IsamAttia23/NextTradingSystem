# ربط Gmail API مع NEXT على Render

أصبح المشروع يدعم الإرسال من حساب Gmail عبر HTTPS، دون SMTP أو كلمة مرور التطبيقات. يستخدم طابور رسائل الأمان الموجود، مع تجديد Access Token تلقائيًا بواسطة Refresh Token. لا يوجد تعديل لقاعدة البيانات أو واجهة المستخدم.

تفويض Google يخص حساب الإرسال فقط؛ لا يحتاج مستلمو رموز OTP إلى تسجيل الدخول عبر Google أو الإضافة إلى Test users.

## 1. إنشاء مشروع Google وتفعيل Gmail API

1. افتح [Google Cloud Console](https://console.cloud.google.com/) بحساب Google الذي تملكه.
2. من قائمة المشاريع أعلى الصفحة اختر **New Project**، وسمّه `NEXT Trading System`، ثم اختره بعد إنشائه.
3. افتح **APIs & Services → Library**، وابحث عن **Gmail API** واضغط **Enable**.

## 2. ضبط شاشة موافقة Google

1. افتح **Google Auth Platform → Branding**، ثم **Get Started** إن لم تكن المنصة مضبوطة.
2. أدخل اسم التطبيق `NEXT Trading System`، وبريد الدعم وبريد التواصل الخاصين بك.
3. لحساب Gmail الشخصي اختر **External** ضمن Audience.
4. أثناء التجربة، افتح **Audience → Test users** وأضف حساب Gmail الذي سيُرسل الرسائل.
5. في **Data Access → Add or Remove Scopes** أضف صلاحية الإرسال فقط:

   ```text
   https://www.googleapis.com/auth/gmail.send
   ```

   هذه الصلاحية لا تمنح قراءة البريد أو حذفه. راجع [دليل الموافقة الرسمي](https://developers.google.com/workspace/guides/configure-oauth-consent) و[صلاحيات Gmail](https://developers.google.com/workspace/gmail/api/auth/scopes).

## 3. إنشاء OAuth Client

1. افتح **Google Auth Platform → Clients → Create client**.
2. اختر النوع **Web application** والاسم `NEXT Mail Sender`.
3. أضف في **Authorized redirect URIs** هذا العنوان حرفيًا:

   ```text
   https://developers.google.com/oauthplayground
   ```

4. اضغط Create، واحتفظ بـ **Client ID** و**Client Secret** في مكان خاص. لا ترسلهما في المحادثة ولا تضعهما في Git.

لا تحتاج إضافة مسار callback إلى تطبيق NEXT؛ سنستخدم أداة Google الرسمية لمنح إذن حساب الإرسال مرة واحدة.

## 4. استخراج Refresh Token

1. افتح [OAuth 2.0 Playground](https://developers.google.com/oauthplayground/).
2. اضغط الترس، وفعّل **Use your own OAuth credentials**.
3. أدخل Client ID وClient Secret اللذين أنشأتهما. اترك OAuth endpoints على Google، وOAuth flow على Server-side، واختر **Access type: Offline** و**Force prompt: Consent Screen**.
4. في Step 1 أدخل الصلاحية:

   ```text
   https://www.googleapis.com/auth/gmail.send
   ```

5. اضغط **Authorize APIs** وسجّل الدخول بحساب Gmail الذي سيُرسل الرسائل، ووافق على الإرسال.
6. في Step 2 اضغط **Exchange authorization code for tokens**.
7. احتفظ بقيمة **Refresh token**، وليس Access token المؤقت، وضعها في إعدادات Render أدناه.

استخدام بيانات OAuth الخاصة بك مهم؛ الرموز الناتجة باستخدام بيانات Playground الافتراضية تُلغى بعد 24 ساعة وفق [تنبيه Google في Playground](https://developers.google.com/oauthplayground/).

## 5. نشر الكود وإعداد Render

يجب أولًا رفع تعديل Gmail API إلى GitHub ونشر النسخة التي تحتوي عليه. إعداد متغيرات Gmail على النسخة القديمة وحده لا يضيف دعم الإرسال.

افتح خدمتك في Render ثم **Environment** وأضف القيم الأربع دفعة واحدة:

| KEY | VALUE |
| --- | --- |
| `Email__Gmail__ClientId` | Client ID من Google |
| `Email__Gmail__ClientSecret` | Client Secret من Google |
| `Email__Gmail__RefreshToken` | Refresh token من الخطوة السابقة |
| `Email__Gmail__From` | `NEXT <your-account@gmail.com>` مع استبدال الحساب بحساب الإرسال الفعلي |

أبقِ `AccountSecurity__SecretKey` الحالي كما هو، ثم اختر **Save and deploy**. لا تحتاج إعداد `Email__Smtp__Password` أو Gmail App Password لاستخدام Gmail API. راجع [متغيرات Render](https://render.com/docs/configure-environment-variables).

- عند اكتمال إعداد Gmail تكون له الأولوية، ثم Resend إن لم يضبط Gmail، ثم SMTP.
- الإعداد الجزئي لـ Gmail يُرفض عند بدء التشغيل برسالة تطلب الحقول الناقصة؛ لذلك احفظ القيم الأربع قبل النشر.
- يجب أن يكون From هو حساب Google الذي منح التفويض، أو عنوان إرسال بديل موثّق ومسموح به لهذا الحساب.
- تنفّذ الخدمة تجديد الرمز والإرسال عبر HTTPS، فلا تعتمد على منافذ SMTP المحجوبة في Render المجاني.
- الترجمة لا تتغير: الرسالة التي أنشأتها الخدمة بالعربية أو الإنجليزية تُرسل كما هي مع ترميز UTF-8.

## 6. اختبار الإرسال

1. تأكد أن النشر الجديد يعرض `Application started`، ولا يظهر تحذير تعطيل إرسال بريد الأمان.
2. من التطبيق أنشئ مستخدم اختبار ببريد تملكه أو اطلب استعادة كلمة المرور لحساب اختبار موجود ونشط.
3. افحص الوارد والبريد غير المرغوب فيه وسجل Sent في حساب Gmail المرسل.
4. لمتابعة حالة طابور الرسائل من Neon دون كشف الرمز أو محتوى الرسالة:

   ```sql
   SELECT "Recipient", "CreatedAt", "Attempts", "SentAt", "CancelledAt"
   FROM security."SecurityEmailMessages"
   ORDER BY "CreatedAt" DESC
   LIMIT 5;
   ```

   وجود SentAt يعني قبول خدمة البريد للإرسال، وليس ضمان وصوله إلى صندوق الوارد. لا تستخدم صفًا قديمًا انتهت صلاحيته للاختبار؛ أنشئ طلبًا جديدًا بعد اكتمال الربط.

## مدة التفويض والأخطاء

- في وضع **External / Testing** تنتهي صلاحية Refresh Token الذي يتضمن Gmail scopes بعد 7 أيام. للإرسال المستمر، اضبط وضع التطبيق المناسب في **Audience** قبل إصدار رمز التجديد النهائي، واتبع متطلبات Google التي تظهر لحالتك. الانتقال إلى Production ليس ضمانًا لعدم انتهاء التفويض؛ تغيير كلمة المرور أو سحب الإذن قد يبطل الرمز أيضًا. [دورة حياة Refresh Token](https://developers.google.com/identity/protocols/oauth2#expiration).
- إذا ظهر `redirect_uri_mismatch`، تأكد أن عنوان Playground مضاف حرفيًا إلى Authorized redirect URIs في **نفس OAuth Client**.
- خطأ التفويض HTTP 400 قد يعني Refresh Token منتهيًا أو ملغى أو صادرًا لعميل آخر. أعد تفويض الحساب بنفس Client ID/Secret واستبدل RefreshToken في Render.
- HTTP 403 عند الإرسال قد يعني أن Gmail API غير مفعّل أو أن صلاحية `gmail.send` لم تُمنح أو أن الحساب يخضع لسياسة تمنعها.
- إلغاء Access Token يؤدي إلى تجديده في محاولة الطابور التالية؛ لا ترسل الخدمة الرسالة مرتين تلقائيًا داخل نفس محاولة HTTP.
- يظل نجاح التسليم الفعلي بحاجة إلى تجربة بعد إدخال بيانات Google؛ اختبارات الكود تستخدم HTTP وهميًا ولا ترسل بريدًا إلى أي شخص.

## الملفات وفصل المسؤوليات

- `Options/GmailEmailOptions.cs`: وصف الإعدادات وحالتها فقط.
- `Email/GmailAccessTokenProvider.cs`: تجديد Access Token وتخزينه في الذاكرة، مع منع التجديد المتكرر عند الطلبات المتزامنة.
- `Email/GmailMessageEncoder.cs`: تجهيز MIME وBase64URL وترميز العناوين والنص بالعربية والإنجليزية، ورفض حقن ترويسات البريد.
- `Email/GmailEmailTransport.cs`: طلب الإرسال إلى Gmail فقط. يعيد استخدام `IEmailTransport` الموجود.
- `ConfiguredEmailTransport` و`SecurityEmailWorker`: توسيع الاختيار والتفعيل الموجودين؛ إعادة استخدام طابور الإرسال وسياسة إعادة المحاولة الحالية.
- `GmailEmailTests`: تغطية الترميز والتجديد والتزامن وانتهاء الرمز والأخطاء الآمنة واختيار مزود البريد. لا توجد حزم NuGet جديدة.

## نتيجة التحقق

نجح تشغيل اختبارات الحل كاملة: **178 اختبارًا** دون فشل أو تخطٍّ، بما فيها اختبارات PostgreSQL في قواعد محلية مؤقتة معزولة. نجحت اختبارات البريد الـ **15**، منها 14 اختبارًا جديدًا لـ Gmail واختبار Resend الموجود. لا يتضمن ذلك إرسال رسالة فعلية من حساب Google؛ يلزم إكمال التفويض والإعدادات السابقة لاختبار التسليم.
