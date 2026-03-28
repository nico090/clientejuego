using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.BossRoom.Editor
{
    public static class LobbyUIBuilder
    {
        // Palette
        static readonly Color kBg       = new(0.11f, 0.11f, 0.15f, 0.97f);
        static readonly Color kBar      = new(0.07f, 0.07f, 0.10f, 1f);
        static readonly Color kBlue     = new(0.20f, 0.50f, 0.82f, 1f);
        static readonly Color kGreen    = new(0.18f, 0.62f, 0.34f, 1f);
        static readonly Color kRed      = new(0.72f, 0.20f, 0.20f, 1f);
        static readonly Color kOrange   = new(0.82f, 0.55f, 0.15f, 1f);
        static readonly Color kInput    = new(0.16f, 0.16f, 0.20f, 1f);
        static readonly Color kScroll   = new(0.09f, 0.09f, 0.12f, 0.9f);
        static readonly Color kEntry    = new(0.14f, 0.14f, 0.18f, 0.95f);
        static readonly Color kModal    = new(0.09f, 0.09f, 0.12f, 0.99f);
        static readonly Color kWhite    = Color.white;
        static readonly Color kMuted    = new(0.60f, 0.60f, 0.65f, 1f);
        static readonly Color kYellow   = new(1f, 0.82f, 0.25f, 1f);

        [MenuItem("Tools/BossRoom/Build Lobby UI")]
        public static void Build()
        {
            var canvas = FindOrCreateCanvas();

            // ── ROOT ──
            var root = Go("LobbyPanel", canvas.transform);
            Anchor(root, 0.1f, 0.05f, 0.9f, 0.95f);
            Img(root, kBg);
            var cg = root.AddComponent<CanvasGroup>();
            var med = root.AddComponent<Gameplay.UI.LobbyUIMediator>();

            var vlg = root.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 1;

            // ═══════════ HEADER ═══════════
            var hdr = Go("Header", root.transform);
            Img(hdr, kBar);
            LE(hdr, -1, 52);
            var hHlg = hdr.AddComponent<HorizontalLayoutGroup>();
            hHlg.padding = new RectOffset(14, 14, 6, 6);
            hHlg.spacing = 10;
            hHlg.childAlignment = TextAnchor.MiddleLeft;
            hHlg.childForceExpandWidth = false;
            hHlg.childForceExpandHeight = false;

            var title = Txt("Title", hdr.transform, "SERVER BROWSER", 18, FontStyles.Bold, kWhite);
            LE(title.gameObject, 190, 40);

            Spacer(hdr.transform);

            var nameInp = InputField("NameInput", hdr.transform, "Your name...", 160, 34);
            var refreshBtn = Btn("Refresh", hdr.transform, "Refresh", 80, 34, kBlue);
            var createBtn = Btn("+ Create", hdr.transform, "+ Create", 110, 34, kGreen);
            var ipBtn = Btn("Direct IP", hdr.transform, "Direct IP", 100, 34, kOrange);

            // ═══════════ STATUS ROW ═══════════
            var sRow = Go("StatusRow", root.transform);
            Img(sRow, Color.clear);
            LE(sRow, -1, 24);
            var sHlg = sRow.AddComponent<HorizontalLayoutGroup>();
            sHlg.padding = new RectOffset(14, 14, 2, 2);
            sHlg.childForceExpandHeight = true;
            sHlg.childControlWidth = true;

            var statusTxt = Txt("Status", sRow.transform, "", 12, FontStyles.Italic, kMuted);
            LE(statusTxt.gameObject).flexibleWidth = 1;

            var spinner = Go("Spinner", sRow.transform);
            var sTmp = spinner.AddComponent<TextMeshProUGUI>();
            sTmp.text = "\u23F3"; sTmp.fontSize = 14; sTmp.alignment = TextAlignmentOptions.Center; sTmp.color = kWhite;
            LE(spinner, 24, -1);
            spinner.SetActive(false);

            // ═══════════ ROOM LIST ═══════════
            var scroll = Go("RoomScroll", root.transform);
            Img(scroll, kScroll);
            LE(scroll).flexibleHeight = 1;

            var sr = scroll.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 25;

            var vp = Go("Viewport", scroll.transform);
            Anchor(vp, 0, 0, 1, 1);
            Img(vp, Color.clear);
            vp.AddComponent<Mask>().showMaskGraphic = false;
            sr.viewport = vp.GetComponent<RectTransform>();

            var content = Go("Content", vp.transform);
            var crt = content.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0, 1); crt.anchorMax = new Vector2(1, 1);
            crt.pivot = new Vector2(0.5f, 1); crt.sizeDelta = Vector2.zero;
            var cVlg = content.AddComponent<VerticalLayoutGroup>();
            cVlg.padding = new RectOffset(6, 6, 6, 6);
            cVlg.spacing = 3;
            cVlg.childForceExpandWidth = true;
            cVlg.childForceExpandHeight = false;
            cVlg.childControlWidth = cVlg.childControlHeight = true;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = crt;

            var noRooms = Txt("NoRooms", vp.transform, "No rooms found. Create one!", 14, FontStyles.Italic, kMuted);
            noRooms.alignment = TextAlignmentOptions.Center;
            var nrrt = noRooms.GetComponent<RectTransform>();
            nrrt.anchorMin = new Vector2(0.5f, 0.5f); nrrt.anchorMax = new Vector2(0.5f, 0.5f);
            nrrt.sizeDelta = new Vector2(350, 30);
            Object.DestroyImmediate(noRooms.GetComponent<LayoutElement>());

            // ═══════════ BOTTOM BAR ═══════════
            var bot = Go("BottomBar", root.transform);
            Img(bot, kBar);
            LE(bot, -1, 44);
            var bHlg = bot.AddComponent<HorizontalLayoutGroup>();
            bHlg.padding = new RectOffset(14, 14, 6, 6);
            bHlg.spacing = 10;
            bHlg.childAlignment = TextAnchor.MiddleRight;
            bHlg.childForceExpandWidth = false;
            bHlg.childForceExpandHeight = false;
            Spacer(bot.transform);
            var closeBtn = Btn("Close", bot.transform, "Close", 90, 32, kRed);

            // ═══════════ CREATE ROOM MODAL ═══════════
            var crP = Modal("CreatePanel", root.transform, 380, 330, kGreen);
            var crVlg = crP.AddComponent<VerticalLayoutGroup>();
            crVlg.padding = new RectOffset(20, 20, 16, 16); crVlg.spacing = 6;
            crVlg.childForceExpandWidth = true; crVlg.childForceExpandHeight = false;
            crVlg.childControlHeight = crVlg.childControlWidth = true;

            Txt("T", crP.transform, "Create New Room", 17, FontStyles.Bold, kWhite);
            Txt("L", crP.transform, "Room Name", 11, FontStyles.Normal, kMuted);
            var rnInp = InputField("RoomName", crP.transform, "My Room", 0, 34);
            Txt("L", crP.transform, "Password (optional)", 11, FontStyles.Normal, kMuted);
            var rpInp = InputField("RoomPass", crP.transform, "", 0, 34);
            rpInp.contentType = TMP_InputField.ContentType.Password;
            Txt("L", crP.transform, "Max Players", 11, FontStyles.Normal, kMuted);
            var dd = Dropdown("MaxPlayers", crP.transform, 0, 32, new[]{"2","4","8"});
            dd.value = 2;

            var crRow = BtnRow(crP.transform);
            var crCancel = Btn("Cancel", crRow.transform, "Cancel", 0, 34, kRed);
            var crOk = Btn("Create", crRow.transform, "Create", 0, 34, kGreen);
            crP.SetActive(false);

            // ═══════════ PASSWORD MODAL ═══════════
            var pwP = Modal("PassPanel", root.transform, 320, 180, kBlue);
            var pwVlg = pwP.AddComponent<VerticalLayoutGroup>();
            pwVlg.padding = new RectOffset(20, 20, 16, 16); pwVlg.spacing = 8;
            pwVlg.childForceExpandWidth = true; pwVlg.childForceExpandHeight = false;
            pwVlg.childControlHeight = pwVlg.childControlWidth = true;

            Txt("T", pwP.transform, "Enter Password", 17, FontStyles.Bold, kWhite);
            var jpInp = InputField("JoinPass", pwP.transform, "Password...", 0, 34);
            jpInp.contentType = TMP_InputField.ContentType.Password;
            var pwRow = BtnRow(pwP.transform);
            var pwCancel = Btn("Cancel", pwRow.transform, "Cancel", 0, 34, kRed);
            var pwOk = Btn("Join", pwRow.transform, "Join", 0, 34, kBlue);
            pwP.SetActive(false);

            // ═══════════ DIRECT IP MODAL ═══════════
            var dipP = Modal("DirectIPPanel", root.transform, 360, 230, kOrange);
            var dipVlg = dipP.AddComponent<VerticalLayoutGroup>();
            dipVlg.padding = new RectOffset(20, 20, 16, 16); dipVlg.spacing = 6;
            dipVlg.childForceExpandWidth = true; dipVlg.childForceExpandHeight = false;
            dipVlg.childControlHeight = dipVlg.childControlWidth = true;

            Txt("T", dipP.transform, "Direct IP Connection", 17, FontStyles.Bold, kWhite);
            Txt("L", dipP.transform, "IP Address", 11, FontStyles.Normal, kMuted);
            var ipAddrInp = InputField("IPAddr", dipP.transform, "127.0.0.1", 0, 34);
            Txt("L", dipP.transform, "Port", 11, FontStyles.Normal, kMuted);
            var ipPortInp = InputField("IPPort", dipP.transform, "7777", 0, 34);
            ipPortInp.contentType = TMP_InputField.ContentType.IntegerNumber;

            var dipRow = BtnRow(dipP.transform);
            var dipCancel = Btn("Cancel", dipRow.transform, "Cancel", 0, 34, kRed);
            var dipConnect = Btn("Join", dipRow.transform, "Join", 0, 34, kBlue);
            var dipHost = Btn("Host", dipRow.transform, "Host", 0, 34, kGreen);
            dipP.SetActive(false);

            // ═══════════ CONNECTING MODAL ═══════════
            var conP = Modal("ConnPanel", root.transform, 320, 130, kBlue);
            var conVlg = conP.AddComponent<VerticalLayoutGroup>();
            conVlg.padding = new RectOffset(20, 20, 18, 16); conVlg.spacing = 12;
            conVlg.childAlignment = TextAnchor.MiddleCenter;
            conVlg.childForceExpandWidth = true; conVlg.childForceExpandHeight = false;
            conVlg.childControlHeight = conVlg.childControlWidth = true;

            var conTxt = Txt("ConnTxt", conP.transform, "Connecting...", 15, FontStyles.Normal, kWhite);
            conTxt.alignment = TextAlignmentOptions.Center;
            var conCancel = Btn("Cancel", conP.transform, "Cancel", 0, 32, kRed);
            conP.SetActive(false);

            // ═══════════ ROOM ENTRY PREFAB ═══════════
            var prefab = BuildRoomEntryPrefab();

            // ═══════════ WIRE FIELDS ═══════════
            var so = new SerializedObject(med);
            S(so, "m_CanvasGroup", cg);
            S(so, "m_PlayerNameInput", nameInp);
            S(so, "m_LoadingSpinner", spinner);
            S(so, "m_StatusText", statusTxt);
            S(so, "m_RoomListContent", content.transform);
            S(so, "m_RoomEntryPrefab", prefab);
            S(so, "m_RefreshButton", refreshBtn.GetComponent<Button>());
            S(so, "m_NoRoomsText", noRooms);
            S(so, "m_CreateRoomPanel", crP);
            S(so, "m_RoomNameInput", rnInp);
            S(so, "m_RoomPasswordInput", rpInp);
            S(so, "m_MaxPlayersDropdown", dd);
            S(so, "m_CreateRoomButton", crOk.GetComponent<Button>());
            S(so, "m_ShowCreatePanelButton", createBtn.GetComponent<Button>());
            S(so, "m_CancelCreateButton", crCancel.GetComponent<Button>());
            S(so, "m_PasswordPanel", pwP);
            S(so, "m_JoinPasswordInput", jpInp);
            S(so, "m_ConfirmJoinButton", pwOk.GetComponent<Button>());
            S(so, "m_CancelPasswordButton", pwCancel.GetComponent<Button>());
            S(so, "m_DirectIPPanel", dipP);
            S(so, "m_IPAddressInput", ipAddrInp);
            S(so, "m_IPPortInput", ipPortInp);
            S(so, "m_ConnectIPButton", dipConnect.GetComponent<Button>());
            S(so, "m_HostIPButton", dipHost.GetComponent<Button>());
            S(so, "m_ShowDirectIPButton", ipBtn.GetComponent<Button>());
            S(so, "m_CancelDirectIPButton", dipCancel.GetComponent<Button>());
            S(so, "m_ConnectingPanel", conP);
            S(so, "m_ConnectingText", conTxt);
            S(so, "m_CancelConnectingButton", conCancel.GetComponent<Button>());
            so.ApplyModifiedProperties();

            // ── Browse button (outside panel) ──
            var browse = Btn("BrowseServers", canvas.transform, "Browse Servers", 200, 48, kBlue);
            var brt = browse.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.5f, 0);
            brt.anchorMax = new Vector2(0.5f, 0);
            brt.anchoredPosition = new Vector2(0, 70);

            // ── Host + Play button (outside panel) ──
            var hostPlay = Btn("HostAndPlay", canvas.transform, "Host + Play", 200, 48, kGreen);
            var hprt = hostPlay.GetComponent<RectTransform>();
            hprt.anchorMin = new Vector2(0.5f, 0);
            hprt.anchorMax = new Vector2(0.5f, 0);
            hprt.anchoredPosition = new Vector2(0, 130);

            // Wire Host+Play to LobbyUIMediator.HostAndPlay
            UnityEditor.Events.UnityEventTools.AddPersistentListener(
                hostPlay.GetComponent<Button>().onClick, new UnityEngine.Events.UnityAction(med.HostAndPlay));

            UnityEditor.Events.UnityEventTools.AddPersistentListener(
                browse.GetComponent<Button>().onClick, new UnityEngine.Events.UnityAction(med.Show));
            UnityEditor.Events.UnityEventTools.AddPersistentListener(
                closeBtn.GetComponent<Button>().onClick, new UnityEngine.Events.UnityAction(med.Hide));

            Undo.RegisterCreatedObjectUndo(root, "Build Lobby UI");
            Undo.RegisterCreatedObjectUndo(browse, "Build Browse Btn");
            Undo.RegisterCreatedObjectUndo(hostPlay, "Build Host+Play Btn");
            Selection.activeGameObject = root;

            Debug.Log("[LobbyUIBuilder] Done. Prefab at Assets/Prefabs/UI/LobbyRoomEntry.prefab");
        }

        // ─── Room Entry Prefab ───
        static GameObject BuildRoomEntryPrefab()
        {
            var r = new GameObject("LobbyRoomEntry");
            r.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 42);
            Img(r, kEntry);
            LE(r, -1, 42);

            var h = r.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(12, 8, 3, 3);
            h.spacing = 8;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            h.childControlWidth = true;

            var lk = Go("Lock", r.transform);
            var lkT = lk.AddComponent<TextMeshProUGUI>();
            lkT.text = "\uD83D\uDD12"; lkT.fontSize = 14; lkT.color = kYellow;
            lkT.alignment = TextAlignmentOptions.Center;
            LE(lk, 22, 36);

            var nm = Txt("Name", r.transform, "Room", 14, FontStyles.Normal, kWhite);
            LE(nm.gameObject).flexibleWidth = 1;
            LE(nm.gameObject).preferredHeight = 36;

            var cnt = Txt("Count", r.transform, "0/8", 13, FontStyles.Normal, kMuted);
            cnt.alignment = TextAlignmentOptions.Center;
            LE(cnt.gameObject, 45, 36);

            var st = Txt("Status", r.transform, "ready", 11, FontStyles.Italic, kMuted);
            st.alignment = TextAlignmentOptions.Center;
            LE(st.gameObject, 55, 36);

            var jb = Btn("Join", r.transform, "Join", 65, 30, kBlue);

            var comp = r.AddComponent<Gameplay.UI.LobbyRoomEntry>();
            var so = new SerializedObject(comp);
            S(so, "m_RoomNameText", nm);
            S(so, "m_PlayerCountText", cnt);
            S(so, "m_StatusText", st);
            S(so, "m_LockIcon", lk);
            S(so, "m_JoinButton", jb.GetComponent<Button>());
            so.ApplyModifiedProperties();

            const string dir = "Assets/Prefabs/UI";
            if (!AssetDatabase.IsValidFolder(dir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                    AssetDatabase.CreateFolder("Assets", "Prefabs");
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
            }
            var pf = PrefabUtility.SaveAsPrefabAsset(r, dir + "/LobbyRoomEntry.prefab");
            Object.DestroyImmediate(r);
            return pf;
        }

        // ─── Helpers ───

        static Canvas FindOrCreateCanvas()
        {
            Canvas c = null;
            if (Selection.activeGameObject) c = Selection.activeGameObject.GetComponentInParent<Canvas>();
            if (!c) c = Object.FindObjectOfType<Canvas>();
            if (!c)
            {
                var g = new GameObject("LobbyCanvas");
                c = g.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                var sc = g.AddComponent<CanvasScaler>();
                sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                sc.referenceResolution = new Vector2(1920, 1080);
                g.AddComponent<GraphicRaycaster>();
                Undo.RegisterCreatedObjectUndo(g, "Canvas");
            }
            return c;
        }

        static GameObject Go(string n, Transform p)
        {
            var g = new GameObject(n);
            g.transform.SetParent(p, false);
            g.AddComponent<RectTransform>();
            return g;
        }

        static void Anchor(GameObject g, float x0, float y0, float x1, float y1)
        {
            var r = g.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(x0, y0);
            r.anchorMax = new Vector2(x1, y1);
            r.offsetMin = r.offsetMax = Vector2.zero;
        }

        static Image Img(GameObject g, Color c)
        {
            var i = g.GetComponent<Image>();
            if (!i) i = g.AddComponent<Image>();
            i.color = c;
            return i;
        }

        static LayoutElement LE(GameObject g, float w = -1, float h = -1)
        {
            var le = g.GetComponent<LayoutElement>();
            if (!le) le = g.AddComponent<LayoutElement>();
            if (w >= 0) le.preferredWidth = w;
            if (h >= 0) le.preferredHeight = h;
            return le;
        }

        static void Spacer(Transform p)
        {
            var g = Go("_", p);
            LE(g).flexibleWidth = 1;
        }

        static TextMeshProUGUI Txt(string n, Transform p, string t, float sz, FontStyles fs, Color c)
        {
            var g = Go(n, p);
            var tmp = g.AddComponent<TextMeshProUGUI>();
            tmp.text = t; tmp.fontSize = sz; tmp.fontStyle = fs; tmp.color = c;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            LE(g, -1, sz + 8);
            return tmp;
        }

        static GameObject Btn(string n, Transform p, string label, float w, float h, Color c)
        {
            var g = Go(n, p);
            g.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
            var img = Img(g, c);
            var b = g.AddComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(.9f,.9f,.9f);
            cb.pressedColor = new Color(.7f,.7f,.7f);
            cb.disabledColor = new Color(.4f,.4f,.4f,.5f);
            b.colors = cb;

            var lb = Go("L", g.transform);
            Anchor(lb, 0, 0, 1, 1);
            var t = lb.AddComponent<TextMeshProUGUI>();
            t.text = label; t.fontSize = 13; t.fontStyle = FontStyles.Bold;
            t.color = kWhite; t.alignment = TextAlignmentOptions.Center;

            var le = LE(g, -1, h);
            if (w > 0) le.preferredWidth = w;
            return g;
        }

        static TMP_InputField InputField(string n, Transform p, string ph, float w, float h)
        {
            var g = Go(n, p);
            g.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
            Img(g, kInput);

            var ta = Go("Area", g.transform);
            Anchor(ta, 0, 0, 1, 1);
            ta.GetComponent<RectTransform>().offsetMin = new Vector2(8, 2);
            ta.GetComponent<RectTransform>().offsetMax = new Vector2(-8, -2);
            ta.AddComponent<RectMask2D>();

            var phg = Go("PH", ta.transform);
            Anchor(phg, 0, 0, 1, 1);
            var pht = phg.AddComponent<TextMeshProUGUI>();
            pht.text = ph; pht.fontSize = 13; pht.fontStyle = FontStyles.Italic;
            pht.color = new Color(.45f,.45f,.5f,.8f);

            var txg = Go("Txt", ta.transform);
            Anchor(txg, 0, 0, 1, 1);
            var txt = txg.AddComponent<TextMeshProUGUI>();
            txt.fontSize = 13; txt.color = kWhite;

            var inp = g.AddComponent<TMP_InputField>();
            inp.textViewport = ta.GetComponent<RectTransform>();
            inp.textComponent = txt;
            inp.placeholder = pht;

            var le = LE(g, -1, h);
            if (w > 0) le.preferredWidth = w;
            return inp;
        }

        static TMP_Dropdown Dropdown(string n, Transform p, float w, float h, string[] opts)
        {
            var g = Go(n, p);
            g.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
            var img = Img(g, kInput);

            var cap = Go("Cap", g.transform); Anchor(cap, 0, 0, 1, 1);
            cap.GetComponent<RectTransform>().offsetMin = new Vector2(8, 0);
            cap.GetComponent<RectTransform>().offsetMax = new Vector2(-24, 0);
            var capT = cap.AddComponent<TextMeshProUGUI>();
            capT.fontSize = 13; capT.color = kWhite;

            var arr = Go("Arr", g.transform);
            var art = arr.GetComponent<RectTransform>();
            art.anchorMin = new Vector2(1, 0); art.anchorMax = new Vector2(1, 1);
            art.sizeDelta = new Vector2(20, 0); art.anchoredPosition = new Vector2(-10, 0);
            var arT = arr.AddComponent<TextMeshProUGUI>();
            arT.text = "\u25BC"; arT.fontSize = 10; arT.color = kMuted; arT.alignment = TextAlignmentOptions.Center;

            var tmpl = Go("Tmpl", g.transform);
            var tr = tmpl.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0, 0); tr.anchorMax = new Vector2(1, 0);
            tr.pivot = new Vector2(.5f, 1); tr.sizeDelta = new Vector2(0, 100);
            Img(tmpl, new Color(.12f,.12f,.16f));
            var tsr = tmpl.AddComponent<ScrollRect>(); tsr.horizontal = false;

            var tvp = Go("VP", tmpl.transform); Anchor(tvp, 0, 0, 1, 1);
            Img(tvp, Color.clear); tvp.AddComponent<Mask>().showMaskGraphic = false;
            tsr.viewport = tvp.GetComponent<RectTransform>();

            var tc = Go("C", tvp.transform);
            var tcr = tc.GetComponent<RectTransform>();
            tcr.anchorMin = new Vector2(0, 1); tcr.anchorMax = new Vector2(1, 1);
            tcr.pivot = new Vector2(.5f, 1); tcr.sizeDelta = new Vector2(0, 28);
            tsr.content = tcr;

            var itm = Go("Item", tc.transform);
            var ir = itm.GetComponent<RectTransform>();
            ir.anchorMin = new Vector2(0, .5f); ir.anchorMax = new Vector2(1, .5f);
            ir.sizeDelta = new Vector2(0, 28);
            Img(itm, Color.clear); itm.AddComponent<Toggle>();

            var il = Go("IL", itm.transform); Anchor(il, 0, 0, 1, 1);
            il.GetComponent<RectTransform>().offsetMin = new Vector2(8, 0);
            il.GetComponent<RectTransform>().offsetMax = new Vector2(-8, 0);
            var ilt = il.AddComponent<TextMeshProUGUI>();
            ilt.fontSize = 13; ilt.color = kWhite;

            tmpl.SetActive(false);

            var dd = g.AddComponent<TMP_Dropdown>();
            dd.template = tr; dd.captionText = capT; dd.itemText = ilt; dd.targetGraphic = img;
            dd.ClearOptions();
            var ol = new System.Collections.Generic.List<TMP_Dropdown.OptionData>();
            foreach (var o in opts) ol.Add(new TMP_Dropdown.OptionData(o));
            dd.AddOptions(ol);

            var le = LE(g, -1, h);
            if (w > 0) le.preferredWidth = w;
            return dd;
        }

        static GameObject Modal(string n, Transform p, float w, float h, Color accent)
        {
            var g = Go(n, p);
            var rt = g.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            rt.sizeDelta = new Vector2(w, h);
            Img(g, kModal);
            var ol = g.AddComponent<Outline>();
            ol.effectColor = accent; ol.effectDistance = new Vector2(1.5f, 1.5f);
            // Remove LayoutElement so parent VLG ignores it
            var le = g.GetComponent<LayoutElement>();
            if (le) Object.DestroyImmediate(le);
            // Add LayoutElement with ignoreLayout = true
            g.AddComponent<LayoutElement>().ignoreLayout = true;
            return g;
        }

        static GameObject BtnRow(Transform p)
        {
            var g = Go("BtnRow", p);
            LE(g, -1, 38);
            var hlg = g.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true;
            return g;
        }

        static void S(SerializedObject so, string f, Object v)
        {
            var p = so.FindProperty(f);
            if (p != null) p.objectReferenceValue = v;
            else Debug.LogWarning($"[LobbyUI] Field '{f}' not found");
        }
    }
}
