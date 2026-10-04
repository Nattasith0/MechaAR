using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

/// <summary>Explicit scene composition. Owns only MechaAR Application Canvas; existing AR objects are retained.</summary>
public static class MechaARPhase3Setup
{
    public const string ScenePath="Assets/Scenes/MechaAR_Main.unity";
    const string Evidence=".agent-system/tasks/MECHA-PHASE3/evidence";
    const string CanvasName="MechaAR Application Canvas";
    static TMP_FontAsset font;
    static Color Ivory=Hex("F4F5ED"),Ink=Hex("142B29"),Lime=Hex("D4F268"),Sage=Hex("DCE8DF"),Muted=Hex("64736C");
    static MechaARUIManager manager;
    static SerializedObject config;

    [MenuItem("MechaAR/Phase 3/Install application UI")]
    public static void Install()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Exit Play Mode before setup.");
        for(int i=0;i<EditorSceneManager.sceneCount;i++) Require(!EditorSceneManager.GetSceneAt(i).isDirty,"Save and close dirty scenes before setup.");
        font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Fonts/MechaARThai SDF.asset");
        Require(font!=null,"Existing Thai font is required.");
        string backup=".agent-system/tasks/MECHA-PHASE3/backups/builder/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(backup);
        File.Copy(ScenePath,backup+"/MechaAR_Main.unity");
        File.Copy(ScenePath+".meta",backup+"/MechaAR_Main.unity.meta");
        var scene=EditorSceneManager.OpenScene(ScenePath);
        var info=UnityEngine.Object.FindFirstObjectByType<EquipmentInfoUI>(FindObjectsInactive.Include);
        var db=UnityEngine.Object.FindFirstObjectByType<EquipmentDatabase>(FindObjectsInactive.Include);
        var session=UnityEngine.Object.FindFirstObjectByType<ARSession>(FindObjectsInactive.Include);
        var cameraManager=UnityEngine.Object.FindFirstObjectByType<ARCameraManager>(FindObjectsInactive.Include);
        Require(info!=null&&db!=null&&session!=null&&cameraManager!=null,"Existing AR/UI/database references missing; no edits made.");
        Require(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==1,"Expected existing single EventSystem.");
        var existing=scene.GetRootGameObjects().FirstOrDefault(x=>x.name==CanvasName);
        if(existing!=null){Require(existing.GetComponent<MechaARUIManager>()!=null,"Canvas name collision.");UnityEngine.Object.DestroyImmediate(existing);}
        var canvas=Rect(CanvasName,null);canvas.gameObject.AddComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        canvas.GetComponent<Canvas>().sortingOrder=30;
        var scaler=canvas.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1080,1920);scaler.matchWidthOrHeight=.5f;
        canvas.gameObject.AddComponent<GraphicRaycaster>();
        manager=canvas.gameObject.AddComponent<MechaARUIManager>(); config=new SerializedObject(manager);
        Set("equipmentDatabase",db);Set("equipmentInfoUI",info);Set("arSession",session);Set("arCameraManager",cameraManager);
        var backdrop=Rect("OfflineBackdrop",canvas);backdrop.gameObject.AddComponent<Image>().color=Ivory;Set("offlineBackdrop",backdrop.gameObject);
        var safe=Rect("ApplicationSafeArea",canvas);safe.gameObject.AddComponent<SafeAreaPanel>();
        BuildHome(safe);BuildLibrary(safe);BuildDetail(safe);BuildTutorial(safe);BuildAbout(safe);BuildScanner(safe);
        config.ApplyModifiedPropertiesWithoutUndo();
        // Defer camera permission until the user explicitly enters Scanner. Never disable the XR Origin/tracker.
        session.enabled=false;cameraManager.enabled=false;
        RestyleScanner(info);
        Validate();
        AssetDatabase.SaveAssets();
        Require(EditorSceneManager.SaveScene(scene),"Scene save failed.");
        Debug.Log("[MechaAR Phase3] INSTALL PASSED");
    }
    static void BuildHome(RectTransform safe)
    {
        var page=Page("HomePage",safe,"homePage",true);Header(page,"MechaAR","FIELD LAB  /  เรียนรู้ผ่านโลกจริง",false);
        var content=Scroll(page,"HomeScroll",out _);
        Text(content,"Eyebrow","EXPLORE. SCAN. UNDERSTAND.",25,Muted,48);
        Text(content,"HeroTitle","เปลี่ยนความสงสัย\nให้มองเห็นได้",66,Ink,192);
        Text(content,"HeroDescription","สำรวจอุปกรณ์วิศวกรรมผ่านโมเดล 3D\nและเรียนรู้หลักการทำงานด้วย AR",32,Muted,105);
        var hero=Block(content,"EngineeringHero",360,Ink);
        var art=Rect("CircuitArtwork",hero);art.gameObject.AddComponent<MechaARArtworkGraphic>().raycastTarget=false;
        FixedText(hero,"HeroCaption","ENGINEERING IN YOUR HANDS",22,Lime,new Vector2(28,10),new Vector2(-28,46),false);
        Action(content,"StartScanButton","เริ่มสแกนอุปกรณ์   /   AR",112,Lime,Ink,manager.StartScan);
        Text(content,"ScanHint","ใช้กล้องสแกนภาพเป้าหมายของอุปกรณ์ที่รองรับ",26,Muted,54);
        Text(content,"FeaturedHeading","ในห้องทดลองของคุณ",40,Ink,82);
        var previews=Vertical(content,"HomePreviewContent",14);Set("homePreviewContent",previews);
        var template=CardTemplate(previews,"HomeCardTemplate",170);Set("homeCardTemplate",template);
        Set("homeEmptyText",Text(content,"HomeEmptyText","ยังไม่มีอุปกรณ์ในคลัง",30,Muted,60));
        Action(content,"ViewLibraryButton","ดูคลังอุปกรณ์ทั้งหมด   >",90,Sage,Ink,manager.ShowLibrary);
        Text(content,"HomeFootnote","เรียนรู้ได้แม้ออฟไลน์ • เปิดกล้องเมื่อเริ่มสแกน",25,Muted,70);
        BottomNav(page,0);
    }
    static void BuildLibrary(RectTransform safe)
    {
        var page=Page("LibraryPage",safe,"libraryPage");Header(page,"คลังอุปกรณ์","EQUIPMENT LIBRARY",true);
        var content=Scroll(page,"LibraryScroll",out _);
        Text(content,"LibraryIntro","รู้จักชิ้นส่วน\nก่อนเริ่มลงมือ",56,Ink,172);
        Text(content,"LibraryDescription","เลือกอุปกรณ์เพื่ออ่านรายละเอียดและตัวอย่างการใช้งาน โดยไม่ต้องเปิดกล้อง",31,Muted,120);
        Set("libraryEmptyText",Text(content,"LibraryEmptyText","ยังไม่มีอุปกรณ์ในคลัง",32,Muted,70));
        var cards=Vertical(content,"LibraryContent",22);Set("libraryContent",cards);Set("libraryCardTemplate",CardTemplate(cards,"LibraryCardTemplate",250));
        Text(content,"LibraryTip","พร้อมเห็นโมเดล 3D? กลับไปเริ่มสแกนภาพเป้าหมายของอุปกรณ์",29,Muted,120);
        BottomNav(page,1);
    }
    static void BuildDetail(RectTransform safe)
    {
        var page=Page("DetailPage",safe,"detailPage");Header(page,"ข้อมูลอุปกรณ์","LEARN THE DETAILS",true);
        var content=Scroll(page,"DetailScroll",out var scroll);Set("detailScroll",scroll);
        var hero=Block(content,"DetailArtwork",300,Ink);Rect("CircuitArtwork",hero).gameObject.AddComponent<MechaARArtworkGraphic>().raycastTarget=false;
        Set("detailCategoryText",Text(content,"DetailCategoryText","CATEGORY",27,Muted));
        Set("detailNameText",Text(content,"DetailNameText","Equipment",50,Ink));
        Set("detailThaiNameText",Text(content,"DetailThaiNameText","ชื่ออุปกรณ์",35,Muted));
        Text(content,"DescriptionHeading","รู้จักอุปกรณ์",34,Ink,76);Set("detailDescriptionText",Text(content,"DetailDescriptionText","",32,Ink));
        Text(content,"PrincipleHeading","หลักการทำงาน",34,Ink,76);Set("detailPrincipleText",Text(content,"DetailPrincipleText","",32,Ink));
        Text(content,"ApplicationsHeading","นำไปใช้อะไรได้บ้าง",34,Ink,76);Set("detailApplicationsText",Text(content,"DetailApplicationsText","",32,Ink));
        Action(content,"DetailScanButton","เปิดกล้องสแกนภาพเป้าหมาย",112,Lime,Ink,manager.StartScan);
        Text(content,"DetailScanHint","เลือกภาพเป้าหมายที่ตรงกับอุปกรณ์ กล้องจะแสดงโมเดลเมื่อจดจำภาพได้",28,Muted,115);BottomNav(page,1);
    }
    static void BuildTutorial(RectTransform safe)
    {
        var page=Page("TutorialPage",safe,"tutorialPage");Header(page,"เริ่มต้นใช้งาน","YOUR FIRST AR EXPERIENCE",true);var content=Scroll(page,"TutorialScroll",out _);
        Text(content,"TutorialHeadline","จากภาพบนกระดาษ\nสู่ความเข้าใจใน 3D",53,Ink,170);
        Step(content,"01","เริ่มสแกนอุปกรณ์","แตะปุ่มเริ่มสแกนจากหน้าแรก แล้วอนุญาตการใช้กล้องเมื่อระบบถาม");
        Step(content,"02","มองหาภาพเป้าหมาย","ใช้ภาพ Image Target ของอุปกรณ์ที่เพิ่มไว้ใน MechaAR ระบบจดจำภาพที่กำหนด ไม่ใช่ AI ตรวจจับวัตถุจริงทั่วไป");
        Step(content,"03","ส่องกล้องและรอสักครู่","ให้ภาพอยู่ในกรอบกล้อง มีแสงเพียงพอ และถือโทรศัพท์ให้นิ่ง เมื่อพบภาพ โมเดล 3D จะปรากฏ");
        Step(content,"04","สำรวจและเรียนรู้","อ่านข้อมูล เลื่อนดูหลักการและการใช้งาน ปิดการ์ดเพื่อดูโมเดล หรือกลับหน้าแรกเพื่อเลือกหัวข้ออื่น");
        Text(content,"PermissionHelp","หากไม่เห็นภาพกล้อง ให้ตรวจสิทธิ์กล้องในการตั้งค่า Android และตรวจว่า Google Play Services for AR พร้อมใช้งาน",29,Muted);
        Action(content,"TutorialScanButton","พร้อมแล้ว เริ่มสแกน",112,Lime,Ink,manager.StartScan);BottomNav(page,2);
    }
    static void BuildAbout(RectTransform safe)
    {
        var page=Page("AboutPage",safe,"aboutPage");Header(page,"เกี่ยวกับ MechaAR","BUILT FOR CURIOUS MINDS",true);var content=Scroll(page,"AboutScroll",out _);
        Text(content,"AboutBrand","MechaAR",80,Ink,132);Text(content,"AboutTagline","วิศวกรรมที่เรียนรู้ได้\nผ่านโลกจริง",51,Ink,174);
        Text(content,"AboutDescription","สื่อการเรียนรู้ด้านวิศวกรรมและอิเล็กทรอนิกส์ด้วยเทคโนโลยี Augmented Reality ช่วยเชื่อมภาพอุปกรณ์ โมเดลสามมิติ และความรู้พื้นฐานเข้าด้วยกัน",33,Ink);
        Text(content,"AboutVersion","APPLICATION VERSION  "+Application.version,26,Muted,80);
        Text(content,"CreditsTitle","เครดิตและแหล่งที่มา",38,Ink,82);
        Text(content,"CreditsModels","Nema 17 Stepper Motor (42mm x 48mm)\nโดย moogh / Sketchfab\n\nArduino Uno Board\nโดย crimsonfalcon / Sketchfab\n\nโมเดลทั้งสองใช้สัญญาอนุญาต CC BY 4.0 มีการปรับขนาดและตำแหน่งเพื่อแสดงผลใน AR",29,Ink);
        Link(content,"StepperSourceButton","แหล่งที่มาโมเดล Stepper Motor","https://sketchfab.com/3d-models/nema-17-stepper-motor-42mm-x-48mm-b970d52c4b554768a1b576cb381abf07");
        Link(content,"ArduinoSourceButton","แหล่งที่มาโมเดล Arduino Uno","https://sketchfab.com/3d-models/arduino-uno-board-f31feafc5e9743abbdf33c54f9d92669");
        Link(content,"LicenseButton","อ่านสัญญาอนุญาต CC BY 4.0","https://creativecommons.org/licenses/by/4.0/");
        Text(content,"FontCredit","ตัวอักษร Noto Sans Thai / SIL Open Font License 1.1\nภาพประกอบวงจรสร้างขึ้นสำหรับ MechaAR\nพัฒนาด้วย Unity, AR Foundation และ TextMeshPro",29,Muted);
        Text(content,"AboutPrivacy","เปิดกล้องเมื่อคุณเลือกสแกน ข้อมูลในคลังอุปกรณ์อ่านได้โดยไม่ต้องใช้กล้อง",30,Ink,140);BottomNav(page,3);
    }
    static void BuildScanner(RectTransform safe)
    {
        var page=Page("ScannerPage",safe,"scannerPage");
        var home=ButtonAt(page,"ScannerHomeButton","<  หน้าหลัก",Ink,Ivory);Anchor(home.GetComponent<RectTransform>(),new Vector2(1,1),Vector2.one,new Vector2(-280,-142),new Vector2(-28,-24));UnityEventTools.AddPersistentListener(home.onClick,manager.ShowHome);
    }
    static void RestyleScanner(EquipmentInfoUI info)
    {
        foreach(var graphic in info.GetComponentsInChildren<RoundedPanelGraphic>(true))
        {
            graphic.color=graphic.name=="Accent"||graphic.name=="Handle"?Lime:Ink;
        }
        var top=info.transform.Find("SafeArea/TopBar");
        if(top!=null)
        {
            var r=(RectTransform)top; r.offsetMax=new Vector2(-298,r.offsetMax.y);
            var title=top.Find("AppNameText")?.GetComponent<TMP_Text>();if(title!=null){title.fontSize=36;title.rectTransform.offsetMax=new Vector2(-220,-15);}
            var status=top.Find("ARStatusText")?.GetComponent<TMP_Text>();if(status!=null){status.fontSize=23;status.rectTransform.anchorMin=new Vector2(.48f,0);}
        }
        foreach(var text in info.GetComponentsInChildren<TMP_Text>(true)) if(text.name=="SectionHeading")text.color=Lime;
    }
    static RectTransform Page(string name,Transform safe,string field,bool active=false){var r=Rect(name,safe);Set(field,r.gameObject);r.gameObject.SetActive(active);return r;}
    static void Header(Transform page,string title,string sub,bool back)
    {
        var r=Rect("Header",page);Anchor(r,new Vector2(0,1),Vector2.one,new Vector2(46,-158),new Vector2(-46,-20));
        var t=FixedText(r,"PageTitle",title,42,Ink,new Vector2(back?138:0,53),new Vector2(-10,-2));t.fontStyle=FontStyles.Bold;
        FixedText(r,"PageSubtitle",sub,22,Muted,new Vector2(back?138:0,6),new Vector2(-10,-84));
        if(back){var b=ButtonAt(r,"BackButton","<",Sage,Ink);Anchor(b.GetComponent<RectTransform>(),Vector2.zero,new Vector2(0,1),Vector2.zero,new Vector2(110,0));UnityEventTools.AddPersistentListener(b.onClick,manager.GoBack);}
        else{var badge=Rect("BrandMark",r);Anchor(badge,new Vector2(1,0),Vector2.one,new Vector2(-90,20),new Vector2(0,-20));badge.gameObject.AddComponent<RoundedPanelGraphic>().color=Lime;FixedText(badge,"Mark","AR",26,Ink,Vector2.zero,Vector2.zero).alignment=TextAlignmentOptions.Center;}
    }
    static void BottomNav(Transform page,int selected)
    {
        var nav=Rect("BottomNavigation",page);Anchor(nav,Vector2.zero,new Vector2(1,0),new Vector2(28,14),new Vector2(-28,138));nav.gameObject.AddComponent<RoundedPanelGraphic>().color=Ink;
        string[] labels={"หน้าหลัก","คลังอุปกรณ์","วิธีใช้งาน","เกี่ยวกับ"};UnityAction[] actions={manager.ShowHome,manager.ShowLibrary,manager.ShowTutorial,manager.ShowAbout};
        for(int i=0;i<4;i++){var b=ButtonAt(nav,"Nav"+i,labels[i],i==selected?Lime:Ink,i==selected?Ink:Ivory);Anchor(b.GetComponent<RectTransform>(),new Vector2(i*.25f,0),new Vector2((i+1)*.25f,1),new Vector2(8,12),new Vector2(-8,-12));b.GetComponentInChildren<TMP_Text>().fontSize=26;UnityEventTools.AddPersistentListener(b.onClick,actions[i]);}
    }
    static RectTransform Scroll(Transform parent,string name,out ScrollRect scroll)
    {
        var r=Rect(name,parent);Anchor(r,Vector2.zero,Vector2.one,new Vector2(46,158),new Vector2(-46,-174));scroll=r.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;
        var viewport=Rect("Viewport",r);viewport.gameObject.AddComponent<RectMask2D>();viewport.gameObject.AddComponent<Image>().color=Color.clear;
        var content=Vertical(viewport,"Content",16);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.offsetMin=Vector2.zero;content.offsetMax=Vector2.zero;
        scroll.viewport=viewport;scroll.content=content;return content;
    }
    static RectTransform Vertical(Transform parent,string name,float spacing)
    {
        var r=Rect(name,parent);var layout=r.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=spacing;layout.childControlHeight=layout.childControlWidth=true;layout.childForceExpandHeight=false;layout.childForceExpandWidth=true;layout.padding=new RectOffset(0,0,8,24);var fit=r.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;return r;
    }
    static EquipmentLibraryCard CardTemplate(Transform parent,string name,float height)
    {
        var r=Block(parent,name,height,Sage);var button=r.gameObject.AddComponent<Button>();button.targetGraphic=r.GetComponent<Graphic>();ButtonColors(button);
        var index=FixedText(r,"IndexText","01",29,Muted,new Vector2(26,12),new Vector2(-26,-12));index.rectTransform.anchorMax=new Vector2(.10f,1);index.alignment=TextAlignmentOptions.Center;
        var n=FixedText(r,"NameText","Equipment",35,Ink,new Vector2(115,height*.53f),new Vector2(-65,-25));
        var thai=FixedText(r,"ThaiNameText","ชื่ออุปกรณ์",29,Ink,new Vector2(115,height*.29f),new Vector2(-65,-height*.45f));
        var category=FixedText(r,"CategoryText","CATEGORY",23,Muted,new Vector2(115,14),new Vector2(-65,-height*.68f));
        var arrow=FixedText(r,"Arrow",">",32,Ink,new Vector2(-50,0),new Vector2(-10,0));arrow.rectTransform.anchorMin=new Vector2(1,0);
        var card=r.gameObject.AddComponent<EquipmentLibraryCard>();var s=new SerializedObject(card);Set(s,"openButton",button);Set(s,"nameText",n);Set(s,"thaiNameText",thai);Set(s,"categoryText",category);Set(s,"indexText",index);s.ApplyModifiedPropertiesWithoutUndo();r.gameObject.SetActive(false);return card;
    }
    static void Step(Transform parent,string number,string heading,string body)
    {Text(parent,"Step"+number+"Heading",number+"  /  "+heading,36,Ink,85);Text(parent,"Step"+number+"Body",body,32,Muted);}
    static void Link(Transform parent,string name,string label,string url)
    {var b=Action(parent,name,label+"   >",92,Sage,Ink,null);UnityEventTools.AddStringPersistentListener(b.onClick,Application.OpenURL,url);}
    static Button Action(Transform parent,string name,string label,float height,Color bg,Color fg,UnityAction action)
    {var b=ButtonAt(parent,name,label,bg,fg);Height(b.gameObject,height);if(action!=null)UnityEventTools.AddPersistentListener(b.onClick,action);return b;}
    static Button ButtonAt(Transform parent,string name,string label,Color bg,Color fg)
    {var r=Rect(name,parent);r.gameObject.AddComponent<RoundedPanelGraphic>().color=bg;var b=r.gameObject.AddComponent<Button>();b.targetGraphic=r.GetComponent<Graphic>();ButtonColors(b);var t=FixedText(r,"Label",label,32,fg,new Vector2(18,6),new Vector2(-18,-6));t.alignment=TextAlignmentOptions.Center;return b;}
    static void ButtonColors(Button b){var c=b.colors;c.highlightedColor=new Color(.94f,.97f,.91f);c.pressedColor=new Color(.76f,.83f,.73f);c.selectedColor=Color.white;c.fadeDuration=.10f;b.colors=c;}
    static RectTransform Block(Transform parent,string name,float height,Color color){var r=Rect(name,parent);r.gameObject.AddComponent<RoundedPanelGraphic>().color=color;Height(r.gameObject,height);return r;}
    static TMP_Text Text(Transform parent,string name,string value,float size,Color color,float height=0)
    {var t=FixedText(parent,name,value,size,color,Vector2.zero,Vector2.zero);t.alignment=TextAlignmentOptions.TopLeft;t.lineSpacing=8;if(height>0)Height(t.gameObject,height);return t;}
    static TMP_Text FixedText(Transform parent,string name,string value,float size,Color color,Vector2 low,Vector2 high,bool stretch=true)
    {var r=Rect(name,parent);if(stretch){r.offsetMin=low;r.offsetMax=high;}else Anchor(r,Vector2.zero,new Vector2(1,0),low,high);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=value;t.fontSize=size;t.color=color;t.raycastTarget=false;t.richText=false;t.textWrappingMode=TextWrappingModes.Normal;t.alignment=TextAlignmentOptions.MidlineLeft;return t;}
    static void Height(GameObject g,float h){var l=g.AddComponent<LayoutElement>();l.minHeight=h;l.preferredHeight=h;l.flexibleHeight=0;}
    static RectTransform Rect(string name,Transform parent){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();if(parent!=null)r.SetParent(parent,false);Anchor(r,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);return r;}
    static void Anchor(RectTransform r,Vector2 min,Vector2 max,Vector2 low,Vector2 high){r.anchorMin=min;r.anchorMax=max;r.offsetMin=low;r.offsetMax=high;}
    static void Set(string field,UnityEngine.Object value){Set(config,field,value);}
    static void Set(SerializedObject obj,string field,UnityEngine.Object value){var p=obj.FindProperty(field);Require(p!=null,"Serialized field not found: "+field);p.objectReferenceValue=value;}
    static Color Hex(string value){ColorUtility.TryParseHtmlString("#"+value,out var color);return color;}
    static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}

    [MenuItem("MechaAR/Phase 3/Validate application scene")]
    public static void Validate()
    {
        Require(EditorSceneManager.GetActiveScene().path==ScenePath,"Open MechaAR_Main first.");
        var managers=UnityEngine.Object.FindObjectsByType<MechaARUIManager>(FindObjectsInactive.Include,FindObjectsSortMode.None);Require(managers.Length==1,"Expected one application UI manager.");
        var serialized=new SerializedObject(managers[0]);
        foreach(var field in new[]{"equipmentDatabase","equipmentInfoUI","arSession","arCameraManager","offlineBackdrop","homePage","scannerPage","libraryPage","detailPage","tutorialPage","aboutPage","homePreviewContent","libraryContent","homeCardTemplate","libraryCardTemplate","detailNameText","detailThaiNameText","detailCategoryText","detailDescriptionText","detailPrincipleText","detailApplicationsText","detailScroll"}) Require(serialized.FindProperty(field)?.objectReferenceValue!=null,"Missing reference: "+field);
        foreach(var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0,"Missing script: "+t.name);
        Require(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==1,"Duplicate EventSystem.");
        Require(!UnityEngine.Object.FindFirstObjectByType<ARSession>(FindObjectsInactive.Include).enabled,"ARSession must wait for StartScan.");
        Require(!UnityEngine.Object.FindFirstObjectByType<ARCameraManager>(FindObjectsInactive.Include).enabled,"Camera manager must wait for StartScan.");
        var apis=PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);Require(apis.Length==1&&apis[0]==GraphicsDeviceType.OpenGLES3,"GLES3 must remain unchanged.");Require(EditorBuildSettings.scenes.First(s=>s.enabled).path==ScenePath,"Main scene must be first.");
        foreach(var text in managers[0].GetComponentsInChildren<TMP_Text>(true))Require(text.font!=null&&text.font.HasCharacters(text.text.Replace("\n", "").Replace("\r", "").Replace("\t", ""),out uint[] missing,true,true),"Missing glyphs in "+text.name);
        Directory.CreateDirectory(Evidence);File.WriteAllText(Evidence+"/phase3-scene-validation.txt","PASSED: references, missing scripts, single manager/EventSystem, deferred AR, GLES3, main scene, Thai font glyphs.\n"+DateTime.UtcNow.ToString("O"));Debug.Log("[MechaAR Phase3] VALIDATION PASSED");
    }
    public static void BuildAndroid()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Exit Play Mode before building.");
        for(int i=0;i<EditorSceneManager.sceneCount;i++)Require(!EditorSceneManager.GetSceneAt(i).isDirty,"Save dirty scenes before building.");
        EditorSceneManager.OpenScene(ScenePath);Validate();Directory.CreateDirectory("Builds");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),locationPathName="Builds/MechaAR-Phase3-HomeUI.apk",target=BuildTarget.Android,options=BuildOptions.None});
        File.WriteAllText(Evidence+"/phase3-android-build.txt",$"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nSize: {report.summary.totalSize}\nDuration: {report.summary.totalTime}\n");Require(report.summary.result==BuildResult.Succeeded,"Android build failed.");
    }
}

