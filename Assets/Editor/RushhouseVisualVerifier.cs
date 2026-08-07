using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class RushhouseVisualVerifier
{
    // Music is the one system a screenshot cannot check. This asserts both halves: the four clips
    // actually load and carry samples, and the state machine asks for the right one per screen.
    public static void VerifyMusic()
    {
        RushhouseSceneBuilder.BuildMainScene();
        var game = UnityEngine.Object.FindFirstObjectByType<RushhouseUnityGame>();
        if (!game) throw new Exception("RushhouseUnityGame not found");
        Type type = typeof(RushhouseUnityGame);
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        type.GetMethod("Awake", flags)?.Invoke(game, null);

        bool clipsOk = true;
        foreach (string track in new[] { "menu", "service", "rush", "result" }) {
            var clip = Resources.Load<AudioClip>("Music/" + track);
            bool ok = clip && clip.samples > 44100 && clip.channels > 0;
            Debug.Log("MUSIC_CLIP " + track + " loaded=" + (clip != null)
                + " seconds=" + (clip ? (clip.samples / (float)clip.frequency).ToString("F1") : "-")
                + " channels=" + (clip ? clip.channels : 0));
            clipsOk &= ok;
        }

        // drive the state machine through each screen and read back what it wants
        var screenField = type.GetField("screen", flags);
        var rushField = type.GetField("rushActive", flags);
        var desired = type.GetMethod("DesiredTrack", flags);
        Type modeType = screenField.FieldType;
        string Ask(string mode, bool rush)
        {
            screenField.SetValue(game, Enum.Parse(modeType, mode));
            rushField.SetValue(game, rush);
            return (string)desired.Invoke(game, null);
        }
        string menu = Ask("Menu", false), play = Ask("Play", false);
        string rushT = Ask("Play", true), result = Ask("Result", false);
        bool routeOk = menu == "menu" && play == "service" && rushT == "rush" && result == "result";

        // and that a switch actually loads a different clip into the live source
        var setMusic = type.GetMethod("SetMusic", flags);
        var updMusic = type.GetMethod("UpdateMusic", flags);
        setMusic.Invoke(game, new object[] { "service" });
        updMusic.Invoke(game, null);
        var srcA = type.GetField("musicA", flags).GetValue(game) as AudioSource;
        string playing = srcA && srcA.clip ? srcA.clip.name : "(none)";
        bool sourceOk = playing == "service";

        bool ok2 = clipsOk && routeOk && sourceOk;
        Debug.Log("MUSIC_STATE menu=" + menu + " play=" + play + " rush=" + rushT + " result=" + result
            + " sourceClip=" + playing + " result=" + (ok2 ? "PASS" : "FAIL"));
        EditorApplication.Exit(ok2 ? 0 : 1);
    }

    public static void VerifyCharacterLifecycle()
    {
        RushhouseSceneBuilder.BuildMainScene();

        var game = UnityEngine.Object.FindFirstObjectByType<RushhouseUnityGame>();
        if (!game) throw new Exception("RushhouseUnityGame not found");

        Type type = typeof(RushhouseUnityGame);
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        type.GetMethod("Awake", flags)?.Invoke(game, null);

        object save = type.GetField("save", flags)?.GetValue(game);
        if (save != null) {
            Type saveType = save.GetType();
            saveType.GetField("waiter")?.SetValue(save, 1);
            saveType.GetField("day")?.SetValue(save, 3);
        }

        type.GetMethod("StartDay", flags)?.Invoke(game, null);
        MethodInfo updateMotion = type.GetMethod("UpdateCustomerMotion", flags);
        MethodInfo updateWorkers = type.GetMethod("UpdateWorkers", flags);
        var customers = type.GetField("customers", flags)?.GetValue(game) as IList;
        if (customers == null || customers.Count == 0) throw new Exception("Opening customer missing");

        object customer = customers[0];
        Type customerType = customer.GetType();
        for (int i = 0; i < 120; i++) updateMotion?.Invoke(game, new object[] { .05f });
        bool seated = (bool)customerType.GetField("seated")?.GetValue(customer);

        for (int i = 0; i < 240; i++) updateWorkers?.Invoke(game, new object[] { .05f });
        bool ordered = (bool)customerType.GetField("ordered")?.GetValue(customer);

        type.GetMethod("CompleteCustomer", flags)?.Invoke(game, new[] { customer });
        for (int i = 0; i < 20; i++) updateMotion?.Invoke(game, new object[] { .05f });
        bool eatingAtTable = (bool)customerType.GetField("served")?.GetValue(customer)
            && (bool)customerType.GetField("seated")?.GetValue(customer)
            && !(bool)customerType.GetField("leaving")?.GetValue(customer);

        for (int i = 0; i < 100; i++) updateMotion?.Invoke(game, new object[] { .05f });
        bool exited = !customers.Contains(customer);
        object table = customerType.GetField("table")?.GetValue(customer);
        bool tableDirty = table != null && (bool)table.GetType().GetField("dirty")?.GetValue(table);

        bool ok = seated && ordered && eatingAtTable && exited && tableDirty;
        Debug.Log("CHARACTER_LIFECYCLE seated=" + seated
            + " ordered=" + ordered
            + " eatingAtTable=" + eatingAtTable
            + " exited=" + exited
            + " tableDirty=" + tableDirty
            + " result=" + (ok ? "PASS" : "FAIL"));
        EditorApplication.Exit(ok ? 0 : 1);
    }

    public static void CapturePlayWorld()
    {
        RushhouseSceneBuilder.BuildMainScene();

        var game = UnityEngine.Object.FindFirstObjectByType<RushhouseUnityGame>();
        if (!game) throw new Exception("RushhouseUnityGame not found");

        Type type = typeof(RushhouseUnityGame);
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        type.GetMethod("Awake", flags)?.Invoke(game, null);

        // Hire staff so cook/waiter/prepper/washer workers appear in the capture.
        object save = type.GetField("save", flags)?.GetValue(game);
        if (save != null) {
            Type st = save.GetType();
            foreach (string nm in new[] { "waiter", "cook", "prepper", "washer" })
                st.GetField(nm)?.SetValue(save, 1);
            st.GetField("day")?.SetValue(save, 3);   // day 3 = RUSH DAY event, shows event banner + varied goals
        }

        type.GetMethod("StartDay", flags)?.Invoke(game, null);
        type.GetField("playerWalking", flags)?.SetValue(game, true);

        Type itemType = type.GetNestedType("Item", BindingFlags.NonPublic);
        object bun = itemType?.GetMethod("Ingredient", BindingFlags.Public | BindingFlags.Static)
            ?.Invoke(null, new object[] { "bun", "ready" });
        if (bun != null) type.GetField("holding", flags)?.SetValue(game, bun);
        type.GetField("playerFacing", flags)?.SetValue(game, Vector2.right);

        MethodInfo updateCustomerMotion = type.GetMethod("UpdateCustomerMotion", flags);
        for (int i = 0; i < 24; i++) updateCustomerMotion?.Invoke(game, new object[] { 0.06f });

        type.GetMethod("BuildPlayUI", flags)?.Invoke(game, null);
        type.GetMethod("RebuildWorld", flags)?.Invoke(game, null);

        var customers = type.GetField("customers", flags)?.GetValue(game) as ICollection;
        Debug.Log("VISUAL_VERIFY customers=" + (customers?.Count ?? -1));

        // Render the overlay HUD into the camera capture (event banner, goals, tickets).
        var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        if (canvas) {
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.sortingOrder = 500;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 1f;
        }
        Canvas.ForceUpdateCanvases();

        CaptureCamera(Path.Combine(WorkspaceWorkDir(), "unity-editor-world-after-fix.png"), 540, 960);
        EditorApplication.Exit(0);
    }

    // set two stations ablaze + hand the player the extinguisher, to eyeball the fire hazard art
    // Open the order-detail card on the LONGEST recipe available, to prove the card grows with its
    // ingredient list instead of spilling outside the frame.
    // Wardrobe + one rewarded-ad offer. Both are pure UI, so a capture is the honest check that
    // the outfit previews actually resolve to rendered sprites rather than empty frames.
    public static void CaptureWardrobe()
    {
        RushhouseSceneBuilder.BuildMainScene();
        var game = UnityEngine.Object.FindFirstObjectByType<RushhouseUnityGame>();
        Type type = typeof(RushhouseUnityGame);
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        type.GetMethod("Awake", flags)?.Invoke(game, null);
        object save = type.GetField("save", flags)?.GetValue(game);
        Type st = save.GetType();
        st.GetField("tokens")?.SetValue(save, 11);
        st.GetField("outfits")?.SetValue(save, "skinCrimson,skinKnight");
        st.GetField("outfit")?.SetValue(save, "skinCrimson");
        type.GetMethod("ShowWardrobe", flags)?.Invoke(game, null);

        // report what the catalogue actually resolved, so a missing render is loud not silent
        var sprites = type.GetField("animatedCharacterSprites", flags)?.GetValue(game) as System.Collections.IDictionary;
        int have = 0;
        foreach (string id in new[] { "player", "skinCrimson", "skinMidnight", "skinMint", "skinGold", "skinNeon",
                                     "skinTuxedo", "skinKnight", "skinForeman", "skinFire" })
            if (sprites != null && sprites.Contains(id + "_front_idle_0")) have++;
        Debug.Log("WARDROBE_SPRITES resolved=" + have + "/10");

        var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        if (canvas) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.sortingOrder = 500; canvas.worldCamera = Camera.main; canvas.planeDistance = 1f; }
        Canvas.ForceUpdateCanvases();
        CaptureCamera(Path.Combine(WorkspaceWorkDir(), "unity-wardrobe.png"), 540, 960);
        EditorApplication.Exit(have == 10 ? 0 : 1);
    }

    public static void CaptureAdOffer()
    {
        RushhouseSceneBuilder.BuildMainScene();
        var game = UnityEngine.Object.FindFirstObjectByType<RushhouseUnityGame>();
        Type type = typeof(RushhouseUnityGame);
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        type.GetMethod("Awake", flags)?.Invoke(game, null);
        type.GetMethod("StartDay", flags)?.Invoke(game, null);
        type.GetField("complaints", flags)?.SetValue(game, 5);
        type.GetMethod("RebuildWorld", flags)?.Invoke(game, null);
        type.GetMethod("OfferSecondChance", flags)?.Invoke(game, null);
        var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        if (canvas) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.sortingOrder = 500; canvas.worldCamera = Camera.main; canvas.planeDistance = 1f; }
        Canvas.ForceUpdateCanvases();
        CaptureCamera(Path.Combine(WorkspaceWorkDir(), "unity-adoffer.png"), 540, 960);
        EditorApplication.Exit(0);
    }

    // Play N days headless and report what the economy actually pays. Outfit prices were set by
    // feel; this replaces the guess with numbers, and catches the case where tokens accumulate so
    // fast the wardrobe is trivial (or so slowly it is ad-only, which is the thing to avoid).
    public static void SimulateEconomy()
    {
        RushhouseSceneBuilder.BuildMainScene();
        var game = UnityEngine.Object.FindFirstObjectByType<RushhouseUnityGame>();
        Type type = typeof(RushhouseUnityGame);
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        type.GetMethod("Awake", flags)?.Invoke(game, null);
        // Start from a clean career every run, or the sim silently continues the PREVIOUS run's
        // save and measures day 11 of a restaurant it never built — which is how a rerun once
        // reported 0 wins for a balance change that was actually fine.
        type.GetMethod("ResetSave", flags)?.Invoke(game, null);
        object save = type.GetField("save", flags)?.GetValue(game);
        Type st = save.GetType();
        // Fully staffed on purpose: the player is the chef, and with no simulated player input a
        // half-staffed kitchen simply stalls (the prepper is what assembles a plate). This measures
        // the late-game, fully-hired restaurant, which is exactly when token income matters.
        foreach (string role in new[] { "waiter", "cook", "prepper", "washer" })
            st.GetField(role)?.SetValue(save, 1);
        st.GetField("coins")?.SetValue(save, 4000);   // afford the staff we just granted

        var startDay = type.GetMethod("StartDay", flags);
        var updatePlay = type.GetMethod("UpdatePlay", flags, null, new[] { typeof(float) }, null);
        var screenField = type.GetField("screen", flags);
        int days = 10;
        int totalTokens = 0, totalCoins = 0, wins = 0;
        var taskCounts = new System.Collections.Generic.Dictionary<string, int>();
        Debug.Log("ECON_HEADER day served/goal coins tips stars tokens totalTokens");
        for (int d = 0; d < days; d++) {
            int coinsBefore = (int)st.GetField("coins").GetValue(save);
            int tokensBefore = (int)st.GetField("tokens").GetValue(save);
            startDay.Invoke(game, null);
            if (d == 0 || d == 5) {
                var apps = type.GetField("appliances", flags)?.GetValue(game) as System.Collections.IList;
                var census = new System.Collections.Generic.Dictionary<string, int>();
                if (apps != null)
                    foreach (var a in apps) {
                        string t = (string)a.GetType().GetField("type").GetValue(a);
                        census.TryGetValue(t, out int n3); census[t] = n3 + 1;
                    }
                var ck = new System.Collections.Generic.List<string>(census.Keys); ck.Sort();
                string line = "";
                foreach (var k in ck) line += k + "=" + census[k] + " ";
                Debug.Log("ECON_LAYOUT day" + (d + 1) + " " + line);
            }
            // step a whole shift; UpdatePlay ends the day itself when the goal or clock lands
            var custField = type.GetField("customers", flags);
            var workField = type.GetField("workers", flags);
            for (int i = 0; i < 12000; i++) {
                if (!screenField.GetValue(game).ToString().Equals("Play")) break;
                updatePlay.Invoke(game, new object[] { .05f });
                // sample every worker's task once a second: the histogram tells us whether a
                // shortfall is "too much work" or "staff standing around", which need opposite fixes
                if (i % 20 == 0) {
                    var ws2 = workField?.GetValue(game) as System.Collections.IList;
                    if (ws2 != null)
                        foreach (var w in ws2) {
                            var wt = w.GetType();
                            string role = (string)wt.GetField("role").GetValue(w);
                            string task = (string)wt.GetField("task").GetValue(w);
                            if (string.IsNullOrEmpty(task)) task = "IDLE";
                            string key = role + "/" + task;
                            taskCounts.TryGetValue(key, out int n2);
                            taskCounts[key] = n2 + 1;
                        }
                }
            }
            int served = (int)type.GetField("served", flags).GetValue(game);
            int goal = (int)type.GetField("goal", flags).GetValue(game);
            int earned = (int)type.GetField("earned", flags).GetValue(game);
            int tips = (int)type.GetField("tips", flags).GetValue(game);
            int stars = (int)type.GetField("starRating", flags).GetValue(game);
            int tokensAfter = (int)st.GetField("tokens").GetValue(save);
            int coinsAfter = (int)st.GetField("coins").GetValue(save);
            int dTok = tokensAfter - tokensBefore;
            totalTokens += dTok;
            totalCoins += coinsAfter - coinsBefore;
            if (served >= goal) wins++;
            int missed = (int)(type.GetField("missed", flags)?.GetValue(game) ?? 0);
            int qc = (int)(type.GetField("queueComplaints", flags)?.GetValue(game) ?? 0);
            var stillIn = type.GetField("customers", flags)?.GetValue(game) as System.Collections.ICollection;
            // arrivals = served + walked out + still inside when the clock ran down. If this is
            // barely above the goal, the shortfall is ARRIVAL RATE, not kitchen throughput.
            Debug.Log("ECON_FLOW day" + (d + 1) + " arrivals~" + (served + missed + (stillIn?.Count ?? 0))
                + " served=" + served + " walkouts=" + missed + " queueComplaints=" + qc
                + " stillInside=" + (stillIn?.Count ?? 0) + " goal=" + goal);
            Debug.Log("ECON_DAY " + (d + 1) + " " + served + "/" + goal + " coins=" + earned
                + " tips=" + tips + " stars=" + stars + " tokens+" + dTok + " total=" + tokensAfter);
        }
        var keys = new System.Collections.Generic.List<string>(taskCounts.Keys);
        keys.Sort();
        int grand = 0;
        foreach (var k in keys) grand += taskCounts[k];
        foreach (var k in keys)
            Debug.Log("ECON_TASK " + k + " samples=" + taskCounts[k]
                + " pct=" + (100f * taskCounts[k] / Mathf.Max(1, grand)).ToString("F1"));
        Debug.Log("ECON_SUMMARY days=" + days + " wins=" + wins + " tokensEarned=" + totalTokens
            + " coinsNet=" + totalCoins + " tokensPerDay=" + (totalTokens / (float)days).ToString("F2"));
        EditorApplication.Exit(0);
    }

    // Dress the whole crew and photograph the kitchen. This is the only check that proves the
    // per-role outfit actually reaches the worker draw call rather than just the save file.
    public static void CaptureStaffOutfits()
    {
        RushhouseSceneBuilder.BuildMainScene();
        var game = UnityEngine.Object.FindFirstObjectByType<RushhouseUnityGame>();
        Type type = typeof(RushhouseUnityGame);
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        type.GetMethod("Awake", flags)?.Invoke(game, null);
        object save = type.GetField("save", flags)?.GetValue(game);
        Type st = save.GetType();
        foreach (string role in new[] { "waiter", "cook", "prepper", "washer" })
            st.GetField(role)?.SetValue(save, 1);
        st.GetField("day")?.SetValue(save, 4);
        st.GetField("outfits")?.SetValue(save, "skinKnight,skinTuxedo,skinFire,skinNeon");
        st.GetField("outfit")?.SetValue(save, "skinKnight");
        st.GetField("outfitWaiter")?.SetValue(save, "skinTuxedo");
        st.GetField("outfitCook")?.SetValue(save, "skinFire");
        st.GetField("outfitPrepper")?.SetValue(save, "skinNeon");

        type.GetMethod("StartDay", flags)?.Invoke(game, null);
        var resolve = type.GetMethod("OutfitSpriteName", flags);
        foreach (string role in new[] { "player", "waiter", "cook", "prepper" })
            Debug.Log("STAFF_OUTFIT " + role + " -> " + resolve.Invoke(game, new object[] { role }));
        type.GetMethod("RebuildWorld", flags)?.Invoke(game, null);
        var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        if (canvas) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.sortingOrder = 500; canvas.worldCamera = Camera.main; canvas.planeDistance = 1f; }
        Canvas.ForceUpdateCanvases();
        CaptureCamera(Path.Combine(WorkspaceWorkDir(), "unity-staff.png"), 540, 960);
        EditorApplication.Exit(0);
    }

    // Ten simulated shifts recorded walkouts=0 every single day. Either guests are infinitely
    // patient -- which would remove all time pressure from the game -- or the walkout path works
    // and the sim simply never starved anyone long enough. This decides it: seat a guest, serve
    // them nothing, and assert they leave and it costs a complaint.
    public static void VerifyPatienceWalkout()
    {
        RushhouseSceneBuilder.BuildMainScene();
        var game = UnityEngine.Object.FindFirstObjectByType<RushhouseUnityGame>();
        Type type = typeof(RushhouseUnityGame);
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        type.GetMethod("Awake", flags)?.Invoke(game, null);
        object save = type.GetField("save", flags)?.GetValue(game);
        save.GetType().GetField("waiter")?.SetValue(save, 1);
        type.GetMethod("StartDay", flags)?.Invoke(game, null);

        var customers = type.GetField("customers", flags)?.GetValue(game) as IList;
        if (customers == null || customers.Count == 0) throw new Exception("no opening customer");
        object c = customers[0];
        Type ct = c.GetType();
        // Drive the REAL shift loop. Patience is decremented in UpdatePlay, not in
        // UpdateCustomerMotion -- a first version of this test stepped only the latter, saw
        // patience frozen, and nearly reported a bug that was not there.
        var updatePlay = type.GetMethod("UpdatePlay", flags, null, new[] { typeof(float) }, null);
        for (int i = 0; i < 200; i++) updatePlay.Invoke(game, new object[] { .05f });
        float startPatience = (float)ct.GetField("patience").GetValue(c);
        bool seated = (bool)ct.GetField("seated").GetValue(c);

        // remove every worker so nobody can rescue the order, then let the clock run
        var wl = type.GetField("workers", flags)?.GetValue(game) as IList;
        wl?.Clear();
        type.GetField("shiftTime", flags)?.SetValue(game, 100000f);   // outlast the shift clock
        int leftAt = -1;
        float minPatience = startPatience;
        for (int i = 0; i < 20000; i++) {
            updatePlay.Invoke(game, new object[] { .05f });
            if (customers.Contains(c)) minPatience = Mathf.Min(minPatience, (float)ct.GetField("patience").GetValue(c));
            else { leftAt = i; break; }
        }
        Debug.Log("PATIENCE_MIN " + minPatience.ToString("F1"));
        float endPatience = leftAt >= 0 ? 0 : (float)ct.GetField("patience").GetValue(c);
        int complaints = (int)type.GetField("complaints", flags).GetValue(game);
        bool ok = leftAt >= 0 && complaints > 0;
        Debug.Log("PATIENCE_WALKOUT seated=" + seated + " startPatience=" + startPatience.ToString("F0")
            + " leftAfter=" + (leftAt >= 0 ? (leftAt * .05f).ToString("F0") + "s" : "NEVER")
            + " endPatience=" + endPatience.ToString("F0") + " complaints=" + complaints
            + " result=" + (ok ? "PASS" : "FAIL"));
        EditorApplication.Exit(ok ? 0 : 1);
    }

    public static void CaptureSettings()
    {
        RushhouseSceneBuilder.BuildMainScene();
        var game = UnityEngine.Object.FindFirstObjectByType<RushhouseUnityGame>();
        Type type = typeof(RushhouseUnityGame);
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        type.GetMethod("Awake", flags)?.Invoke(game, null);
        object save = type.GetField("save", flags)?.GetValue(game);
        save.GetType().GetField("musicOn")?.SetValue(save, false);   // one row OFF proves both states draw
        type.GetMethod("ShowMenu", flags)?.Invoke(game, null);
        type.GetMethod("DrawSettingsOverlay", flags)?.Invoke(game, null);
        var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        if (canvas) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.sortingOrder = 500; canvas.worldCamera = Camera.main; canvas.planeDistance = 1f; }
        Canvas.ForceUpdateCanvases();
        CaptureCamera(Path.Combine(WorkspaceWorkDir(), "unity-settings.png"), 540, 960);
        EditorApplication.Exit(0);
    }

    public static void CapturePause()
    {
        RushhouseSceneBuilder.BuildMainScene();
        var game = UnityEngine.Object.FindFirstObjectByType<RushhouseUnityGame>();
        if (!game) throw new Exception("RushhouseUnityGame not found");
        Type type = typeof(RushhouseUnityGame);
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        type.GetMethod("Awake", flags)?.Invoke(game, null);
        object save = type.GetField("save", flags)?.GetValue(game);
        save.GetType().GetField("day")?.SetValue(save, 6);
        type.GetMethod("StartDay", flags)?.Invoke(game, null);
        type.GetField("served", flags)?.SetValue(game, 3);
        type.GetField("earned", flags)?.SetValue(game, 214);
        type.GetMethod("RebuildWorld", flags)?.Invoke(game, null);
        type.GetField("paused", flags)?.SetValue(game, true);
        type.GetMethod("DrawPauseOverlay", flags)?.Invoke(game, null);
        var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        if (canvas) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.sortingOrder = 500; canvas.worldCamera = Camera.main; canvas.planeDistance = 1f; }
        Canvas.ForceUpdateCanvases();
        CaptureCamera(Path.Combine(WorkspaceWorkDir(), "unity-pause.png"), 540, 960);
        EditorApplication.Exit(0);
    }

    public static void CaptureOrderDetail()
    {
        RushhouseSceneBuilder.BuildMainScene();
        var game = UnityEngine.Object.FindFirstObjectByType<RushhouseUnityGame>();
        if (!game) throw new Exception("RushhouseUnityGame not found");
        Type type = typeof(RushhouseUnityGame);
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        type.GetMethod("Awake", flags)?.Invoke(game, null);
        object save = type.GetField("save", flags)?.GetValue(game);
        save.GetType().GetField("day")?.SetValue(save, 9);
        type.GetMethod("StartDay", flags)?.Invoke(game, null);

        var recipes = type.GetField("recipes", flags)?.GetValue(game) as System.Collections.IList;
        object longest = null; int best = -1;
        foreach (var rec in recipes) {
            var t = rec.GetType();
            if ((string)t.GetField("theme").GetValue(rec) != "burger") continue;
            var parts = (string[])t.GetField("parts").GetValue(rec);
            if (parts.Length > best) { best = parts.Length; longest = rec; }
        }
        type.GetField("detailRecipe", flags)?.SetValue(game, longest);
        Debug.Log("ORDER_DETAIL parts=" + best);
        type.GetMethod("BuildPlayUI", flags)?.Invoke(game, null);
        type.GetMethod("RebuildWorld", flags)?.Invoke(game, null);
        var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        if (canvas) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.sortingOrder = 500; canvas.worldCamera = Camera.main; canvas.planeDistance = 1f; }
        Canvas.ForceUpdateCanvases();
        CaptureCamera(Path.Combine(WorkspaceWorkDir(), "unity-orderdetail.png"), 540, 960);
        EditorApplication.Exit(0);
    }

    // Walk a worker in each of the four directions and report the sprite direction actually chosen.
    // Guards the "waiter walks forward but shows their back" class of bug.
    public static void VerifyWorkerFacing()
    {
        RushhouseSceneBuilder.BuildMainScene();
        var game = UnityEngine.Object.FindFirstObjectByType<RushhouseUnityGame>();
        if (!game) throw new Exception("RushhouseUnityGame not found");
        Type type = typeof(RushhouseUnityGame);
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object save = type.GetField("save", flags)?.GetValue(game);
        type.GetMethod("Awake", flags)?.Invoke(game, null);
        save = type.GetField("save", flags)?.GetValue(game);
        save.GetType().GetField("waiter")?.SetValue(save, 1);
        type.GetMethod("StartDay", flags)?.Invoke(game, null);

        var workers = type.GetField("workers", flags)?.GetValue(game) as System.Collections.IList;
        if (workers == null || workers.Count == 0) throw new Exception("no workers spawned");
        object w = workers[0];
        Type wt = w.GetType();
        var updateWorkers = type.GetMethod("UpdateWorkers", flags);
        var facingKey = type.GetMethod("FacingKey", flags);

        // logical +y is UP the screen (away from the camera) -> we should see the BACK there
        var cases = new (Vector2 dir, string expect)[] {
            (Vector2.up, "back"), (Vector2.down, "front"), (Vector2.left, "left"), (Vector2.right, "right")
        };
        bool ok = true;
        foreach (var (dir, expect) in cases) {
            Vector2 start = (Vector2)type.GetMethod("SafeOpenPosition", flags).Invoke(game, new object[] { new Vector2(0f, 0f), .15f });
            wt.GetField("pos").SetValue(w, start);
            wt.GetField("target").SetValue(w, start + dir * 2.2f);
            wt.GetField("pathWaypoint").SetValue(w, Vector2.zero);
            wt.GetField("pathTimer").SetValue(w, 0f);
            wt.GetField("pendingArrival").SetValue(w, true);   // keep them committed to the walk
            // small dt on purpose: at ~125fps one frame of travel is tiny, which is exactly where the
            // old "facing from achieved delta" test silently stopped updating and left a stale direction.
            for (int i = 0; i < 30; i++) updateWorkers.Invoke(game, new object[] { .008f });
            var facing = (Vector2)wt.GetField("facing").GetValue(w);
            string key = (string)facingKey.Invoke(game, new object[] { facing });
            bool pass = key == expect;
            ok &= pass;
            Debug.Log($"WORKER_FACING move={dir} facing={facing} sprite={key} expect={expect} {(pass ? "PASS" : "FAIL")}");
        }
        Debug.Log("WORKER_FACING_RESULT " + (ok ? "PASS" : "FAIL"));
        EditorApplication.Exit(ok ? 0 : 1);
    }

    // Stand the player right at the kitchen pass so the swing door is fully OPEN — used to verify
    // which way the leaves swing (they must open OUT into the dining room, not into the kitchen).
    public static void CaptureDoor()
    {
        RushhouseSceneBuilder.BuildMainScene();
        var game = UnityEngine.Object.FindFirstObjectByType<RushhouseUnityGame>();
        if (!game) throw new Exception("RushhouseUnityGame not found");
        Type type = typeof(RushhouseUnityGame);
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        type.GetMethod("Awake", flags)?.Invoke(game, null);
        type.GetMethod("StartDay", flags)?.Invoke(game, null);

        float divY = (float)type.GetMethod("DividerY", flags).Invoke(game, null);
        type.GetField("playerPos", flags)?.SetValue(game, new Vector2(0f, divY - .35f));
        type.GetField("playerFacing", flags)?.SetValue(game, Vector2.up);
        type.GetMethod("BuildPlayUI", flags)?.Invoke(game, null);
        type.GetMethod("RebuildWorld", flags)?.Invoke(game, null);
        Canvas.ForceUpdateCanvases();
        CaptureCamera(Path.Combine(WorkspaceWorkDir(), "unity-door.png"), 540, 960);
        EditorApplication.Exit(0);
    }

    public static void CaptureFire()
    {
        RushhouseSceneBuilder.BuildMainScene();
        var game = UnityEngine.Object.FindFirstObjectByType<RushhouseUnityGame>();
        if (!game) throw new Exception("RushhouseUnityGame not found");
        Type type = typeof(RushhouseUnityGame);
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        type.GetMethod("Awake", flags)?.Invoke(game, null);
        type.GetMethod("StartDay", flags)?.Invoke(game, null);

        var appliances = type.GetField("appliances", flags)?.GetValue(game) as System.Collections.IList;
        int lit = 0;
        if (appliances != null) {
            foreach (var ap in appliances) {
                Type at = ap.GetType();
                string atype = at.GetField("type")?.GetValue(ap) as string;
                if (atype == "hob" || atype == "counter") {
                    at.GetField("fire")?.SetValue(ap, lit == 0 ? 9f : 4f);
                    if (++lit >= 2) break;
                }
            }
        }
        // give the player the extinguisher and stand them by the fire
        Type itemType = type.GetNestedType("Item", BindingFlags.NonPublic);
        object tool = itemType?.GetMethod("Tool", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, new object[] { "extinguisher" });
        if (tool != null) type.GetField("holding", flags)?.SetValue(game, tool);
        type.GetField("playerFacing", flags)?.SetValue(game, Vector2.up);

        type.GetMethod("BuildPlayUI", flags)?.Invoke(game, null);
        type.GetMethod("RebuildWorld", flags)?.Invoke(game, null);
        var fcanvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        if (fcanvas) { fcanvas.renderMode = RenderMode.ScreenSpaceCamera; fcanvas.sortingOrder = 500; fcanvas.worldCamera = Camera.main; fcanvas.planeDistance = 1f; }
        Canvas.ForceUpdateCanvases();
        CaptureCamera(Path.Combine(WorkspaceWorkDir(), "unity-fire.png"), 540, 960);
        EditorApplication.Exit(0);
    }

    public static void CaptureResultScreen()
    {
        RushhouseSceneBuilder.BuildMainScene();
        var game = UnityEngine.Object.FindFirstObjectByType<RushhouseUnityGame>();
        if (!game) throw new Exception("RushhouseUnityGame not found");
        Type type = typeof(RushhouseUnityGame);
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        type.GetMethod("Awake", flags)?.Invoke(game, null);

        // Seed a perk choice so we can capture the new perk-select screen.
        var pending = type.GetField("pendingPerks", flags)?.GetValue(game) as System.Collections.IList;
        if (pending != null) { pending.Clear(); pending.Add("sharp"); pending.Add("big_tips"); pending.Add("overtime"); }
        type.GetField("starRating", flags)?.SetValue(game, 3);
        type.GetField("served", flags)?.SetValue(game, 5);
        type.GetMethod("DrawResultScreen", flags)?.Invoke(game, new object[] { true });

        // Overlay canvases aren't captured by camera.Render(); switch to ScreenSpaceCamera so UI renders.
        var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        if (canvas) {
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.sortingOrder = 500;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 1f;
        }
        Canvas.ForceUpdateCanvases();

        CaptureCamera(Path.Combine(WorkspaceWorkDir(), "unity-result-perks.png"), 540, 960);
        EditorApplication.Exit(0);
    }

    public static void CaptureMenu()
    {
        RushhouseSceneBuilder.BuildMainScene();
        var game = UnityEngine.Object.FindFirstObjectByType<RushhouseUnityGame>();
        if (!game) throw new Exception("RushhouseUnityGame not found");
        Type type = typeof(RushhouseUnityGame);
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        type.GetMethod("Awake", flags)?.Invoke(game, null);

        object save = type.GetField("save", flags)?.GetValue(game);
        if (save != null) {
            Type st = save.GetType();
            st.GetField("perks")?.SetValue(save, "sharp,big_tips,overtime,regulars,wide_doors");
            st.GetField("day")?.SetValue(save, 4);
            st.GetField("coins")?.SetValue(save, 340);
            st.GetField("reputation")?.SetValue(save, 12);
            st.GetField("totalStars")?.SetValue(save, 9);
        }
        type.GetMethod("ShowMenu", flags)?.Invoke(game, null);

        var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        if (canvas) {
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.sortingOrder = 500;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 1f;
        }
        Canvas.ForceUpdateCanvases();

        CaptureCamera(Path.Combine(WorkspaceWorkDir(), "unity-menu.png"), 540, 960);
        EditorApplication.Exit(0);
    }

    public static void CaptureRecipes()
    {
        RushhouseSceneBuilder.BuildMainScene();
        var game = UnityEngine.Object.FindFirstObjectByType<RushhouseUnityGame>();
        Type type = typeof(RushhouseUnityGame);
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        type.GetMethod("Awake", flags)?.Invoke(game, null);
        object save = type.GetField("save", flags)?.GetValue(game);
        if (save != null) {
            Type st = save.GetType();
            st.GetField("day")?.SetValue(save, 9);
            st.GetField("theme")?.SetValue(save, "burger");
        }
        type.GetMethod("ShowRecipes", flags)?.Invoke(game, null);
        var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        if (canvas) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.sortingOrder = 500; canvas.worldCamera = Camera.main; canvas.planeDistance = 1f; }
        Canvas.ForceUpdateCanvases();
        CaptureCamera(Path.Combine(WorkspaceWorkDir(), "unity-recipes.png"), 540, 960);
        EditorApplication.Exit(0);
    }

    // one batch, every menu/overlay screen -> lets us audit cross-screen alignment cheaply
    public static void CaptureScreens()
    {
        RushhouseSceneBuilder.BuildMainScene();
        var game = UnityEngine.Object.FindFirstObjectByType<RushhouseUnityGame>();
        if (!game) throw new Exception("RushhouseUnityGame not found");
        Type type = typeof(RushhouseUnityGame);
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        type.GetMethod("Awake", flags)?.Invoke(game, null);

        object save = type.GetField("save", flags)?.GetValue(game);
        if (save != null) {
            Type st = save.GetType();
            st.GetField("perks")?.SetValue(save, "sharp,big_tips,overtime,regulars,wide_doors");
            st.GetField("day")?.SetValue(save, 6);
            st.GetField("coins")?.SetValue(save, 480);
            st.GetField("reputation")?.SetValue(save, 22);
            st.GetField("totalStars")?.SetValue(save, 14);
            st.GetField("theme")?.SetValue(save, "burger");
        }

        void Cap(string name) {
            var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            if (canvas) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.sortingOrder = 500; canvas.worldCamera = Camera.main; canvas.planeDistance = 1f; }
            Canvas.ForceUpdateCanvases();
            CaptureCamera(Path.Combine(WorkspaceWorkDir(), name), 540, 960);
        }

        type.GetMethod("ShowMenu", flags)?.Invoke(game, null);
        Cap("scr-menu.png");
        type.GetMethod("ShowRecipes", flags)?.Invoke(game, null);
        Cap("scr-recipes.png");
        type.GetMethod("ShowLayout", flags)?.Invoke(game, null);
        Cap("scr-layout.png");

        var pending = type.GetField("pendingPerks", flags)?.GetValue(game) as System.Collections.IList;
        if (pending != null) { pending.Clear(); pending.Add("sharp"); pending.Add("big_tips"); pending.Add("overtime"); }
        type.GetField("starRating", flags)?.SetValue(game, 3);
        type.GetField("served", flags)?.SetValue(game, 6);
        type.GetMethod("DrawResultScreen", flags)?.Invoke(game, new object[] { true });
        Cap("scr-result.png");

        EditorApplication.Exit(0);
    }

    // force a 4-top family party so we can eyeball multi-figure seating
    public static void CaptureParty()
    {
        RushhouseSceneBuilder.BuildMainScene();
        var game = UnityEngine.Object.FindFirstObjectByType<RushhouseUnityGame>();
        if (!game) throw new Exception("RushhouseUnityGame not found");
        Type type = typeof(RushhouseUnityGame);
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        type.GetMethod("Awake", flags)?.Invoke(game, null);
        object save = type.GetField("save", flags)?.GetValue(game);
        if (save != null) {
            Type st = save.GetType();
            foreach (string nm in new[] { "waiter", "cook", "prepper", "washer" }) st.GetField(nm)?.SetValue(save, 1);
            st.GetField("day")?.SetValue(save, 6);
        }
        type.GetMethod("StartDay", flags)?.Invoke(game, null);

        var customers = type.GetField("customers", flags)?.GetValue(game) as System.Collections.IList;
        if (customers != null && customers.Count > 0) {
            object cust = customers[0];
            Type ct = cust.GetType();
            object table = ct.GetField("table")?.GetValue(cust);
            table?.GetType().GetField("seats")?.SetValue(table, 4);
            ct.GetField("partySize")?.SetValue(cust, 4);
        }
        MethodInfo upd = type.GetMethod("UpdateCustomerMotion", flags);
        for (int i = 0; i < 30; i++) upd?.Invoke(game, new object[] { 0.06f });
        if (customers != null && customers.Count > 0) {
            object cust = customers[0];
            Type ct = cust.GetType();
            ct.GetField("ordered")?.SetValue(cust, true);
        }
        type.GetMethod("RebuildWorld", flags)?.Invoke(game, null);

        var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        if (canvas) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.sortingOrder = 500; canvas.worldCamera = Camera.main; canvas.planeDistance = 1f; }
        Canvas.ForceUpdateCanvases();
        CaptureCamera(Path.Combine(WorkspaceWorkDir(), "unity-party.png"), 540, 960);
        EditorApplication.Exit(0);
    }

    // force several seated + ordered guests so the ticket panel and overhead dishes render
    public static void CaptureService()
    {
        RushhouseSceneBuilder.BuildMainScene();
        var game = UnityEngine.Object.FindFirstObjectByType<RushhouseUnityGame>();
        if (!game) throw new Exception("RushhouseUnityGame not found");
        Type type = typeof(RushhouseUnityGame);
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        type.GetMethod("Awake", flags)?.Invoke(game, null);
        object save = type.GetField("save", flags)?.GetValue(game);
        if (save != null) {
            Type st = save.GetType();
            foreach (string nm in new[] { "waiter", "cook", "prepper", "washer" }) st.GetField(nm)?.SetValue(save, 1);
            st.GetField("day")?.SetValue(save, 4);
        }
        type.GetMethod("StartDay", flags)?.Invoke(game, null);
        MethodInfo spawn = type.GetMethod("SpawnCustomer", flags);
        for (int i = 0; i < 3; i++) spawn?.Invoke(game, null);
        MethodInfo upd = type.GetMethod("UpdateCustomerMotion", flags);
        for (int i = 0; i < 45; i++) upd?.Invoke(game, new object[] { 0.06f });
        var customers = type.GetField("customers", flags)?.GetValue(game) as System.Collections.IList;
        if (customers != null) {
            foreach (var cust in customers) {
                Type ct = cust.GetType();
                bool seated = (bool)(ct.GetField("seated")?.GetValue(cust) ?? false);
                if (seated) ct.GetField("ordered")?.SetValue(cust, true);
            }
        }
        // hand the player a COMPLETED cheeseburger plate to verify the carried-dish visual
        Type itemType = type.GetNestedType("Item", BindingFlags.NonPublic);
        object plate = itemType?.GetMethod("Plate", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, new object[] { false });
        var pparts = itemType?.GetField("parts")?.GetValue(plate) as System.Collections.IList;
        if (pparts != null) foreach (var p in new[] { "bun", "patty", "cheese", "bun" }) pparts.Add(p);
        type.GetField("holding", flags)?.SetValue(game, plate);
        type.GetField("playerFacing", flags)?.SetValue(game, Vector2.down);

        type.GetMethod("BuildPlayUI", flags)?.Invoke(game, null);
        type.GetMethod("RebuildWorld", flags)?.Invoke(game, null);
        var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        if (canvas) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.sortingOrder = 500; canvas.worldCamera = Camera.main; canvas.planeDistance = 1f; }
        Canvas.ForceUpdateCanvases();
        CaptureCamera(Path.Combine(WorkspaceWorkDir(), "unity-service.png"), 540, 960);
        EditorApplication.Exit(0);
    }

    // day-complete stats screen (no pending perks) with goals + market populated
    public static void CaptureResultStats()
    {
        RushhouseSceneBuilder.BuildMainScene();
        var game = UnityEngine.Object.FindFirstObjectByType<RushhouseUnityGame>();
        if (!game) throw new Exception("RushhouseUnityGame not found");
        Type type = typeof(RushhouseUnityGame);
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        type.GetMethod("Awake", flags)?.Invoke(game, null);
        object save = type.GetField("save", flags)?.GetValue(game);
        if (save != null) {
            Type st = save.GetType();
            st.GetField("day")?.SetValue(save, 5);
            st.GetField("coins")?.SetValue(save, 420);
            st.GetField("reputation")?.SetValue(save, 26);
        }
        foreach (var (nm, val) in new (string, int)[] { ("served", 6), ("goal", 5), ("earned", 180), ("tips", 24), ("bestCombo", 4), ("starRating", 3), ("goalBonus", 40), ("drinksServed", 3), ("wrongOrders", 1), ("missed", 0), ("queueComplaints", 0) })
            type.GetField(nm, flags)?.SetValue(game, val);
        type.GetMethod("GenerateDailyGoals", flags)?.Invoke(game, null);
        type.GetMethod("GenerateMarketOffers", flags)?.Invoke(game, null);
        var pending = type.GetField("pendingPerks", flags)?.GetValue(game) as System.Collections.IList;
        pending?.Clear();
        type.GetMethod("DrawResultScreen", flags)?.Invoke(game, new object[] { true });
        var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        if (canvas) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.sortingOrder = 500; canvas.worldCamera = Camera.main; canvas.planeDistance = 1f; }
        Canvas.ForceUpdateCanvases();
        CaptureCamera(Path.Combine(WorkspaceWorkDir(), "unity-result-stats.png"), 540, 960);
        EditorApplication.Exit(0);
    }

    // bowl cuisine theme: verify providers (rice sack etc.) + a completed grain bowl
    public static void CaptureBowl()
    {
        RushhouseSceneBuilder.BuildMainScene();
        var game = UnityEngine.Object.FindFirstObjectByType<RushhouseUnityGame>();
        if (!game) throw new Exception("RushhouseUnityGame not found");
        Type type = typeof(RushhouseUnityGame);
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        type.GetMethod("Awake", flags)?.Invoke(game, null);
        object save = type.GetField("save", flags)?.GetValue(game);
        if (save != null) {
            Type st = save.GetType();
            foreach (string nm in new[] { "waiter", "cook", "prepper", "washer" }) st.GetField(nm)?.SetValue(save, 1);
            st.GetField("day")?.SetValue(save, 3);
            st.GetField("theme")?.SetValue(save, "bowl");
        }
        type.GetMethod("StartDay", flags)?.Invoke(game, null);
        MethodInfo spawn = type.GetMethod("SpawnCustomer", flags);
        for (int i = 0; i < 2; i++) spawn?.Invoke(game, null);
        MethodInfo upd = type.GetMethod("UpdateCustomerMotion", flags);
        for (int i = 0; i < 45; i++) upd?.Invoke(game, new object[] { 0.06f });
        var customers = type.GetField("customers", flags)?.GetValue(game) as System.Collections.IList;
        if (customers != null)
            foreach (var cust in customers) {
                Type ct = cust.GetType();
                if ((bool)(ct.GetField("seated")?.GetValue(cust) ?? false)) ct.GetField("ordered")?.SetValue(cust, true);
            }
        Type itemType = type.GetNestedType("Item", BindingFlags.NonPublic);
        object plate = itemType?.GetMethod("Plate", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, new object[] { false });
        var pparts = itemType?.GetField("parts")?.GetValue(plate) as System.Collections.IList;
        if (pparts != null) foreach (var p in new[] { "rice", "patty", "lettuce" }) pparts.Add(p);
        type.GetField("holding", flags)?.SetValue(game, plate);
        type.GetField("playerFacing", flags)?.SetValue(game, Vector2.down);
        type.GetMethod("BuildPlayUI", flags)?.Invoke(game, null);
        type.GetMethod("RebuildWorld", flags)?.Invoke(game, null);
        var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        if (canvas) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.sortingOrder = 500; canvas.worldCamera = Camera.main; canvas.planeDistance = 1f; }
        Canvas.ForceUpdateCanvases();
        CaptureCamera(Path.Combine(WorkspaceWorkDir(), "unity-bowl.png"), 540, 960);
        EditorApplication.Exit(0);
    }

    static string WorkspaceWorkDir()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string workspace = Path.GetFullPath(Path.Combine(projectRoot, "..", ".."));
        string work = Path.Combine(workspace, "work");
        Directory.CreateDirectory(work);
        return work;
    }

    static void CaptureCamera(string path, int width, int height)
    {
        Camera cam = Camera.main;
        if (!cam) throw new Exception("Main camera not found");

        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        var prevTarget = cam.targetTexture;
        var prevActive = RenderTexture.active;
        cam.targetTexture = rt;
        RenderTexture.active = rt;
        cam.Render();

        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());

        RenderTexture.active = prevActive;
        cam.targetTexture = prevTarget;
        UnityEngine.Object.DestroyImmediate(tex);
        UnityEngine.Object.DestroyImmediate(rt);
        Debug.Log("VISUAL_VERIFY screenshot=" + path);
    }
}
