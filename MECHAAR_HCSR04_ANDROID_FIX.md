# HC-SR04 PCB colour on Android — 2026-10-01

> STATUS CORRECTION: The user confirmed the URP/Lit test APK still renders white PCB on Redmi. This attempted fix is NOT successful. Earlier attribution to the glTF shader alone is unproven and is withdrawn. The user subsequently replaced EQ004's prefab with HC_SR04_V2 using the Rusty HC-SR04 GLB; that new model also shows abnormal PCB colour on Android. Investigation continues against the new assets. The historical tests below apply to the earlier model, not the new one.

## Scope and backup

Only HC-SR04 PCB material rendering is changed. Snapshot of the current Assets, Packages and ProjectSettings is in `.agent-system/tasks/MECHA-HCSR04/snapshot`, with SHA-256 inventory in `baseline-hashes.json`. The APK originally installed on Redmi is preserved as `snapshot/Redmi-original.apk`. No old scene was restored.

## Diagnosis

- Reproduced white PCB in the existing Redmi installation and in a diagnostic build, while metal and pins render. EQ004 correctly references HC_SR04_AR. All 25 original material slots are populated.
- PCB materials `hc-sr04_fort` and `hc-sr04_back` use `Shader Graphs/glTF-pbrMetallicRoughness`, also used by the untextured metal materials. PCB base colour is white and its blue artwork comes from embedded GLB textures.
- References resolve to `image_2` (256×128, 9 mips) and `image_0` (128×64, 8 mips). Both use ARGB32. The glTFast ScriptedImporter owns these subassets; they do not have independent TextureImporter Android compression overrides.
- On the connected Redmi, the diagnostic build reports **OpenGLES3 / Mali-G615 MC2**. Both textures are present and `SupportsTextureFormat(ARGB32)` is true. GPU readback reproduces the correct blue artwork: front has 15,491 coloured pixels out of 32,768, back has 5,023 out of 8,192. Thus the missing colour is not a missing PNG, broken texture reference or unsupported compression format.
- The glTF shader reports supported on-device. The diagnostic build includes 36 GLES3 internal programs for this shader, including 24 Universal Forward variants. Its Built-In pipeline passes are stripped, but URP passes remain; this is not evidence of the entire shader being stripped. No global shader stripping settings are changed.
- The fault is isolated to the original glTF material/shader texture-rendering path on this Android/GLES3 device. GPU texture data itself remains correct. The exact internal shader/compiler/driver failure is not established; this report does not claim a particular stripped keyword or driver defect.

## Minimal correction

1. Add `Assets/Models/HC_SR04/Materials/HC_SR04_PCB_Front.mat`.
2. Add `Assets/Models/HC_SR04/Materials/HC_SR04_PCB_Back.mat`.
3. Override exactly two existing material slots in `Assets/Prefabs/HC_SR04_AR.prefab`.

The new materials use **Universal Render Pipeline/Lit**, opaque, double-sided rendering, metallic 0 and smoothness 0.5. They reference the **same embedded GLB textures** with the original tiling/offset. No textures are extracted into production assets or recompressed. The other 23 material slots, mesh data, transforms and hierarchy are unchanged.

The temporary runtime probe is removed before the fixed APK is built. Diagnostic/test tooling is retained outside Assets as evidence, not as a new runtime system.

## Validation

- Unity compilation and Play Mode check: PASS. EQ004 Detail opens; all 25 material slots have materials/shaders; both PCB sides render blue with artwork; rotation and existing ResetView work. Reset output matches initial output byte-for-byte; return to Library succeeds.
- Editor screenshots: `.agent-system/tasks/MECHA-HCSR04/evidence/unity-front.png` is the initial rear view; `unity-back.png` is the rotated front view. Names describe test capture order, not physical sides. `unity-reset.png` matches the initial view.
- Hash comparison after Play Mode: only HC_SR04_AR.prefab differs among pre-existing Assets/Packages/ProjectSettings. Scene, AR/Gesture/Viewer scripts, EquipmentDatabase/EquipmentData, GLB and other model prefabs remain identical.
- Android diagnostic build: succeeded, 0 errors. White PCB reproduced with intact GPU texture data. Evidence: `diagnostic-device.log`, `gpu-front.png`, `gpu-back.png`, `device-diagnostic.png`.
- Fixed Android build/device verification: pending.

## Redmi checks

After installing the fixed APK, inspect both PCB sides in EQ004 Detail, drag to rotate, press Reset View and reopen the detail. Then scan the HC-SR04 target to confirm colour in AR, including existing rotation, pinch and reset. The AR target test requires a physical target and is separate from the material/Detail test.

No Vulkan, AR logic, gestures, viewer behaviour, database or scene structure changes. No other development phase is included.
