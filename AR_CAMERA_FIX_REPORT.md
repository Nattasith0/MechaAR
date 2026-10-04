# MechaAR — ผลตรวจกล้อง AR (23 กันยายน 2026)

สถานะ: แก้ไฟล์และตรวจคอมไพล์แล้ว ยืนยันว่ากล้องแสดงภาพได้เมื่อบังคับ APK เดิมใช้ OpenGLES3 แต่ **ยังไม่ได้ Build APK จากไฟล์ที่แก้ใหม่ และยังไม่ยืนยันโมเดลบนภาพกระดาษ**

## สาเหตุและหลักฐาน

- Logcat จากมือถือแสดง `InvalidOperationException` ว่า Vulkan + URP ต้องมี `ARCommandBufferSupportRendererFeature` และ `The texture buffer handle is null.` การตั้งค่าจริงเลือก Vulkan ก่อน GLES3 ขณะที่ Mobile_Renderer มีเฉพาะ ARBackgroundRendererFeature
- CAMERA permission ของแอปเป็น `granted=true` และ Google Play Services for AR ติดตั้งแล้ว เวอร์ชัน 1.56.262080393 จึงไม่ใช่หลักฐานว่าจอดำครั้งนี้เกิดจากสิทธิ์กล้อง
- ทดลองเปิด APK เดิมด้วย `--es unity -force-gles30` แล้วเห็นภาพกล้องหลังจริงใน screenshot; Logcat รายงาน RearCamera, ImageDetection และ AugmentedImageDatabase การทดลองนี้ไม่ใช่การทดสอบ Build ใหม่
- Scene มี XROrigin เปิดสองตัว โดยตัวซ้ำอยู่บน AR Session และ ARTapToPlace ยังเปิดอยู่
- GLB มีความกว้าง 42 หน่วยสำหรับโมเดล 42 mm แต่ Prefab ใช้ scale 1 พร้อม child offset x/z ประมาณ 6.656 เมตร จึงต้องแก้ขนาดและตำแหน่งเพื่อแสดงบนกระดาษ

## ไฟล์ที่แก้

| ไฟล์ | การแก้ไข |
|---|---|
| ProjectSettings/ProjectSettings.asset | Android ใช้ OpenGLES3 เท่านั้น; ปิด Auto Graphics API ตามเดิม |
| Assets/Scenes/MechaAR_Main.unity | ปิด ARTapToPlace ชั่วคราว และปิด XROrigin ที่ซ้ำบน AR Session |
| Assets/Prefabs/StepperMotor_AR.prefab | child x/z เป็น 0 และ child scale xyz เป็น 0.001; คง rotation และ root scale |
| Assets/Scripts/AR/ARRuntimeDiagnostics.cs และ .meta | เพิ่ม diagnostics เฉพาะ MechaAR_Main อัตโนมัติ; Log ทุก 5 วินาที, แสดงคำแนะนำเมื่อ permission/AR services/กล้องมีปัญหา และปุ่มเปิด App Settings |

ไฟล์เดิมสำรองไว้ที่ `.agent-system/tasks/MECHA-AR-CAMERA/backups/` ตามโครงสร้าง path เดิม ไม่มีการเปลี่ยนเวอร์ชัน Unity/Package หรือระบบ AI

## รายการที่ตรวจแล้วและคงไว้

- Unity 6000.3.21f1, AR Foundation/ARCore 6.3.5, URP 17.3.0, glTFast 6.20.0
- Main Camera มี Camera, ARCameraManager, ARCameraBackground, Input System TrackedPoseDriver, UniversalAdditionalCameraData เปิดอยู่; Facing=World และ XR Origin อ้างอิงถูกต้อง
- Camera Offset อยู่ใต้ XR Origin; ไม่มี Main Camera ซ้ำ
- Android Quality index 0 ใช้ Mobile_RPAsset ซึ่งอ้าง Mobile_Renderer ที่มี ARBackgroundRendererFeature เปิดอยู่ แม้ Graphics default จะเป็น PC_RPAsset แต่ Quality override มีผลบน Android จึงไม่ต้องเปลี่ยน Graphics default
- ARCore Loader ผูก Android และ InitManagerOnStart เปิดอยู่; ค่า automaticLoading/automaticRunning=0 ไม่ใช่ปัญหา เพราะ XRGeneralSettings จัดการ InitializeLoader/StartSubsystems เอง
- ARImageTracking.cs ใช้ API `trackablesChanged` ของ AR Foundation 6 ถูกต้อง รวมถึง removed.Key; ชื่อ `StepperMotor` ตรงกับ Library แบบ case-sensitive และอ้าง StepperMotor_AR ถูกต้อง จึงไม่แก้โค้ดนี้
- Library มีข้อมูล ARCore image database และภาพกำหนดขนาดประมาณ 15.957448 × 15 cm; m_Texture เป็น 0 ได้เมื่อไม่ได้เก็บ texture ไว้ใน runtime ไม่ได้แปลว่าภาพหาย
- MechaAR_Main เป็น scene แรกที่ enabled; Android profile ไม่ override scene list/Player Settings; IL2CPP, ARM64, min API 29, Target Auto โดย Manifest จาก Build เดิมเป็น API 36
- Manifest ที่ merge จาก Build เดิมมี CAMERA และ ARCore required; ARCore package มี runtime permission provider อยู่แล้ว จึงไม่เพิ่ม Manifest หรือคำขอ permission ซ้ำ
- Diagnostics ไม่เก็บภาพหรือส่งข้อมูลกล้องออกเครือข่าย ข้อความบนมือถือเป็นภาษาอังกฤษเพื่อใช้ฟอนต์เดิมได้

## ผลทดสอบและข้อจำกัด

- PASSED: คอมไพล์ C# ทั้ง Editor และ Android defines ด้วย Roslyn ของ Unity และ response files/dependency assemblies จาก Build เดิม รวม script ใหม่; exit code 0 ทั้งคู่ ไม่มี compiler error
- PASSED: independent code/config/prefab review ไม่พบข้อผิดพลาดที่ต้องแก้ในขอบเขตที่ตรวจ
- PASSED: ทดลอง APK เดิมโดยบังคับ GLES3 ได้ภาพกล้องหลังจริง
- PASSED: Logcat ของ APK เดิมหลังบังคับ GLES3 เวลา 13:57:26 แสดง `ตรวจพบอุปกรณ์: StepperMotor` จาก ARImageTracking ยืนยันว่า pipeline ตรวจพบชื่อภาพได้ แต่ยังไม่ยืนยันภาพโมเดล ขนาด หรือตำแหน่งของ Prefab ใหม่
- NOT_RUN: Build/IL2CPP/Gradle ใหม่และ Manifest หลัง Build ใหม่; Unity Editor ของโปรเจกต์กำลังเปิดอยู่ ไม่ปิดเพื่อหลีกเลี่ยงการสูญเสียงานที่ยังไม่ได้ save
- NOT_RUN: diagnostics บนเครื่องจริง, permission ปฏิเสธ/อนุญาตภายหลัง, พักแอปแล้วกลับมา, การแสดงและติดตาม StepperMotor_AR จาก Prefab ที่แก้ใหม่
- การคอมไพล์ C# ไม่เท่ากับการยืนยัน full Android Build หรือ IL2CPP stripping

หลักฐานอยู่ใน `.agent-system/tasks/MECHA-AR-CAMERA/evidence/`: before-logcat.txt, gles-old-apk-logcat.txt, device-gles-test.png, compile-E/P.rsp และ .log, implementation-sha256.json

หมายเหตุ: Logcat เป็น ring buffer; ไฟล์ gles-old-apk-logcat.txt ที่บันทึกภายหลังเก็บช่วงตรวจพบ StepperMotor ส่วนข้อความเริ่มต้น RearCamera/ImageDetection ที่อ่านระหว่างทดสอบถูกหมุนออกไปแล้ว

## Build And Run ใหม่

1. กลับ Unity รอ import/compile จนเสร็จ หากถาม reload Scene จาก disk ให้เลือกโหลดไฟล์ที่แก้ใหม่ ระวังอย่า Save Scene เก่าทับไฟล์บน disk; ถ้ามีงานค้างให้ Save As ชื่ออื่นก่อน แล้วเปิด MechaAR_Main ใหม่
2. ตรวจ Project Settings > Player > Android > Other Settings: Auto Graphics API ปิด และรายการเหลือ OpenGLES3 เท่านั้น หาก Editor ยังแสดงค่าเก่าให้ reload โปรเจกต์หลังเก็บงานค้างแล้ว
3. ตรวจ Hierarchy: XROrigin บน AR Session ถูกปิด แต่ ARSession ยังเปิด; XROrigin ตัวหลักยังเปิด และ ARTapToPlace ปิด
4. File > Build Profiles เลือก Android ให้ active; Scene List ให้ MechaAR_Main เป็นตัวแรกที่ enabled; เลือกมือถือที่เชื่อมต่อ USB
5. เปิด Development Build สำหรับการทดสอบเพื่อเห็นสถานะ diagnostics ตลอด รอ Console ไม่มี error จากนั้น Build And Run ไปยัง APK ชื่อใหม่ เช่น MechaAR-camera-fix.apk เพื่อเก็บ 1.apk เดิมไว้
6. อนุญาต Camera เมื่อ Android ถาม ถ้าปฏิเสธ ให้ใช้ Open app settings > Permissions > Camera > Allow แล้วกลับแอป หากยังไม่เริ่มให้ปิด/เปิดแอปใหม่
7. ยืนยันเห็นภาพกล้องหลัง; diagnostics ควรมี Loader ARCore, camera/session running=true, Frames เพิ่ม และ SessionTracking เมื่อเคลื่อนมือถือช้า ๆ ในพื้นที่มีรายละเอียด
8. พิมพ์ `Assets/ImageTargets/StepperMotor_Target.jpg` โดยรักษาสัดส่วน ให้ส่วนภาพสูง 15 cm กว้างประมาณ 15.96 cm ไม่ใช่วัดรวมขอบกระดาษ ส่องในแสงพอเหมาะ ไม่สะท้อน และให้เห็นภาพทั้งรูป
9. ตรวจโมเดลแสดงเหนือภาพประมาณ 3 cm ขนาดตัวมอเตอร์กว้างประมาณ 4.2 cm เคลื่อนตามภาพ และซ่อนเมื่อ tracking หาย ตรวจว่าแตะพื้นไม่สร้างโมเดลซ้ำ
10. ทดสอบ Home แล้วกลับแอป, ล็อก/ปลดล็อก, เปิดใหม่ และปฏิเสธ permission แล้วอนุญาตภายหลัง บันทึก Logcat หากผิดพลาด โดยกรอง Unity, Unity-ARCore และ `[MechaAR AR]`

การเปิด APK เดิมด้วย GLES3 เป็นการ override เฉพาะการเปิดทดสอบครั้งนั้น ต้อง Build ใหม่เพื่อให้ GLES3 เป็นค่าถาวร

อ้างอิงอุปกรณ์: รายการทางการ Google ระบุ REDMI Note 15 Pro 5G รองรับ ARCore/Depth API: https://developers.google.com/ar/devices แต่ผลบนเฟิร์มแวร์และ Build นี้ยังต้องทดสอบตามรายการข้างต้น
