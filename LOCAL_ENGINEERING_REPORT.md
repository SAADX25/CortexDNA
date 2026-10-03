# تقرير المراجعة المحلية — CortexDNA

التاريخ: 2026-10-04، Asia/Amman. المصدر الوحيد للفحص والتعديل: E:/Code-Setup/CortexDNA.
كانت نسخة العمل نظيفة في أول git status. لم تُستخدم نسخة GitHub أو remote للمقارنة.
لم يُنشأ branch أو commit أو Pull Request أو Release، ولم تُنفّذ أي عملية push/fetch/pull.

## 1. المشاكل المكتشفة والإصلاحات

1. Disk Cleanup كان يتبع Junction/Reparse Points في الفحص والحذف؛ صار يرفضها في الجذور والأسلاف والملفات.
2. إمكانية استبدال المجلد بعد فحصه؛ أضيفت handles للأسلاف دون مشاركة write/delete، وتبقى مفتوحة أثناء الحذف. أثبت اختبار Windows أن قراءة attributes وحدها غير كافية؛ استُخدم GENERIC_READ.
3. قبول مسارات تنظيف عشوائية؛ التحقق الآن من category/root/RequiresUpdateServices مقابل قائمة معتمدة، مع رفض جذور الأقراص.
4. الاعتماد على TEMP القابل للتغيير؛ مجلد المستخدم أصبح LocalAppData/Temp القياسي.
5. تعديل ReadOnly قبل الحذف؛ أُلغي تعديل السمات، والملفات المحمية تُترك.
6. حذف المجلدات مع traversal recursive؛ التنظيف يحذف الملفات فقط ويحتفظ بالجذور والمجلدات.
7. اختلاف نطاق Recent بين scan وclean؛ كلاهما top-level فقط.
8. Scan يحدّث نماذج UI من worker؛ يعيد نماذج منفصلة الآن.
9. ابتلاع cancellation وغياب serialization؛ الإلغاء يُراعى والعمليات serialized، مع نسخ قائمة المدخلات قبل الانتظار.
10. net stop اعتبر انتهاء العملية نجاحًا دون ExitCode، مع timeout يترك العملية؛ استُبدل بـSCM API والتحقق من الحالة ومهلة واضحة.
11. بدء خدمات كانت متوقفة أصلًا، وعدم استعادة partial stops؛ تُحفظ الخدمات التي كانت running فقط وتُستعاد عند النجاح والفشل والإلغاء.
12. إمكانية استمرار تنظيف Update Cache بعد فشل الإيقاف أو restart خارجي؛ يتوقف التنظيف ويبلغ الخطأ، مع فحص الحالة قبل الحذف.
13. فشل استعادة الخدمات لا يظهر للمستخدم؛ نتيجة التنظيف تعرضه، وتستمر محاولة استعادة الخدمة الأخرى.
14. GetHashCode عشوائي عبر العمليات وقد يفشل Math.Abs على int.MinValue؛ SHA-256 ثابت يحل المشكلة، مع SID لعزل المستخدمين.
15. ExtractExecutable وSplitCommand كانا مختلفين، وقد تعتبر .exe في arguments اسم الملف؛ parser موحد يحافظ على arguments ويرفض الاقتباس غير المغلق.
16. Delay يستطيع جدولة مسار غير كامل أو ملتبس؛ لا يقبل إلا executable موجودًا بمسار absolute، ويرفض working directory غير صالح ويحفظ الصالح من الاختصارات.
17. فشل حذف task كان يُبتلع وتعرض UI نجاحًا؛ صار exception واضحًا، مع rollback في facade عند فشل خطوة لاحقة.
18. تعطيل delayed item لا يزيل scheduled task؛ أصبح تغيير الحالة يزيل التأخير وفق العملية المطلوبة.
19. Delay معطّل أصليًا كان يظهر Disabled دون وصف التأخير؛ UI يعرض بدءًا بعد 30 ثانية.
20. Policy Run entries تُعرض كأن StartupApproved يتحكم بها، مع احتمال تصادم الهوية؛ أُزيلت من قائمة التعديل.
21. packaged startup كان يجمع sibling tasks ويخلق مفاتيح registry تخمينية؛ الكتابة إلى exact existing task فقط، دون policy/unknown states، مع rollback للقيم المكتوبة عند الفشل.
22. موارد COM وProcess قد تبقى عند exceptions/early return؛ أُضيف تحرير shortcut/task/action/trigger وعمليات probe/game detection.
23. تشغيل initialization قبل إنشاء timer، وOpen/Accept/Close أو counters أثناء disposal؛ timer يسبق initialization، والمهام متتبعة والموارد serialized والإغلاق ينتظرها.
24. async refresh المتكرر يمكن أن يضيع مرجع المهمة الجارية؛ لا يستبدل tracking عند عدم بدء عملية جديدة.
25. shutdown أثناء scan/clean/boost أو startup load؛ cancellation وانتظار العمليات قبل الإغلاق، لمنع تحرير الموارد مبكرًا واستعادة الخدمات قبل الخروج العادي.
26. reentrant Exit أثناء shutdown؛ guard يمنع تكرار الإغلاق.
27. افتراض clock ثابت لمعالج محدد؛ أُزيل الافتراض. Network rate كان لا يقسم على الوقت الحقيقي خاصة في Game Mode؛ صار يعتمد على elapsed time.
28. RAM Boost كان يستخدم GC ويستهدف عمليات خارج session/user؛ أُزيل forced GC، واستُخدم owner/session check، واستُبعد foreground/critical/game processes، مع confirmation وcancellation.
29. Administrator إلزامي للتطبيق وسكربتات البناء؛ app صار asInvoker وسكربتات البناء دون elevation/force kill، وأُصلحت tooltips.
30. تشغيل system tools بالبحث عن الاسم قد يختار executable من directory/PATH؛ المسارات تُشتق من Windows SystemDirectory الآن.
31. تحميل theme name من ملف إعداد دون whitelist؛ لا تُحمّل إلا الثيمات المدمجة.
32. log في install directory غير قابل للكتابة أحيانًا وغير محدود؛ نُقل إلى LocalAppData مع rotation عند 1 MiB ونسخة احتياطية وexception details.
33. IPC global لجميع المستخدمين وwait loop لا ينتهي عند disposal؛ Local session + SID وregistered wait يُلغى عند الخروج.
34. تجاهل كل UI exceptions قد يبقي التطبيق في حالة فاسدة؛ الأخطاء غير المتوقعة تُسجل ولا تُبتلع عالميًا.
35. Build/installer بمسارات أجهزة ثابتة، ومخرجات stale/duplicates؛ مسارات مشتقة من script، publish جديد لكل build، وملفات installer دون تكرار.
36. Uninstaller يحذف app/profile folders بشكل recursive وforce-kills processes؛ أُلغي ذلك، واستُخدم CloseApplications وinstaller-owned file removal.
37. README وصف privacy classes/features غير موجودة؛ أعيدت كتابته وفق النسخة المحلية، مع صلاحيات وأمان واختبارات وحدود واضحة.
38. gitignore يغطي bin/obj للجذر فقط؛ أضيفت المخرجات المتداخلة وIDE/logs/tests/artifacts/backups.
39. ستة سكربتات تحويل قديمة بلا runtime/build/test references وdead helpers؛ حُذفت بعد البحث والفحص.
40. لا اختبارات أو CI محلية؛ أضيف harness محلي بدون test-framework packages وoffline verification وCI file محلي فقط.

## 2. الملفات المعدلة والمضافة

الملفات المعدلة الموجودة أصلًا:

- .gitignore
- App.xaml.cs
- AssemblyInfo.cs
- Build_Release.bat
- Core/DiskCleanupService.cs
- Core/Logger.cs
- Core/NativeMethods.cs
- Core/RamOptimizer.cs
- Core/Startup/StartupCatalogService.cs
- Core/Startup/StartupDelayService.cs
- Core/Startup/StartupFeatureService.cs
- Core/Startup/StartupPackagedCatalog.cs
- Core/Startup/StartupPaths.cs
- Core/Startup/StartupProcessProbe.cs
- CortexDNA.csproj
- CortexDNA_Installer.iss
- Cortex_Dev.bat
- MainWindow.xaml
- MainWindow.xaml.cs
- Models/StartupModels.cs
- README.md
- ViewModels/HardwareViewModel.cs
- ViewModels/StartupViewModel.cs
- app.manifest

الملفات الجديدة:

- .github/workflows/windows.yml
- Core/CleanupPathGuard.cs
- Core/UpdateServiceLease.cs
- Tests/CortexDNA.Tests.csproj
- Tests/NuGet.Offline.config
- Tests/Program.cs
- scripts/Build-Release.ps1
- scripts/Verify-Local.ps1
- LOCAL_ENGINEERING_REPORT.md

المخرجات المحلية المحفوظة، gitignored:

- artifacts/verification/full-verification.txt
- artifacts/installer/CortexDNA_Installer_v2.0.0.exe
- artifacts/publish/<build-id>/
- bin/obj وTests/bin/obj الناتجة عن البناء.

## 3. الملفات المحذوفة وأسبابها

جرى البحث عن أسماء السكربتات ومراجعها في source/build/tests والملفات المخفية خارج .git/bin/obj،
وفحص contents ومشروع SDK. لا references لها ولا CopyToOutput/targets تستدعيها؛ هي أدوات تعديل source لمرة واحدة.

| الملف | السبب |
|---|---|
| append_row2.py | نقل XAML سابق من backup قديم غير موجود؛ غير مستخدم |
| append_row2_fix.py | نسخة إصلاح لأداة append قديمة تستخرج source من HEAD؛ غير مستخدمة |
| clean_mainwindow_cs.py | تحويل regex قديم لحذف handlers ونقل UI؛ غير مستخدم |
| extract_dashboard.py | استخراج dashboard من XAML قديم؛ التحويل مكتمل |
| update_mainwindow.py | تعديل layout وبindings مرة واحدة؛ غير مستخدم |
| update_xaml.py | توليد sidebar/privacy layout قديم؛ غير مستخدم في البناء أو runtime |

حُذفت كذلك methods/declarations غير المستخدمة بعد reference search:
GenerateColoredCircle وCopyBiosInfo_Click، وSW_RESTORE/SetForegroundWindow/ShowWindow،
وبعد إصلاح packaged paths أُزيلت FindOrCreateTaskPath وFindStartupTasksFolder وAddPath التي لم تعد لها callers.
من DiskCleanupService أزيلت helpers التنظيف القديمة وأوامر cmd المعيبة بعد استبدال callers بالتنفيذ الآمن.
أُزيل ملف scratch أنشأته هذه الجلسة فقط: %TEMP%/cortex-cleanup-header.txt؛ لم يُستخدم.
اختبارات harness حذفت fixtures التي أنشأتها تحت Tests/bin فقط؛ الروابط تُحذف دون recursion.
لم تُحذف بيانات Windows الحقيقية أو logs المستخدم السابقة أو مجلدات المشروع الأصلية bin/obj.

## 4–5. نتائج Git

كل التغييرات محلية وغير committed/staged. سيأتي أدناه git status --short وgit diff --stat النهائيان.
ملاحظة: git diff --stat يعرض الملفات tracked فقط؛ الملفات التسعة الجديدة لا تدخل إحصاءه ما دامت untracked.

## 6. نتائج التحقق النهائية

- .NET SDK 10.0.400؛ MSBuild 18.9.6؛ Windows build 26300.
- Offline restore من cache: نجح؛ مصادر NuGet محذوفة في config المستخدم للتحقق، وNuGetAudit معطل.
- Debug build مع -warnaserror: نجح؛ 0 errors، 0 warnings.
- Release build مع -warnaserror: نجح؛ 0 errors، 0 warnings.
- Regression harness: 27 total، 27 passed، 0 failed؛ exit code 0.
- WPF smoke: ناجح ضمن الاختبارات الـ27؛ dashboard/startup read-only scan/minimize/restore/explicit exit.
- Release win-x64 framework-dependent ReadyToRun publish: نجح.
- Inno Setup installer compile: نجح؛ المحلي artifacts/installer/CortexDNA_Installer_v2.0.0.exe.
- git diff --check: نجح؛ لا whitespace errors.
- CI prepared locally only؛ لم تُشغل GitHub Actions ولم تُرفع الملفات.
- لم يُشغّل installer install/uninstall على الجهاز؛ اختُبر تجميعه فقط.

سجل التحقق الكامل محفوظ محليًا في artifacts/verification/full-verification.txt.
حالات فشل سابقة أثناء التطوير أصلحت وأعيد التحقق؛ الأرقام أعلاه للنسخة النهائية.

## 7. المشاكل/حدود التحقق المتبقية

- مهام Delay القديمة ذات randomized IDs لا تُهاجر أو تُحذف آليًا. مراجعتها في Task Scheduler مطلوبة قبل إعادة تطبيق delay على عنصر قديم؛ لم نعدل مهام الجهاز الحالية.
- استعادة الخدمات اختُبرت بفakes للحالات stopped/running/partial failure/cancellation/restore failure/external restart. لم نوقف الخدمات الحقيقية أو ننظف Update Cache الحقيقي.
- لم نغير startup approval/packaged state أو ننشئ scheduled tasks حقيقية للتحقق؛ يلزم integration testing على Windows الهدف وفي VM.
- لا اختبار تثبيت/إزالة في VM نظيفة، ولا مصفوفة Windows 10/11، أو multi-user/elevation IPC، أو جميع أجهزة CPU/GPU.
- لم يُجرَ online dependency vulnerability audit؛ versions الأصلية بقيت، ولا يوجد LICENSE في المشروع المحلي.
- تأخيرات/cancellation داخل WMI/LibreHardwareMonitor/COM ليست قابلة للمقاطعة أثناء native call؛ shutdown ينتظر اكتمال العمليات الجارية.

## 8. المخاطر المتبقية

- Cleanup حذف دائم؛ الملفات المؤقتة قد تكون مستخدمة حتى إن لم تكن locked، وقياس FreedBytes منطقي لا physical space guaranteed.
- Windows أو برنامج آخر قد يغير service state بعد آخر فحص. لا ضمان لاستعادة الخدمات إذا force-kill/power loss/crash قطع finally؛ استعادة الحالات الحقيقية تحتاج اختبار VM.
- Read locks قد تجعل بعض المجلدات غير قابلة للتنظيف؛ الاختيار الآمن هو skip.
- Windows startup approval/packaged registry state يختلف حسب OS/policy؛ unknown/policy writes مرفوضة، وبعض shortcuts/options لا يمثلها Task Scheduler بالكامل.
- RAM trimming مؤقت وقد يزيد page faults/latency؛ available-memory delta لا يثبت attribution. لا ضمان لزيادة الأداء.
- sensor drivers وعمليات native والامتيازات عند تشغيل التطبيق يدويًا كAdministrator توسع سطح المخاطر؛ التشغيل الافتراضي standard user.
- cache/theme/logs في profile المستخدم؛ logs قد تحتوي paths/exception details ولا تطبق redaction. حمايتها تعتمد على ACLs المحلية؛ rotation best-effort.
- التطبيق قد يفتح روابط خارجية عند اختيار المستخدم زر Updates/About؛ لم يتم استدعاء هذه الأزرار أثناء المراجعة.
- CI الملف المحلي لن يعمل حتى يقرر المستخدم رفعه لاحقًا. لم يحصل أي رفع في هذه الجلسة.

## 9. Commit محلي

لم يُنشأ commit محلي، وبقيت التغييرات قابلة للمراجعة في working tree.

## 10. تأكيد

**Nothing was pushed or uploaded to GitHub or any remote service.**

## git status --short

~~~text
 M .gitignore
 M App.xaml.cs
 M AssemblyInfo.cs
 M Build_Release.bat
 M Core/DiskCleanupService.cs
 M Core/Logger.cs
 M Core/NativeMethods.cs
 M Core/RamOptimizer.cs
 M Core/Startup/StartupCatalogService.cs
 M Core/Startup/StartupDelayService.cs
 M Core/Startup/StartupFeatureService.cs
 M Core/Startup/StartupPackagedCatalog.cs
 M Core/Startup/StartupPaths.cs
 M Core/Startup/StartupProcessProbe.cs
 M CortexDNA.csproj
 M CortexDNA_Installer.iss
 M Cortex_Dev.bat
 M MainWindow.xaml
 M MainWindow.xaml.cs
 M Models/StartupModels.cs
 M README.md
 M ViewModels/HardwareViewModel.cs
 M ViewModels/StartupViewModel.cs
 M app.manifest
 D append_row2.py
 D append_row2_fix.py
 D clean_mainwindow_cs.py
 D extract_dashboard.py
 D update_mainwindow.py
 D update_xaml.py
?? .github/
?? Core/CleanupPathGuard.cs
?? Core/UpdateServiceLease.cs
?? LOCAL_ENGINEERING_REPORT.md
?? Tests/
?? scripts/
~~~

## git diff --stat

~~~text
 .gitignore                             |  17 +-
 App.xaml.cs                            |  51 ++--
 AssemblyInfo.cs                        |   3 +
 Build_Release.bat                      |  73 +-----
 Core/DiskCleanupService.cs             | 451 ++++++++++-----------------------
 Core/Logger.cs                         |   8 +-
 Core/NativeMethods.cs                  |   9 -
 Core/RamOptimizer.cs                   |  41 ++-
 Core/Startup/StartupCatalogService.cs  |  23 +-
 Core/Startup/StartupDelayService.cs    |  37 ++-
 Core/Startup/StartupFeatureService.cs  |  19 +-
 Core/Startup/StartupPackagedCatalog.cs |  91 ++-----
 Core/Startup/StartupPaths.cs           |  35 +--
 Core/Startup/StartupProcessProbe.cs    |   4 +
 CortexDNA.csproj                       |   3 +
 CortexDNA_Installer.iss                |  43 +---
 Cortex_Dev.bat                         |  21 +-
 MainWindow.xaml                        |   2 +-
 MainWindow.xaml.cs                     |  67 ++---
 Models/StartupModels.cs                |   1 +
 README.md                              | 354 ++++++++------------------
 ViewModels/HardwareViewModel.cs        | 197 ++++++++------
 ViewModels/StartupViewModel.cs         |  22 +-
 app.manifest                           |   4 +-
 append_row2.py                         |  30 ---
 append_row2_fix.py                     |  31 ---
 clean_mainwindow_cs.py                 |  16 --
 extract_dashboard.py                   |  51 ----
 update_mainwindow.py                   |  25 --
 update_xaml.py                         | 292 ---------------------
 30 files changed, 594 insertions(+), 1427 deletions(-)
~~~
