# TaoKaeNoi Seaweed Slayer

ใช้ฉากเดียว `Assets/Scenes/Game.unity` สำหรับ Windows, WebGL Arena และ WebGL Controller จอเกมหลักคำนวณการเดิน การปา การชน และคะแนน มือถือส่งคำสั่งผ่าน Photon PUN ใน region `asia` ใช้ game version `seaweed-slayer-v1` แยกจากเกมเดิม

## เริ่มใช้งาน

1. เปิดโปรเจกต์ด้วย Unity 6000.4.10f1 แล้วเลือก **Seaweed Slayer → Prepare Game Scene** เมื่อต้องการเตรียมหรืออัปเดต Asset ที่สร้างไว้
2. **Seaweed Slayer → Build Windows PC** ได้ `Builds/Windows/SeaweedSlayer.exe`
3. **Seaweed Slayer → Build WebGL Site (Arena + Controller)** ได้เกมที่ `Builds/WebGL/index.html` และจอยที่ `Builds/WebGL/Controller/index.html` นำทั้งหมดภายใน `Builds/WebGL` ขึ้นเว็บ HTTPS
4. เปิดเกมบน PC ใส่ URL ของ `index.html` ในช่อง URL แล้วกด **สร้าง QR จาก URL จอย** หรือใส่ `controllerUrl` ใน `Assets/SeaweedSlayer/SeaweedSettings.asset` ก่อน Build ค่า URL ที่ตั้งผ่าน PC จะจำไว้สำหรับการเปิดครั้งต่อไป
5. มือถือสแกน QR เลือกชื่อ สี และกด **Ready** เมื่อทุกคนในห้องพร้อม QR จะซ่อนและนับถอยหลัง 3 วินาที ถ้ายกเลิก Ready ระหว่างนับถอยหลังจะกลับมาที่ Lobby

ต้องตั้ง Photon App ID ใน PhotonServerSettings และเครื่อง PC/มือถือมีอินเทอร์เน็ต เว็บ `localhost` บน PC ไม่ใช่ที่อยู่ที่มือถือเปิดได้ ให้ใช้เว็บ HTTPS หรือ IP LAN ของ PC ที่มือถือเข้าถึงได้

WebGL บีบอัดด้วย Gzip พร้อม decompression fallback เพื่อให้อัปโหลดไปยังเว็บ static ได้โดยไม่ต้องตั้ง Content-Encoding เพิ่ม ([Unity API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/PlayerSettings.WebGL-decompressionFallback.html))

## กติกา

- ผู้เล่น 1–20 คน ห้อง Photon มี 21 slot โดยนับ PC อีก 1 slot ปรับจำนวนผู้เล่นสูงสุดได้ใน Settings
- ขนม `item.png` ซองละ 25 คะแนน ตามระบบ Taokaenoi เดิม สร้างเพิ่มระหว่างรอบ จัดอันดับคะแนนจากมากไปน้อย หากเท่ากันเรียงตาม actor เพื่อให้ลำดับคงที่
- ลาก Joystick Pack เพื่อเดิน กด **THROW** ปาคิวบ์สีเทาไปทางที่หัน คูลดาวน์ 0.65 วินาที ผู้โดนจะกระพริบและเดิน/ปา/เก็บขนมไม่ได้ 2 วินาที ช่วงนี้โดนซ้ำจะไม่เพิ่มระยะเวลา
- รอบละ 90 วินาที ปรับได้ใน Settings แสดงผล 10 วินาที แล้วกลับ Lobby ห้องเดิม ทุกคนกด Ready ใหม่
- ต้นไม้สุ่มทุกครั้งที่เริ่มรอบ มีทางกลางและรอบขอบให้เดินได้ บ่อน้ำและรั้วกั้นการเดิน กล้อง orthographic มองแนวตรง ไม่มีมุมหมุนแบบ isometric
- PC เล่นเดี่ยวได้จากปุ่ม **เล่นคนเดียว** เมื่อยังไม่มีผู้เล่นมือถือ กด WASD/ลูกศรเพื่อเดิน และ Space เพื่อปา

## UID และการเชื่อมต่อใหม่

จอยบันทึก Photon UserId, รหัสห้อง, host UID, ชื่อ และสีลง PlayerPrefs ของ WebGL หลังหลุดจะเชื่อมต่อใหม่ด้วย UID เดิม ห้องเก็บ slot และคะแนนไว้ 60 วินาที และหยุดตัวละครเมื่อไม่ได้รับ input เกิน 0.35 วินาที หลังหมดเวลา slot หรือ PC ปิดเกม ต้องสแกน QR ใหม่ หากล้างข้อมูลเว็บไซต์ UID ที่บันทึกไว้จะหาย

เพื่อทดสอบจอยใน Unity Editor ให้เปิด `editorController` บน SeaweedSession แล้วกรอก `editorRoom` ปิดตัวเลือกนี้สำหรับจอเกมหลัก WebGL Arena เปิดโหมดจอยได้ด้วย query `mode=controller` ส่วน Build Controller จะเป็นจอยโดยอัตโนมัติ

## การตรวจสอบ

เมนู **Seaweed Slayer → Validate** ตรวจ Asset และจำนวนผู้เล่น ก่อน Build แต่ละแพลตฟอร์ม

Windows player รองรับการทดสอบด้วย:

```powershell
./Builds/Windows/SeaweedSlayer.exe -batchmode -nographics -seaweed-smoke -logFile ./Builds/network-smoke.log
```

ทดสอบห้อง Photon จริงกับ controller จำลอง 2 ตัว: slot ของ PC, Ready ทุกคน, ยกเลิกนับถอยหลัง, การเดิน, หยุดเมื่อ input หาย, ปาคิวบ์/หยุด 2 วินาที, เก็บขนม +25, ยิงศัตรู +50 ทั้งระยะปกติและประชิด, จำนวนศัตรูสูงสุด, หลุดและ rejoin UID เดิม, Results และ Ready สำหรับรอบใหม่ ดูข้อความ `SEAWEED_NETWORK_SMOKE_SUCCESS` ไม่ใช่การทดสอบโหลดมือถือจริง 20 เครื่อง

ทดสอบภาพด้วย `-seaweed-preview` จะบันทึก `Preview.png` ข้าง exe แล้วปิดเกม โหมดทดสอบไม่ทำงานในการเปิดเกมตามปกติ

ระบบสร้างสำเนา Prefab/Material ที่รองรับ URP ใน `Assets/SeaweedSlayer/Generated` โดยไม่แก้ Material ป่าเดิม และผูกคลิป `Idle_Cute`, `Run_Cute`, `Throw_Left` จาก GLB ให้เล่นได้ เมื่ออัปเดตโปรเจกต์ที่สร้าง Prefab ไว้ก่อนหน้า ใช้เมนู **Upgrade Player Animations** ได้โดยไม่เปลี่ยนฉาก Fonts: upheavtt สำหรับหัวเรื่อง/ตัวเลขหลัก และ KANIT-SEMIBOLD สำหรับ UI/ชื่อผู้เล่น สีชื่อและ Handle_Outline ตรงกับสีที่เลือกบนจอย

อ้างอิง API: [Photon Custom Properties](https://doc.photonengine.com/pun/current/gameplay/synchronization-and-state), [Photon rejoin และ UserID](https://doc-api.photonengine.com/en/pun/current/class_photon_1_1_pun_1_1_photon_network.html)


## WebGL Arena รุ่นใหม่

- จอเกมหลัก: `Builds/WebGL/index.html` และจอยมือถือ: `Builds/WebGL/Controller/index.html` คำสั่ง Build Controller สร้างลง Subfolder นี้โดยตรง
- อัปโหลดทุกไฟล์ภายใน `Builds/WebGL` เป็นรากเว็บไซต์ Vercel จะได้จอเกมที่ `/` และจอยที่ `/Controller/` แต่ละหน้ามี Unity instance เดียว
- QR ใช้ `https://taokaenoiarena.vercel.app/Controller` พร้อม `?room=...&uid=...&host=...&mode=controller` โดย uid ใน URL คือเจ้าของห้อง ส่วน UID ผู้เล่นมือถือจำใน PlayerPrefs แยกกัน
- ชุด Arena เปิดจอยได้ด้วย `?mode=controller&room=...&uid=...` และจำห้อง/UID เพื่อเชื่อมต่อกลับ
- หน้าเกมขยายเต็มพื้นที่หน้าต่าง ปุ่ม “เต็มจอ” ต้องกดหนึ่งครั้งตามข้อกำหนดของเบราว์เซอร์
- การ์ดอันดับขวาบนแสดง 1st, 2nd, 3rd, 4th–20th เรียงคะแนนทุก 0.15 วินาที แสดงเฉพาะจำนวนผู้เล่น
- ซองขนมใหญ่ขึ้นจาก 0.85 เป็น 1.15 เมตร เก็บได้ +25 คะแนน
- ศัตรู enemy.glb สุ่มสี เดินก่อกวนและสตันเมื่อชน มีไม่เกิน 3 ตัว ยิงได้ +50 คะแนน เกิดใหม่ห่างกัน 7–11 วินาที
- สร้างทั้งสามชุดจากเมนู `Seaweed Slayer > Build WebGL Arena + Controller + Windows`

## แก้ฉากผ่าน Hierarchy

UI และองค์ประกอบเกมถูกบันทึกใน `Assets/Scenes/Game.unity` แล้ว มี `Arena`, `PC UI`, `Controller UI` และ Prefab slots สำหรับตัวละคร/ขนม/ศัตรู/กระสุน การเล่นใช้วัตถุเดิม เปิด/ปิดตามสถานะเกม และไม่เขียนทับ Main Camera หรือ RectTransform ของ UI

ดู [คู่มือแก้ฉาก](Assets/SeaweedSlayer/EDITING.md) สำหรับการแก้ Canvas, Prefab, Layout Group และปิดการสุ่มต้นไม้ หาก Unity เปิดฉากรุ่นเดิมค้างอยู่ ให้เปิด Game ใหม่เพื่อโหลด Hierarchy ล่าสุด

## ??????????? Controller

????????? 10 ??????????: ???, ???, ?????, ???????, ??????, ????, ??????, ????, ???, ??? ????????????????????????????? ????????????? 2 ??? ?????????????????? ?????????? THROW ???????????????????????????????????? safe area ???????????
