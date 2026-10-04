# MechaAR — Phase 6 และ Phase 7.4

ปรับลง Scene ปัจจุบัน `Assets/Scenes/MechaAR_Main.unity` โดยใช้ Unity Editor API และตรวจต่อถึง 28 กันยายน 2026 ไม่ได้ Restore Scene เก่าและไม่ได้ Build APK

## งานที่ทำ

1. **Library 2.0:** แก้สาเหตุการ์ดหายจากข้อความตัวอย่างที่ถูกบันทึกเป็นค่าค้นหาจริงใน TMP Input Field ให้เริ่มต้นช่องค้นหาว่าง ใช้ RoundedPanelGraphic เดิมทำพื้นหลังมุมโค้ง จัดช่องค้นหา หมวดหมู่ จำนวนผลลัพธ์ และ Empty State ใหม่
2. **Equipment Cards:** สร้างรายการจาก EquipmentDatabase เดิม ไม่ hardcode จำนวนอุปกรณ์ แสดงครบ 8 รายการเมื่อค้นหาว่างและเลือกทั้งหมด คง EquipmentLibraryCard.cs และ template เดิม
3. **Search / Category:** ค้นหาชื่ออังกฤษ ชื่อไทย รหัส และหมวดหมู่ ไม่สนตัวพิมพ์อังกฤษ ใช้เงื่อนไขค้นหา AND หมวดหมู่ หมวดทั้งหมด 8, Motor & Actuator 4, Sensor 2, Microcontroller & Controller 2; รวม Electric Motor เฉพาะตัวกรองฝั่ง UI ตามที่อนุมัติ ไม่เปลี่ยน EquipmentData
4. **ภาพอุปกรณ์:** ผูก Texture ที่มีจริงใน Assets/UI/Images/Equipment ครบ 8 รายการ รักษาอัตราส่วน หากเพิ่มอุปกรณ์ที่ยังไม่มีภาพจะแสดงเลขรายการสำรองแทน
5. **ARResetButton:** เพิ่มลูกของ ScannerPage ด้านขวาใต้พื้นที่สถานะ ขนาด 240 × 112 หน่วย UI สี Lime/Ink เดิม ข้อความ “คืนมุมมอง” และไอคอน ResetViewIcon ที่มีอยู่
6. **OnClick:** เชื่อมแบบ persistent ไปยัง ARModelGestureController.ResetCurrent() เดิม ไม่เพิ่มพารามิเตอร์ ไม่เขียนระบบหมุน/ซูมใหม่
7. **Phase 7:** คง ARModelGestureController บน XR Origin, references, ค่าการหมุน/สเกล และ ARImageTracking เดิมทั้งหมด การทดสอบ Reset ใน Editor เป็นสถานการณ์จำลอง แยกจากการทดสอบ touch และ tracking บนโทรศัพท์
8. **Phase 8:** คง scripts และ Learning Data ทั้งหมด พบ Hotspot ใน Prefab Variant Assets/Prefabs/Learning/ArduinoUNO_Learning_AR.prefab; ArduinoUNO_AR ต้นฉบับไม่เปลี่ยน ไม่ติดตั้ง Learning UI หรือแก้ Equipment3DViewer.cs
9. **Compilation / References:** Unity 6000.3.21f1 คอมไพล์ผ่าน การตรวจ components และ serialized references ใน Scene ผ่าน ไม่พบ Compilation Error หรือ Missing References ในชุดตรวจนี้
10. **Manual Android Test บน Redmi:** ทดสอบคีย์บอร์ดไทย/อังกฤษและพื้นที่เมื่อคีย์บอร์ดเปิด; เลื่อนรายการ เปิด Detail และกลับ; สแกน Stepper/Arduino แล้วหมุนและ pinch ก่อนกดคืนมุมมอง; ตรวจมุม/สเกลเดิมแล้วหมุนและซูมต่อ; กดขณะยังไม่มีโมเดล/ภาพหลุด tracking; ตรวจการเลือกโมเดลเมื่อมีหลาย target และการกลับเข้า Scanner งานเหล่านี้ยังไม่ได้ยืนยันบนอุปกรณ์จริงในรอบนี้

## ไฟล์ที่แก้/เพิ่ม

- Assets/Scenes/MechaAR_Main.unity
- Assets/Scripts/UI/MechaARUIManager.cs
- Assets/Scripts/UI/LibraryCardArtwork.cs
- Assets/Scripts/UI/Editor/MechaARPhase6Library.cs
- Assets/Scripts/UI/Editor/MechaARPhase74Reset.cs
- Assets/Scripts/UI/Editor/MechaARPhase6Tests.cs
- Assets/Scripts/UI/MechaARPhase6TestDispatcher.cs (Editor only)
- ไฟล์ .meta ของไฟล์ใหม่ และเอกสาร/หลักฐานใน .agent-system/tasks/MECHA-PHASE6

## การรักษางานเดิม

Snapshot ล่าสุด: `.agent-system/tasks/MECHA-PHASE6/snapshots/20260927-073813` พร้อม SHA256 263 ไฟล์ เก็บแยกจาก backup เดิม ตรวจทั้งไฟล์และวัตถุ Scene เดิม 947 รายการ อนุญาตเพียงการเพิ่มลูก ARResetButton ใน ScannerPage ส่วนที่อยู่นอกขอบเขตต้องตรง snapshot เดิม

Unity จัดค่า RectTransform ที่ถูก layout ควบคุมของหน้าเดิมใหม่หนึ่งรายการระหว่างบันทึก จึงคืนเฉพาะ block นั้นให้ตรง snapshot โดยไม่ Restore Scene ทั้งไฟล์

การทดสอบทำให้ TMP Dynamic Font บันทึก glyph U+00B7 (จุดคั่นใน Scanner เดิม) ลง asset อัตโนมัติ จึงคืน font asset ให้ตรง snapshot หลัง Unity ปิด การทดสอบ 720 × 1600 เริ่มจาก font ต้นฉบับและผ่าน การสร้าง glyph ขณะใช้งานยังเป็นพฤติกรรมเดิม

## หลักฐานการทดสอบ

| ขนาด | ผลรอบสุดท้าย | หลักฐาน |
|---|---|---|
| 1080 × 1920 | PASSED | [ผลทดสอบ](D:/Unity/MechaAR/.agent-system/tasks/MECHA-PHASE6/evidence/after/1080x1920/20260928-013628/playmode-tests.txt) |
| 1080 × 2340 | PASSED | [ผลทดสอบ](D:/Unity/MechaAR/.agent-system/tasks/MECHA-PHASE6/evidence/after/1080x2340/20260928-013711/playmode-tests.txt) |
| 720 × 1600 | PASSED | [ผลทดสอบ](D:/Unity/MechaAR/.agent-system/tasks/MECHA-PHASE6/evidence/after/720x1600/20260928-013538/playmode-tests.txt) |

ภาพตัวอย่าง: [Before](D:/Unity/MechaAR/.agent-system/tasks/MECHA-PHASE6/evidence/before/1080x1920/20260927-074015/library.png) · [After](D:/Unity/MechaAR/.agent-system/tasks/MECHA-PHASE6/evidence/after/1080x1920/20260928-013628/library-all.png) · [รายการท้ายจอ 720](D:/Unity/MechaAR/.agent-system/tasks/MECHA-PHASE6/evidence/after/720x1600/20260928-013538/library-bottom.png) · [AR Reset กับแผงข้อมูล](D:/Unity/MechaAR/.agent-system/tasks/MECHA-PHASE6/evidence/after/1080x2340/20260928-013711/scanner-reset-expanded.png)

ทดสอบใน Unity Editor ด้วย Game View ขนาดจริง พร้อมตรวจพื้นที่กดด้วย UI raycast, ภาษาไทย, ขอบเขตและ overflow, safe area จำลอง, scroll, การเปิด Detail ทั้ง 8 รายการ, 3D Preview และ Reset เดิม, กลับ Library แล้วรักษาคำค้น/หมวด/ตำแหน่งเลื่อน, Search/Filter/Empty/Count และอุปกรณ์ลำดับที่ 9 แบบชั่วคราวในหน่วยความจำ

AR Reset ทดสอบด้วย tracked-image events และ transform/state จำลอง: คืน rotation/scale เดิม, กระทบเฉพาะโมเดลที่เลือก, ปลอดภัยเมื่อไม่มีโมเดลหรือ tracking หลุด, ล้าง gesture state และยังเลือกโมเดลต่อได้ ไม่ใช่การทดสอบกล้องหรือ touch/pinch บน Android

ระหว่างตรวจพบและแก้ชื่อหมวด Controller กับชื่อ DHT11 ที่ถูกตัด ส่วนการกดการ์ดล้มเหลวรอบแรกเกิดจากชุดทดสอบไม่รอ UI อัปเดตหลังเปลี่ยนตัวกรอง ได้เพิ่มการรอ render frame แล้วตรวจ raycast จริงต่อไปโดยไม่ข้ามเงื่อนไข

Before ล่าสุด: `.agent-system/tasks/MECHA-PHASE6/evidence/before/1080x1920/20260927-074015/`

After และผลแต่ละรอบ: `.agent-system/tasks/MECHA-PHASE6/evidence/after/`

ตรวจความคงเดิม: `.agent-system/tasks/MECHA-PHASE6/evidence/protected-check.json`

ผลตรวจสุดท้าย: 263 ไฟล์และวัตถุเดิมใน Scene 947 รายการ ไม่พบการเปลี่ยนแปลงนอกขอบเขต ไม่มี APK ถูกสร้างในงานนี้ และยังไม่เริ่ม Phase 8
