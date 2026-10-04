# MechaAR

**Explore engineering equipment through interactive 3D models and augmented reality.**

Android · Unity 6.3 LTS · **v1.0.0**

## Project Overview

MechaAR is an Android learning application that combines an equipment library, technical information, a 3D viewer, and image-based AR. Learners can explore equipment on screen or scan a matching image target to view its model in their surroundings.

แอปพลิเคชันสำหรับเรียนรู้อุปกรณ์วิศวกรรมผ่านข้อมูล โมเดลสามมิติ และเทคโนโลยีความเป็นจริงเสริม

## Features

- **Equipment Library:** browse eight engineering devices.
- **Search & Filter:** search by Thai name, English name, or equipment ID; combine search with category filters.
- **Equipment Detail:** read descriptions, working principles, and applications.
- **3D Viewer:** drag to rotate a model and reset its view.
- **AR Image Tracking:** scan a configured image target to display its associated model.
- **AR Interaction:** drag to rotate, pinch to zoom, and reset rotation and scale.
- **Tutorial & About:** usage guidance and project information.

## Supported Equipment

| ID | Equipment |
| --- | --- |
| EQ001 | Stepper Motor |
| EQ002 | Arduino UNO |
| EQ003 | Servo Motor SG90 |
| EQ004 | HC-SR04 Ultrasonic Sensor |
| EQ005 | ESP32 DevKit |
| EQ006 | DHT11 |
| EQ007 | L298N Motor Driver |
| EQ008 | TT DC Gear Motor |

## Tech Stack

| Technology | Role |
| --- | --- |
| Unity 6.3 LTS (`6000.3.21f1`) | Game engine and Android application development |
| C# | Application logic |
| AR Foundation `6.3.5` | AR session and image tracking |
| Google ARCore XR Plugin `6.3.5` | Android AR provider |
| Universal Render Pipeline (URP) `17.3.0` | Rendering |
| OpenGLES3 | Android graphics API |
| TextMeshPro | UI text, including Thai |
| Input System `1.20.0` | Input handling |

## Project Structure

```text
MechaAR/
├── Assets/
│   ├── Scenes/MechaAR_Main.unity  # Main application scene
│   ├── Scripts/
│   │   ├── AR/                   # Tracking and AR interaction
│   │   ├── Equipment/            # Equipment data and database code
│   │   └── UI/                   # Navigation, library, and 3D viewer
│   ├── EquipmentData/            # Equipment data assets
│   ├── ImageLibraries/           # AR reference image library
│   ├── ImageTargets/             # Target images
│   ├── Models/                   # Model assets
│   ├── Prefabs/                  # Reusable objects
│   ├── Materials/                # Materials
│   └── UI/                       # UI assets
├── Packages/                     # Package dependencies
├── ProjectSettings/              # Unity project configuration
└── README.md
```

## Requirements

- Unity Hub and **Unity 6.3 LTS (`6000.3.21f1`)**.
- Android Build Support, including Android SDK & NDK Tools and OpenJDK, installed through Unity Hub.
- Git and an internet connection for cloning and restoring Unity packages.
- For device testing: an **ARCore-supported Android device**, Google Play Services for AR, and camera permission.
- Current project settings target **ARM64**, with minimum **Android 10 / API 29**. Android version alone does not guarantee ARCore compatibility.
- A matching reference image from `Assets/ImageTargets/` for AR scanning.

## Clone & Open in Unity

```bash
git clone https://github.com/Nattasith0/MechaAR.git
cd MechaAR
```

1. In Unity Hub, choose **Add project from disk** and select the cloned folder.
2. Open with Unity `6000.3.21f1` and wait for package restoration and asset import.
3. Open `Assets/Scenes/MechaAR_Main.unity`.
4. Enter Play Mode to explore the UI and 3D viewer. Validate camera tracking and touch gestures on an Android device.

## Build for Android

1. Open **File → Build Profiles**, select Android, and switch to the Android platform.
2. Confirm `Assets/Scenes/MechaAR_Main.unity` is enabled in the scene list.
3. Keep the existing Android configuration: **ARCore**, **URP**, **OpenGLES3**, and **ARM64**.
4. For a local APK, leave **Build App Bundle** disabled and select **Build**. Save the output outside `Assets/`, such as `Builds/Android/`.
5. Install on a supported device, allow camera access, and scan a matching target image. Use **Build And Run** if the device is connected and USB debugging is authorized.

Release signing and distribution require the project's own signing configuration. Do not commit keystores or passwords.

## Screenshots

> Add final screenshots here:

| Screen | Placeholder |
| --- | --- |
| Home | TODO: add final Home screenshot |
| Equipment Library | TODO: show equipment cards, search, and category filters |
| Equipment Detail | TODO: show equipment information |
| 3D Viewer | TODO: show a model and the reset-view control |
| AR Scanner | TODO: show the scanner interface |
| AR on Redmi | TODO: add an actual device capture with a tracked model |

Label Unity captures and Android device captures accurately. Use real app screenshots and preserve their aspect ratio.

<!-- Example after adding a real image:
![MechaAR Home](docs/screenshots/home.png)
-->

## Release / APK

**Version: v1.0.0**

- Release page: **TODO — add the published GitHub Release link.**
- Android APK: **TODO — add the download link after final QA and publication.**

<!-- Replace with verified links once published:
[Release v1.0.0](RELEASE_URL)
[Download Android APK](APK_DOWNLOAD_URL)
-->

## Known Limitations

- Android is the V1 target platform; no iOS release is provided here.
- AR recognizes configured **image targets**, not arbitrary physical equipment.
- Tracking quality depends on lighting, image visibility, camera angle, and device support.
- The 3D Viewer supports rotation and reset; pinch zoom is available in AR.
- V1 contains eight equipment entries. It does not provide circuit simulation or automatic equipment recognition.
- HC-SR04 color, AR scale, and gestures have been confirmed on Redmi. Detailed confirmation for the remaining final device QA cases is still pending; this README does not claim full Android QA completion.

## Future Development (V2)

Planned ideas, **not included in V1**:

- Learning Hotspots
- AI Scanner
- Quizzes
- Wiring Guides
- Circuit / equipment simulation

These are future directions, not a release commitment.

## Credits / 3D Model Sources

MechaAR uses third-party model assets. Complete the attribution below against the exact files included in the release; do not assume that all models share a license.

| Model | Creator | Source URL | License / Attribution |
| --- | --- | --- | --- |
| Stepper Motor | TODO: verify | TODO: verify original source | TODO: verify license and modifications |
| Arduino UNO | TODO: verify | TODO: verify original source | TODO: verify license and modifications |
| Servo Motor SG90 | TODO: verify | TODO: verify original source | TODO: verify license and modifications |
| HC-SR04 (current V2 model asset) | TODO: verify | TODO: verify replacement model source | TODO: verify license and modifications |
| ESP32 DevKit | TODO: verify | TODO: verify original source | TODO: verify license and modifications |
| DHT11 | TODO: verify | TODO: verify original source | TODO: verify license and modifications |
| L298N Motor Driver | TODO: verify | TODO: verify original source | TODO: verify license and modifications |
| TT DC Gear Motor | TODO: verify | TODO: verify original source | TODO: verify license and modifications |

Here, “V2 model asset” identifies the replacement HC-SR04 asset used in MechaAR V1; it does not indicate an application V2 feature.

- Fonts, images, and other third-party assets: **TODO — add verified sources and required notices.**
- Repository source-code license: **TODO — specify the intended license.** Third-party assets retain their respective licenses.

## Developer

- **GitHub:** [Nattasith0](https://github.com/Nattasith0)
- **Name:** TODO — add preferred developer name.
- **Institution / Course:** TODO — add project affiliation if applicable.
- **Contact:** TODO — add a public contact method if desired.

---

**MechaAR v1.0.0 — Explore · Scan · Understand**
