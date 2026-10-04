# MechaAR V1 — Phase 9 Final UX/UI Polish

ปรับจาก Scene ปัจจุบัน `Assets/Scenes/MechaAR_Main.unity` โดยไม่ Restore Scene เก่า ไม่ Build APK และไม่เริ่ม V2

## ผลงานตามขอบเขต

1. **Global UI:** จัด Header ของ Library/Tutorial/About ให้สอดคล้องกัน ย่อเฉพาะพื้นหลังปุ่มย้อนกลับเป็น 96 × 96 โดยคงพื้นที่กดเดิม 110 × 138 ใช้ Chevron แบบวาดเส้นแทน glyph ปรับ padding และระยะข้อความใน Tutorial/About ให้สม่ำเสมอ รักษาสีและรูปแบบเดิม
2. **Home:** คง Branding, Hero, ภาพ, CTA, Featured Cards และ Navigation เดิมทั้งหมด ตรวจเทียบ subtree ของหน้า Home กับ Snapshot
3. **Library:** คง Search/Category/Layout เดิมและพฤติกรรม Phase 6 ทุกอย่าง จัดภาพในพื้นที่ 150 × 150 เท่ากัน ตัดเฉพาะขอบโปร่งใสด้วย UV ที่คำนวณจาก Texture เดิม พร้อมรักษาอัตราส่วน ไม่แก้ไฟล์ภาพ เพิ่ม Chevron แบบเดียวกันบนการ์ดทั้ง 8
4. **Tutorial:** อธิบาย Viewer ลากเพื่อหมุน/Reset View และ Scanner ส่องภาพเป้าหมาย ลากหมุน Pinch สองนิ้ว และคืน Rotation/Scale ด้วยปุ่ม “คืนมุมมอง” คง Camera Permission และ Google Play Services for AR ไม่มีขั้นตอน Learning Hotspot
5. **About:** Collection สร้างจาก EquipmentDatabase เดิมครบ 8 รายการ คงหัวข้อ 01–04 และส่วนเครดิต สัญญาอนุญาต เวอร์ชัน 05–07 ที่มีจริงใน Scene ล่าสุด รวมถึงลิงก์เดิม เพิ่มระยะท้าย Scroll Content และระยะเหนือ Navigation
6. **Scanner:** ลด TopBar จาก 116 เป็น 96 หน่วย UI จัดปุ่มหน้าหลักให้สัมพันธ์กันโดยคงพื้นที่กดเดิม ลดแผงคำแนะนำจาก 128 เป็น 102 และเลื่อน Reset ขึ้นตามพื้นที่ใหม่ คง Reticle และข้อความสถานะที่ระบบเดิมอัปเดต ซ่อนข้อความแนะนำใต้ Reticle ที่ซ้ำกัน
7. **About ครบ 8:** ใช่ ใช้ข้อมูลจริงจาก Database ไม่เปลี่ยน EquipmentData และไม่ hardcode รายการใหม่
8. **Bottom Navigation:** คงทั้ง 4 เมนูและ Lime active state ใช้ความสูงเดิม เพิ่มระยะปลอดภัยของ Tutorial/About ตรวจทั้งการเลื่อนหัวข้อเทคโนโลยี 04 และส่วนสุดท้าย 07 เนื่องจาก Scene ปัจจุบันมี 7 ส่วน
9. **Instruction ซ้ำ:** เหลือจุดหลักเดียวที่ ScanStatusPanel พร้อมสถานะตรวจพบอุปกรณ์เดิม ไม่พบ GameObject จุดสีเขียวแยกใน Scene ปัจจุบัน จึงคง ARStatusText เป็นสถานะหลักเพียงชุดเดียว
10. **Responsive:** ผลการทดสอบสามขนาดระบุในตารางหลักฐานด้านล่าง รวมการจำลองขอบปลอดภัยบน/ล่างของ Canvas ทั้งสองชุด
11. **Compilation / References:** ใช้ Unity 6000.3.21f1 ตรวจ compilation, components, serialized references, Navigation, scroll, Search/Filter, การ์ดทั้ง 8, Viewer Reset และ ARResetButton.OnClick ที่ยังเรียก ResetCurrent() เดิม
12. **Manual Redmi:** ตรวจภาพจากกล้องจริงและพื้นที่ overlay เมื่อมี status bar/gesture navigation; ตรวจคีย์บอร์ดค้นหาไทย/อังกฤษ; เลื่อนและกดการ์ดครบ 8; เปิด/กลับ Detail; ส่อง Target แล้วลาก/Pinch/คืนมุมมอง; ตรวจข้อความเมื่อกำลังเริ่ม AR/ขอ permission และเมื่อ target หลุด การทดสอบบนโทรศัพท์ในรอบ Phase 9 ยังไม่ได้ดำเนินการ ไม่มีการอ้าง Editor ว่าเป็นผลบน Redmi

## การรักษาระบบเดิม

Snapshot ใหม่แยกจาก Backup เดิม: `.agent-system/tasks/MECHA-PHASE9/snapshots/20260928-133049` ครบ 381 ไฟล์ใน Assets/Packages/ProjectSettings พร้อม SHA256

ห้ามเปลี่ยนและได้เก็บไว้: ARImageTracking, ARModelGestureController, ResetCurrent, AR Image Library, EquipmentDatabase/Data, Equipment3DViewer, Search/Filter, Prefab โมเดล และ Phase 8 scripts/data/variant การปรับ runtime มีเฉพาะ LibraryCardArtwork เพื่อแสดง UV โดยไม่ยืดภาพ และ Chevron Graphic ใหม่ ไม่มี feature หรือ architecture ใหม่

การบันทึก Scene ทำให้ Unity จัด RectTransform ที่ layout ควบคุมของ HomePreviewContent ใหม่ จึงคืนเฉพาะ block นั้นจาก Snapshot ล่าสุดโดยไม่ย้อน Scene ทั้งไฟล์ ส่วน cache glyph ของ TMP ที่เปลี่ยนจากการทดสอบได้คืนไฟล์ต้นฉบับหลัง Unity ปิดแล้ว

## ไฟล์ที่แก้/เพิ่ม

- Assets/Scenes/MechaAR_Main.unity
- Assets/Scripts/UI/LibraryCardArtwork.cs
- Assets/Scripts/UI/MechaARChevronGraphic.cs
- Assets/Scripts/UI/Editor/MechaARPhase9Polish.cs
- Assets/Scripts/UI/Editor/MechaARPhase9Tests.cs
- Assets/Scripts/UI/MechaARPhase9TestDispatcher.cs (Editor only)
- .meta ของไฟล์ใหม่ และเอกสาร/หลักฐาน Phase 9

## หลักฐานการทดสอบ

ผลตรวจใน Unity Editor ผ่านทั้งสามขนาด รวม Navigation, Search/Filter, การ์ดทั้ง 8, Detail/Reset, Scroll, ข้อความยาว และ Safe Area จำลองบน 120 / ล่าง 100 พิกเซล

| ขนาด | Before | After / ผลทดสอบ |
|---|---|---|
| 1080 × 1920 | [ภาพ Before](D:/Unity/MechaAR/.agent-system/tasks/MECHA-PHASE9/evidence/before/1080x1920/20260928-063543/) | [PASSED](D:/Unity/MechaAR/.agent-system/tasks/MECHA-PHASE9/evidence/after/1080x1920/20260928-115651/playmode-tests.txt) |
| 1080 × 2340 | [ภาพ Before](D:/Unity/MechaAR/.agent-system/tasks/MECHA-PHASE9/evidence/before/1080x2340/20260928-063653/) | [PASSED](D:/Unity/MechaAR/.agent-system/tasks/MECHA-PHASE9/evidence/after/1080x2340/20260928-115842/playmode-tests.txt) |
| 720 × 1600 | [ภาพ Before](D:/Unity/MechaAR/.agent-system/tasks/MECHA-PHASE9/evidence/before/720x1600/20260928-063739/) | [PASSED](D:/Unity/MechaAR/.agent-system/tasks/MECHA-PHASE9/evidence/after/720x1600/20260928-115954/playmode-tests.txt) |

ภาพ After ตัวอย่าง: [Library](D:/Unity/MechaAR/.agent-system/tasks/MECHA-PHASE9/evidence/after/1080x1920/20260928-115651/library-all.png) · [Tutorial](D:/Unity/MechaAR/.agent-system/tasks/MECHA-PHASE9/evidence/after/1080x1920/20260928-115651/tutorial-bottom.png) · [About ครบ 8 และหัวข้อเทคโนโลยี](D:/Unity/MechaAR/.agent-system/tasks/MECHA-PHASE9/evidence/after/1080x1920/20260928-115651/about-technology.png) · [Scanner](D:/Unity/MechaAR/.agent-system/tasks/MECHA-PHASE9/evidence/after/1080x1920/20260928-115651/scanner-polished.png) · [Scanner Safe Area 720](D:/Unity/MechaAR/.agent-system/tasks/MECHA-PHASE9/evidence/after/720x1600/20260928-115954/scanner-safe-area-polished.png)

Compilation และ Missing References ผ่าน ไม่พบ C# compilation error หรือ exception ในสามรอบสุดท้าย ผลเทียบ SHA256 ของไฟล์เดิม 381 ไฟล์และ Scene objects ที่ป้องกัน 274 รายการผ่าน รวม OnClick เดิมทุกปุ่ม ภาพ Home และ Detail ขนาด 1080 × 1920 ก่อน/หลังมี SHA256 ตรงกันทั้งไฟล์

หลักฐานฉบับสุดท้าย: `.agent-system/tasks/MECHA-PHASE9/evidence/final-hashes.json` และ `protected-check.json`

Before ทั้งสามขนาดผ่านการเก็บภาพและตรวจ References แล้ว: `.agent-system/tasks/MECHA-PHASE9/evidence/before/`

After: `.agent-system/tasks/MECHA-PHASE9/evidence/after/`

การตรวจ AR ใช้ events/state จำลองและไม่เปิดกล้องจริง Debug diagnostics ถูกปิดเฉพาะใน instance ทดสอบที่ไม่บันทึกลง Scene เพื่อให้ภาพ Before/After เปรียบเทียบ UI ได้ตรงกัน ระบบ diagnostics และคำแนะนำเมื่อ AR ขัดข้องในตัวแอปยังคงเดิม

ประวัติการแก้ระหว่างตรวจ: เพิ่ม CanvasRenderer ที่ Chevron ใหม่ต้องใช้ และเพิ่มการรอ render frame ในชุดทดสอบหลังสลับหน้า โดยคงเงื่อนไขตรวจ layout และ raycast เดิม
