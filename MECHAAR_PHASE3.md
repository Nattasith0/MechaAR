# MechaAR Phase 3 — Field Lab

สถานะ: กำลังพัฒนาและตรวจสอบ — ยังไม่ใช่รายงานผลผ่านขั้นสุดท้าย

## แนวคิดและสี

แอปเรียนรู้แนวห้องทดลองวิศวกรรม ใช้พื้นสว่างเพื่ออ่านภาษาไทยได้สบาย มีภาพประกอบวงจรที่สร้างด้วย Unity UI และปุ่มเริ่มสแกนที่เด่นชัด

| การใช้งาน | สี |
| --- | --- |
| พื้นหลัก | `#F4F5ED` |
| ตัวอักษร/พื้นเข้ม | `#142B29` |
| จุดเน้น/ปุ่มหลัก | `#D4F268` |
| พื้นรอง | `#DCE8DF` |
| ข้อความรอง | `#64736C` |

ใช้ Noto Sans Thai / TextMeshPro เดิม ไม่เพิ่มระบบเรนเดอร์โมเดลสำหรับหน้า Home

## ขอบเขตการรักษาระบบ AR

สำรองไฟล์ต้นฉบับและ `.meta` ไว้ใน `.agent-system/tasks/MECHA-PHASE3/backups` ก่อนแก้ไข ผู้ใช้ยืนยันบันทึกและปิด Unity แล้ว

แผนเชื่อมต่อเปลี่ยนเฉพาะค่าเริ่มต้น `enabled` ของ ARSession และ ARCameraManager ใน Scene เป็น false แล้วเปิดคอมโพเนนต์เดิมเมื่อกดสแกนครั้งแรก ไม่สร้าง Session, XR Origin หรือกล้องเพิ่ม และไม่รีเซ็ต Session เมื่อกลับ Home หลังเริ่มสแกนแล้ว AR จะทำงานต่อหลังหน้าจอทึบตามทางเลือกที่ข้อกำหนดอนุญาต ดังนั้นการกลับ Home หลังสแกนยังไม่ปล่อยกล้อง

Library ใช้ EquipmentDatabase เดิม โดยเพิ่มช่องทางอ่านรายการเท่านั้น รายการที่ตั้งค่าไว้จริงคือ EQ001 และ EQ002 ไฟล์ EQ003 Servo มีอยู่แต่ยังไม่ถูกเชื่อมเข้าฐานข้อมูล จึงไม่เพิ่มให้โดยอัตโนมัติ

## เครดิตที่ตรวจจากไฟล์ GLB

ข้อมูลต่อไปนี้มาจาก `asset.extras` ในไฟล์โมเดลจริง:

- **Nema 17 Stepper Motor (42mm x 48mm)** — moogh, [ต้นฉบับ](https://sketchfab.com/3d-models/nema-17-stepper-motor-42mm-x-48mm-b970d52c4b554768a1b576cb381abf07), CC BY 4.0
- **Arduino Uno Board** — crimsonfalcon, [ต้นฉบับ](https://sketchfab.com/3d-models/arduino-uno-board-f31feafc5e9743abbdf33c54f9d92669), CC BY 4.0
- **Servo Motor sg 90** — peddintiudaykiran176, [ต้นฉบับ](https://sketchfab.com/3d-models/servo-motor-sg-90-527862090927476fbb1f525b1ba93046), CC BY 4.0; ยังไม่ได้อยู่ในรายการฐานข้อมูลของ Scene

สิทธิ์โมเดล: [Creative Commons Attribution 4.0](https://creativecommons.org/licenses/by/4.0/) โปรเจกต์มีการปรับ Transform สำหรับแสดงใน AR; Phase 3 ไม่แก้ไฟล์โมเดล

Noto Sans Thai: SIL Open Font License 1.1 ตาม `Assets/UI/Fonts/OFL.txt` ภาพประกอบวงจรใน UI สร้างขึ้นสำหรับ Phase 3 โดยไม่ใช้ภาพจากภายนอก ไม่สร้างชื่อผู้พัฒนา/สถาบันที่ไม่ได้รับข้อมูลยืนยัน

## การตรวจสอบ

ผล Unity, ภาพหน้าจอ, APK และผลเครื่องจริงจะบันทึกหลังรันจริงใน `.agent-system/tasks/MECHA-PHASE3/evidence` ไม่ถือว่า Editor synthetic AR events พิสูจน์กล้องหรือตัวตรวจจับภาพบนโทรศัพท์
