# MechaAR Phase 2 — Equipment Information UI

ระบบข้อมูลอุปกรณ์ใน Scene `Assets/Scenes/MechaAR_Main.unity` ใช้ Canvas แบบ Screen Space Overlay, TextMeshPro และข้อมูล ScriptableObject ภายในโปรเจกต์ ไม่เชื่อม Backend และไม่เพิ่ม AI Chat

## ผลตรวจที่ทำแล้ว

- Android APK Build **Succeeded**, 0 errors, 3 warnings ใช้เวลา 7 นาที 23 วินาที ไฟล์ `Builds/MechaAR-Phase2.apk` ขนาด 44,552,554 bytes (ประมาณ 44.6 MB)
- ตรวจ APK ด้วย aapt2: package `com.mechaar.app`, ARM64, min API 29, target API 36 และ CAMERA permission ครบ เป็น non-development build เพื่อไม่แสดง diagnostics ปกติทับ UI
- Unity import/compilation และ Scene validation ผ่าน ไม่พบ Missing Script หรือ reference ที่จำเป็นขาด
- Play Mode integration checks ผ่าน: รอบ 1080×1920 ผ่าน 49 assertions และรอบ 1080×2340 ผ่าน 51 assertions รวม raycast ที่ปุ่ม Close จริง ครอบคลุม tracking/loss/reacquisition, หลาย trackable, การลบ object, model reuse, callback ปุ่ม Close และการเลื่อนเนื้อหายาวถึงล่างสุด
- ตรวจ glyph ของข้อความที่แสดงทั้งหมด รวมภาษาไทยและ bullet ผ่าน และตรวจภาพเรนเดอร์จริงทั้ง 1080×1920 และ 1080×2340 แล้ว ไม่มีสี่เหลี่ยมหรือข้อความถูกตัด
- ข้อมูลชุดปัจจุบันสูงประมาณ 637 หน่วย อยู่ใน viewport สูง 657 หน่วยพอดี จึงไม่จำเป็นต้องมี scrollbar ที่ความละเอียดนี้ ส่วนข้อมูลยาวที่ใช้ทดสอบสูง 1736 หน่วยเลื่อนได้จริง
- Independent code/scene/visual review ผ่าน รายการที่พบเรื่องป้องกัน Scene ยังไม่ save ในเมนูทดสอบแก้และตรวจซ้ำแล้ว
- Hash ของ prefab, image library, ARRuntimeDiagnostics, Player Settings, Build Settings และ package manifests เท่าเดิม
- ADB ไม่พบมือถือในรอบนี้ จึงยังไม่ยืนยันภาพกล้อง การสัมผัส และ safe area บน Redmi ของ APK Phase 2

ภาพ `ui-tracked.png` ในโฟลเดอร์ evidence เป็นภาพจาก Editor ที่ปิด AR subsystem เพื่อทดสอบ UI จึงมีพื้นกล้องสีดำและสถานะ Preparing AR ไม่ใช่หลักฐานว่ากล้อง Android มีปัญหา

Build warnings ที่ยังเหลือ: ภาพ StepperMotor ได้คะแนน arcoreimg 50/100 (รายงานของเครื่องมือแนะนำอย่างน้อย 75), Active Input Handling เดิมตั้ง Both ซึ่ง Unity เตือนเรื่อง Android, และ ARCore build processor เปลี่ยนข้อมูลใน test library ของ package cache ระหว่าง Build ไม่ได้แก้ target/settings ที่ผู้ใช้ทดสอบผ่านแล้วใน Phase นี้ ควรตรวจการกด/ลากบนมือถือเป็นพิเศษ และหากการติดตามไม่เสถียรให้พิจารณาคุณภาพภาพ target ในงานถัดไป

SHA256 APK: `24F96EBCDFF9194A6BE80B7F397233FD3BC88522C9912E0F129DC06C8B5887FC`

## การทำงาน

- `ARImageTracking.EquipmentTrackingChanged` ส่งชื่อภาพและสถานะเฉพาะเมื่อสถานะรวมเปลี่ยน ระบบสร้าง/ซ่อนโมเดลเดิมยังอยู่
- `EquipmentInfoUI` รับ event และจับคู่ `referenceImageName` กับ catalog; StepperMotor จับคู่กับ EQ001
- Tracking หรือการกลับมาติดตามได้ใหม่แสดงโมเดลและข้อมูล; Limited/None หรือการลบภาพที่ติดตามตัวสุดท้ายซ่อนข้อมูล
- Close ปิดเฉพาะข้อมูล ไม่ปิดกล้อง ไม่ลบโมเดล ไม่หยุดการติดตาม และไม่เปิดใหม่ทุกเฟรม จะเปิดอีกครั้งหลังภาพหายแล้วตรวจพบใหม่
- UI ถูกบันทึกอยู่ใน Scene ไม่สร้างใหม่ทุกเฟรม รายละเอียดที่ยาวเลื่อนอ่านได้

## Scene และข้อมูล

เพิ่ม `MechaAR Canvas > SafeArea` โดยมี `TopBar`, `ScanStatusPanel` และ `EquipmentInfoPanel` ภายใน; Panel มีชื่อ ประเภท หน้าที่ หลักการทำงาน ตัวอย่างการใช้งาน Close และ ScrollRect พร้อมแถบเลื่อน เพิ่ม EventSystem ที่ใช้ InputSystemUIInputModule

Canvas Scaler: Scale With Screen Size, reference 1080×1920, Match Width Or Height=0.5 SafeAreaPanel ปรับตาม Screen.safeArea และขนาดหน้าจอ แผงด้านล่างใช้ประมาณ 46% ของพื้นที่ปลอดภัยเพื่อเหลือส่วนกล้องสำหรับโมเดล

ข้อมูล EQ001 อยู่ใน `Assets/EquipmentData/StepperMotor.asset` ใช้ข้อความจากคำขอ ไม่มีการเพิ่มข้อมูลแรงดัน กระแส หรือกำลังไฟฟ้าของรุ่น ฟอนต์ Noto Sans Thai พร้อม OFL อยู่ใน `Assets/UI/Fonts/` และ TMP essential resources มาจาก package ที่ติดตั้งอยู่

## ไฟล์

แก้ไข:

- `Assets/Scripts/AR/ARImageTracking.cs` — event สถานะรวมและ lifecycle ของโมเดล/UI
- `Assets/Scripts/Equipment/EquipmentInfoUI.cs` — จาก stub เป็นตัวควบคุม UI
- `Assets/Scenes/MechaAR_Main.unity` — เพิ่ม Canvas และ references

สร้าง:

- `Assets/Scripts/Equipment/EquipmentData.cs` — schema ข้อมูลอุปกรณ์
- `Assets/Scripts/Equipment/SafeAreaPanel.cs` — safe area
- `Assets/Scripts/Equipment/RoundedPanelGraphic.cs` — panel มุมมนแบบ mesh
- `Assets/Scripts/Equipment/Editor/MechaARPhase2Setup.cs` — setup/validation/build แบบสั่งทำ ไม่รันเองเมื่อ import
- `Assets/Scripts/Equipment/Editor/MechaARPhase2Tests.cs` — Play Mode integration checks ด้วย AR Foundation event จริงและข้อมูลจำลอง
- `Assets/Scripts/Equipment/MechaARPhase2TestDispatcher.cs` — ตัวขับการทดสอบผ่าน PlayerLoop ที่คอมไพล์เฉพาะ UNITY_EDITOR ไม่มีโค้ดนี้ใน APK
- `Assets/EquipmentData/StepperMotor.asset`
- `Assets/UI/Fonts/NotoSansThai.ttf`, `MechaARThai SDF.asset`, `OFL.txt` และไฟล์ .meta
- `Assets/TextMesh Pro/` — essential resources จาก Unity package เดิม พร้อม TMP Settings ที่ใช้ฟอนต์ไทย

ไฟล์เดิมสำรองก่อนแก้ไว้ใน `.agent-system/tasks/MECHA-PHASE2/backups/` คง StepperMotor_AR, image library, ARRuntimeDiagnostics, Graphics API OpenGLES3 และ Package versions เดิม

## เพิ่มข้อมูลชนิดอื่นในอนาคต

ใช้ Create > MechaAR > Equipment Data แล้วกรอกข้อมูลและ referenceImageName ให้ตรงกับ image library แบบ case-sensitive เพิ่ม asset ใน Equipment Catalog ของ EquipmentInfoUI ระบบ UI รองรับหลายรายการ แต่ระบบโมเดล AR ปัจจุบันยังใช้ target/prefab เดิม การเพิ่มโมเดลสำหรับ target ใหม่ต้องขยาย mapping ของระบบ AR แยกต่างหาก

## ทดสอบบน Redmi Note 15 Pro 5G

1. เปิด Scene MechaAR_Main รอ Unity import และตรวจ Console ไม่มี compile error ไม่จำเป็นต้องกด Install อีกครั้ง เพราะ Canvas ถูกบันทึกแล้ว
2. เลือก Android Build Profile และตรวจ MechaAR_Main เป็น scene แรกที่ enabled; Graphics API ยังเป็น OpenGLES3
3. Build And Run หรือใช้ APK Phase 2 ที่ส่งมอบ อนุญาตกล้องตามปกติ
4. ก่อนพบภาพ ต้องมี TopBar กับข้อความ “ส่องกล้องไปยังภาพอุปกรณ์” และยังไม่แสดงข้อมูล
5. ส่อง StepperMotor_Target.jpg ต้องเห็นโมเดล พร้อมชื่อ Stepper Motor / มอเตอร์สเต็ป / Electric Motor และข้อความไทยอ่านได้ ไม่มีสี่เหลี่ยม
6. เลื่อนอ่านข้อมูลทั้งสามหัวข้อและรายการใช้งาน ตรวจรอยบากกล้องและแถบ Navigation ไม่ทับ UI
7. กด Close: ข้อมูลปิด แต่โมเดลและกล้องยังทำงาน จับภาพเดิมค้างไว้ข้อมูลต้องไม่เปิดเอง
8. นำภาพออกแล้วส่องใหม่: ข้อมูลและโมเดลต้องกลับมา ตรวจซ้ำหลายรอบว่าไม่มีโมเดลหรือ Canvas ซ้ำ
9. พักแอปแล้วกลับมา ตรวจการฟื้นตัวของกล้องและสถานะ UI รวมถึงมือถือที่มีสัดส่วนหน้าจอต่างกัน

ทดสอบใน Editor ได้ผ่าน MechaAR > Phase 2 > Run integration checks ซึ่งเปิด Play Mode และจำลอง events โดยไม่บันทึกการปิด AR managers ลง Scene ผลและ Log อยู่ใน `.agent-system/tasks/MECHA-PHASE2/evidence/` การทดสอบนี้ไม่แทนการสแกนด้วยกล้องจริง

เมื่อหยุดการทดสอบแบบ interactive เครื่องมือจะโหลด Scene ที่บันทึกไว้กลับมา เพื่อไม่ทิ้งสถานะ AR managers ที่ถูกปิดชั่วคราวให้ผู้ใช้เผลอ Save การทดสอบ batch รองรับ `-mechaarTestHeight 2340` สำหรับจอยาว โดยเก็บหลักฐานแยกใน `evidence/tall/`

แหล่งฟอนต์และใบอนุญาต: [Google Fonts — Noto Sans Thai](https://github.com/google/fonts/tree/main/ofl/notosansthai)
