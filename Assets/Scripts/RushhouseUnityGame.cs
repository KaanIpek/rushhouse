using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RushhouseUnityGame : MonoBehaviour
{
    enum ScreenMode { Menu, Layout, Play, Recipes, Result, Wardrobe }

    [Serializable]
    class SaveData
    {
        public int day = 1;
        public int coins = 160;
        public string theme = "burger";
        public int speed = 0;
        public int grill = 0;
        public int prepUpgrade = 0;
        public int patience = 0;
        public int sinkUpgrade = 0;
        public int decor = 0;
        public int marketing = 0;
        public int room = 0;
        public int counter = 3;
        public int table = 3;
        public int hob = 2;
        public int prep = 1;
        public int sink = 1;
        public int drink = 0;
        public int oven = 1;
        public int espresso = 1;
        public int waiter = 0;
        public int cook = 0;
        public int washer = 0;
        public int prepper = 0;
        public int reputation = 0;
        public int totalStars = 0;
        public bool sfxOn = true;    // sound effects toggle (pause menu)
        public bool musicOn = true;  // background music toggle
        public int tokens = 0;       // cosmetic currency: earned by watching a rewarded ad or by 3-star days
        public string outfit = "player";   // equipped chef skin (sprite-set prefix)
        public string outfitWaiter = "waiter";   // staff can wear the same wardrobe
        public string outfitCook = "cook";
        public string outfitPrepper = "prepper";
        public string outfits = "";        // comma-separated ids the player owns
        public int adDay = 0;              // which game-day the daily ad allowance belongs to
        public int adsToday = 0;           // rewarded ads watched on that day
        public bool hapticsOn = true;// vibration toggle
        public bool touchButtons = true;  // show the on-screen stick + ACT. Off = tap/hold to play.
        public bool cameraFollow = false; // lock the camera to the chef instead of free framing
        public int stars = 3;       // restaurant star level 1..5 — drives how many guests show up
        public int bestDay = 1;
        public string layout = "";
        public string chairs = "";
        public int layoutVersion = 2;
        public string perks = "";
    }

    class Recipe
    {
        public string id;
        public string label;
        public string theme;
        public int day;
        public int value;
        public string[] parts;

        public Recipe(string id, string label, string theme, int day, int value, params string[] parts)
        {
            this.id = id;
            this.label = label;
            this.theme = theme;
            this.day = day;
            this.value = value;
            this.parts = parts;
        }
    }

    class Appliance
    {
        public string id;
        public string type;
        public int c;
        public int r;
        public int w;
        public int h;
        public string itemId;
        public Item item;
        public Customer customer;
        public bool dirty;
        public int seats = 2;
        public string tableKind = "small";
        public float fire;      // 0 = safe; >0 = seconds this station has been on fire
        public float nextSpread; // fire seconds at which this blaze may jump to one neighbour
        public int rotation;    // 0-3 = 0/90/180/270 deg, set in the layout screen
        public float orphanAge; // seconds a completed plate has matched no waiting guest (auto-binned)
    }

    class Item
    {
        public string kind;
        public string id;
        public string state;
        public float progress;
        public List<string> parts = new List<string>();
        public bool dirty;

        public static Item Ingredient(string id, string state = "")
        {
            return new Item { kind = "ingredient", id = id, state = state };
        }

        public static Item Plate(bool dirty = false)
        {
            return new Item { kind = "plate", id = "plate", dirty = dirty };
        }

        public static Item Drink()
        {
            return new Item { kind = "drink", id = "drink", state = "ready" };
        }

        public static Item Tool(string id)
        {
            return new Item { kind = "tool", id = id, state = "ready" };
        }
    }

    class Customer
    {
        public Recipe recipe;
        public List<string> orderParts;    // the ACTUAL required ingredients (recipe.parts +/- a modifier)
        public string orderMod = "";        // "NO LETTUCE" / "EXTRA CHEESE" / "" — shown on the ticket
        public bool ordered;
        public bool served;
        public float patience;
        public float maxPatience;
        public Appliance table;
        public bool wantsDrink;
        public bool mealServed;
        public bool drinkServed;
        public int partySize = 1;
        public int mealsServed;
        public int drinkCount;
        public string typeId = "regular";
        public string typeLabel = "";
        public int bonus;
        public float tipRate = 1f;
        public Vector2 visualPos;
        public Vector2 pathWaypoint; public float pathTimer;   // cached A*
        public float seatTimer;   // seconds spent trying to reach the seat (unreachable-seat watchdog)
        public bool seated;
        public bool leaving;
        public float walkSeed;
        public Vector2 facing = Vector2.down;
        public string visualId = "customer0";
        public float animationClock;
        public float transitionTime;
        public float eatTimer;
        public bool sittingDown;
        public bool standingUp;
        public bool dirtyOnLeave;
    }

    class MarketOffer
    {
        public string id;
        public string label;
        public string tier;
        public string desc;
        public int cost;
        public bool bought;
        public Color color;
    }

    class Worker
    {
        public string role;
        public Vector2 pos;
        public Vector2 target;
        public float timer;
        public string task = "IDLE";
        public Vector2 facing = Vector2.down;
        public float actionPulse;
        public Item carry;
        public Recipe carryRecipe;
        public Customer targetCustomer;
        public Appliance washTable;     // table the washer is walking to clear
        public Appliance pickupCounter; // counter the waiter is walking to (grab the plate ON ARRIVAL)
        public bool pendingArrival;
        public float arrivalPause;
        public float taskAge;           // seconds on the current pendingArrival trip (stuck-timeout)
        public Vector2 pathWaypoint;    // cached A* waypoint (recomputed periodically, not per frame)
        public float pathTimer;
        // cook/prepper two-leg TRANSPORT: fetch from one station, deliver to another (no teleport)
        public Appliance fetchFrom, placeTo;
        public string transportKind;    // cook|counter|trash|chop|plate|spawn (null = not transporting)
        public string spawnId;          // for "spawn": ingredient id to create at placeTo
        public bool delivering;         // false = walking to fetchFrom, true = walking to placeTo
        public float bestDist = float.MaxValue, noProgress;   // no-progress stuck detector
        public Vector2 prevTarget;
    }

    class DailyGoal
    {
        public string id;
        public string label;
        public int target;
        public int progress;
        public int reward;
        public bool done;
    }

    class Popup
    {
        public Vector2 pos;
        public string text;
        public Color color;
        public float life;
        public float maxLife;
        public bool burst;
    }

    class Perk
    {
        public string id, label, desc;
        public Perk(string id, string label, string desc) { this.id = id; this.label = label; this.desc = desc; }
    }

    const int W = 720;
    const int H = 1280;
    const int Cols = 10;
    const int Rows = 17;
    const float Tile = 0.56f;
    const float GridCenterY = 0.0f;
    const int DivRow = 9;          // the kitchen/dining divider sits on this row
    const int KitchenRow = 10;     // first row of the kitchen (below the divider)
    const float CustomerSitDuration = .72f;
    const float CustomerStandDuration = .72f;
    const float CustomerEatDuration = 2.35f;

    readonly Color bg = Hex("#080a10");
    readonly Color panel = Hex("#111722");
    readonly Color text = Hex("#f8f8fb");
    readonly Color muted = Hex("#aab1c0");
    readonly Color gold = Hex("#ffd166");
    readonly Color mint = Hex("#4ecdc4");
    readonly Color red = Hex("#ff5d67");
    readonly Color blue = Hex("#6aa8ff");
    readonly Color green = Hex("#58d68d");
    readonly Color violet = Hex("#b889ff");

    SaveData save;
    ScreenMode screen = ScreenMode.Menu;
    Texture2D objectAtlas;
    Texture2D foodAtlas;
    Texture2D characterAtlas;
    Texture2D floorAtlas;
    Sprite whiteSprite;
    readonly Dictionary<string, Sprite> objectSprites = new Dictionary<string, Sprite>();
    readonly Dictionary<string, Sprite> foodSprites = new Dictionary<string, Sprite>();
    readonly Dictionary<string, Sprite> characterSprites = new Dictionary<string, Sprite>();
    readonly Dictionary<string, Sprite> directionalCharacterSprites = new Dictionary<string, Sprite>();
    readonly Dictionary<string, Sprite> animatedCharacterSprites = new Dictionary<string, Sprite>();
    readonly Dictionary<string, Sprite> finalDishSprites = new Dictionary<string, Sprite>();
    readonly Dictionary<string, Sprite> carrySprites = new Dictionary<string, Sprite>();
    readonly Dictionary<string, Sprite> floorSprites = new Dictionary<string, Sprite>();
    readonly Dictionary<string, Sprite> menuSprites = new Dictionary<string, Sprite>();
    readonly Dictionary<string, Sprite> uiIconSprites = new Dictionary<string, Sprite>();
    readonly Dictionary<string, AudioClip> toneCache = new Dictionary<string, AudioClip>();
    // ---- real 3D props (the user's FBX models rendered as actual meshes, not sprites) ----
    readonly Dictionary<string, GameObject> modelPrefabCache = new Dictionary<string, GameObject>();
    readonly Dictionary<string, Material> modelMatCache = new Dictionary<string, Material>();
    readonly Dictionary<string, Material> worldMatCache = new Dictionary<string, Material>();
    Dictionary<string, string> propModelMap;   // game sprite name -> Models3D/<folder>
    GameObject propRoot;                        // persistent parent for pooled 3D prop meshes
    readonly Dictionary<string, GameObject> propPool = new Dictionary<string, GameObject>();
    // Renderers per pooled prop, and the solved fit per (model, footprint, yaw). Both were
    // recomputed for every prop on every ~30fps rebuild: GetComponentsInChildren allocates a fresh
    // array each call and the two CombinedBounds passes walk every renderer twice (#32). Neither
    // result can change once a prop is instantiated, so both are cached.
    readonly Dictionary<string, Renderer[]> propRends = new Dictionary<string, Renderer[]>();
    readonly Dictionary<string, Vector4> propFit = new Dictionary<string, Vector4>();
    readonly HashSet<string> propUsed = new HashSet<string>();
    Light sunLight;
    float spriteLift;              // extra world height for the next Make* sprites (food ON counter tops)
    float spriteDepthBoost;        // pull the next sprites toward the camera so overlays never clip
    // Bottom fraction of the screen reserved for the stick/ACT pads, i.e. the strip where a touch
    // steers instead of walking. Now derived from where the pads actually SIT (they end 40px above
    // the bottom of a 1280 frame and are ~190 tall, so ~0.20) instead of a flat 0.30 that ate a
    // third of the room. With the pads switched off nothing is reserved and the whole screen taps.
    float TouchBand => (save != null && !save.touchButtons) ? 0f : .20f;
    const float CameraPitch = 38f; // low isometric view: vertical faces read as standing on the floor
    static readonly float GroundProjection = Mathf.Sin(CameraPitch * Mathf.Deg2Rad);
    const float CameraDistance = 24f;
    const float SpriteDepthStep = .012f;
    Font uiFont;
    GameObject worldRoot;
    GameObject staticRoot;          // floor/walls/backdrop — built once, not torn down every rebuild
    bool drawStatic;                // routes Make* to staticRoot while a static build is in progress
    bool staticDirty = true;        // rebuild the static layer on the next RebuildWorld
    Transform ActiveRoot => (drawStatic && staticRoot) ? staticRoot.transform : worldRoot.transform;
    Canvas canvas;
    RectTransform uiRoot;
    Camera cam;
    Vector3 camHome = new Vector3(0, 1.2f, -10);
    float shakeAmp;                 // decaying camera-shake amplitude (juice)
    const float BaseOrtho = 6.8f;   // default zoom (frames the whole room: door at top, kitchen at bottom)
    float camZoom = 1f;             // user pinch/scroll zoom; >1 = zoomed in, <1 = zoomed out
    float pinchPrevDist = -1f;
    Vector2 camPan;                 // drag-to-pan offset
    Vector2 panPrev, rmbPrev;
    bool panPrevValid, rmbPrevValid;
    bool paused;                    // in-shift pause overlay (prevents stray-tap day abandon)
    bool fireFailed;                // a fire reached flashover this shift -> day already failed
    bool lastResultSuccess = true;  // remember day outcome so Result redraws (market/perk) keep the right header
    AudioSource audioSource;
    // Music runs on two sources so a track change can crossfade instead of cutting: `musicA` is
    // whatever is playing, `musicB` is whatever is arriving, and they swap when the fade lands.
    AudioSource musicA, musicB;
    string musicTrack = "";         // the track name currently owned by musicA
    float musicFade = 1f;           // 0 -> mid-crossfade, 1 -> settled on musicA
    const float MusicVolume = .34f; // sits under the SFX, which are deliberately loud and short

    readonly List<Recipe> recipes = new List<Recipe>();
    readonly List<Appliance> appliances = new List<Appliance>();
    readonly List<Customer> customers = new List<Customer>();
    readonly List<Worker> workers = new List<Worker>();
    readonly List<MarketOffer> marketOffers = new List<MarketOffer>();
    readonly List<DailyGoal> dailyGoals = new List<DailyGoal>();
    readonly List<Popup> popups = new List<Popup>();
    readonly List<Perk> allPerks = new List<Perk> {
        new Perk("sharp", "SHARP KNIVES", "Prep 35% faster"),
        new Perk("grill_master", "GRILL MASTER", "Cook 22% faster"),
        new Perk("clean_sweep", "POWER RINSE", "Wash 40% faster"),
        new Perk("big_tips", "GENEROUS", "Tips +30%"),
        new Perk("regulars", "REGULARS", "Patience +15%"),
        new Perk("wide_doors", "WIDE DOORS", "Queue capacity +2"),
        new Perk("combo_king", "COMBO KING", "Combo tips boosted"),
        new Perk("second_wind", "SECOND WIND", "Forgive 1 complaint/day"),
        new Perk("overtime", "OVERTIME", "Shift time +15%"),
        new Perk("investor", "INVESTOR", "Meal payout +12%"),
        new Perk("showman", "SHOWMAN", "Combo survives complaints"),
        new Perk("busy_bee", "BUSY BEE", "12% more customers"),
        new Perk("premium", "PREMIUM", "+2 coins per meal"),
        new Perk("vip_magnet", "CELEBRITY", "More big spenders"),
    };
    readonly List<string> pendingPerks = new List<string>();
    bool complaintForgivenUsed;
    GameObject playerObj;
    Item holding;
    Vector2 playerPos;
    Vector2 playerFacing = Vector2.down;
    bool playerWalking;
    Vector2 moveTarget;
    Appliance tapAction;
    bool hasMoveTarget;
    Vector2 playerWaypoint; float playerPathTimer;   // cached A* for the player
    float playerStall;                               // how long we've been wedged; drives re-route then give-up
    float pointerHold;                               // how long the pointer has been down on the world
    bool pointerHoldAct;                             // holding on a station within reach = work it
    GameObject actLabelGo;                           // hidden with the pads when buttons are off
    Vector2 camFollowPos;                            // smoothed camera focus when locked to the chef
    float spawnTimer;
    float shiftTime;
    float maxShiftTime;
    float holdProgress;
    bool holdPressed;
    // ---- mobile touch controls (transparent joystick + ACT) ----
    Sprite circleSprite;
    GameObject touchRoot, joyBaseGo, joyKnobGo, actBtnGo;
    Vector2 joyMove;
    Vector2 joyBaseCenter;
    int joyFinger = -1;
    Vector2 joyOrigin;
    bool actHeld;
    bool mouseJoyActive;
    float actDownTime;
    Appliance holdTarget;
    int queue;
    int queueMax;
    int served;
    int complaints;
    int earned;
    int goal;
    int drinksServed;
    int wrongOrders;
    int queueComplaints;
    float queueComplaintCooldown;
    int missed;
    int tips;
    int combo;
    int bestCombo;
    int goalBonus;
    int starRating;
    int starDelta;                  // restaurant-star change at the last day end (result screen note)
    float shiftElapsed;
    bool rushActive;
    float rebuildTimer;
    string shopTab = "upgrades";
    string message = "";
    float messageTimer;
    Appliance selectedLayout;
    bool draggingLayout;
    Appliance infoAppliance;      // station whose detail card is showing (tap/hover during play)
    float infoTimer;
    // tap a TICKET to open a full recipe breakdown (what exactly the guest ordered)
    readonly List<(Rect rect, Recipe recipe)> ticketRows = new List<(Rect, Recipe)>();
    // tap the dish floating over a guest's head for the same breakdown
    readonly List<(Vector2 world, float lift, float size, Recipe recipe)> orderIcons = new List<(Vector2, float, float, Recipe)>();
    Recipe detailRecipe;
    Recipe specialRecipe;

    readonly Dictionary<string, Vector2Int> objectCells = new Dictionary<string, Vector2Int>
    {
        ["table"] = new Vector2Int(0, 0), ["familyTable"] = new Vector2Int(1, 0),
        ["counter"] = new Vector2Int(2, 0), ["hob"] = new Vector2Int(3, 0),
        ["plates"] = new Vector2Int(0, 1), ["prep"] = new Vector2Int(1, 1),
        ["sink"] = new Vector2Int(2, 1), ["trash"] = new Vector2Int(3, 1),
        ["oven"] = new Vector2Int(0, 2), ["espresso"] = new Vector2Int(1, 2),
        ["drink"] = new Vector2Int(2, 2), ["provider"] = new Vector2Int(3, 2)
    };

    readonly Dictionary<string, Rect> objectCrops = new Dictionary<string, Rect>
    {
        ["table"] = Crop(38, 146, 247, 159), ["familyTable"] = Crop(19, 57, 294, 324),
        ["counter"] = Crop(0, 86, 312, 272), ["hob"] = Crop(43, 98, 236, 267),
        ["plates"] = Crop(54, 93, 192, 208), ["prep"] = Crop(10, 59, 291, 280),
        ["sink"] = Crop(32, 58, 276, 285), ["trash"] = Crop(79, 106, 163, 215),
        ["oven"] = Crop(47, 33, 239, 288), ["espresso"] = Crop(51, 24, 207, 311),
        ["drink"] = Crop(48, 32, 235, 303), ["provider"] = Crop(42, 38, 231, 296)
    };

    readonly Dictionary<string, Vector2Int> foodCells = new Dictionary<string, Vector2Int>
    {
        ["bun"] = new Vector2Int(0, 0), ["pattyRaw"] = new Vector2Int(1, 0),
        ["pattyCooked"] = new Vector2Int(2, 0), ["lettuce"] = new Vector2Int(3, 0),
        ["tomato"] = new Vector2Int(0, 1), ["cheese"] = new Vector2Int(1, 1),
        ["sauce"] = new Vector2Int(2, 1), ["dough"] = new Vector2Int(3, 1),
        ["doughBaked"] = new Vector2Int(0, 2), ["coffee"] = new Vector2Int(1, 2),
        ["milk"] = new Vector2Int(2, 2), ["drink"] = new Vector2Int(3, 2)
    };

    readonly Dictionary<string, Rect> foodCrops = new Dictionary<string, Rect>
    {
        ["bun"] = Crop(40, 130, 246, 249), ["pattyRaw"] = Crop(30, 130, 251, 253),
        ["pattyCooked"] = Crop(22, 129, 249, 256), ["lettuce"] = Crop(8, 126, 265, 257),
        ["tomato"] = Crop(38, 77, 248, 246), ["cheese"] = Crop(45, 94, 228, 223),
        ["sauce"] = Crop(36, 95, 220, 222), ["dough"] = Crop(12, 84, 257, 254),
        ["doughBaked"] = Crop(32, 29, 263, 261), ["coffee"] = Crop(50, 47, 217, 221),
        ["milk"] = Crop(39, 50, 223, 218), ["drink"] = Crop(28, 53, 216, 217)
    };

    readonly Dictionary<string, Vector2Int> charCells = new Dictionary<string, Vector2Int>
    {
        ["player"] = new Vector2Int(0, 0), ["waiter"] = new Vector2Int(1, 0),
        ["cook"] = new Vector2Int(2, 0), ["prepper"] = new Vector2Int(3, 0),
        ["customerHappy"] = new Vector2Int(0, 1), ["customerNeutral"] = new Vector2Int(1, 1),
        ["customerAngry"] = new Vector2Int(2, 1), ["customerWalk"] = new Vector2Int(3, 1)
    };

    readonly Dictionary<string, Rect> charCrops = new Dictionary<string, Rect>
    {
        ["player"] = Crop(113, 38, 232, 474), ["waiter"] = Crop(89, 44, 219, 468),
        ["cook"] = Crop(9, 52, 322, 417), ["prepper"] = Crop(61, 49, 207, 463),
        ["customerHappy"] = Crop(133, 0, 206, 405), ["customerNeutral"] = Crop(93, 0, 216, 406),
        ["customerAngry"] = Crop(72, 4, 216, 402), ["customerWalk"] = Crop(40, 0, 221, 440)
    };

    void Awake()
    {
        try {
            ConfigureDisplay();
            cam = Camera.main;
            if (!cam) cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = BaseOrtho;   // updated live by ApplyCamera() (pinch/scroll zoom)
            cam.transform.rotation = Quaternion.Euler(CameraPitch, 0f, 0f);
            camHome = GameGroundPoint(new Vector2(0, 1.2f), 0f) - cam.transform.forward * CameraDistance;
            cam.transform.position = camHome;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = bg;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 80f;
            EnsureSceneLight();
            LoadArt();
            BuildPropModelMap();
            BuildRecipes();
            LoadSave();
            EnsureRoots();
            EnsureAudio();
            ShowMenu();
        } catch (Exception ex) {
            Debug.LogException(ex);
            throw;
        }
    }

    void ConfigureDisplay()
    {
        Application.targetFrameRate = 60;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        Screen.fullScreenMode = FullScreenMode.Windowed;
        Screen.SetResolution(540, 960, false);
#endif
    }

    void Update()
    {
        if (messageTimer > 0) messageTimer -= Time.deltaTime;
        UpdateTouchControls();
        if (screen == ScreenMode.Play && !paused) UpdatePlay();
        if (screen == ScreenMode.Layout) UpdateLayoutDrag();
        // Escape / M: during a live shift this PAUSES (so a stray tap can't abandon the day); elsewhere -> menu
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.M)) {
            if (screen == ScreenMode.Play) TogglePause();
            else ShowMenu();
        }
        HandleZoom();
        ApplyCamera();
        SetMusic(DesiredTrack());
        UpdateMusic();
    }

    // pinch + two-finger drag (mobile) / scroll + right-drag (desktop) / on-screen ± buttons
    void HandleZoom()
    {
        if (screen != ScreenMode.Play && screen != ScreenMode.Layout) return;
        float sc = Input.mouseScrollDelta.y;
        if (Mathf.Abs(sc) > .01f) SetZoom(camZoom + sc * .1f);
        float wpp = (2f * cam.orthographicSize) / Mathf.Max(1, Screen.height);   // world units per pixel
        if (Input.touchCount == 2) {
            var a = Input.GetTouch(0).position; var b = Input.GetTouch(1).position;
            float d = (a - b).magnitude;
            if (pinchPrevDist > 0f) SetZoom(camZoom * (d / Mathf.Max(1f, pinchPrevDist)));
            pinchPrevDist = d;
            Vector2 cen = (a + b) * .5f;
            if (panPrevValid) camPan -= (cen - panPrev) * wpp;   // drag the room with two fingers
            panPrev = cen; panPrevValid = true;
        } else { pinchPrevDist = -1f; panPrevValid = false; }
        // desktop: hold right mouse to pan around
        if (Input.GetMouseButton(1)) {
            Vector2 m = Input.mousePosition;
            if (rmbPrevValid) camPan -= (m - rmbPrev) * wpp;
            rmbPrev = m; rmbPrevValid = true;
        } else rmbPrevValid = false;
        camPan.x = Mathf.Clamp(camPan.x, -3.2f, 3.2f);
        camPan.y = Mathf.Clamp(camPan.y, -3.5f, 3.5f);
    }

    void SetZoom(float z) { camZoom = Mathf.Clamp(z, .72f, 1.6f); }

    void ApplyCamera()
    {
        if (!cam) return;
        float ortho = Mathf.Clamp(BaseOrtho / camZoom, 4.2f, 8.9f);
        cam.orthographicSize = ortho;
        // lift the framing with zoom so the entrance door stays in view below the top HUD while the
        // kitchen stays above the bottom controls; camPan lets the player drag around
        float cy = Mathf.Lerp(1.05f, 2.1f, Mathf.InverseLerp(5.4f, 8.9f, ortho));
        Vector2 focus = new Vector2(camPan.x, cy + camPan.y);
        // Optional lock-to-chef. Smoothed rather than rigid: a camera pinned exactly to a body that
        // changes direction every step reads as the ROOM twitching, not the chef moving. The
        // exponential lerp is frame-rate independent, and camPan still applies so a drag nudges the
        // framing without breaking the follow. Offset upward so the chef sits below centre, clear of
        // the top HUD.
        if (save != null && save.cameraFollow && screen == ScreenMode.Play) {
            if (camFollowPos == Vector2.zero) camFollowPos = playerPos;
            camFollowPos = Vector2.Lerp(camFollowPos, playerPos, 1f - Mathf.Exp(-5.5f * Time.deltaTime));
            focus = camFollowPos + new Vector2(camPan.x, camPan.y + .55f);
        }
        Vector3 target = GameGroundPoint(focus, 0f);
        camHome = target - cam.transform.forward * CameraDistance;
        // decaying camera shake (juice) on top of the framed home position
        if (shakeAmp > .0006f) {
            shakeAmp = Mathf.Lerp(shakeAmp, 0f, Time.deltaTime * 8.5f);
            cam.transform.position = camHome + cam.transform.right * (Mathf.Sin(Time.time * 96f) * shakeAmp) + cam.transform.up * (Mathf.Cos(Time.time * 78f) * shakeAmp);
        } else {
            shakeAmp = 0f;
            cam.transform.position = camHome;
        }
    }

    void Shake(float amp) { shakeAmp = Mathf.Min(.32f, Mathf.Max(shakeAmp, amp)); }

    void Buzz(long ms = 30)
    {
        if (save != null && !save.hapticsOn) return;   // haptics toggle (#25)
#if UNITY_ANDROID || UNITY_IOS
        if (SystemInfo.supportsVibration) Handheld.Vibrate();
#endif
    }

    // a warm key light + cool ambient so the 3D prop meshes read with real form/shadow (matches the
    // lighting the sprites were baked with, so meshes and any fallback sprites sit together)
    void EnsureSceneLight()
    {
        if (!sunLight) {
            var go = new GameObject("Sun");
            sunLight = go.AddComponent<Light>();
        }
        sunLight.type = LightType.Directional;
        sunLight.intensity = 1.12f;
        sunLight.color = new Color(1f, .97f, .9f);
        sunLight.shadows = LightShadows.Soft;
        sunLight.shadowStrength = .55f;
        sunLight.transform.rotation = Quaternion.Euler(52f, -32f, 0f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.55f, .57f, .6f);
    }

    void TogglePause()
    {
        paused = !paused;
        if (paused) { joyMove = Vector2.zero; DrawPauseOverlay(); }
        else BuildPlayUI();
    }

    void DrawPauseOverlay()
    {
        AddPanel(0, 0, W, H, new Color(.02f, .03f, .05f, .88f), uiRoot, "pause-veil");
        const int px = 56, pw = 608;
        AddCard(px, 318, pw, 562, gold, "pause");
        AddText("PAUSED", 360, 372, 34, gold, FontStyle.Bold);
        AddText("DAY " + save.day, 360, 404, 13, muted, FontStyle.Bold);
        DrawStarPips(304, 424, 16);

        // shift snapshot: the two numbers that decide whether resuming is worth it
        AddChip(px + 22, 462, 268, 62, mint, "pause-served", .09f);
        AddText(served + "/" + goal, px + 156, 484, 22, mint, FontStyle.Bold);
        AddText("SERVED", px + 156, 508, 10, muted, FontStyle.Bold);
        AddChip(px + 300, 462, 268, 62, gold, "pause-earned", .09f);
        AddText("$" + earned, px + 434, 484, 22, gold, FontStyle.Bold);
        AddText("EARNED", px + 434, 508, 10, muted, FontStyle.Bold);

        // audio + haptics toggles (#23/#25) — full-width rows so each label has room to say
        // what it does and its state, rather than three cramped side-by-side buttons
        PauseToggle("MUSIC", save.musicOn, 546, () => { save.musicOn = !save.musicOn; });
        PauseToggle("SOUND EFFECTS", save.sfxOn, 606, () => { save.sfxOn = !save.sfxOn; });
        PauseToggle("VIBRATION", save.hapticsOn, 666, () => { save.hapticsOn = !save.hapticsOn; });

        AddIconButton("RESUME", "ic_open", px + 22, 730, pw - 44, 72, mint, () => { paused = false; BuildPlayUI(); }, "pause-resume");
        AddIconButton("QUIT TO MENU", "ic_back", px + 22, 812, pw - 44, 52, red, () => { paused = false; ShowMenu(); }, "pause-quit");
        AnimateUIIn("pause", .018f, .2f);
    }

    // Audio settings reachable without starting a shift. Same rows as pause, so there is one
    // place the toggles are defined and one place they can drift out of sync.
    void DrawSettingsOverlay()
    {
        // the veil is drawn over the live menu, so a toggle redraw must rebuild both layers
        void Reopen() { ShowMenu(); DrawSettingsOverlay(); }
        AddPanel(0, 0, W, H, new Color(.02f, .03f, .05f, .88f), uiRoot, "set-veil");
        const int px = 56, pw = 608;
        // The consent row only exists for players a CMP applies to (EEA/UK/Switzerland), so the
        // card grows for them rather than leaving a gap for everyone else.
        bool privacy = RushhouseConsent.PrivacyOptionsRequired;
        // Five toggles now (audio x3 + controls x2), so the card is sized from the row count rather
        // than from two hand-tuned constants that had to be edited every time a row was added.
        const int rowTop = 452, rowStep = 56;
        const int rows = 5;
        // Everything below the rows is positioned from the same running y, so adding a sixth toggle
        // later moves the buttons and resizes the card automatically instead of silently overlapping
        // them the way two hand-tuned constants did.
        int lastRowEnd = rowTop + 40 + rows * rowStep;          // 492 + 5*56 = 772
        int privacyY = lastRowEnd + 4;
        int doneY2 = privacy ? privacyY + 62 : privacyY;
        int creditY = doneY2 + 78;
        AddCard(px, 420, pw, creditY - 420 + 30, gold, "settings");
        AddText("SETTINGS", 360, 452, 30, gold, FontStyle.Bold);
        int y = rowTop + 40;
        PauseToggle("MUSIC", save.musicOn, y, () => { save.musicOn = !save.musicOn; }, Reopen); y += rowStep;
        PauseToggle("SOUND EFFECTS", save.sfxOn, y, () => { save.sfxOn = !save.sfxOn; }, Reopen); y += rowStep;
        PauseToggle("VIBRATION", save.hapticsOn, y, () => { save.hapticsOn = !save.hapticsOn; }, Reopen); y += rowStep;
        // Turning the pads off is only safe because tap-to-walk, hold-to-keep-walking and
        // hold-on-a-station-to-work-it all exist; see UpdatePlay. TouchBand also drops to 0 so the
        // strip the pads used to reserve becomes tappable world again.
        PauseToggle("ON-SCREEN BUTTONS", save.touchButtons, y,
            () => { save.touchButtons = !save.touchButtons; }, Reopen); y += rowStep;
        PauseToggle("CAMERA FOLLOWS CHEF", save.cameraFollow, y,
            () => { save.cameraFollow = !save.cameraFollow; camFollowPos = playerPos; }, Reopen); y += rowStep;
        // GDPR requires the consent choice to stay changeable after the first run, and the privacy
        // policy promises this exact entry. Google's UMP owns the form; we only re-open it.
        if (privacy)
            AddIconButton("PRIVACY OPTIONS", "ic_open", px + 22, privacyY, pw - 44, 52, gold,
                () => RushhouseConsent.ShowPrivacyOptions(Reopen), "set-privacy");
        AddIconButton("DONE", "ic_open", px + 22, doneY2, pw - 44, 52, mint, ShowMenu, "set-done");
        // Required attribution: the soundtrack is Stable Audio 3 output under the Stability AI
        // Community License, which obliges this exact string wherever the game is distributed.
        // It also lives in NOTICE and the README; this is the copy a player can actually see.
        AddText("Music powered by Stability AI", 360, creditY, 10, muted, FontStyle.Normal);
        AnimateUIIn("settings", .018f, .2f);
    }

    // one settings row: name on the left, an ON/OFF pill on the right, whole row tappable
    void PauseToggle(string label, bool on, int y, System.Action flip, System.Action redraw = null)
    {
        const int px = 56, pw = 608;
        AddHitArea(px + 22, y, pw - 44, 52, () => { flip(); Persist(); suppressAnim = true; (redraw ?? DrawPauseOverlay)(); }, "pause-toggle");
        AddChip(px + 22, y, pw - 44, 52, on ? mint : muted, "pause-toggle-bg", on ? .12f : .05f);
        AddText(label, px + 44, y + 26, 15, on ? Color.white : muted, FontStyle.Bold, TextAnchor.MiddleLeft, "pause-toggle-label");
        AddPanel(px + pw - 132, y + 11, 88, 30, on ? new Color(.24f, .78f, .58f, .9f) : new Color(1, 1, 1, .1f), uiRoot, "pause-pill");
        AddText(on ? "ON" : "OFF", px + pw - 88, y + 26, 12, on ? new Color(.03f, .06f, .05f) : muted, FontStyle.Bold);
    }

    // 5 filled/dim pips = the restaurant's star level (glyph stars can render as boxes in this font)
    void DrawStarPips(int x, int y, int size)
    {
        for (int i = 0; i < 5; i++) {
            bool on = i < save.stars;
            int px = x + i * (size + 6);
            AddPanel(px, y, size, size, on ? new Color(1f, .8f, .25f, .95f) : new Color(1, 1, 1, .13f), uiRoot, "star-pip");
            if (on) AddPanel(px + 3, y + 3, size - 6, size - 6, new Color(1f, .93f, .6f, .95f), uiRoot, "star-pip-core");
        }
    }

    void LoadArt()
    {
        objectAtlas = Resources.Load<Texture2D>("Art/kitchen-sprite-atlas-v1-alpha");
        foodAtlas = Resources.Load<Texture2D>("Art/food-sprite-atlas-v1-alpha");
        characterAtlas = Resources.Load<Texture2D>("Art/character-sprite-atlas-v2-alpha");
        floorAtlas = Resources.Load<Texture2D>("Art/floor-texture-atlas-v1");
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1);
        circleSprite = MakeCircleSprite(96);
        LoadSpriteSet("Art/Objects", objectCells.Keys.Concat(ProviderSpriteNames()).Concat(new[] { "dtable", "dchair", "dchairR", "dchairF", "dchairB", "wallwindow", "wallpic", "walldoor", "wallSideL", "wallSideR", "wallBackH", "wallCorner", "wallCornerR", "extinguisher", "flame", "kitchenDivider" }), objectSprites);
        var singlePlate = ResourceSprite("Art/Objects/singlePlate");
        if (singlePlate) objectSprites["singlePlate"] = singlePlate;
        var dirtyPlate = ResourceSprite("Art/Objects/dirtyPlate");
        if (dirtyPlate) objectSprites["dirtyPlate"] = dirtyPlate;
        LoadSpriteSet("Art/Foods", foodCells.Keys.Concat(new[] { "lettuceReady", "tomatoReady", "sausageRaw", "sausageCooked", "onion", "bunBottom", "rice" }), foodSprites);
        LoadSpriteSet("Art/Characters", charCells.Keys, characterSprites);
        LoadSpriteSet("Art/CharactersDirectional", DirectionalCharacterNames(), directionalCharacterSprites);
        LoadSpriteSet("Art/CharactersRigged", AnimatedCharacterNames(), animatedCharacterSprites);
        LoadSpriteSet("Art/FinalDishes", new[] {
            "basic", "green", "fresh", "cheese", "deluxe", "saucy", "tower", "bacon", "double",
            "margherita", "garden", "rosso", "supreme", "bianca",
            "espresso", "latte", "sweet_latte", "double_shot", "cappuccino",
            "classic", "cheesy", "loaded",
            "ricebowl", "greenbowl", "gardenbowl", "cheddarbowl", "fiesta"
        }, finalDishSprites);
        LoadSpriteSet("Art/Carry", new[] {
            "plate", "dirtyPlate", "bun", "pattyRaw", "pattyCooked", "lettuce", "lettuceReady",
            "tomato", "tomatoReady", "cheese", "sauce", "dough", "doughBaked", "coffee",
            "milk", "drink", "basic", "green", "fresh", "cheese", "deluxe", "saucy",
            "tower", "bacon", "double", "margherita", "garden", "rosso", "supreme", "bianca",
            "espresso", "latte", "sweet_latte", "double_shot", "cappuccino",
            "sausageRaw", "sausageCooked", "onion", "classic", "cheesy", "loaded"
        }, carrySprites);
        LoadSpriteSet("Art/Menu", new[] {
            "menu_panel", "menu_hero", "title_plaque", "primary_button", "secondary_button",
            "danger_button", "coin_chip", "shop_panel", "tab_teal", "tab_dark", "tab_gold",
            "divider_gold", "divider_teal", "divider_blue", "divider_silver", "divider_dots", "ornate_footer"
        }, menuSprites);
        LoadSpriteSet("Art/UI", new[] { "ic_rotate", "ic_seats", "ic_reset", "ic_back", "ic_open", "ic_pause" }, uiIconSprites);
        floorSprites.Clear();
        floorSprites["wood"] = ResourceSprite("Art/Floors/wood");
        floorSprites["tile"] = ResourceSprite("Art/Floors/tile");
        floorSprites["wall_warm"] = ResourceSprite("Art/Floors/wall_warm");
        floorSprites["marble"] = ResourceSprite("Art/Floors/marble");
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (!uiFont) uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    void LoadSpriteSet(string resourceRoot, IEnumerable<string> names, Dictionary<string, Sprite> target)
    {
        target.Clear();
        foreach (var name in names) {
            var sprite = ResourceSprite(resourceRoot + "/" + name);
            if (sprite) target[name] = sprite;
        }
    }

    IEnumerable<string> DirectionalCharacterNames()
    {
        string[] bases = {
            "player", "waiter", "cook", "prepper",
            "customerHappy", "customerNeutral", "customerAngry", "customerWalk"
        };
        string[] dirs = { "front", "back", "left", "right" };
        foreach (var baseName in bases) {
            foreach (var dir in dirs) yield return baseName + "_" + dir;
        }
    }

    // ---- rewarded ads ----------------------------------------------------------------------
    // Rushhouse never interrupts play with an ad. Every ad is an OFFER the player opens, and every
    // one pays something the player wanted anyway — that is the whole monetisation design, and it
    // is why there is no banner or interstitial code anywhere in this file.
    RushhouseAds ads;
    string adPending = "";        // which offer is mid-watch ("" = none)
    float adProgress;             // 0..1 while the simulated ad plays
    bool adOverlayUp;
    bool tipsDoubled;             // one DOUBLE TIPS per result screen
    int tokensEarnedToday;        // wardrobe tokens the finished shift paid out
    bool secondChanceUsed;        // one SECOND CHANCE per shift
    bool perkRerollUsed;          // one perk REROLL per offer
    const int AdsPerDay = 3;      // daily cap on the token bonus, so it stays a bonus not a job

    int AdsLeftToday()
    {
        if (save.adDay != save.day) return AdsPerDay;   // a new day resets the allowance
        return Mathf.Max(0, AdsPerDay - save.adsToday);
    }

    void NoteAdWatched()
    {
        if (save.adDay != save.day) { save.adDay = save.day; save.adsToday = 0; }
        save.adsToday++;
    }

    /// Start an offer. `kind` decides the payout, applied only when the ad reports a real reward.
    void WatchAd(string kind, Action redraw)
    {
        if (!string.IsNullOrEmpty(adPending)) return;
        ads = RushhouseAds.Ensure();
        adPending = kind;
        adProgress = 0f;
        adOverlayUp = true;
        ads.onSimulatedProgress = p => { adProgress = p; DrawAdOverlay(); };
        DrawAdOverlay();
        ads.Show(earned => {
            adPending = "";
            adOverlayUp = false;
            if (ads != null) ads.onSimulatedProgress = null;
            if (earned) GrantAdReward(kind);
            redraw?.Invoke();
        });
    }

    void GrantAdReward(string kind)
    {
        switch (kind) {
            case "tips":                       // double the shift's tips
                tipsDoubled = true;
                save.coins += tips;
                earned += tips;
                tips *= 2;
                SetMessage("TIPS DOUBLED  +" + (tips / 2), 2f);
                PlayEventSound("coin");
                break;
            case "second":                     // stay in the shift with breathing room
                complaints = Mathf.Max(0, complaints - 2);
                SetMessage("SECOND CHANCE - 2 complaints cleared", 2f);
                PlayEventSound("perk");
                break;
            case "reroll":                     // a fresh set of perk cards
                pendingPerks.Clear();
                pendingPerks.AddRange(allPerks.Where(p => !HasPerk(p.id))
                    .OrderBy(_ => UnityEngine.Random.value).Take(3).Select(p => p.id));
                PlayEventSound("perk");
                break;
            case "token":                      // the daily cosmetic-currency bonus
                NoteAdWatched();
                save.tokens += 1;
                SetMessage("+1 TOKEN", 1.6f);
                PlayEventSound("coin");
                break;
        }
        Persist();
    }

    // Drawn over whatever screen opened the offer. On device the real ad covers this entirely;
    // on desktop it IS the ad, which is what makes the reward loop testable without a phone.
    void DrawAdOverlay()
    {
        if (!adOverlayUp) return;
        ClearUI();
        AddPanel(0, 0, W, H, new Color(.01f, .015f, .02f, .96f), uiRoot, "ad-veil");
        AddCard(76, 500, 568, 244, gold, "ad-card");
        AddText("LOADING AD", 360, 560, 26, gold, FontStyle.Bold);
        AddText(AdRewardLine(adPending), 360, 596, 13, muted, FontStyle.Bold);
        AddPanel(120, 636, 480, 16, new Color(1, 1, 1, .08f), uiRoot, "ad-track");
        int w = Mathf.Max(4, Mathf.RoundToInt(480 * Mathf.Clamp01(adProgress)));
        AddPanel(120, 636, w, 16, mint, uiRoot, "ad-fill");
        AddText(Mathf.RoundToInt(adProgress * 100) + "%", 360, 686, 12, text, FontStyle.Bold);
        AddText("Reward is paid when the ad finishes", 360, 716, 10, muted, FontStyle.Normal);
    }

    static string AdRewardLine(string kind)
    {
        switch (kind) {
            case "tips": return "REWARD:  double this shift's tips";
            case "second": return "REWARD:  clear 2 complaints and keep serving";
            case "reroll": return "REWARD:  a fresh set of perk cards";
            case "token": return "REWARD:  +1 wardrobe token";
            default: return "";
        }
    }

    // Shown the moment the fifth complaint lands. Declining ends the day exactly as before, so
    // the ad is a genuine choice and never a paywall in front of losing.
    void OfferSecondChance()
    {
        paused = true;
        ClearUI();
        AddPanel(0, 0, W, H, new Color(.02f, .02f, .04f, .92f), uiRoot, "sc-veil");
        AddCard(56, 380, 608, 400, red, "sc-card");
        AddText("SERVICE COLLAPSING", 360, 436, 30, red, FontStyle.Bold);
        AddText("Five complaints. One more and the day is lost.", 360, 472, 12, muted, FontStyle.Bold);
        AddChip(80, 506, 560, 82, mint, "sc-offer", .12f);
        AddText("CLEAR 2 COMPLAINTS", 360, 534, 18, mint, FontStyle.Bold);
        AddText("and carry on serving this shift", 360, 562, 11, muted, FontStyle.Bold);
        AddAdButton("WATCH AD", 80, 606, 560, 74, "second",
            () => { paused = false; BuildPlayUI(); }, "sc-watch");
        AddIconButton("GIVE UP", "ic_back", 80, 694, 560, 60, muted,
            () => { paused = false; FinishDay(false); }, "sc-quit");
    }

    // A consistent "watch an ad for X" button. Purple so it never reads as a normal action.
    void AddAdButton(string label, int x, int y, int w, int h, string kind, Action redraw, string name)
    {
        AddIconButton(label, "ic_open", x, y, w, h, violet, () => WatchAd(kind, redraw), name);
    }

    // ---- cosmetics ------------------------------------------------------------------------
    // Each outfit is a full sprite set rendered by Tools/render_outfits.py, NOT a colour tint —
    // tinting the sprite would recolour the chef's face along with his jacket. The default is the
    // stock `player_*` set, which is why it is priced at zero and always owned.
    class Outfit
    {
        public string id, label, blurb;
        public int price;          // in tokens; 0 = owned from the start
        public Color accent;
        public Outfit(string id, string label, string blurb, int price, Color accent)
        { this.id = id; this.label = label; this.blurb = blurb; this.price = price; this.accent = accent; }
    }

    readonly List<Outfit> outfitCatalogue = new List<Outfit>();

    void BuildOutfitCatalogue()
    {
        if (outfitCatalogue.Count > 0) return;
        outfitCatalogue.Add(new Outfit("player", "CLASSIC", "This role's own uniform", 0, new Color(.85f, .87f, .92f)));
        outfitCatalogue.Add(new Outfit("skinCrimson", "CRIMSON", "Red service jacket, gold band", 4, new Color(.85f, .22f, .2f)));
        outfitCatalogue.Add(new Outfit("skinMidnight", "MIDNIGHT", "Navy blacks with a gold trim", 5, new Color(.32f, .42f, .85f)));
        outfitCatalogue.Add(new Outfit("skinMint", "MINT", "Teal apron, clean whites", 6, new Color(.24f, .78f, .58f)));
        outfitCatalogue.Add(new Outfit("skinGold", "GOLD LEAF", "For a five-star room", 10, new Color(1f, .78f, .25f)));
        outfitCatalogue.Add(new Outfit("skinNeon", "NEON", "Magenta and cyan, no apologies", 12, new Color(.9f, .3f, .75f)));
        // Costumes: a different base model, so the silhouette changes too, not just the colours.
        outfitCatalogue.Add(new Outfit("skinTuxedo", "MAITRE D'", "Black tux, white shirt, red tie", 8, new Color(.82f, .84f, .9f)));
        outfitCatalogue.Add(new Outfit("skinKnight", "PLATE ARMOUR", "Helm, pauldrons, breastplate", 15, new Color(.72f, .76f, .84f)));
        outfitCatalogue.Add(new Outfit("skinForeman", "SITE FOREMAN", "Hard hat and hi-viz bib", 8, new Color(.95f, .72f, .12f)));
        outfitCatalogue.Add(new Outfit("skinFire", "FIRE CHIEF", "Red helmet, turnout coat", 12, new Color(.92f, .28f, .22f)));
    }

    bool OwnsOutfit(string id)
    {
        if (id == "player") return true;    // the starter set is never bought
        return !string.IsNullOrEmpty(save.outfits) && ("," + save.outfits + ",").Contains("," + id + ",");
    }

    // The sprite prefix a role draws with. Falls back to that role's stock set if the equipped
    // id was never rendered, so a save from a build with more outfits cannot make anyone invisible.
    string OutfitSpriteName(string role)
    {
        string id;
        switch (role) {
            case "waiter": id = save.outfitWaiter; break;
            case "cook": id = save.outfitCook; break;
            case "prepper": id = save.outfitPrepper; break;
            default: role = "player"; id = save.outfit; break;
        }
        if (string.IsNullOrEmpty(id) || id == role) return role;
        return animatedCharacterSprites.ContainsKey(id + "_front_idle_0") ? id : role;
    }

    string PlayerSpriteName() => OutfitSpriteName("player");

    // Which role the wardrobe is currently dressing.
    string wardrobeRole = "player";

    string EquippedFor(string role)
    {
        switch (role) {
            case "waiter": return save.outfitWaiter;
            case "cook": return save.outfitCook;
            case "prepper": return save.outfitPrepper;
            default: return save.outfit;
        }
    }

    void SetEquipped(string role, string id)
    {
        switch (role) {
            case "waiter": save.outfitWaiter = id; break;
            case "cook": save.outfitCook = id; break;
            case "prepper": save.outfitPrepper = id; break;
            default: save.outfit = id; break;
        }
    }

    int StaffCount(string role)
    {
        switch (role) {
            case "waiter": return save.waiter;
            case "cook": return save.cook;
            case "prepper": return save.prepper;
            default: return 1;
        }
    }

    static string RoleLabel(string role)
    {
        switch (role) {
            case "waiter": return "WAITER";
            case "cook": return "COOK";
            case "prepper": return "PREP";
            default: return "CHEF";
        }
    }

    // ---- wardrobe screen ---------------------------------------------------------------------
    void ShowWardrobe()
    {
        screen = ScreenMode.Wardrobe;
        BuildOutfitCatalogue();
        ClearWorld();
        ClearStaticWorld();
        TrimPropPool();
        DrawWardrobe();
    }

    void DrawWardrobe()
    {
        ClearUI();
        AddPanel(0, 0, W, H, new Color(.03f, .035f, .05f, 1), uiRoot, "wr-bg");
        AddText("WARDROBE", 360, 78, 38, gold, FontStyle.Bold);
        AddText("Buy once - wear on the chef or any staff member", 360, 112, 11, muted, FontStyle.Bold);

        // token balance + how to get more, stated plainly so the currency is never a mystery
        AddCard(56, 140, 608, 96, gold, "wr-bank");
        AddText(save.tokens.ToString(), 118, 178, 34, gold, FontStyle.Bold);
        AddText("TOKENS", 118, 208, 10, muted, FontStyle.Bold);
        int left = AdsLeftToday();
        if (left > 0)
            AddAdButton("WATCH AD  +1  (" + left + " left today)", 196, 158, 448, 62, "token", DrawWardrobe, "wr-earn");
        else {
            AddChip(196, 158, 448, 62, muted, "wr-earn-done", .06f);
            AddText("Daily bonus used - 3-star days also pay tokens", 420, 189, 11, muted, FontStyle.Bold);
        }

        // Role tabs: outfits are bought once and can be worn by the chef OR any hired staff,
        // because every outfit is rendered from one of the four models the game already uses.
        string[] roles = { "player", "waiter", "cook", "prepper" };
        for (int r = 0; r < roles.Length; r++) {
            string role = roles[r];
            bool on = wardrobeRole == role;
            bool hired = role == "player" || StaffCount(role) > 0;
            int tx = 40 + r * 161, tw2 = 149;
            AddHitArea(tx, 246, tw2, 52, () => { wardrobeRole = role; DrawWardrobe(); }, "wr-tab" + r);
            AddChip(tx, 246, tw2, 52, on ? gold : muted, "wr-tabbg" + r, on ? .18f : .05f);
            AddText(RoleLabel(role), tx + tw2 / 2, 268, 13, on ? gold : hired ? text : muted, FontStyle.Bold);
            if (!hired) AddText("not hired", tx + tw2 / 2, 286, 8, muted, FontStyle.Bold);
        }

        // Two columns: ten outfits down a single column overran the screen and hid the last two
        // behind the BACK button. Five rows of two fit inside 1280 with room to spare.
        BuildOutfitCatalogue();
        const int px = 40, gap = 16, cardW = 312, cardH = 130, rowGap = 12;
        int top = 316;
        for (int i = 0; i < outfitCatalogue.Count; i++) {
            var o = outfitCatalogue[i];
            int col = i % 2, row = i / 2;
            int x = px + col * (cardW + gap);
            int y = top + row * (cardH + rowGap);

            bool owned = OwnsOutfit(o.id);
            bool equipped = o.id == "player" ? EquippedFor(wardrobeRole) == wardrobeRole
                                             : EquippedFor(wardrobeRole) == o.id;
            bool rendered = o.id == "player" || animatedCharacterSprites.ContainsKey(o.id + "_front_idle_0");
            bool affordable = save.tokens >= o.price;
            // CLASSIC is "wear your own uniform", so for staff it previews their own sprite set
            string previewId = o.id == "player" ? wardrobeRole : o.id;
            Color accent = !rendered ? muted : equipped ? mint : owned ? gold : o.accent;

            int idx = i;
            if (rendered && (owned || affordable))
                AddHitArea(x, y, cardW, cardH, () => TapOutfit(idx), "wr-hit" + i);
            AddCard(x, y, cardW, cardH, accent, "wr" + i, .94f);

            // the outfit wearing itself: the actual idle frame, so the card cannot lie about it
            AddPanel(x + 10, y + 12, 80, 100, new Color(1, 1, 1, .05f), uiRoot, "wr-thumb" + i);
            if (rendered && animatedCharacterSprites.TryGetValue(previewId + "_front_idle_0", out var sp) && sp)
                AddUIImage("wr-prev" + i, sp, x + 12, y + 8, 76, 108, Color.white);

            AddText(o.label, x + 100, y + 34, 16, rendered ? text : muted, FontStyle.Bold, TextAnchor.MiddleLeft);
            AddText(o.blurb, x + 100, y + 58, 9, muted, FontStyle.Bold, TextAnchor.MiddleLeft);

            string state = !rendered ? "COMING SOON" : equipped ? "EQUIPPED" : owned ? "TAP TO WEAR"
                : affordable ? "TAP TO BUY" : "NEED " + (o.price - save.tokens) + " MORE";
            AddText(state, x + 100, y + 114, 9, equipped ? mint : owned ? gold : affordable ? violet : muted,
                FontStyle.Bold, TextAnchor.MiddleLeft);

            if (!owned && rendered) {
                AddChip(x + 196, y + 74, 100, 32, affordable ? violet : muted, "wr-price" + i, .16f);
                AddText(o.price + " TK", x + 246, y + 90, 13, affordable ? violet : muted, FontStyle.Bold);
            } else if (equipped) {
                AddChip(x + 196, y + 74, 100, 32, mint, "wr-eq" + i, .16f);
                AddText("WEARING", x + 246, y + 90, 11, mint, FontStyle.Bold);
            }
        }
        int rows = (outfitCatalogue.Count + 1) / 2;
        AddIconButton("BACK", "ic_back", px, top + rows * (cardH + rowGap) + 14, cardW * 2 + gap, 74, mint, ShowMenu, "wr-back");
    }

    // One tap does the obvious thing: buy it if you can, otherwise wear it.
    void TapOutfit(int idx)
    {
        BuildOutfitCatalogue();
        if (idx < 0 || idx >= outfitCatalogue.Count) return;
        var o = outfitCatalogue[idx];
        if (o.id == "player") { SetEquipped(wardrobeRole, wardrobeRole); Persist(); PlayEventSound("place"); DrawWardrobe(); return; }
        if (!OwnsOutfit(o.id)) {
            if (save.tokens < o.price) { SetMessage("Not enough tokens", 1.4f); DrawWardrobe(); return; }
            save.tokens -= o.price;
            save.outfits = string.IsNullOrEmpty(save.outfits) ? o.id : save.outfits + "," + o.id;
            PlayEventSound("coin");
        } else PlayEventSound("place");
        SetEquipped(wardrobeRole, o.id);
        Persist();
        DrawWardrobe();
    }

    IEnumerable<string> AnimatedCharacterNames()
    {
        string[] dirs = { "front", "back", "left", "right" };
        // Outfits share the player's state/frame layout exactly, so they extend the same loop.
        string[] employees = { "player", "waiter", "cook", "prepper",
                               "skinCrimson", "skinMidnight", "skinMint", "skinGold", "skinNeon",
                               "skinTuxedo", "skinKnight", "skinForeman", "skinFire" };
        foreach (var baseName in employees) {
            foreach (var dir in dirs) {
                for (int i = 0; i < 4; i++) yield return baseName + "_" + dir + "_idle_" + i;
                for (int i = 0; i < 8; i++) yield return baseName + "_" + dir + "_walk_" + i;
                for (int i = 0; i < 8; i++) yield return baseName + "_" + dir + "_act_" + i;
                for (int i = 0; i < 4; i++) yield return baseName + "_" + dir + "_carry_" + i;
                for (int i = 0; i < 8; i++) yield return baseName + "_" + dir + "_carrywalk_" + i;
            }
        }
        for (int customerIndex = 0; customerIndex < 6; customerIndex++) {
            string baseName = "customer" + customerIndex;
            foreach (var dir in dirs) {
                for (int i = 0; i < 4; i++) yield return baseName + "_" + dir + "_idle_" + i;
                for (int i = 0; i < 8; i++) yield return baseName + "_" + dir + "_walk_" + i;
                for (int i = 0; i < 8; i++) yield return baseName + "_" + dir + "_sitdown_" + i;
                for (int i = 0; i < 2; i++) yield return baseName + "_" + dir + "_sit_" + i;
                for (int i = 0; i < 8; i++) yield return baseName + "_" + dir + "_standup_" + i;
                for (int i = 0; i < 6; i++) yield return baseName + "_" + dir + "_eat_" + i;
            }
        }
    }

    Sprite ResourceSprite(string resourcePath)
    {
        var tex = Resources.Load<Texture2D>(resourcePath);
        if (!tex) return null;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f), 100);
    }

    void BuildRecipes()
    {
        recipes.Clear();
        recipes.Add(new Recipe("basic", "BASIC", "burger", 1, 38, "bun", "patty", "bun"));
        recipes.Add(new Recipe("green", "GREEN", "burger", 2, 50, "bun", "patty", "lettuce", "bun"));
        recipes.Add(new Recipe("fresh", "FRESH", "burger", 3, 62, "bun", "patty", "lettuce", "tomato", "bun"));
        recipes.Add(new Recipe("cheese", "CHEESE", "burger", 4, 66, "bun", "patty", "cheese", "bun"));
        recipes.Add(new Recipe("deluxe", "DELUXE", "burger", 5, 82, "bun", "patty", "cheese", "lettuce", "tomato", "bun"));
        recipes.Add(new Recipe("saucy", "SAUCY", "burger", 6, 90, "bun", "patty", "cheese", "sauce", "bun"));
        recipes.Add(new Recipe("tower", "TOWER", "burger", 8, 112, "bun", "patty", "cheese", "patty", "lettuce", "bun"));
        recipes.Add(new Recipe("margherita", "MARGHERITA", "pizza", 1, 42, "dough", "sauce", "cheese"));
        recipes.Add(new Recipe("garden", "GARDEN", "pizza", 2, 56, "dough", "sauce", "cheese", "lettuce"));
        recipes.Add(new Recipe("rosso", "ROSSO", "pizza", 3, 66, "dough", "sauce", "cheese", "tomato"));
        recipes.Add(new Recipe("supreme", "SUPREME", "pizza", 5, 86, "dough", "sauce", "cheese", "tomato", "lettuce"));
        recipes.Add(new Recipe("espresso", "ESPRESSO", "coffee", 1, 34, "coffee"));
        recipes.Add(new Recipe("latte", "LATTE", "coffee", 2, 48, "coffee", "milk"));
        recipes.Add(new Recipe("sweet_latte", "SWEET LATTE", "coffee", 4, 62, "coffee", "milk", "sauce"));
        recipes.Add(new Recipe("double_shot", "DOUBLE SHOT", "coffee", 7, 88, "coffee", "coffee", "milk"));
        recipes.Add(new Recipe("ricebowl", "RICE BOWL", "bowl", 1, 40, "rice", "patty"));
        recipes.Add(new Recipe("greenbowl", "GREEN BOWL", "bowl", 2, 54, "rice", "patty", "lettuce"));
        recipes.Add(new Recipe("gardenbowl", "GARDEN BOWL", "bowl", 3, 62, "rice", "lettuce", "tomato", "onion"));
        recipes.Add(new Recipe("cheddarbowl", "CHEDDAR BOWL", "bowl", 4, 70, "rice", "patty", "cheese"));
        recipes.Add(new Recipe("fiesta", "FIESTA BOWL", "bowl", 6, 92, "rice", "patty", "cheese", "tomato", "onion"));
        recipes.Add(new Recipe("classic", "CLASSIC DOG", "hotdog", 1, 40, "bun", "sausage"));
        recipes.Add(new Recipe("cheesy", "CHEESY DOG", "hotdog", 3, 62, "bun", "sausage", "cheese"));
        recipes.Add(new Recipe("loaded", "LOADED DOG", "hotdog", 5, 88, "bun", "sausage", "onion", "sauce"));
    }

    void EnsureRoots()
    {
        if (!worldRoot) worldRoot = new GameObject("World");
        if (!staticRoot) staticRoot = new GameObject("StaticWorld");
        if (!propRoot) propRoot = new GameObject("Props3D");   // persistent: pooled 3D meshes (not torn down each rebuild)
        if (!FindAnyObjectByType<EventSystem>()) {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
        if (!canvas) {
            canvas = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(W, H);
            scaler.matchWidthOrHeight = 0f;   // MATCH WIDTH: the 720-wide portrait UI is never clipped on tall phones (#19)
            uiRoot = canvas.GetComponent<RectTransform>();
        }
    }

    void EnsureAudio()
    {
        audioSource = GetComponent<AudioSource>();
        if (!audioSource) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = .82f;
        musicA = gameObject.AddComponent<AudioSource>();
        musicB = gameObject.AddComponent<AudioSource>();
        foreach (var m in new[] { musicA, musicB }) {
            m.playOnAwake = false; m.loop = true; m.spatialBlend = 0f; m.volume = 0f;
        }
    }

    // ---- music: one looping track per game state, crossfaded (#23) ----
    // Tracks live in Resources/Music (rendered by Tools/music_gen.py). A missing clip is not an
    // error: the game runs silent rather than throwing, so the build never depends on the assets.
    void SetMusic(string track)
    {
        if (musicA == null || track == musicTrack) return;
        var clip = Resources.Load<AudioClip>("Music/" + track);
        if (!clip) return;
        // hand the outgoing track to B so A can take the new one; fade runs in UpdateMusic
        var tmp = musicA; musicA = musicB; musicB = tmp;
        musicA.clip = clip;
        musicA.volume = 0f;
        musicA.time = 0f;
        musicA.Play();
        musicTrack = track;
        musicFade = 0f;
    }

    void UpdateMusic()
    {
        if (musicA == null) return;
        bool on = save == null || save.musicOn;
        if (musicFade < 1f) musicFade = Mathf.Min(1f, musicFade + Time.unscaledDeltaTime / 1.1f);
        float target = on ? MusicVolume : 0f;
        musicA.volume = target * musicFade;
        musicB.volume = target * (1f - musicFade);
        if (musicFade >= 1f && musicB.isPlaying) musicB.Stop();
        if (!on && musicA.isPlaying) musicA.Pause();
        else if (on && musicA.clip && !musicA.isPlaying) musicA.UnPause();
    }

    // which track the current game state wants — called once per frame so a rush starting
    // mid-shift swaps the music without any explicit call at the rush site
    string DesiredTrack()
    {
        switch (screen) {
            case ScreenMode.Play: return rushActive ? "rush" : "service";
            case ScreenMode.Result: return "result";
            default: return "menu";
        }
    }

    void ClearWorld()
    {
        for (int i = worldRoot.transform.childCount - 1; i >= 0; i--) {
            var child = worldRoot.transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }

    void ClearUI()
    {
        for (int i = uiRoot.childCount - 1; i >= 0; i--) {
            var child = uiRoot.GetChild(i).gameObject;
            if (child == touchRoot) continue;   // persistent touch overlay survives rebuilds
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }

    Sprite MakeCircleSprite(int size)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float r = size * .5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++) {
                float d = Mathf.Sqrt((x - r + .5f) * (x - r + .5f) + (y - r + .5f) * (y - r + .5f)) / r;
                float a = Mathf.Clamp01((1f - d) / .06f);
                t.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        t.Apply();
        return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
    }

    void EnsureTouchControls()
    {
        if (touchRoot || uiRoot == null || circleSprite == null || uiFont == null) return;
        touchRoot = new GameObject("touch-controls", typeof(RectTransform));
        touchRoot.transform.SetParent(uiRoot, false);
        var trt = touchRoot.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        // Both pads sit at the BOTTOM of the frame (y is measured from the top; H is 1280, the pads
        // are 190/180 tall, so ~1050 leaves a 40px margin clear of the home indicator). They used to
        // float around 72% down, which put them right over the kitchen you were trying to watch.
        joyBaseGo = MakeTouchImage("joy-base", new Color(1, 1, 1, .12f), 44, 1050, 190, 190);
        joyKnobGo = MakeTouchImage("joy-knob", new Color(1, 1, 1, .34f), 109, 1115, 60, 60);
        joyBaseCenter = joyKnobGo.GetComponent<RectTransform>().anchoredPosition;
        actBtnGo = MakeTouchImage("act-btn", new Color(gold.r, gold.g, gold.b, .24f), 486, 1060, 180, 180);
        var label = new GameObject("act-label", typeof(Text));
        label.transform.SetParent(touchRoot.transform, false);
        var lt = label.GetComponent<Text>();
        lt.font = uiFont; lt.text = "ACT"; lt.fontSize = 30; lt.fontStyle = FontStyle.Bold;
        lt.alignment = TextAnchor.MiddleCenter; lt.color = new Color(1, 1, 1, .82f); lt.raycastTarget = false;
        Place(label.GetComponent<RectTransform>(), 486, 1130, 180, 40);
        actLabelGo = label;
        // zoom +/- buttons (persistent so their onClick survives the per-tick UI rebuild), right edge
        MakeZoomButton("zoom-in", "+", 636, 300, () => SetZoom(camZoom + .18f));
        MakeZoomButton("zoom-out", "-", 636, 366, () => SetZoom(camZoom - .18f));
        touchRoot.SetActive(false);
    }

    void MakeZoomButton(string name, string glyph, int x, int y, Action action)
    {
        var go = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(touchRoot.transform, false);
        var img = go.GetComponent<Image>();
        img.sprite = circleSprite; img.color = new Color(.05f, .06f, .09f, .62f); img.raycastTarget = true;
        Place(go.GetComponent<RectTransform>(), x, y, 58, 58);
        go.GetComponent<Button>().onClick.AddListener(() => action?.Invoke());
        var lg = new GameObject(name + "-g", typeof(Text));
        lg.transform.SetParent(go.transform, false);
        var t = lg.GetComponent<Text>();
        t.font = uiFont; t.text = glyph; t.fontSize = 40; t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter; t.color = new Color(1, 1, 1, .9f); t.raycastTarget = false;
        var rt = lg.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    GameObject MakeTouchImage(string name, Color color, int x, int y, int w, int h)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(touchRoot.transform, false);
        var img = go.GetComponent<Image>();
        img.sprite = circleSprite; img.color = color; img.raycastTarget = false;
        Place(go.GetComponent<RectTransform>(), x, y, w, h);
        return go;
    }

    void UpdateTouchControls()
    {
        EnsureTouchControls();
        if (!touchRoot) return;
        bool play = screen == ScreenMode.Play;
        if (touchRoot.activeSelf != play) touchRoot.SetActive(play);
        // The stick and ACT are optional (Settings). The zoom buttons share this root and must stay,
        // so hide the pads individually rather than the whole root.
        bool pads = save == null || save.touchButtons;
        if (joyBaseGo && joyBaseGo.activeSelf != pads) joyBaseGo.SetActive(pads);
        if (joyKnobGo && joyKnobGo.activeSelf != pads) joyKnobGo.SetActive(pads);
        if (actBtnGo && actBtnGo.activeSelf != pads) actBtnGo.SetActive(pads);
        if (actLabelGo && actLabelGo.activeSelf != pads) actLabelGo.SetActive(pads);
        if (!play) { joyMove = Vector2.zero; joyFinger = -1; actHeld = false; return; }
        if (paused) { joyMove = Vector2.zero; joyFinger = -1; actHeld = false; return; }   // pause overlay swallows gameplay taps

        joyMove = Vector2.zero;
        bool actNow = false;
        // the band must COVER the drawn joystick/ACT art (its top reaches ~28% up) or touches on the
        // upper half of the visible controls were dead AND leaked through as walk commands. Kept in
        // lockstep with the tap-to-walk gate in UpdatePlay.
        float band = Screen.height * TouchBand;
        float midX = Screen.width * .5f;
        float radius = Screen.height * .10f;

        if (Input.touchCount > 0) {
            // adopt a joystick finger anywhere in the bottom-left band (not just on Began, so a resting finger can take over)
            if (joyFinger < 0) {
                foreach (var tch in Input.touches) {
                    Vector2 sp = tch.position;
                    if (sp.y < band && sp.x < midX && tch.phase != TouchPhase.Ended && tch.phase != TouchPhase.Canceled) {
                        joyFinger = tch.fingerId; joyOrigin = sp; break;
                    }
                }
            }
            bool found = false;
            foreach (var tch in Input.touches) {
                Vector2 sp = tch.position;
                if (tch.fingerId == joyFinger) {          // steer the joystick with its own finger
                    found = true;
                    if (tch.phase == TouchPhase.Ended || tch.phase == TouchPhase.Canceled) joyFinger = -1;
                    else joyMove = Vector2.ClampMagnitude((sp - joyOrigin) / radius, 1f);
                    continue;                             // never let the joystick finger also fire ACT
                }
                if (sp.y < band && sp.x >= midX && tch.phase != TouchPhase.Ended && tch.phase != TouchPhase.Canceled) actNow = true;
            }
            if (joyFinger >= 0 && !found) joyFinger = -1;
            mouseJoyActive = false;
        } else if (Input.GetMouseButton(0)) {
            Vector2 sp = Input.mousePosition;
            if (sp.y < band && sp.x < midX) {
                if (!mouseJoyActive) { joyOrigin = sp; mouseJoyActive = true; }   // re-seed origin whenever the mouse enters the joystick zone
                joyMove = Vector2.ClampMagnitude((sp - joyOrigin) / radius, 1f);
            } else {
                mouseJoyActive = false;
                if (sp.y < band && sp.x >= midX) actNow = true;
            }
        } else mouseJoyActive = false;

        // ACT: press-hold = hold action (prep/wash), quick tap = interact nearest
        if (actNow && !actHeld) actDownTime = Time.time;
        if (!actNow && actHeld && Time.time - actDownTime < .26f && holdProgress <= .03f) InteractNearest();
        actHeld = actNow;

        if (joyKnobGo) {
            joyKnobGo.GetComponent<RectTransform>().anchoredPosition = joyBaseCenter + joyMove * 62f;
            joyKnobGo.GetComponent<Image>().color = new Color(1, 1, 1, joyMove.sqrMagnitude > .01f ? .5f : .32f);
        }
        if (actBtnGo) actBtnGo.GetComponent<Image>().color = new Color(gold.r, gold.g, gold.b, actHeld ? .46f : .24f);
    }

    void ShowMenu()
    {
        screen = ScreenMode.Menu;
        paused = false;
        pendingPerks.Clear();   // leaving the perk screen without choosing forfeits it (no stale resurface)
        ClearWorld();
        ClearStaticWorld();
        TrimPropPool();
        ClearUI();
        AddPanel(0, 0, W, H, new Color(.006f, .008f, .012f, 1), uiRoot, "menu-bg");
        AddMenuImage("menu-panel", "menu_panel", 34, 28, 652, 1152, new Color(1, 1, 1, .98f), false);
        AddMenuImage("menu-hero", "menu_hero", 64, 50, 592, 246, Color.white, false);
        AddPanel(64, 238, 592, 58, new Color(.01f, .012f, .016f, .44f), uiRoot, "menu-hero-fade");
        AddMenuImage("menu-title-plate", "title_plaque", 68, 314, 584, 106, Color.white, false);
        AddText("RUSHHOUSE", 100, 348, 31, gold, FontStyle.Bold, TextAnchor.MiddleLeft);
        AddText("DAY " + save.day, 580, 342, 19, gold, FontStyle.Bold, TextAnchor.MiddleRight);
        AddText(save.coins + " COINS", 580, 376, 14, mint, FontStyle.Bold, TextAnchor.MiddleRight);
        AddText("REP " + save.reputation + "   BEST D" + save.bestDay, 102, 386, 11, muted, FontStyle.Bold, TextAnchor.MiddleLeft);
        DrawStarPips(102, 398, 14);   // restaurant star level (drives customer flow)
        if (OwnedPerkCount() > 0) {
            AddText("PERKS " + OwnedPerkCount() + "/" + allPerks.Count, 580, 404, 11, violet, FontStyle.Bold, TextAnchor.MiddleRight);
            AddText(PerkSummary(), 360, 420, 9, violet, FontStyle.Bold, TextAnchor.MiddleCenter);
        }
        if (messageTimer > 0) AddText(message, 360, 430, 13, text, FontStyle.Bold);

        AddImageButton("OPEN SHIFT " + save.day, "primary_button", 72, 452, 276, 74, StartDay, "menu-open", 16, text);
        AddImageButton("FLOORPLAN", "secondary_button", 372, 452, 276, 74, ShowLayout, "menu-floorplan", 16, text);
        // five equal slots across the 72..648 content band (104 wide, 14 apart)
        AddImageButton("RECIPES", "tab_gold", 72, 540, 104, 54, ShowRecipes, "menu-recipes", 12, text);
        AddImageButton(save.theme.ToUpperInvariant(), "tab_dark", 190, 540, 104, 54, CycleTheme, "menu-theme", 12, text);
        AddImageButton("STYLE", "tab_gold", 308, 540, 104, 54, ShowWardrobe, "menu-wardrobe", 12, text);
        AddImageButton("AUDIO", "tab_dark", 426, 540, 104, 54, DrawSettingsOverlay, "menu-audio", 12, text);
        AddImageButton("RESET", "danger_button", 544, 540, 104, 54, ResetSave, "menu-reset", 12, text);

        AddMenuImage("studio-frame", "shop_panel", 58, 626, 604, 430, Color.white, false);
        // studio interior shares one symmetric content band [106..614] (centre 360)
        AddText("STUDIO", 106, 666, 23, text, FontStyle.Bold, TextAnchor.MiddleLeft);
        AddText("UPGRADES & STAFF", 614, 670, 10, gold, FontStyle.Bold, TextAnchor.MiddleRight);
        // NOTE: these MUST NOT be named "shop-*" — BuildShop() wipes every child whose name starts
        // with "shop-", which was destroying the tabs themselves (so the shop was stuck on one tab
        // and EQUIPMENT / STAFF were unreachable).
        BuildStudioTabs();
        AddMenuImage("studio-divider", "divider_teal", 106, 1070, 508, 18, Color.white, false);
        BuildShop(shopTab);
        AnimateUIIn("menu");
    }

    // big, obvious, filled tabs — the old thin ones were invisible AND got wiped by BuildShop
    void BuildStudioTabs()
    {
        foreach (Transform child in uiRoot) {
            if (!child.name.StartsWith("studio-tab")) continue;
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }
        string[] ids = { "upgrades", "equipment", "staff" };
        string[] labels = { "UPGRADES", "EQUIPMENT", "STAFF" };
        int[] xs = { 100, 276, 452 };
        for (int i = 0; i < 3; i++) {
            string id = ids[i];
            bool on = shopTab == id;
            AddPanel(xs[i], 698, 168, 56, on ? new Color(.13f, .55f, .52f, 1) : new Color(.09f, .11f, .15f, 1), uiRoot, "studio-tab-bg");
            AddImageButton(labels[i], on ? "primary_button" : "secondary_button", xs[i], 698, 168, 56,
                           () => { BuildShop(id); BuildStudioTabs(); }, "studio-tab", 14, on ? Color.white : muted);
            if (on) AddPanel(xs[i] + 12, 750, 144, 5, gold, uiRoot, "studio-tab-underline");   // active indicator
        }
    }

    void BuildShop(string tab)
    {
        shopTab = tab;
        foreach (Transform child in uiRoot) {
            if (!child.name.StartsWith("shop-")) continue;
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }
        // two-column card grid centred in the [106..614] band (was left-shifted to 86/354)
        int leftX = 106;
        int rightX = 376;
        AddText(tab.ToUpperInvariant(), 360, 782, 12, tab == "staff" ? green : tab == "equipment" ? blue : gold, FontStyle.Bold, TextAnchor.MiddleCenter, "shop-caption");
        if (tab == "upgrades") {
            AddShopCard("SHOES", "move +" + save.speed, Cost(90, save.speed), leftX, 804, mint, () => BuyUpgrade("speed"));
            AddShopCard("HOB", "cook +" + save.grill, Cost(110, save.grill), rightX, 804, red, () => BuyUpgrade("grill"));
            AddShopCard("PREP", "prep +" + save.prepUpgrade, Cost(85, save.prepUpgrade), leftX, 864, green, () => BuyUpgrade("prep"));
            AddShopCard("COMFORT", "patience +" + save.patience, Cost(100, save.patience), rightX, 864, blue, () => BuyUpgrade("patience"));
            AddShopCard("WATER", "wash +" + save.sinkUpgrade, Cost(95, save.sinkUpgrade), leftX, 924, mint, () => BuyUpgrade("sink"));
            AddShopCard("BAY", "space +" + save.room, Cost(220, save.room), rightX, 924, gold, () => BuyUpgrade("room"));
            AddShopCard("DECOR", "tips +" + save.decor, Cost(130, save.decor), leftX, 984, violet, () => BuyUpgrade("decor"));
            AddShopCard("ADS", "flow +" + save.marketing, Cost(140, save.marketing), rightX, 984, green, () => BuyUpgrade("marketing"));
        } else if (tab == "equipment") {
            AddShopCard("COUNTER", $"floor {Math.Min(OwnedCount("counter"), MaxOwned("counter"))}/{MaxOwned("counter")}", EquipmentCost("counter"), leftX, 804, violet, () => BuyEquipment("counter"), OwnedCount("counter") >= MaxOwned("counter"));
            AddShopCard("TABLE", $"floor {Math.Min(save.table, MaxOwned("table"))}/{MaxOwned("table")}", EquipmentCost("table"), rightX, 804, gold, () => BuyEquipment("table"), save.table >= MaxOwned("table"));
            AddShopCard("HOB", $"floor {Math.Min(save.hob, MaxOwned("hob"))}/{MaxOwned("hob")}", EquipmentCost("hob"), leftX, 864, red, () => BuyEquipment("hob"), save.hob >= MaxOwned("hob"));
            AddShopCard("SINK", $"owned {save.sink}/{MaxOwned("sink")}", EquipmentCost("sink"), rightX, 864, blue, () => BuyEquipment("sink"), save.sink >= MaxOwned("sink"));
            AddShopCard("DRINK", $"owned {save.drink}/{MaxOwned("drink")}", EquipmentCost("drink"), leftX, 924, mint, () => BuyEquipment("drink"), save.drink >= MaxOwned("drink"));
            if (save.theme == "pizza")
                AddShopCard("OVEN", $"owned {save.oven}/{MaxOwned("oven")}", EquipmentCost("oven"), rightX, 924, red, () => BuyEquipment("oven"), save.oven >= MaxOwned("oven"));
            else if (save.theme == "coffee")
                AddShopCard("ESPRESSO", $"owned {save.espresso}/2", EquipmentCost("espresso"), rightX, 924, violet, () => BuyEquipment("espresso"), save.espresso >= 2);
        } else {
            AddShopCard("WAITER", "takes orders + serves", StaffCost(save.waiter), leftX, 804, gold, () => BuyStaff("waiter"), save.waiter >= 3);
            AddShopCard("COOK", "works the hobs", StaffCost(save.cook), leftX, 864, red, () => BuyStaff("cook"), save.cook >= 3);
            AddShopCard("WASHER", "clears + washes", StaffCost(save.washer), leftX, 924, blue, () => BuyStaff("washer"), save.washer >= 3);
            AddShopCard("PREPPER", "chops + plates", StaffCost(save.prepper), rightX, 804, green, () => BuyStaff("prepper"), save.prepper >= 3);
        }
    }

    void AddShopCard(string title, string desc, int cost, int x, int y, Color stroke, Action action, bool soldOut = false)
    {
        const int cardW = 238, cardH = 60;
        bool broke = !soldOut && save.coins < cost;
        // hit area first, card art on top (so labels are never painted over); an unaffordable card is
        // inert too — its red price is the feedback (#37)
        if (!soldOut && !broke) AddButton("", x, y, cardW, cardH, stroke, action, "shop-card");
        AddCard(x, y, cardW, cardH, soldOut ? muted : broke ? new Color(.5f, .5f, .55f) : stroke, "shop" + title, .94f);
        Color iconTint = soldOut ? new Color(1, 1, 1, .3f) : broke ? new Color(1, 1, 1, .55f) : Color.white;
        AddPanel(x + 10, y + 12, 40, 40, new Color(1, 1, 1, .06f), uiRoot, "shop-icon-bg");
        AddUIImage("shop-icon-" + title, ShopIconSprite(title), x + 12, y + 14, 36, 36, iconTint, true);
        AddText(title, x + 60, y + 24, 14, soldOut ? muted : text, FontStyle.Bold, TextAnchor.MiddleLeft, "shop-title");
        AddText(desc, x + 60, y + 44, 9, muted, FontStyle.Bold, TextAnchor.MiddleLeft, "shop-desc");
        if (soldOut) DrawSoldOutBadge(x, y, cardW, cardH);
        else {
            // price pill so the cost reads at a glance and turns red when unaffordable
            Color pc = broke ? red : gold;
            int pw = 62;
            AddPanel(x + cardW - pw - 10, y + 18, pw, 24, new Color(pc.r, pc.g, pc.b, .18f), uiRoot, "shop-price-bg");
            AddText(cost.ToString(), x + cardW - 10 - pw / 2, y + 30, 13, pc, FontStyle.Bold, TextAnchor.MiddleCenter, "shop-cost");
        }
    }

    // Animated MAX badge stamped across a card you already own out — pulses so a purchase is obvious.
    void DrawSoldOutBadge(int x, int y, int w, int h)
    {
        float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 3.4f);
        AddPanel(x, y, w, h, new Color(.02f, .03f, .05f, .62f), uiRoot, "shop-sold-veil");
        int bh = 26;
        int by = y + h / 2 - bh / 2;
        AddPanel(x + 18, by, w - 36, bh, Color.Lerp(new Color(.10f, .13f, .18f, .95f), red, .28f + pulse * .22f), uiRoot, "shop-sold-bar");
        AddPanel(x + 18, by, w - 36, 2, Color.Lerp(red, Color.white, .35f + pulse * .3f), uiRoot, "shop-sold-line");
        var t = AddText("SOLD OUT", x + w / 2, by + bh / 2, 13, Color.Lerp(Color.white, gold, pulse), FontStyle.Bold, TextAnchor.MiddleCenter, "shop-sold-text");
        t.raycastTarget = false;
    }

    void StartDay()
    {
        screen = ScreenMode.Play;
        camPan = Vector2.zero; camZoom = 1f;   // each shift opens at the default door-to-kitchen framing (#34)
        ClearUI();
        served = 0;
        complaints = 0;
        earned = 0;
        drinksServed = 0;
        wrongOrders = 0;
        queueComplaints = 0;
        missed = 0;
        tips = 0;
        combo = 0;
        bestCombo = 0;
        goalBonus = 0;
        starRating = 0;
        shiftElapsed = 0;
        rushActive = false;
        secondChanceUsed = false;
        tipsDoubled = false;
        queue = 0;
        queueMax = QueueMaxForDay(save.day) + (HasPerk("wide_doors") ? 2 : 0);
        maxShiftTime = Mathf.Clamp(150f + save.room * 12f, 140f, 190f) * (HasPerk("overtime") ? 1.15f : 1f);
        shiftTime = maxShiftTime;
        complaintForgivenUsed = false;
        paused = false;
        fireFailed = false;
        staticDirty = true;              // fresh room backdrop for the day
        goal = GoalForDay(save.day);
        holding = null;
        holdTarget = null;
        holdProgress = 0;
        holdPressed = false;
        tapAction = null;
        hasMoveTarget = false;
        popups.Clear();
        playerPos = SafeOpenPosition(CellCenter(4, 15), .16f);
        spawnTimer = CurrentSpawnDelay();
        customers.Clear();
        GenerateDailyGoals();
        PickDailySpecial();
        BuildLayoutData();
        SpawnWorkers();
        EnsureOpeningCustomer();
        BuildPlayUI();
        RebuildWorld();
        PlayEventSound("start");
    }

    void ShowLayout()
    {
        screen = ScreenMode.Layout;
        camPan = Vector2.zero; camZoom = 1f;   // open the floorplan at the default framing (#34)
        ClearUI();
        BuildLayoutData();
        staticDirty = true;                    // build the floor/walls once on entry, then reuse
        BuildLayoutUI();
        RebuildWorld();
        AnimateUIIn("layout");
    }

    void ShowRecipes()
    {
        screen = ScreenMode.Recipes;
        ClearWorld();
        ClearStaticWorld();
        TrimPropPool();
        ClearUI();
        AddPanel(0, 0, W, H, new Color(.03f, .035f, .05f, 1), uiRoot, "recipes-bg");
        AddText("RECIPES", 360, 82, 40, gold, FontStyle.Bold);
        AddText(save.theme.ToUpperInvariant() + " KITCHEN", 360, 122, 13, mint, FontStyle.Bold);
        var list = recipes.Where(r => r.theme == save.theme).OrderBy(r => r.day).ToList();
        // fill the screen: size rows to the count so the whole card list spans the page, no dead half
        int top = 158, bottom = 1150, rowH = Mathf.Clamp((bottom - top) / Mathf.Max(1, list.Count), 60, 96);
        int y = top;
        foreach (var r in list) {
            bool locked = r.day > save.day;
            Color card = locked ? new Color(.05f, .06f, .08f, 1) : new Color(.09f, .11f, .15f, 1);
            AddPanel(70, y, 580, rowH - 10, card, uiRoot, "recipe");
            AddText(r.label, 116, y + 16, 16, locked ? muted : text, FontStyle.Bold, TextAnchor.MiddleLeft);
            AddText(string.Join(" + ", r.parts).ToUpperInvariant(), 116, y + 38, 11, muted, FontStyle.Bold, TextAnchor.MiddleLeft);
            AddText(locked ? "DAY " + r.day : "+" + r.value, 596, y + 16, 12, locked ? red : gold, FontStyle.Bold, TextAnchor.MiddleRight);
            AddUIImage("recipe-dish-" + r.id, FinalDishSprite(r.id), 590, y + (rowH - 10) / 2 - 20, 44, 44, locked ? new Color(.5f, .5f, .5f, .8f) : Color.white, true);
            y += rowH;
        }
        AddButton("BACK", 92, 1176, 536, 66, blue, ShowMenu);
    }

    void BuildPlayUI()
    {
        ClearUI();
        // transparent blocker over the top HUD band so reading GOALS/tickets/timer never fires a
        // tap-to-walk (MenuHotspot + TicketAt still work — they hit-test on their own, #17)
        var blocker = new GameObject("hud-blocker", typeof(Image));
        blocker.transform.SetParent(uiRoot, false);
        blocker.GetComponent<Image>().color = new Color(0, 0, 0, 0);
        Place(blocker.GetComponent<RectTransform>(), 0, 0, W, 224);
        // ---- modern status bar: one flat surface, big centred clock, stat chips either side ----
        AddPanel(0, 0, W, 96, new Color(.045f, .06f, .085f, .96f), uiRoot, "hud-bar");
        AddPanel(0, 94, W, 2, new Color(1, 1, 1, .08f), uiRoot, "hud-bar-edge");
        // left: day + service progress + stars
        AddText("DAY " + save.day, 26, 30, 20, gold, FontStyle.Bold, TextAnchor.MiddleLeft);
        AddText(served + "/" + goal, 26, 60, 15, mint, FontStyle.Bold, TextAnchor.MiddleLeft);
        AddText("SERVED", 74, 61, 11, muted, FontStyle.Bold, TextAnchor.MiddleLeft);
        DrawStarPips(26, 76, 9);
        // centre: the clock is the primary readout
        Color timeCol = shiftTime <= 30f ? red : shiftTime <= 60f ? gold : text;
        AddText(FormatTime(shiftTime), 360, 36, 30, timeCol, FontStyle.Bold);
        AddText("$" + earned, 360, 68, 15, gold, FontStyle.Bold);
        // right: flow + complaints as compact chips, clear of the MENU button
        bool queueHot = queue >= queueMax - 1 && queue > 0;
        AddChip(392, 18, 116, 28, queueHot ? red : mint, "hud-queue", .16f);
        AddText(queue > 0 ? "QUEUE " + queue + "/" + queueMax : "NEXT " + Mathf.CeilToInt(spawnTimer) + "s", 404, 32, 12, queueHot ? red : mint, FontStyle.Bold, TextAnchor.MiddleLeft);
        AddChip(392, 52, 116, 28, complaints > 0 ? red : muted, "hud-compl", .16f);
        AddText("FAILS " + complaints + "/5", 404, 66, 12, complaints > 0 ? red : muted, FontStyle.Bold, TextAnchor.MiddleLeft);
        if (combo >= 2) {
            Color cc = Color.Lerp(gold, red, Mathf.Clamp01((combo - 2) / 9f));
            AddChip(170, 22, 108, 26, cc, "hud-combo", .2f);
            AddText("COMBO x" + combo, 182, 35, 13, cc, FontStyle.Bold, TextAnchor.MiddleLeft);
        }
        DrawTicketHud();
        DrawGoalHud();
        DrawOrderDetail();
        // MENU is a VISUAL only — MenuHotspot toggles pause on pointer-DOWN. It must NOT also be a
        // Button: its onClick fired on pointer-UP and immediately un-paused ("hold to keep it open").
        AddCard(528, 14, 168, 68, mint, "play-menu", .95f);
        var menuLabel = AddText("MENU", 612, 50, 18, text, FontStyle.Bold);
        menuLabel.raycastTarget = false;
        DrawTutorialHud();
        // day modifiers (special / event / rush) now live inside the GOALS card — see DrawGoalHud
        // suppress the generic HOLD label while the plate-guide is showing NEXT step (avoids overlap)
        if (holding != null && holding.kind != "tool" && !(holding.kind == "plate" && !holding.dirty)) AddText("HOLD " + HoldingLabel(), 360, 886, 11, gold, FontStyle.Bold);
        DrawPlateGuide();
        if (holdProgress > 0 && holdTarget != null) {
            string label = holdTarget.type == "sink" ? "WASH" : "PREP";
            AddText(label + " " + Mathf.CeilToInt(holdProgress * 100) + "%", 360, 902, 12, holdTarget.type == "sink" ? blue : green, FontStyle.Bold);
        }
        if (messageTimer > 0) AddText(message, 360, 1018, 13, text, FontStyle.Bold);
        EnsureTouchControls();
        if (touchRoot) { touchRoot.SetActive(true); touchRoot.transform.SetAsLastSibling(); }
        // Faster and shallower than the menus: this rebuilds mid-shift (opening an order card,
        // closing pause), and a leisurely cascade over the HUD while customers are waiting would be
        // in the way rather than pleasing. The token guard stops it replaying on every rebuild.
        AnimateUIIn("play", .012f, .14f);
    }

    void AddActionStrip()
    {
        AddPanel(132, 912, 220, 72, new Color(.006f, .009f, .014f, .52f), uiRoot, "action-strip-shadow");
        AddPanel(126, 906, 230, 72, new Color(.04f, .052f, .072f, .72f), uiRoot, "action-strip");
        AddPanel(134, 912, 214, 3, new Color(1f, 1f, 1f, .12f), uiRoot, "action-strip-shine");
        AddText(ActionHint(), 242, 942, 12, text, FontStyle.Bold);
    }

    void DrawTutorialHud()
    {
        if (save.day > 2 || served > 2) return;   // keep onboarding through the first few serves / two days
        string hint = TutorialHint();
        if (string.IsNullOrEmpty(hint)) return;
        AddGlassPanel(76, 228, 568, 48, "tutorial", gold, .68f);
        AddText(hint, 360, 252, 12, gold, FontStyle.Bold);
    }

    string TutorialHint()
    {
        var waitingOrder = customers.FirstOrDefault(c => c.seated && !c.ordered);
        if (waitingOrder != null) return "WALK TO THE TABLE, PRESS ACT TO TAKE THE ORDER";
        var active = customers.FirstOrDefault(c => c.ordered && !c.mealServed);
        if (active == null) return customers.Any(c => !c.seated) ? "JOYSTICK = MOVE    ACT = INTERACT" : "WAIT FOR THE FIRST ORDER";
        if (holding == null) return "GO TO THE PLATES, PRESS ACT TO TAKE ONE";
        if (holding.kind == "ingredient") {
            if (NeedsStation(holding)) return "COOK " + holding.id.ToUpperInvariant() + " - PRESS ACT AT THE HOB";
            if (NeedsPrep(holding.id) && holding.state != "ready") return "PUT IT ON A COUNTER, THEN HOLD ACT TO CHOP";
            return "ADD " + holding.id.ToUpperInvariant() + " - PRESS ACT AT A PLATE";
        }
        if (holding.kind == "plate") {
            var need = active.orderParts ?? active.recipe.parts.ToList();
            if (holding.dirty) return "HOLD ACT AT THE SINK TO WASH";
            if (PlateMatchesOrder(holding, active)) return "SERVE - PRESS ACT AT THE TABLE";
            if (holding.parts.Count < need.Count) return "NEXT: " + need[holding.parts.Count].ToUpperInvariant();
        }
        return "";
    }

    void DrawPlateGuide()
    {
        if (holding == null || holding.kind != "plate" || holding.dirty) return;
        string guide = PlateGuideText();
        if (string.IsNullOrEmpty(guide)) return;
        AddPanel(120, 844, 360, 58, new Color(.035f, .045f, .065f, .88f), uiRoot, "plate-guide");
        AddText(guide, 300, 872, 13, gold, FontStyle.Bold);
    }

    string PlateGuideText()
    {
        // match the plate against each guest's ACTUAL order (orderParts), so "NO LETTUCE" / "EXTRA
        // CHEESE" guides correctly instead of stepping through the base recipe.
        var c = customers
            .Where(c => c.ordered && !c.mealServed)
            .FirstOrDefault(c => PartsPrefixMatches(holding, c.orderParts ?? c.recipe.parts.ToList()));
        if (c == null) return "";
        var parts = c.orderParts ?? c.recipe.parts.ToList();
        if (holding.parts.Count >= parts.Count) return "READY: " + c.recipe.label + (string.IsNullOrEmpty(c.orderMod) ? "" : " (" + c.orderMod + ")");
        return "NEXT: " + parts[holding.parts.Count].ToUpperInvariant() + "  (" + c.recipe.label + ")";
    }

    void DrawTicketHud()
    {
        // Order queue as tappable ticket cards: dish thumb, name, a real patience bar. Sized so up to
        // three tickets plus an overflow line always fit inside the card.
        const int tx = 20, tw = 276, rowH = 44;   // stops short of the centre sight-line (x 296..424)
        var allActive = customers.Where(c => c.ordered && !c.served).OrderBy(c => c.patience).ToList();
        var active = allActive.Take(allActive.Count > 3 ? 2 : 3).ToList();
        int bodyH = Mathf.Max(1, active.Count) * rowH + (allActive.Count > active.Count ? 20 : 0);
        AddCard(tx, 104, tw, 40 + bodyH + 10, gold, "tickets", .93f);
        AddText("ORDERS", tx + 18, 130, 13, gold, FontStyle.Bold, TextAnchor.MiddleLeft);
        AddText(allActive.Count.ToString(), tx + tw - 18, 130, 13, muted, FontStyle.Bold, TextAnchor.MiddleRight);
        ticketRows.Clear();
        if (active.Count == 0) {
            AddText("Waiting for orders...", tx + 18, 168, 12, muted, FontStyle.Bold, TextAnchor.MiddleLeft);
            return;
        }
        int y = 148;
        foreach (var c in active) {
            bool low = c.patience < c.maxPatience * .3f;
            Color tone = low ? red : c.typeId == "critic" ? violet : mint;
            ticketRows.Add((new Rect(tx + 8, y, tw - 16, rowH - 4), c.recipe));   // tap zone -> recipe detail
            AddChip(tx + 8, y, tw - 16, rowH - 6, tone, "tk" + y, .1f);
            AddUIImage("ticket-icon-" + c.recipe.id + "-" + y, FinalDishSprite(c.recipe.id), tx + 16, y + 3, 32, 32, Color.white);
            string mark = c.typeId == "critic" ? "! " : specialRecipe != null && c.recipe.id == specialRecipe.id ? "* " : "";
            string line = mark + c.recipe.label + (c.partySize > 1 ? " x" + c.partySize : "") + (c.wantsDrink && !c.drinkServed ? " +DR" : "");
            AddText(line, tx + 56, y + 14, 13, low ? red : text, FontStyle.Bold, TextAnchor.MiddleLeft);
            if (!string.IsNullOrEmpty(c.orderMod))
                AddText(c.orderMod, tx + tw - 24, y + 14, 10, c.orderMod[0] == 'N' ? red : gold, FontStyle.Bold, TextAnchor.MiddleRight);
            int barW = tw - 72;
            AddPanel(tx + 56, y + 27, barW, 6, new Color(1, 1, 1, .1f), uiRoot, "tk-bg" + y);
            AddPanel(tx + 56, y + 27, Mathf.Max(3, (int)(barW * Mathf.Clamp01(c.patience / c.maxPatience))), 6, low ? red : tone, uiRoot, "tk-fill" + y);
            y += rowH;
        }
        if (allActive.Count > active.Count)
            AddText("+" + (allActive.Count - active.Count) + " waiting", tx + 18, y + 8, 11, muted, FontStyle.Bold, TextAnchor.MiddleLeft);
    }

    // Tap a ticket -> a big card showing EXACTLY what that order contains, ingredient by ingredient
    // (e.g. which burger layers), so you can read a complex dish at a glance. Tap anywhere to close.
    void DrawOrderDetail()
    {
        if (detailRecipe == null) return;
        var r = detailRecipe;
        var guest = customers.FirstOrDefault(c => c.ordered && !c.served && c.recipe.id == r.id);
        var parts = guest?.orderParts ?? r.parts.ToList();
        AddPanel(0, 0, W, H, new Color(.02f, .03f, .05f, .78f), uiRoot, "od-veil");
        bool hasMod = guest != null && !string.IsNullOrEmpty(guest.orderMod);

        // Height is DERIVED from the content (header + optional modifier + one row per ingredient +
        // footer), so a 6-ingredient tower fits exactly like a 2-ingredient dog. The old card used a
        // fixed-aspect frame with a hand-guessed height, so long recipes spilled outside it.
        const int cardX = 56, cardW = 608, pad = 22, rowH = 56, rowGap = 8;
        int headerH = 122, footerH = 96, modH = hasMod ? 46 : 0;
        int listH = parts.Count * rowH + Mathf.Max(0, parts.Count - 1) * rowGap;
        int h = pad + headerH + modH + 34 + listH + footerH + pad;
        int top = Mathf.Clamp((H - h) / 2, 96, H - h - 24);
        AddCard(cardX, top, cardW, h, gold, "od");

        // header: dish thumbnail + name + reward, side by side (compact, saves the old hero's height)
        int hx = cardX + pad, hy = top + pad + 8;
        AddPanel(hx, hy, 108, 108, new Color(1, 1, 1, .06f), uiRoot, "od-thumb-bg");
        AddUIImage("od-dish", FinalDishSprite(r.id), hx + 6, hy + 6, 96, 96, Color.white, true);
        AddText(r.label, hx + 130, hy + 34, 27, gold, FontStyle.Bold, TextAnchor.MiddleLeft);
        AddText("+" + r.value + " COINS", hx + 130, hy + 74, 16, mint, FontStyle.Bold, TextAnchor.MiddleLeft);
        if (specialRecipe != null && specialRecipe.id == r.id)
            AddText("DAILY SPECIAL +40%", hx + 130, hy + 98, 12, violet, FontStyle.Bold, TextAnchor.MiddleLeft);

        int y = top + pad + headerH;
        if (hasMod) {
            Color mc = guest.orderMod[0] == 'N' ? red : gold;
            AddChip(cardX + pad, y, cardW - pad * 2, 38, mc, "od-mod", .16f);
            AddText(guest.orderMod, cardX + pad + 18, y + 19, 16, mc, FontStyle.Bold, TextAnchor.MiddleLeft);
            y += modH;
        }
        AddText("CONTAINS", cardX + pad, y + 12, 13, muted, FontStyle.Bold, TextAnchor.MiddleLeft);
        AddText(parts.Count + " ITEMS", cardX + cardW - pad, y + 12, 13, muted, FontStyle.Bold, TextAnchor.MiddleRight);
        AddPanel(cardX + pad, y + 26, cardW - pad * 2, 2, new Color(1, 1, 1, .1f), uiRoot, "od-rule");
        y += 34;

        for (int i = 0; i < parts.Count; i++) {
            string how = NeedsStation(Item.Ingredient(parts[i], "raw")) ? "COOK" : NeedsPrep(parts[i]) ? "CHOP" : "";
            Color tone = how == "COOK" ? red : how == "CHOP" ? green : mint;
            AddChip(cardX + pad, y, cardW - pad * 2, rowH, tone, "od-row" + i, .09f);
            AddUIImage("od-ing-" + i, FoodSprite(FoodSpriteName(parts[i], CarryStateForPart(parts[i]))), cardX + pad + 16, y + 5, 46, 46, Color.white, true);
            AddText((i + 1).ToString(), cardX + pad + 76, y + rowH / 2, 13, muted, FontStyle.Bold, TextAnchor.MiddleLeft);
            AddText(parts[i].ToUpperInvariant(), cardX + pad + 104, y + rowH / 2, 18, text, FontStyle.Bold, TextAnchor.MiddleLeft);
            if (how != "") {
                int bw = 74, bx = cardX + cardW - pad - bw - 12;
                AddPanel(bx, y + 15, bw, 26, new Color(tone.r, tone.g, tone.b, .22f), uiRoot, "od-tag" + i);
                AddText(how, bx + bw / 2, y + 28, 12, tone, FontStyle.Bold);
            }
            y += rowH + rowGap;
        }
        y = top + h - pad - 74;
        AddIconButton("CLOSE", "ic_back", cardX + pad, y, cardW - pad * 2, 68, blue, () => { detailRecipe = null; BuildPlayUI(); }, "od-close");
    }

    // Tap a station (play OR layout) → a floating speech bubble above it with its name + what it does.
    // Drawn in world space (from RebuildWorld) so it sits over the prop like a balloon.
    void DrawStationBubble()
    {
        // Info balloon is a FLOORPLAN-only feature now (the user found it distracting mid-cook). In
        // play you read a station by tapping a TICKET (order detail), not the prop.
        if (screen != ScreenMode.Layout) return;
        if (infoTimer <= 0 || infoAppliance == null || !appliances.Contains(infoAppliance)) return;
        var a = infoAppliance;
        Rect rect = CellRect(a.c, a.r, a.w, a.h);
        Color stroke = ApplianceTagColor(a) == muted ? gold : ApplianceTagColor(a);
        spriteDepthBoost = 12f;   // draw the whole card in FRONT of every prop mesh
        // rich readable balloon: shadow → framed panel art → accent bar → station THUMBNAIL (the
        // pre-3D render of this exact prop) → title / divider / description. Tap empty floor to close.
        float bw = 3.8f, bh = 1.5f;
        Vector2 anchor = new Vector2(rect.center.x, rect.yMax + ApplianceVisualSize(a, rect.size).y * .45f);
        float roomHalf = CellRect(0, 0, Cols, Rows).xMax;
        float bx = Mathf.Clamp(anchor.x, -roomHalf + bw * .55f, roomHalf - bw * .55f);
        Vector2 bc = new Vector2(bx, anchor.y + bh * .5f + .24f);
        MakeRect("sb-sh", bc + new Vector2(.07f, -.09f), new Vector2(bw + .12f, bh + .12f), new Color(0, 0, 0, .38f), 60);
        MakeRect("sb-frame", bc, new Vector2(bw + .06f, bh + .06f), Color.Lerp(stroke, Color.white, .2f), 61);
        var panel = MakeStretch("sb-bg", MenuSprite("shop_panel"), bc, new Vector2(bw, bh), Color.white, 62);
        if (!panel.GetComponent<SpriteRenderer>().sprite) MakeRect("sb-bg2", bc, new Vector2(bw, bh), new Color(.07f, .09f, .13f, .97f), 62);
        MakeRect("sb-accent", bc + new Vector2(0, bh * .5f - .05f), new Vector2(bw, .09f), stroke, 64);
        var tail = MakeRect("sb-tail", anchor + new Vector2(0, .1f), new Vector2(.28f, .28f), Color.Lerp(stroke, Color.white, .2f), 61);
        SetBillboardAngle(tail, 45f);
        // station thumbnail chip on the left
        float iconX = bc.x - bw * .5f + .5f;
        MakeRect("sb-icon-chip", new Vector2(iconX, bc.y - .02f), new Vector2(.78f, .78f), new Color(0, 0, 0, .35f), 63);
        MakeSprite("sb-icon", ObjectSprite(ApplianceSpriteName(a)), new Vector2(iconX, bc.y - .02f), new Vector2(.68f, .68f), Color.white, 64);
        float tx = bc.x + .34f;
        DrawWorldText(StationTitle(a), new Vector2(tx, bc.y + .32f), .032f, stroke, 64);
        MakeRect("sb-div", new Vector2(tx, bc.y + .1f), new Vector2(bw * .5f, .028f), new Color(stroke.r, stroke.g, stroke.b, .45f), 64);
        DrawWorldText(StationDescription(a), new Vector2(tx, bc.y - .22f), .023f, Color.white, 64);
        spriteDepthBoost = 0f;
    }

    void DrawGoalHud()
    {
        // Daily goals as a checklist card: a filled tick for done, a hollow marker for pending.
        // The day's modifiers (special dish, event, rush) ride ALONG THE TOP of this same card.
        // They used to float as centred badges over the room, which put them squarely across the
        // entrance arch — the one thing that must stay readable, since that is where guests appear.
        const int gx = 424, gw = 276, rowH = 30, modH = 26;   // right of the centre sight-line
        var goals = dailyGoals.Take(3).ToList();
        var mods = DayModifiers();
        AddCard(gx, 104, gw, 40 + mods.Count * modH + goals.Count * rowH + 10, mint, "goals", .93f);
        AddText("GOALS", gx + 18, 130, 13, gold, FontStyle.Bold, TextAnchor.MiddleLeft);
        int done = goals.Count(g => g.done);
        AddText(done + "/" + goals.Count, gx + gw - 18, 130, 13, done == goals.Count && goals.Count > 0 ? mint : muted, FontStyle.Bold, TextAnchor.MiddleRight);
        int y = 148;
        foreach (var m in mods) {
            AddChip(gx + 14, y, gw - 28, modH - 4, m.Value, "goal-mod", .15f);
            AddPanel(gx + 14, y, 4, modH - 4, m.Value, uiRoot, "goal-mod-edge");
            AddText(m.Key, gx + 28, y + (modH - 4) / 2, 11, m.Value, FontStyle.Bold, TextAnchor.MiddleLeft);
            y += modH;
        }
        if (mods.Count > 0) y += 2;
        foreach (var g in goals) {
            AddPanel(gx + 18, y + 5, 14, 14, g.done ? mint : new Color(1, 1, 1, .14f), uiRoot, "goal-tick");
            if (g.done) AddPanel(gx + 21, y + 8, 8, 8, new Color(.05f, .07f, .1f, 1f), uiRoot, "goal-tick-in");
            AddText(GoalLine(g), gx + 42, y + 12, 12, g.done ? mint : text, FontStyle.Bold, TextAnchor.MiddleLeft);
            y += rowH;
        }
    }

    // Whatever is unusual about today, label + colour, in the order a player cares about it.
    List<KeyValuePair<string, Color>> DayModifiers()
    {
        var list = new List<KeyValuePair<string, Color>>();
        bool tutorialUp = save.day <= 2 && served <= 2 && !string.IsNullOrEmpty(TutorialHint());
        if (rushActive) list.Add(new KeyValuePair<string, Color>("RUSH HOUR", red));
        string ev = DayEvent();
        if (ev != "") list.Add(new KeyValuePair<string, Color>(DayEventLabel(),
            ev == "vip" || ev == "critic" ? violet : ev == "happy" ? mint : red));
        if (specialRecipe != null && !tutorialUp)
            list.Add(new KeyValuePair<string, Color>("SPECIAL: " + specialRecipe.label.ToUpperInvariant() + "  +40%", violet));
        return list;
    }

    void BuildLayoutUI()
    {
        ClearUI();
        AddCard(20, 14, 680, 124, gold, "layout-hud", .95f);
        AddText("FLOORPLAN", 48, 44, 22, gold, FontStyle.Bold, TextAnchor.MiddleLeft);
        AddChip(496, 28, 196, 40, gold, "layout-bank", .16f);
        AddText(save.coins + " COINS", 676, 51, 14, gold, FontStyle.Bold, TextAnchor.MiddleRight);
        AddText("Drag units to move.  Select one, then:", 48, 80, 10, muted, FontStyle.Bold, TextAnchor.MiddleLeft);
        // control row: icon buttons for the deliberate actions
        AddIconButton("ROTATE", "ic_rotate", 300, 88, 126, 40, gold, RotateSelected, "lay-rotate");
        AddIconButton("SEATS", "ic_seats", 434, 88, 118, 40, violet, CycleSeats, "lay-seats");
        AddIconButton("RESET", "ic_reset", 560, 88, 116, 40, muted, ResetLayout, "lay-reset");
        if (messageTimer > 0) AddText(message, 360, 156, 13, text, FontStyle.Bold);
        BuildLayoutStore();
        AddIconButton("BACK", "ic_back", 80, 1138, 218, 72, blue, ShowMenu, "lay-back");
        AddIconButton("OPEN", "ic_open", 330, 1138, 218, 72, mint, StartDay, "lay-open");
    }

    void BuildLayoutStore()
    {
        AddCard(20, 946, 680, 178, mint, "layout-store", .95f);
        AddText("BUY UNITS", 48, 972, 13, gold, FontStyle.Bold, TextAnchor.MiddleLeft);
        AddLayoutBuyCard("table", 36, 990, gold);
        AddLayoutBuyCard("counter", 260, 990, violet);
        AddLayoutBuyCard("hob", 484, 990, red);
        AddLayoutBuyCard("drink", 36, 1058, green);
        AddLayoutBuyCard("sink", 260, 1058, blue);
        AddLayoutBuyCard("room", 484, 1058, mint);
    }

    void AddLayoutBuyCard(string id, int x, int y, Color stroke)
    {
        const int cardW = 198;
        const int cardH = 58;
        bool isRoom = id == "room";
        bool maxed = isRoom ? save.room >= 2 : OwnedCount(id) >= MaxOwned(id);
        int cost = isRoom ? Cost(220, save.room) : EquipmentCost(id);
        Action action = isRoom ? () => BuyUpgrade("room", true) : () => BuyEquipment(id, true);
        bool broke = !maxed && save.coins < cost;
        // hit area under the card art, matching the studio cards
        if (!maxed && !broke) AddButton("", x, y, cardW, cardH, stroke, action, "layout-buy-" + id);
        AddCard(x, y, cardW, cardH, maxed ? muted : broke ? new Color(.5f, .5f, .55f) : stroke, "lbuy" + id, .94f);
        AddPanel(x + 10, y + 12, 38, 38, new Color(1, 1, 1, .06f), uiRoot, "layout-buy-icon-bg");
        AddUIImage("layout-buy-icon-" + id, LayoutBuySprite(id), x + 12, y + 14, 34, 34, maxed ? new Color(1, 1, 1, .4f) : Color.white);
        AddText(LayoutBuyTitle(id), x + 56, y + 22, 12, maxed ? muted : text, FontStyle.Bold, TextAnchor.MiddleLeft, "layout-buy-title");
        AddText(isRoom ? save.room + "/2" : OwnedCount(id) + "/" + MaxOwned(id), x + 56, y + 42, 9, muted, FontStyle.Bold, TextAnchor.MiddleLeft, "layout-buy-count");
        Color pc = maxed ? muted : broke ? red : gold;
        AddPanel(x + cardW - 62, y + 17, 52, 24, new Color(pc.r, pc.g, pc.b, .18f), uiRoot, "layout-buy-price-bg");
        AddText(maxed ? "MAX" : cost.ToString(), x + cardW - 36, y + 29, 12, pc, FontStyle.Bold, TextAnchor.MiddleCenter, "layout-buy-cost");
    }

    Sprite LayoutBuySprite(string id)
    {
        if (id == "room") return FloorSprite(1, 0);
        return ObjectSprite(id);
    }

    string LayoutBuyTitle(string id)
    {
        if (id == "counter") return "CNTR";
        if (id == "room") return "BAY";
        return id.ToUpperInvariant();
    }

    void RebuildWorld()
    {
        // the floor / walls / backdrop never change during a shift — build them ONCE into staticRoot
        // and only tear down + rebuild the dynamic layer (stations, actors, items) each tick. This
        // kills the per-tick background flicker and makes the rebuild cheap enough to run far more
        // often (smoother worker/customer motion + fire/steam flicker).
        // rebuild the floor/walls ONLY when they actually changed — not on every 80ms layout drag tick
        // (that was re-instantiating ~30 FBX+primitive GameObjects 12×/sec during a table drag).
        if (staticDirty) { BuildStaticWorld(); staticDirty = false; }
        ClearWorld();
        propUsed.Clear();
        DrawExpansionZones();
        DrawMoveTarget();
        DrawKitchenDoor();
        foreach (var a in appliances) DrawAppliance(a);
        DrawFireWarnings();
        orderIcons.Clear();
        foreach (var c in customers) DrawCustomer(c);
        foreach (var w in workers) DrawWorker(w);
        DrawPlayer();
        foreach (var p in popups) DrawPopup(p);
        DrawStationBubble();
        PrunePropPool();
    }

    void BuildStaticWorld()
    {
        ClearStaticWorld();
        drawStatic = true;
        DrawFloor();          // floor tiles + room backdrop/walls + wall shadows + ambience + divider
        drawStatic = false;
    }

    void ClearStaticWorld()
    {
        staticDirty = true;             // whatever redraws the world next must rebuild the floor/walls too
        HideAllProps();                 // 3D meshes live outside staticRoot — hide them when leaving the world
        if (!staticRoot) return;
        for (int i = staticRoot.transform.childCount - 1; i >= 0; i--) {
            var child = staticRoot.transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
    }

    void DrawFloor()
    {
        DrawFloorAndWalls3D();
        DrawAmbience();
    }

    // The old room was a stack of screen-facing sprites. These slabs and boxes establish a real XZ
    // floor plane, receive shadows, and give the room physical wall/divider height.
    void DrawFloorAndWalls3D()
    {
        Rect all = CellRect(0, 0, Cols, Rows);
        Rect dining = CellRect(0, 0, Cols, KitchenRow);
        Rect kitchen = CellRect(0, KitchenRow, Cols, Rows - KitchenRow);
        MakeBox3D("grass-3d", all.center, new Vector2(all.width + 3.2f, all.height + 3.2f), .08f,
                  WorldMaterial("grass-3d", null, new Color(.34f, .5f, .29f), Vector2.one), -.14f, false);
        // roomier tiling: ~half the repeats so each floor square reads bigger
        MakeBox3D("dining-floor", dining.center, new Vector2(dining.width, dining.height), .09f,
                  WorldMaterial("dining-floor", Resources.Load<Texture2D>("Art/Floors/wood"), new Color(.72f, .55f, .35f), new Vector2(5f, 5f)), -.055f, false);
        MakeBox3D("kitchen-floor", kitchen.center, new Vector2(kitchen.width, kitchen.height), .09f,
                  WorldMaterial("kitchen-floor", Resources.Load<Texture2D>("Art/Floors/tile"), new Color(.74f, .79f, .8f), new Vector2(5f, 3f)), -.055f, false);

        // Build the room from the supplied modular wall kit.  The former version clamped every wall
        // mesh to prop-sized heights, leaving tiny windows/doors perched on large brown placeholder
        // blocks.  These thin backing strips only seal seams; the authored modules are the walls.
        Material wallBody = WorldMaterial("wall-shell", null, new Color(.46f, .31f, .19f), Vector2.one);
        Material kitchenWallBody = WorldMaterial("kitchen-wall-shell", null, new Color(.34f, .23f, .16f), Vector2.one);
        MakeBox3D("wall-seam-left", new Vector2(all.xMin - .14f, all.center.y), new Vector2(.28f, all.height + .32f), .34f, wallBody, .17f, false);
        MakeBox3D("wall-seam-right", new Vector2(all.xMax + .14f, all.center.y), new Vector2(.28f, all.height + .32f), .34f, wallBody, .17f, false);
        MakeBox3D("wall-seam-back", new Vector2(0f, all.yMax + .14f), new Vector2(all.width + .4f, .28f), .36f, wallBody, .18f, false);

        // fewer, longer wall spans: 10 short modules per side repeated their end-posts and read as a
        // row of bamboo poles; 4 long spans read as continuous walls
        const int sideCount = 4;
        float sideSegment = all.height / sideCount;
        for (int i = 0; i < sideCount; i++) {
            float y = all.yMin + (i + .5f) * sideSegment;
            string sideFolder = i == 0 ? "kitchenwall" : "walls";
            MakeArchitecturalModel("wall-left-mesh-" + i, sideFolder, new Vector2(all.xMin - .12f, y), sideSegment + .08f, .3f, 1.0f, 90f);
            MakeArchitecturalModel("wall-right-mesh-" + i, sideFolder, new Vector2(all.xMax + .12f, y), sideSegment + .08f, .3f, 1.0f, 90f);
        }

        float cornerSpan = .78f;
        float backPiece = (all.width - cornerSpan * 2f) / 5f;
        float backY = all.yMax + .12f;
        // ONE window type on the back wall (the square one) — mixing two styles looked off
        string[] backModules = { "walls", "brightwindow", "gatewayententrence", "brightwindow", "walls" };
        for (int i = 0; i < backModules.Length; i++) {
            float x = all.xMin + cornerSpan + (i + .5f) * backPiece;
            MakeArchitecturalModel("back-module-" + i, backModules[i], new Vector2(x, backY), backPiece + .025f, .28f, 1.05f, 0f);
        }
        // Corners: the "cornerwall" asset never read as a corner from this angle (it perched above the
        // wall line like a floating post). A plain column in the wall's own material seals the join
        // cleanly and always faces correctly.
        // Use the cornerwall MODEL from the kit, not a plain coloured box. The old code fell back to
        // a box because the asset "perched above the wall line like a floating post" -- but that was
        // a fit problem, not the asset's fault: MakeArchitecturalModel grounds to the mesh's own
        // min.y, so a corner squeezed into a narrow span reads as a post. Given the real span it has
        // to fill, and the same 1.05 height as the back modules, it sits in the wall line properly.
        //
        // The span matters: the back modules only begin at all.xMin + cornerSpan, and the previous
        // 0.42-wide column reached just xMin+0.19, leaving a 0.58-wide hole at full wall height at
        // BOTH ends of the storefront. That hole -- plus a pale corner-cap slab floating above the
        // wall tops -- is what read as the broken wall either side of the entrance.
        float cornerW = cornerSpan + .3f;
        for (int s = 0; s < 2; s++) {
            float cxp = s == 0 ? all.xMin + cornerSpan * .5f - .12f : all.xMax - cornerSpan * .5f + .12f;
            MakeArchitecturalModel("corner-wall-" + s, "cornerwall", new Vector2(cxp, backY),
                                   cornerW, .3f, 1.05f, 0f);
        }
        MakeArchitecturalModel("entrance-gate", "gate", new Vector2(0f, backY - .035f), backPiece * .72f, .14f, .9f, 0f);
        var pictureFrame = MakeArchitecturalModel("back-picture-frame", "frame",
            new Vector2(all.xMin + cornerSpan + backPiece * .5f, backY - .17f), .72f, .07f, .58f, 0f);
        if (pictureFrame) pictureFrame.transform.position += Vector3.up * .43f;
        MakeBox3D("door-mat-3d", new Vector2(0f, all.yMax - .34f), new Vector2(1.0f, .32f), .018f,
                  WorldMaterial("door-mat-3d", null, new Color(.42f, .09f, .1f), Vector2.one), .009f, false);

        float gap = DoorHalfGap, divY = DividerY(), half = all.width * .5f;
        float segW = half - gap;
        MakeBox3D("divider-seam-left", new Vector2(-(gap + segW * .5f), divY), new Vector2(segW, .22f), .3f, kitchenWallBody, .15f, false);
        MakeBox3D("divider-seam-right", new Vector2(gap + segW * .5f, divY), new Vector2(segW, .22f), .3f, kitchenWallBody, .15f, false);
        MakeArchitecturalModel("divider-left-mesh", "kitchenwall", new Vector2(-(gap + segW * .5f), divY), segW, .28f, .72f, 0f);
        MakeArchitecturalModel("divider-right-mesh", "kitchenwall", new Vector2(gap + segW * .5f, divY), segW, .28f, .72f, 0f);
    }

    // Half-width of the divider doorway. Was 1.0, which left only the two cell centres at x=+-0.28
    // walkable for EVERY actor radius in use -- the whole restaurant had to thread a two-cell needle
    // with effectively one route through it, so any contention or slightly off-centre approach
    // jammed. Cell centres sit at +-0.28 and +-0.84; 1.12 clears +-0.84 with 0.08-0.12 to spare at
    // radius 0.15-0.16, doubling the doorway's routing width. The drawn jambs and leaves are derived
    // from this constant, so the art follows automatically.
    const float DoorHalfGap = 1.12f;
    float DividerY() => CellCenter(0, DivRow).y;

    // soft shadow the 3 walls cast onto the floor edges — layered strips fake a gradient falloff
    void DrawWallShadows()
    {
        Rect all = CellRect(0, 0, Cols, Rows);
        float L = all.xMin, Rr = all.xMax, T = all.yMax, cy = all.center.y;
        float[] a = { .30f, .17f, .09f };
        float[] w = { .13f, .26f, .44f };
        for (int i = 0; i < 3; i++) {
            MakeBar("wsh-t" + i, new Vector2(0, T - w[i]), new Vector2(all.width, w[i] * 2), new Color(0, 0, 0, a[i]), -15);
            MakeBar("wsh-l" + i, new Vector2(L + w[i], cy), new Vector2(w[i] * 2, all.height), new Color(0, 0, 0, a[i]), -15);
            MakeBar("wsh-r" + i, new Vector2(Rr - w[i], cy), new Vector2(w[i] * 2, all.height), new Color(0, 0, 0, a[i]), -15);
        }
    }

    void DrawRoomBackdrop()
    {
        Rect all = CellRect(0, 0, Cols, Rows);
        float Rr = all.xMax, T = all.yMax;
        float ext = 4.3f, wallH = 1.2f;
        Color grass = new Color(.36f, .5f, .30f, 1);               // green outside, like the reference
        Color faceD = new Color(.44f, .38f, .32f, 1), faceM = new Color(.60f, .54f, .46f, 1), faceHi = new Color(.77f, .70f, .60f, 1);
        Color rail = new Color(.92f, .86f, .76f, 1);
        Color baseSh = new Color(.20f, .16f, .12f, .72f);
        float sideW = ext - Rr, hh = all.height + wallH * 2, cy = all.center.y;
        MakeBar("room-ext", new Vector2(0, cy), new Vector2(ext * 2, all.height + wallH * 4 + 8f), grass, -34);
        // ANGLED (isometric) walls: pre-baked panels whose top edge recedes — taller far, shorter near —
        // so the side walls read as leaning away instead of flat vertical strips. See Tools/wall_gen.py.
        if (objectSprites.ContainsKey("wallSideL")) {
            // Solid walls hugging the room edge; the back wall spans the full outer width so the
            // side↔back junction is physically covered, then a corner pilaster caps each top corner.
            float sideThick = .74f, backThick = 1.5f;
            float sOuter = Rr + sideThick, sCx = Rr + sideThick * .5f;
            float sBoxBot = all.yMin - .14f, sBoxTop = T + backThick * .4f;   // run the sides UP into the back wall so they connect
            float sideH = sBoxTop - sBoxBot, sCy = (sBoxTop + sBoxBot) * .5f;
            float backCy = T + backThick * .5f;
            MakeStretch("wall-left", ObjectSprite("wallSideL"), new Vector2(-sCx, sCy), new Vector2(sideThick, sideH), Color.white, -33);
            MakeStretch("wall-right", ObjectSprite("wallSideR"), new Vector2(sCx, sCy), new Vector2(sideThick, sideH), Color.white, -33);
            MakeStretch("wall-back", ObjectSprite("wallBackH"), new Vector2(0, backCy), new Vector2(sOuter * 2, backThick), Color.white, -32);
            // NO corner pilasters here either. wallCorner/wallCornerR are SQUARE art (300x300) and
            // were being stretched to 0.83 x 1.59, smearing the corner into a distorted slab. The
            // back wall above already spans the full OUTER width (sOuter*2), so it covers the
            // side/back junction unaided. NOTE this whole branch is the SPRITE FALLBACK -- the
            // shipping scene builds the room from 3D meshes further up, and the corner fault the
            // user actually saw lives there, in corner-col/corner-cap.
            // soft contact shadow where each side wall meets the floor, so the wall grounds into the room
            MakeBar("wall-l-foot", new Vector2(-Rr + .06f, cy), new Vector2(.12f, all.height), baseSh, -30);
            MakeBar("wall-r-foot", new Vector2(Rr - .06f, cy), new Vector2(.12f, all.height), baseSh, -30);
        } else {
            for (int s = 0; s < 2; s++) {
                float sign = s == 0 ? -1f : 1f;
                MakeBar("wall-s0" + s, new Vector2(sign * (Rr + sideW * .17f), cy), new Vector2(sideW * .35f, hh), faceD, -32);
                MakeBar("wall-s1" + s, new Vector2(sign * (Rr + sideW * .5f), cy), new Vector2(sideW * .36f, hh), faceM, -32);
                MakeBar("wall-s2" + s, new Vector2(sign * (Rr + sideW * .83f), cy), new Vector2(sideW * .38f, hh), faceHi, -32);
                MakeBar("wall-s-rail" + s, new Vector2(sign * (ext - .09f), cy), new Vector2(.2f, hh), rail, -30);
                MakeBar("wall-s-base" + s, new Vector2(sign * (Rr + .05f), cy), new Vector2(.13f, all.height), baseSh, -30);
            }
            MakeBar("wall-t0", new Vector2(0, T + wallH * .17f), new Vector2(ext * 2, wallH * .35f), faceD, -32);
            MakeBar("wall-t1", new Vector2(0, T + wallH * .5f), new Vector2(ext * 2, wallH * .36f), faceM, -32);
            MakeBar("wall-t2", new Vector2(0, T + wallH * .83f), new Vector2(ext * 2, wallH * .38f), faceHi, -32);
            MakeBar("wall-t-rail", new Vector2(0, T + wallH - .06f), new Vector2(ext * 2, .16f), rail, -30);
            MakeBar("wall-t-base", new Vector2(0, T + .05f), new Vector2(ext * 2, .13f), baseSh, -30);
        }
        // decor + the entrance door the guests come through (bigger, sits in the back wall, centred)
        float doorTop = T + .72f;
        MakeBar("wall-door-frame", new Vector2(0, doorTop), new Vector2(1.34f, 1.42f), new Color(.24f, .16f, .10f, 1f), -29);
        MakeSprite("wall-door", ObjectSprite("walldoor"), new Vector2(0, doorTop), new Vector2(1.24f, 1.4f), Color.white, -28);
        MakeRect("wall-door-mat", new Vector2(0, T - .18f), new Vector2(1.0f, .14f), new Color(.5f, .12f, .14f, .6f), -17);
        MakeSprite("wall-window", ObjectSprite("wallwindow"), new Vector2(-2.05f, T + .92f), new Vector2(1.05f, .64f), Color.white, -28);
        MakeSprite("wall-pic", ObjectSprite("wallpic"), new Vector2(2.05f, T + .95f), new Vector2(.54f, .6f), Color.white, -28);
    }

    void DrawAmbience()
    {
        if (save.decor <= 0) return;
        MakeBox3D("front-rug-3d", CellCenter(4, 4), new Vector2(Tile * 5.2f, Tile * 2.1f), .018f,
                  WorldMaterial("front-rug-3d", null, new Color(.38f, .09f, .12f), Vector2.one), .009f, false);
        if (save.decor >= 2) MakeBox3D("runner-3d", CellCenter(4, 7), new Vector2(Tile * 7f, Tile * .42f), .018f,
                  WorldMaterial("runner-3d", null, new Color(.08f, .28f, .22f), Vector2.one), .009f, false);
        if (save.decor >= 3) {
            MakeCylinder3D("plant-left-3d", CellCenter(0, 1), .16f, .48f, WorldMaterial("plant-3d", null, new Color(.08f, .4f, .2f), Vector2.one));
            MakeCylinder3D("plant-right-3d", CellCenter(Cols - 1, 1), .16f, .48f, WorldMaterial("plant-3d", null, new Color(.08f, .4f, .2f), Vector2.one));
        }
    }

    void DrawExpansionZones()
    {
        // Removed: the taped-off "RENOVATE / LEASED" bays confused more than they informed
        // ("ne olduğu asla belli değil"). Room expansion is bought from the BAY card in the store.
    }

    void DrawRenovationBay(int tier)
    {
        int r = tier == 0 ? 1 : 14;
        Rect zone = CellRect(8, r, 2, 2);
        Vector2 center = zone.center;
        MakeRect("reno-shadow", center + new Vector2(.04f, -.05f), zone.size * .95f, new Color(.004f, .005f, .008f, .28f), -11);
        MakeRect("reno-wash", center, zone.size * .88f, new Color(.14f, .12f, .08f, .18f), -10);
        for (int i = -1; i <= 2; i++) {
            var stripe = MakeRect("reno-stripe", center + new Vector2((i - .4f) * .28f, 0), new Vector2(.045f, zone.size.y * .76f), new Color(.95f, .72f, .22f, .32f), -8);
            SetBillboardAngle(stripe, -38f);
        }
        MakeRect("reno-door", center + new Vector2(0, -.42f), new Vector2(zone.size.x * .72f, .07f), new Color(.95f, .72f, .22f, .52f), -7);
        DrawWorldLabel(tier == 0 ? "NEXT BAY" : "SERVICE BAY", center + new Vector2(0, .08f), new Vector2(1.18f, .24f), gold);
        DrawWorldText(tier == 0 ? "RENOVATE" : "LEASED", center + new Vector2(0, -.12f), .018f, muted, 42);
    }

    // Double swing-door in the divider gap. The two leaves swing open as the player or a waiter
    // approaches the pass, and fall shut again when nobody is near.
    void DrawKitchenDoor()
    {
        if (screen != ScreenMode.Play) return;
        Vector2 c = new Vector2(0, DividerY());
        float nearest = Vector2.Distance(playerPos, c);
        foreach (var w in workers) nearest = Mathf.Min(nearest, Vector2.Distance(w.pos, c));
        foreach (var cu in customers) if (!cu.seated) nearest = Mathf.Min(nearest, Vector2.Distance(cu.visualPos, c));
        float open = 1f - Mathf.Clamp01((nearest - .8f) / .95f);
        open = open * open * (3f - 2f * open);                     // smoothstep
        // Classic commercial-kitchen double swing door: two BRUSHED-METAL leaves with a round
        // porthole and a kick plate. No lintel/header — the leaves hang free in the wall opening.
        float divY = DividerY();
        Material steel = WorldMaterial("kdoor-steel", null, new Color(.74f, .77f, .80f, 1), Vector2.one);
        Material steelDark = WorldMaterial("kdoor-steel-dark", null, new Color(.55f, .58f, .62f, 1), Vector2.one);
        Material glass = WorldMaterial("kdoor-glass", null, new Color(.62f, .78f, .86f, 1), Vector2.one);
        // slim steel jambs only (no top beam)
        MakeBox3D("kdoor-jamb-l", new Vector2(-DoorHalfGap - .05f, divY), new Vector2(.1f, .26f), .92f, steelDark, .46f, true);
        MakeBox3D("kdoor-jamb-r", new Vector2(DoorHalfGap + .05f, divY), new Vector2(.1f, .26f), .92f, steelDark, .46f, true);

        float leaf = DoorHalfGap * .96f, th = .09f, doorH = .88f;
        for (int s = 0; s < 2; s++) {
            float sign = s == 0 ? -1f : 1f;
            float ang = Mathf.Lerp(0f, 84f, open);
            Vector2 hinge = new Vector2(sign * DoorHalfGap, divY);
            float rad = ang * Mathf.Deg2Rad;
            // leaf spans hinge -> centre when shut, and swings OUT INTO THE DINING ROOM as it opens
            // (logical +y is the restaurant side; the kitchen is below the divider).
            Vector2 dir = new Vector2(-sign * Mathf.Cos(rad), Mathf.Sin(rad));
            Vector2 mid = hinge + dir * leaf * .5f;
            float yaw = sign * ang;
            var lo = MakeBox3D("kdoor-leaf" + s, mid, new Vector2(leaf, th), doorH, steel, doorH * .5f, true);
            lo.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            // porthole window up high
            Vector2 port = hinge + dir * (leaf * .52f);
            var win = MakeBox3D("kdoor-port" + s, port, new Vector2(leaf * .42f, th * 1.6f), .26f, glass, doorH * .72f, false);
            win.transform.rotation = lo.transform.rotation;
            // kick plate along the bottom
            var kick = MakeBox3D("kdoor-kick" + s, mid, new Vector2(leaf * .92f, th * 1.5f), .2f, steelDark, .1f, false);
            kick.transform.rotation = lo.transform.rotation;
        }
    }

    void DrawMoveTarget()
    {
        if (!hasMoveTarget) return;
        Vector2 delta = moveTarget - playerPos;
        var line = MakeRect("path-line", (playerPos + moveTarget) * .5f, new Vector2(delta.magnitude, .025f), new Color(.3f, .9f, .85f, .35f), 18);
        SetBillboardAngle(line, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        MakeRect("target", moveTarget, new Vector2(.22f, .22f), new Color(.3f, .9f, .85f, .65f), 19);
    }

    void DrawAppliance(Appliance a)
    {
        Rect rect = CellRect(a.c, a.r, a.w, a.h);
        string sprite = ApplianceSpriteName(a);
        // each ingredient now has its OWN filled-container sprite (providerBun, providerPatty …);
        // only tint / stack an icon when we fall back to the generic shelf for an unmapped item.
        bool providerCustom = a.type == "provider" && sprite != "provider";
        bool splitTable = a.type == "table";
        string modelFolder = splitTable ? null : PropModelFolder(sprite);
        Vector2 visualSize = ApplianceVisualSize(a, rect.size);
        Vector2 visualCenter = rect.center + ApplianceVisualOffset(a);
        Color spriteTint = (a.type == "provider" && !providerCustom) ? Color.Lerp(Color.white, ProviderColor(a.itemId), .18f) : Color.white;
        // soft elliptical ground shadow so each unit sits on the floor (adds 3-D depth)
        if (!splitTable && modelFolder == null && a.type != "extinguisher") {   // real meshes receive actual light shadows
            float ss = rect.height * .46f;
            var gsh = MakeSprite(a.id + "-gshadow", circleSprite, rect.center + new Vector2(0, -rect.height * .34f), new Vector2(ss, ss), new Color(.01f, .012f, .016f, .4f), a.r - 1);
            var gsc = gsh.transform.localScale;
            gsh.transform.localScale = new Vector3(gsc.x * (rect.width * .96f / ss), gsc.y, gsc.z);
        }
        if (splitTable) DrawTableWithChairs(a, rect);
        else {
            if (modelFolder != null) {
                // REAL 3D mesh (the user's FBX). In-game rotation maps to yaw around up.
                float meshLift = 0f;
                if (a.type == "hob") {
                    // cooktop is a countertop unit — sit it ON one of the game's own counters,
                    // measured so the burners rest exactly on the counter top
                    var baseGo = Make3DProp(a.id + "-base", "prepcounter", rect.center,
                                            new Vector2(rect.size.x * 1.3f, rect.size.y * 1.36f), a.rotation * -90f, a.r - 1);   // same size as the real counters
                    if (baseGo) meshLift = CombinedBounds(baseGo.GetComponentsInChildren<Renderer>()).max.y - .01f;
                    else meshLift = .3f;
                }
                Make3DProp(a.id, modelFolder, rect.center, visualSize, a.rotation * -90f, a.r, meshLift);
            } else {
                var apGo = MakeSprite(a.id, ObjectSprite(sprite), visualCenter, visualSize, spriteTint, a.r);
                if (a.rotation != 0) SetBillboardAngle(apGo, -90 * a.rotation);
            }
        }
        if (screen == ScreenMode.Layout) {
            var go = MakeRect(a.id + "-hit", rect.center, rect.size * .96f, selectedLayout == a ? new Color(1, .82f, .25f, .18f) : new Color(1, 1, 1, .04f), 30);
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
        }
        if (a.type == "provider") {
            if (!providerCustom) {
                Vector2 tray = rect.center + new Vector2(0, .1f);
                Color trayColor = ProviderColor(a.itemId);
                trayColor.a = .2f;
                MakeRect("provider-tray", tray + new Vector2(0, -.02f), new Vector2(.4f, .16f), trayColor, 24);
                DrawFood(a.itemId, tray + new Vector2(0, .03f), .32f, a.itemId == "patty" ? "raw" : "", 31);
            }
            if (screen == ScreenMode.Layout) MakeRect("provider-pin", rect.center + new Vector2(0, -.22f), new Vector2(.18f, .055f), ProviderColor(a.itemId), 41);
        }
        // station contents render ON the unit's top surface (spriteLift), not at its feet
        spriteLift = a.type == "hob" ? .62f : .52f;
        if (a.type == "counter" && a.item != null) DrawHeld(a.item, rect.center, .3f);
        if (a.type == "hob" && a.item != null) DrawFood(a.item.id, rect.center, .27f, a.item.state);
        if (a.type == "oven" && a.item != null) DrawFood("dough", rect.center, .27f, a.item.state);
        if (a.type == "espresso" && a.item != null) DrawFood("coffee", rect.center, .26f, a.item.state);
        if (a.type == "prep" && a.item != null) DrawFood(a.item.id, rect.center, .26f, "ready");
        if (a.type == "sink" && a.item != null) DrawHeld(a.item, rect.center, .26f);
        if (a.type == "sink" && holdTarget == a && holdProgress > 0) DrawWaterFlow(rect.center);
        // The dirty plate a customer leaves behind belongs ON the table. It used to be drawn below,
        // after spriteLift was reset to 0, which put it on the floor next to the table instead --
        // every other station's contents are drawn here precisely because this block is lifted.
        if (a.type == "table" && a.dirty) DrawHeld(Item.Plate(true), rect.center, .22f);
        if ((a.type == "hob" || a.type == "oven" || a.type == "espresso") && a.item != null && a.item.state != "burnt") {
            float cook = CookDuration();
            if (!IngredientReady(a.item)) {
                DrawWorldBar(rect.center + new Vector2(0, -.25f), .42f, a.item.progress / cook, a.type == "espresso" ? violet : gold);
            } else if (a.type == "hob" || a.type == "oven") {   // cooked — overcook/burn warning (gold -> red)
                float over = Mathf.Clamp01((a.item.progress - cook) / (cook * 2f));
                if (over > .03f) DrawWorldBar(rect.center + new Vector2(0, -.25f), .42f, over, Color.Lerp(gold, red, over));
            }
        }
        spriteLift = 0f;
        DrawStationEffects(a, rect);
        if (a.fire > 0) DrawFire(a, rect);
    }

    bool ShouldShowApplianceTag(Appliance a)
    {
        // No persistent name tags anywhere — the user disliked the black-background labels under every
        // prop. Tap a unit (play OR layout) for a floating speech bubble instead (DrawStationBubble).
        return false;
    }

    string TableKindLabel(Appliance a)
    {
        if (a.seats <= 1) return "BAR";
        if (a.seats >= 4) return "FAM";
        return "2C";
    }

    string ApplianceTag(Appliance a)
    {
        if (a.type == "provider") return a.itemId.ToUpperInvariant();
        if (a.type == "plates") return "PLATE";
        if (a.type == "counter") return "COUNTER";
        if (a.type == "hob") return "HOB";
        if (a.type == "oven") return "OVEN";
        if (a.type == "espresso") return "ESP";
        if (a.type == "prep") return "PREP";
        if (a.type == "sink") return "SINK";
        if (a.type == "drink") return "DRINK";
        if (a.type == "trash") return "BIN";
        if (a.type == "extinguisher") return "EXTG";
        if (a.type == "table") return TableKindLabel(a);
        return a.type.ToUpperInvariant();
    }

    // Human-readable name + one-line "what it does", shown in the tap/hover detail card during play.
    string StationTitle(Appliance a)
    {
        switch (a.type) {
            case "provider": return a.itemId.ToUpperInvariant() + " STATION";
            case "plates": return "CLEAN PLATES";
            case "counter": return "PREP COUNTER";
            case "hob": return "HOB";
            case "oven": return "OVEN";
            case "espresso": return "ESPRESSO BAR";
            case "prep": return "PREP COUNTER";
            case "sink": return "WASH SINK";
            case "drink": return "DRINKS FRIDGE";
            case "trash": return "BIN";
            case "extinguisher": return "FIRE EXTINGUISHER";
            case "table": return TableKindLabel(a) == "FAM" ? "FAMILY TABLE" : TableKindLabel(a) == "BAR" ? "BAR SEAT" : "TABLE";
            default: return a.type.ToUpperInvariant();
        }
    }

    string StationDescription(Appliance a)
    {
        switch (a.type) {
            case "provider": return "Grab fresh " + a.itemId;
            case "plates": return "Take a clean plate";
            case "counter": return "Stack food - HOLD to chop";
            case "hob": return "Cook meat - watch burns!";
            case "oven": return "Bake pizza dough";
            case "espresso": return "Brew coffee shots";
            case "prep": return "Hold ACT to chop";
            case "sink": return "Hold ACT to wash plates";
            case "drink": return "Grab a soda for guests";
            case "trash": return "Dump burnt food here";
            case "extinguisher": return "Hold ACT to fight fire";
            case "table": return "Seat guests & serve food";
            default: return "";
        }
    }

    Color ApplianceTagColor(Appliance a)
    {
        if (a.type == "table") return gold;
        if (a.type == "provider") return ProviderColor(a.itemId);
        if (a.type == "hob" || a.type == "oven") return red;
        if (a.type == "sink") return blue;
        if (a.type == "prep") return mint;
        return muted;
    }

    Color ProviderColor(string id)
    {
        if (id == "bun" || id == "dough") return new Color(.96f, .66f, .28f, 1);
        if (id == "patty") return new Color(.78f, .22f, .18f, 1);
        if (id == "lettuce") return new Color(.34f, .78f, .34f, 1);
        if (id == "tomato" || id == "sauce") return new Color(.95f, .18f, .16f, 1);
        if (id == "cheese" || id == "milk") return new Color(1f, .82f, .28f, 1);
        if (id == "coffee") return new Color(.55f, .31f, .18f, 1);
        if (id == "sausage") return new Color(.8f, .4f, .35f, 1);
        if (id == "onion") return new Color(.92f, .9f, .82f, 1);
        if (id == "rice") return new Color(.9f, .86f, .72f, 1);
        return mint;
    }

    void DrawCustomer(Customer c)
    {
        if (c.table == null) return;
        Vector2 p = c.visualPos == Vector2.zero ? CustomerSeatPosition(c) : c.visualPos;
        string state;
        float animationTime;
        if (c.standingUp) {
            state = "standup";
            animationTime = Mathf.Clamp01(c.transitionTime / CustomerStandDuration);
        } else if (c.sittingDown) {
            state = "sitdown";
            animationTime = Mathf.Clamp01(c.transitionTime / CustomerSitDuration);
        } else if (c.leaving || !c.seated) {
            state = "walk";
            animationTime = c.animationClock;
        } else if (c.served) {
            state = "eat";
            animationTime = c.eatTimer;
        } else {
            state = "sit";
            animationTime = c.animationClock;
        }
        Vector2 size = new Vector2(1.02f, 1.46f);
        Color tint = CustomerTypeColor(c.typeId);

        // Walking in / leaving: the whole party moves as a single figure.
        if (state == "walk") {
            Vector2 wp = c.visualPos == Vector2.zero ? CustomerSeatPosition(c) : c.visualPos;
            DrawCharacter("customer-" + c.visualId, c.visualId, wp, size, c.facing, true, false, false, false, 20, tint, state, animationTime);
            return;
        }

        // Seated: one figure per party member, each on its own chair facing the table.
        var slots = CustomerSeatSlots(c.table);
        int n = Mathf.Clamp(c.partySize, 1, Mathf.Max(1, slots.Count));
        int baseNum; int.TryParse(c.visualId.Length > 8 ? c.visualId.Substring(8) : "0", out baseNum);
        Vector2 tableCenter = CellRect(c.table.c, c.table.r, c.table.w, c.table.h).center;
        Vector2 headAnchor = slots[0].pos;
        for (int i = 0; i < n; i++) {
            string vid = "customer" + ((baseNum + i) % 6);
            // Seat the guest ON the chair: nudge them a little TOWARD the table so they're tucked in at
            // its edge, raise to seat height, and pull only SLIGHTLY toward the camera — just enough to
            // sit in front of the cushion while the chair's back still reads behind them. The old .7
            // pull yanked the body clear of the chair entirely, so they looked like they were standing.
            Vector2 seatPos = slots[i].pos + slots[i].face * .04f;
            if (i == 0) headAnchor = seatPos;      // the readout stacks over THIS guest
            // ART FIX: the rendered seated frames are NOT centred in their sprite — the figure is drawn
            // ~19% of the frame width off-centre, AWAY from the way they face (measured across
            // Art/CharactersRigged/*_sit_*). Uncompensated, a seated guest lands a fifth of a body-width
            // beside their chair, which is exactly why they never looked seated and why the order icon
            // never lined up with the head. Shift the DRAW position back so the BODY sits on the seat.
            Vector2 drawPos = seatPos + new Vector2(slots[i].face.x * SeatArtOffset(state) * size.x, 0f);
            float zBack = Mathf.Max(0f, slots[i].pos.y - tableCenter.y);   // logical units behind the table
            // DEPTH: the chair mesh is ~.97 world-z deep, so its FRONT face reaches further toward the
            // camera than the tilted billboard's upper half — that is why chair parts were drawing over
            // the body ("içine giriyor"). Under an ORTHO camera this pull is screen-neutral (the y and z
            // components cancel), so pushing the guest a full chair-depth forward only fixes occlusion.
            // LIFT puts the seated figure's hips on the cushion rather than sunk through it.
            spriteLift = .6f;
            spriteDepthBoost = .95f * (1f - Mathf.Clamp01(zBack / .6f));
            DrawCharacter("customer-" + c.visualId + "-" + i, vid, drawPos, size, slots[i].face, false, false, false, true, 26, tint, state, animationTime + i * .17f);
            spriteLift = 0f;
            spriteDepthBoost = 0f;
        }
        if (!c.seated) return;

        // Readout stacks STRAIGHT UP over the guest's head. It uses a world-UP lift (spriteLift) rather
        // than a logical +y offset: a logical offset also pushes the icon back in Z, which drifted it
        // sideways/behind on this tilted camera — that's why the dish never sat on the head.
        Vector2 hud = headAnchor;
        Color moodColor = c.patience < c.maxPatience * .3f ? red : tint;
        float barValue = c.served
            ? 1f - Mathf.Clamp01(c.eatTimer / CustomerEatDuration)
            : Mathf.Clamp01(c.patience / c.maxPatience);
        spriteLift = 1.1f;                             // just clear of the head
        DrawWorldBar(hud, .52f, barValue, c.served ? mint : c.patience < c.maxPatience * .3f ? red : mint);
        spriteLift = 0f;
        if (c.ordered && !c.mealServed) {
            spriteLift = 1.48f;                        // the dish they want, dead centre above the head
            DrawFinalDish(c.recipe, hud, .62f, 44);
            orderIcons.Add((hud, 1.48f, .62f, c.recipe));   // tap zone -> DrawOrderDetail
            if (c.partySize > 1) DrawWorldText(c.mealsServed + "/" + c.partySize, hud + new Vector2(.42f, 0f), .019f, Color.white, 46);
            if (!string.IsNullOrEmpty(c.orderMod)) {
                spriteLift = 1.92f;
                DrawWorldLabel(c.orderMod, hud, new Vector2(.66f, .17f), c.orderMod[0] == 'N' ? red : gold);
            }
            spriteLift = 0f;
        } else if (!c.ordered) {
            spriteLift = 1.56f;
            DrawWorldText("...", hud, .03f, moodColor, 46);
            spriteLift = 0f;
        } else if (c.wantsDrink && !c.drinkServed) {
            spriteLift = 1.56f;
            DrawWorldText("DRINK " + c.drinkCount + "/" + c.partySize, hud, .015f, blue, 46);
            spriteLift = 0f;
        }
        if (c.served) DrawFinalDish(c.recipe, hud + new Vector2(0, -.08f), .24f, 24);
        // impatient guest emote: a pulsing red "!" that jitters as they get closer to walking out
        if (!c.served && c.ordered && !c.mealServed && c.patience < c.maxPatience * .3f) {
            float pulse = .5f + .5f * Mathf.Sin(Time.time * 9f);
            Vector2 jab = new Vector2(Mathf.Sin(Time.time * 20f) * .03f, .58f + pulse * .05f);
            DrawWorldText("!", hud + jab, .026f + pulse * .006f, red, 47);
        }
    }

    // How far off-centre the figure is drawn inside a seated frame, as a fraction of the sprite width,
    // measured from the rigged art itself (side "sit" ≈ .19, side "eat" ≈ .06, front/back ≈ 0).
    static float SeatArtOffset(string state)
    {
        if (state == "sit") return .19f;
        if (state == "eat") return .06f;
        return 0f;
    }

    Color CustomerTypeColor(string type)
    {
        if (type == "vip") return new Color(1f, .42f, .83f, 1);
        if (type == "rush") return red;
        if (type == "critic") return violet;
        if (type == "family") return gold;
        if (type == "patient") return blue;
        if (type == "bulk") return green;
        return gold;
    }

    void DrawWorker(Worker w)
    {
        string sprite = OutfitSpriteName(w.role == "waiter" || w.role == "cook" ? w.role : "prepper");
        bool working = !string.IsNullOrEmpty(w.task) && w.task != "IDLE";
        bool moving = Vector2.Distance(w.pos, w.target) > .04f;
        bool carrying = w.carry != null || w.carryRecipe != null;
        string state = carrying ? (moving ? "carrywalk" : "carry") : moving ? "walk" : working ? "act" : "idle";
        DrawCharacter(w.role, sprite, w.pos, new Vector2(1.0f, 1.44f), w.facing, moving, working, carrying, false, 21, working ? blue : muted, state, Time.time);
        DrawWorkerCarry(w);
        if (!string.IsNullOrEmpty(w.task) && w.task != "IDLE") {
            DrawWorldLabel(w.task, w.pos + new Vector2(0, -.43f), new Vector2(.58f, .16f), blue);
        }
    }

    void DrawWorkerCarry(Worker w)
    {
        if (w == null || (w.carry == null && w.carryRecipe == null)) return;
        Vector2 hand = w.pos + CarryOffset(w.facing);
        int carryOrder = CarryDrawOrder(w.facing, 21);
        if (w.carryRecipe != null) {
            DrawFinalDish(w.carryRecipe, hand, .25f, carryOrder);
            return;
        }
        DrawHeld(w.carry, hand, .23f, true, carryOrder);
    }

    void DrawPlayer()
    {
        bool working = holdProgress > 0;
        string state = holding != null ? (playerWalking ? "carrywalk" : "carry") : working ? "act" : playerWalking ? "walk" : "idle";
        playerObj = DrawCharacter("player", PlayerSpriteName(), playerPos, new Vector2(1.05f, 1.5f), playerFacing, playerWalking, working, holding != null, false, 25, gold, state, Time.time);
        if (holding != null) DrawHeld(holding, playerPos + CarryOffset(playerFacing), .46f, true, CarryDrawOrder(playerFacing, 25));
    }

    GameObject DrawCharacter(string name, string spriteName, Vector2 pos, Vector2 size, Vector2 facing, bool walking, bool working, bool carrying, bool seated, int order, Color accent, string forcedState = null, float animationTime = -1f)
    {
        Vector2 dir = FacingOrDefault(facing);
        MakeRect(name + "-shadow", pos + new Vector2(0, -.32f), new Vector2(size.x * .62f, .08f), new Color(.005f, .006f, .009f, .36f), order - 2);
        bool usedDirectional;
        var sprite = CharacterFrameSprite(spriteName, facing, walking, working, carrying, seated, pos, forcedState, animationTime, out usedDirectional);
        var go = MakeSprite(name, sprite, pos, size, Color.white, order);
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr && !usedDirectional && Mathf.Abs(dir.x) > .2f) sr.flipX = dir.x < 0;
        return go;
    }

    Vector2 FacingOrDefault(Vector2 v)
    {
        return v.sqrMagnitude > .001f ? v.normalized : Vector2.down;
    }

    Sprite DirectionalCharacterSprite(string name, Vector2 facing, out bool usedDirectional)
    {
        string key = name + "_" + FacingKey(facing);
        if (directionalCharacterSprites.TryGetValue(key, out var sprite) && sprite) {
            usedDirectional = true;
            return sprite;
        }
        usedDirectional = false;
        return CharacterSprite(name);
    }

    Sprite CharacterFrameSprite(string name, Vector2 facing, bool walking, bool working, bool carrying, bool seated, Vector2 pos, string forcedState, float animationTime, out bool usedDirectional)
    {
        string dir = FacingKey(facing);
        string state = forcedState;
        if (string.IsNullOrEmpty(state)) {
            if (seated) state = "sit";
            else if (working) state = "act";
            else if (carrying) state = walking ? "carrywalk" : "carry";
            else state = walking ? "walk" : "idle";
        }
        int count = state == "walk" || state == "carrywalk" || state == "act" || state == "sitdown" || state == "standup" ? 8
            : state == "eat" ? 6
            : state == "idle" || state == "carry" ? 4
            : 2;
        float fps = state == "walk" || state == "carrywalk" ? 9f : state == "act" ? 10f : state == "eat" ? 6f : state == "sit" ? 1.5f : 3f;
        float clock = animationTime >= 0 ? animationTime : Time.time + pos.x * .07f + pos.y * .11f;
        int frame = state == "sitdown" || state == "standup"
            ? Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(clock) * count), 0, count - 1)
            : Mathf.Abs(Mathf.FloorToInt(clock * fps)) % count;
        string key = name + "_" + dir + "_" + state + "_" + frame;
        if (animatedCharacterSprites.TryGetValue(key, out var sprite) && sprite) {
            usedDirectional = true;
            return sprite;
        }
        if (name.StartsWith("customer")) return DirectionalCharacterSprite("customerWalk", facing, out usedDirectional);
        return DirectionalCharacterSprite(name, facing, out usedDirectional);
    }

    string FacingKey(Vector2 facing)
    {
        Vector2 dir = FacingOrDefault(facing);
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y)) return dir.x < 0 ? "left" : "right";
        return dir.y > .1f ? "back" : "front";
    }

    Vector2 CarryOffset(Vector2 facing)
    {
        Vector2 dir = FacingOrDefault(facing);
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y)) return new Vector2(dir.x > 0 ? .23f : -.23f, .015f);
        if (dir.y > 0) return new Vector2(0f, .13f);
        return new Vector2(0f, .015f);
    }

    int CarryDrawOrder(Vector2 facing, int characterOrder)
    {
        return FacingOrDefault(facing).y > .35f ? characterOrder - 1 : characterOrder + 8;
    }

    void DrawFacingMarker(Vector2 pos, Vector2 facing, Color color, int order)
    {
        Vector2 dir = FacingOrDefault(facing);
        Color c = color;
        c.a = .75f;
        Vector2 markerPos = pos + dir * .3f + new Vector2(0, -.02f);
        var marker = MakeRect("facing-marker", markerPos, new Vector2(.09f, .035f), c, order);
        SetBillboardAngle(marker, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }

    void DrawActionSpark(Vector2 pos, Vector2 facing, Color color, int order)
    {
        Vector2 hand = pos + CarryOffset(facing);
        float pulse = .7f + Mathf.Sin(Time.time * 16f) * .3f;
        Color c = Color.Lerp(color, Color.white, .35f);
        c.a = .45f + .28f * pulse;
        MakeRect("action-spark-a", hand + new Vector2(.04f, .03f), new Vector2(.11f, .018f), c, order);
        MakeRect("action-spark-b", hand + new Vector2(-.04f, -.025f), new Vector2(.08f, .016f), c, order);
    }

    void DrawPopup(Popup p)
    {
        float t = Mathf.Clamp01(p.life / Mathf.Max(.01f, p.maxLife));
        if (p.burst) {
            float grow = 1f - t;
            const int n = 6;
            for (int i = 0; i < n; i++) {
                float ang = i * (Mathf.PI * 2f / n) + grow * 1.2f;
                float r = .12f + grow * .34f;
                Vector2 sp = p.pos + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                Color s = Color.Lerp(p.color, Color.white, .3f);
                s.a = t * .7f;
                var g = MakeRect("burst", sp, Vector2.one * (.07f * (.5f + t)), s, 45);
                SetBillboardAngle(g, ang * Mathf.Rad2Deg);
            }
        }
        Color c = p.color;
        c.a = t;
        DrawWorldLabel(p.text, p.pos, new Vector2(.95f, .2f), c);
    }

    void DrawHeld(Item item, Vector2 p, float size, bool carryVisual = false, int carryOrder = 34)
    {
        if (item.kind == "ingredient") {
            if (carryVisual) {
                string spriteName = FoodSpriteName(item.id, item.state);
                MakeSprite("carry-" + item.id, CarrySprite(spriteName), p, Vector2.one * size,
                    item.state == "burnt" ? BurntTint : Color.white, carryOrder);
            } else DrawFood(item.id, p, size * .82f, item.state, 32);
            return;
        }
        if (item.kind == "drink") {
            if (carryVisual) MakeSprite("carry-drink", CarrySprite("drink"), p, Vector2.one * size, Color.white, carryOrder);
            else DrawFood("drink", p, size * .95f, "", 32);
            return;
        }
        if (item.kind == "tool") {
            Sprite ex = objectSprites.TryGetValue("extinguisher", out var es) && es ? es : circleSprite;
            MakeSprite("carry-tool-" + item.id, ex, p, new Vector2(size * 1.05f, size * 1.4f), Color.white, carryVisual ? carryOrder : 33);
            return;
        }
        if (item.dirty) {
            Sprite dirty = carryVisual ? CarrySprite("dirtyPlate") : ObjectSprite("dirtyPlate");
            MakeSprite("dirty-plate", dirty, p, new Vector2(size * 1.55f, size * 1.32f), Color.white, carryVisual ? carryOrder : 28);
            return;
        }
        var completed = CompletedRecipeForPlate(item);
        if (completed != null) {
            // Carry it as a PLATE with the dish sitting on it. The old code swapped in a giant
            // dish sprite, so the plate vanished and a huge burger filled the player's hands.
            if (carryVisual) {
                Sprite plate = CarrySprite("plate") ?? ObjectSprite("singlePlate");
                MakeSprite("carry-plate", plate, p, new Vector2(size * 1.5f, size * 1.28f), Color.white, carryOrder);
                // dish sits centred ON the plate, small enough to stay inside its rim
                Sprite dish = FinalDishSprite(completed.id) ?? CarrySprite(completed.id);
                MakeSprite("carry-dish-" + completed.id, dish, p + new Vector2(0, size * .1f), Vector2.one * size * .72f, Color.white, carryOrder + 1);
            } else {
                // Same treatment as the carried version above: a plate with the dish ON it. This
                // branch used to draw the dish ALONE at 1.85x and no plate at all, so a finished
                // burger swallowed the plate it was supposedly sitting on and towered over the
                // table. 0.92x is sized to sit inside the plate rim, matching the ingredient stack
                // it replaces the instant the recipe completes -- otherwise the dish visibly jumps
                // in size at that moment.
                DrawSinglePlate(p, size, 28);
                DrawFinalDish(completed, p + new Vector2(0, size * .12f), size * .92f, 30);
            }
            return;
        }
        if (carryVisual) {
            MakeSprite("carry-plate", CarrySprite("plate"), p, Vector2.one * size, Color.white, carryOrder);
            if (item.parts.Count > 0) DrawPlateStack(item, p, size * .82f, carryOrder + 1);
            return;
        }
        DrawSinglePlate(p, size, 28);
        DrawPlateStack(item, p, size);
    }

    void DrawSinglePlate(Vector2 p, float size, int order)
    {
        MakeSprite("single-plate", ObjectSprite("singlePlate"), p, new Vector2(size * 1.45f, size * 1.25f), Color.white, order);
    }

    void DrawPlateStack(Item item, Vector2 p, float size, int baseOrder = 29)
    {
        if (item.parts.Count == 0) return;
        // stack the ingredients into a burger-like tower (bottom bun -> fillings -> top bun)
        float layerH = size * .17f;
        float baseY = -(item.parts.Count - 1) * layerH * .5f;
        bool bottomBunDone = false;
        for (int i = 0; i < item.parts.Count; i++) {
            string part = item.parts[i];
            float y = baseY + i * layerH;
            string state = part == "patty" || part == "sausage" ? "cooked" : NeedsPrep(part) ? "ready" : part == "dough" ? "baked" : "";
            float partSize = part == "bun" ? size * .98f : part == "patty" ? size * .86f : part == "dough" ? size * 1.05f : size * .78f;
            string drawId = part;
            if (part == "bun" && !bottomBunDone) { drawId = "bunBottom"; bottomBunDone = true; }   // first bun = bottom bun
            DrawFood(drawId, p + new Vector2(0, y), partSize, state, baseOrder + i);
        }
    }

    Recipe CompletedRecipeForPlate(Item item)
    {
        if (item == null || item.kind != "plate" || item.dirty) return null;
        // if the plate is still a prefix of a longer OPEN order (e.g. building toward EXTRA CHEESE),
        // it isn't finished yet — don't flash the base dish art prematurely.
        foreach (var c in customers) {
            if (!c.ordered || c.served || c.mealServed || c.leaving) continue;
            var parts = c.orderParts ?? c.recipe.parts.ToList();
            if (item.parts.Count < parts.Count && PartsPrefixMatches(item, parts)) return null;
        }
        // a plate that fully satisfies an open (possibly modified) order shows that dish
        foreach (var c in customers) {
            if (!c.ordered || c.served || c.mealServed || c.leaving) continue;
            if (PlateMatchesOrder(item, c)) return c.recipe;
        }
        return recipes.FirstOrDefault(r => r.theme == save.theme && PlateMatches(item, r));
    }

    void DrawFinalDish(Recipe recipe, Vector2 p, float size, int order)
    {
        if (recipe == null) return;
        var sprite = FinalDishSprite(recipe.id);
        if (sprite) MakeSprite("final-dish-" + recipe.id, sprite, p, Vector2.one * size, Color.white, order);
    }

    void DrawWaterFlow(Vector2 sinkCenter)
    {
        float pulse = .65f + Mathf.Sin(Time.time * 18f) * .25f;
        Color water = new Color(.34f, .72f, 1f, .42f + pulse * .18f);
        MakeRect("water-stream", sinkCenter + new Vector2(.04f, .12f), new Vector2(.045f, .24f), water, 35);
        MakeRect("water-splash-a", sinkCenter + new Vector2(-.04f, -.03f), new Vector2(.16f, .026f), new Color(.58f, .88f, 1f, .28f), 36);
        MakeRect("water-splash-b", sinkCenter + new Vector2(.07f, -.07f), new Vector2(.12f, .02f), new Color(.58f, .88f, 1f, .22f), 36);
    }

    // warn on stations a nearby blaze is about to jump to, so the spread is readable (5s spread window)
    void DrawFireWarnings()
    {
        var fires = appliances.Where(a => a.fire > 0).ToList();
        if (fires.Count == 0) return;
        foreach (var n in appliances) {
            if (n.fire > 0 || n.type == "extinguisher") continue;
            bool threatened = fires.Any(a => a.fire >= a.nextSpread - 2f
                && Mathf.Abs(n.c - a.c) + Mathf.Abs(n.r - a.r) == 1
                && (n.type == "hob" || n.type == "oven" || n.type == "counter" || n.type == "prep" || n.type == "provider" || n.type == "plates"));
            if (!threatened) continue;
            Rect rect = CellRect(n.c, n.r, n.w, n.h);
            float t = Mathf.Abs(Mathf.Sin(Time.time * 4f));
            MakeSprite("fwarn-glow-" + n.id, circleSprite, rect.center, new Vector2(rect.width * .98f, rect.height * .82f), new Color(1f, .42f, .12f, .16f + t * .18f), 21);
            for (int k = 0; k < 2; k++)
                MakeSprite("fwarn-smoke-" + n.id + k, circleSprite, rect.center + new Vector2(Mathf.Sin(Time.time * 2f + k) * .1f, rect.height * (.28f + t * .26f) + k * .12f), new Vector2(rect.width * .4f, rect.width * .4f), new Color(.22f, .21f, .23f, .34f - k * .12f), 62);
        }
    }

    void DrawFire(Appliance a, Rect rect)
    {
        Sprite fl = objectSprites.TryGetValue("flame", out var fs) && fs ? fs : circleSprite;
        float baseSize = rect.width * .5f;
        int n = 3 + Mathf.Min(3, (int)(a.fire / 6f));           // more tongues as the blaze grows
        // floor danger glow
        MakeSprite("firewarn-" + a.id, circleSprite, rect.center + new Vector2(0, -.05f), new Vector2(rect.width * 1.3f, rect.height * .9f), new Color(1f, .32f, .08f, .17f), 22);
        for (int i = 0; i < n; i++) {
            float ph = Time.time * (8f + i * 1.3f) + i * 2.1f;
            float fx = Mathf.Sin(ph) * rect.width * .24f;
            float fy = rect.height * .06f + Mathf.Abs(Mathf.Sin(ph * 1.7f)) * rect.height * .3f;
            float sz = baseSize * (.62f + .34f * Mathf.Abs(Mathf.Sin(ph * 1.3f + i)));
            Color c = (i % 2 == 0) ? new Color(1f, .55f, .12f, .95f) : new Color(1f, .84f, .28f, .95f);
            MakeSprite("fire-" + a.id + "-" + i, fl, rect.center + new Vector2(fx, fy), new Vector2(sz, sz * 1.5f), c, 60 + i);
        }
        MakeSprite("firesmoke-" + a.id, circleSprite, rect.center + new Vector2(Mathf.Sin(Time.time * 2.1f) * .12f, rect.height * .6f), new Vector2(baseSize * 1.1f, baseSize * 1.1f), new Color(.16f, .15f, .15f, .42f), 64);
    }

    void DrawStationEffects(Appliance a, Rect rect)
    {
        if (a == null) return;
        bool cookStation = a.type == "hob" || a.type == "oven" || a.type == "espresso";
        if (cookStation && a.item != null) DrawCookEffect(a, rect);
        if ((a.type == "prep" && holdTarget == a && holdProgress > 0) || WorkerTargeting(a, "PREP") || WorkerTargeting(a, "PLATE")) {
            DrawPrepEffect(rect.center);
        }
        if (a.type == "counter" && (WorkerTargeting(a, "PREP") || WorkerTargeting(a, "PLATE"))) {
            DrawPrepEffect(rect.center);
        }
        if (a.type == "sink" && WorkerTargeting(a, "WASH")) DrawWaterFlow(rect.center);
        if (a.type == "table" && WorkerTargeting(a, "WASH")) DrawCleanEffect(rect.center);
    }

    void DrawCookEffect(Appliance a, Rect rect)
    {
        bool active = a.item != null && !IngredientReady(a.item);
        Color heat = a.type == "espresso" ? violet : a.type == "oven" ? red : gold;
        float pulse = .5f + Mathf.Sin(Time.time * 10f + rect.center.x) * .5f;
        Color glow = Color.Lerp(heat, Color.white, .18f);
        glow.a = active ? .15f + pulse * .18f : .08f;
        MakeRect("station-glow", rect.center + new Vector2(0, .02f), new Vector2(.52f, .18f), glow, 26);
        int wisps = active ? 3 : 1;
        for (int i = 0; i < wisps; i++) {
            float t = Time.time * (1.5f + i * .35f) + i * 1.7f;
            float x = Mathf.Sin(t) * .08f + (i - 1) * .08f;
            float y = .21f + Mathf.Repeat(t * .06f, .16f);
            Color steam = new Color(.92f, .96f, 1f, active ? .18f : .08f);
            var wisp = MakeRect("steam", rect.center + new Vector2(x, y), new Vector2(.03f, .16f), steam, 36);
            SetBillboardAngle(wisp, Mathf.Sin(t * 1.4f) * 18f);
        }
    }

    void DrawPrepEffect(Vector2 center)
    {
        float pulse = .5f + Mathf.Sin(Time.time * 18f) * .5f;
        Color cut = new Color(.9f, 1f, .84f, .3f + pulse * .22f);
        var a = MakeRect("prep-cut-a", center + new Vector2(-.08f, .08f), new Vector2(.22f, .025f), cut, 37);
        SetBillboardAngle(a, -32f);
        var b = MakeRect("prep-cut-b", center + new Vector2(.08f, .02f), new Vector2(.18f, .022f), cut, 37);
        SetBillboardAngle(b, 28f);
        MakeRect("prep-spark", center + new Vector2(.02f, .12f), new Vector2(.045f, .045f), new Color(.95f, 1f, .78f, .28f), 38);
    }

    void DrawCleanEffect(Vector2 center)
    {
        float pulse = .5f + Mathf.Sin(Time.time * 16f) * .5f;
        Color shine = new Color(.65f, .9f, 1f, .22f + pulse * .2f);
        var a = MakeRect("clean-shine-a", center + new Vector2(-.06f, .12f), new Vector2(.22f, .022f), shine, 38);
        SetBillboardAngle(a, 24f);
        var b = MakeRect("clean-shine-b", center + new Vector2(.08f, .02f), new Vector2(.16f, .02f), shine, 38);
        SetBillboardAngle(b, -28f);
    }

    bool WorkerTargeting(Appliance a, string task)
    {
        if (a == null) return false;
        // match on the worker actually STANDING at the appliance (its target is the offset approach
        // point, so the old .12 distance-to-centre check never passed and station effects never drew).
        return workers.Any(w => w.task == task && DistanceToAppliance(w.pos, a) < .5f);
    }

    void DrawFood(string id, Vector2 p, float size, string state = "", int order = 30)
    {
        string sprite = FoodSpriteName(id, state);
        MakeSprite("food-" + id, FoodSprite(sprite), p, Vector2.one * size, state == "burnt" ? BurntTint : Color.white, order);
        if (state == "burnt") MakeSprite("food-smoke-" + id, circleSprite, p + new Vector2(.05f, size * .5f), Vector2.one * size * .5f, new Color(.2f, .2f, .22f, .35f), order + 1);
    }

    static readonly Color BurntTint = new Color(.26f, .22f, .2f, 1);

    // Split so a headless economy sim can step the REAL shift loop at a fixed dt instead of
    // reimplementing it; the game itself still calls the no-arg version every frame.
    void UpdatePlay() { UpdatePlay(Time.deltaTime); }

    void UpdatePlay(float dt)
    {
        // top-right corner = pause. This fires on pointer-DOWN, which is also what makes pause reachable
        // at all: BuildPlayUI recreates the MENU button every rebuild tick, so its onClick (which needs
        // down+up on the same object) almost never completes. It must NOT ShowMenu() — that silently
        // abandoned the whole shift with no result screen.
        if (Input.GetMouseButtonDown(0) && MenuHotspot(Input.mousePosition)) {
            TogglePause();
            return;
        }
        // ticket OR the dish over a guest's head -> full order breakdown (any tap closes it again)
        if (Input.GetMouseButtonDown(0)) {
            if (detailRecipe != null) { detailRecipe = null; BuildPlayUI(); return; }
            var hit = TicketAt(Input.mousePosition) ?? OrderIconAt(Input.mousePosition);
            if (hit != null) { detailRecipe = hit; BuildPlayUI(); return; }
        }
        shiftElapsed += dt;
        shiftTime = Mathf.Max(0, shiftTime - dt);
        rushActive = IsRushWindow();
        Vector2 move = joyMove;
        if (move.sqrMagnitude < .01f) move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (move.sqrMagnitude > 1) move.Normalize();
        playerWalking = false;
        if (move.sqrMagnitude > .01f) {
            hasMoveTarget = false;
            tapAction = null;
            playerFacing = move.normalized;
            playerWalking = true;
            playerPos = MoveActorWithCollision(playerPos, playerPos + move * (2.2f + save.speed * .18f) * dt, .16f);
        } else if (hasMoveTarget) {
            Vector2 way = SteerAlongPath(playerPos, moveTarget, .16f, null, ref playerWaypoint, ref playerPathTimer, dt);   // route around furniture
            Vector2 delta = way - playerPos;
            if (delta.sqrMagnitude > .002f) {
                playerFacing = delta.normalized;
                playerWalking = true;
            }
            Vector2 next = Vector2.MoveTowards(playerPos, way, (2.2f + save.speed * .18f) * dt);
            Vector2 moved = MoveActorWithCollision(playerPos, next, .16f);
            // Clipping a doorjamb for one frame is normal and is NOT a reason to abandon the trip.
            // This used to drop moveTarget the very first frame collision ate the step, which is
            // exactly why tapping across the room so often stopped dead at the kitchen door: one
            // graze and the whole order was forgotten. Now a stall first forces a fresh route, and
            // only a genuine wedge (most of a second with no progress at all) cancels.
            if (Vector2.Distance(moved, playerPos) < .002f && Vector2.Distance(playerPos, moveTarget) > .12f) {
                playerStall += dt;
                if (playerStall > .10f) { playerPathTimer = 0f; playerWaypoint = Vector2.zero; }
                if (playerStall > .75f) { hasMoveTarget = false; tapAction = null; playerStall = 0f; }
            } else playerStall = 0f;
            playerPos = moved;
            if (Vector2.Distance(playerPos, moveTarget) < .04f) {
                hasMoveTarget = false;
                if (tapAction != null) {
                    Interact(tapAction);
                    tapAction = null;
                }
            }
        }
        playerPos.x = Mathf.Clamp(playerPos.x, CellCenter(0, 0).x - .25f, CellCenter(Cols - 1, 0).x + .25f);
        playerPos.y = Mathf.Clamp(playerPos.y, CellCenter(0, Rows - 1).y - .25f, CellCenter(0, 0).y + .25f);
        if (playerObj && playerObj.GetComponent<SpriteRenderer>()) {
            playerObj.transform.position = BillboardPoint(playerPos, 25);
            playerObj.transform.rotation = BillboardRotation();
        }
        if (Input.GetKeyDown(KeyCode.Space)) InteractNearest();
        HandleHold(dt, actHeld || Input.GetKey(KeyCode.Space) || holdPressed || pointerHoldAct);
        if (Input.GetMouseButtonDown(0) && !PointerOverUI() && Input.mousePosition.y > Screen.height * TouchBand) {
            Vector2 p = ScreenToGamePoint(Input.mousePosition);
            Appliance a = ApplianceAtScreen(Input.mousePosition);   // pick the prop you actually clicked
            if (a != null) {
                if (DistanceToAppliance(playerPos, a) < InteractionRange(a)) Interact(a);
                else {
                    moveTarget = ApproachPoint(a);
                    tapAction = a;
                    hasMoveTarget = true;
                    SetMessage("Going to " + ApplianceLabel(a), .65f);
                }
            } else {
                infoTimer = 0; infoAppliance = null;   // tapping empty floor closes the info balloon
                if (WorldInsideGrid(p) && IsWalkablePosition(p, .16f)) {
                    moveTarget = p;
                    tapAction = null;
                    hasMoveTarget = true;
                }
            }
        }
        // HOLD TO KEEP WALKING. A tap sets one destination; holding steers continuously toward the
        // finger, which is what makes the game fully playable with the on-screen pads switched off.
        // Holding ON a station already within reach works it instead (chop, wash) — otherwise there
        // would be no way to run a hold action without the ACT pad.
        bool pointerDown = Input.GetMouseButton(0) && !PointerOverUI()
                           && Input.mousePosition.y > Screen.height * TouchBand;
        pointerHoldAct = false;
        if (pointerDown) {
            pointerHold += dt;
            if (pointerHold > .18f) {                       // .18s so a quick tap stays a tap
                Appliance under = ApplianceAtScreen(Input.mousePosition);
                if (under != null && DistanceToAppliance(playerPos, under) < InteractionRange(under)) {
                    pointerHoldAct = true;
                    hasMoveTarget = false;
                } else {
                    Vector2 wp = ScreenToGamePoint(Input.mousePosition);
                    if (WorldInsideGrid(wp)) {
                        moveTarget = IsWalkablePosition(wp, .16f) ? wp : SafeOpenPosition(wp, .16f);
                        tapAction = under;                  // held over a distant station: go and use it
                        hasMoveTarget = true;
                    }
                }
            }
        } else pointerHold = 0f;

        // NOTE: no hover-info during play — it popped cards constantly while you were cooking. The
        // hover preview lives on the FLOORPLAN screen only; in play you TAP a station to read it.
        if (infoTimer > 0) infoTimer -= dt;
        if (queue > 0 && SpawnCustomer()) queue--;
        spawnTimer -= dt;
        if (queueComplaintCooldown > 0) queueComplaintCooldown -= dt;
        if (spawnTimer <= 0) {
            if (!SpawnCustomer()) {
                queue = Mathf.Min(queueMax, queue + 1);
                if (queue >= queueMax) {
                    queue = Mathf.Max(1, queue - 2);
                    queueComplaints++;
                    // the FIRST overflow is a free warning; after that, at most one complaint per ~9s,
                    // and it never fails the clean goal (a full house isn't a mistake).
                    if (queueComplaints == 1) SetMessage("Queue is full!", 1.1f);
                    else if (queueComplaintCooldown <= 0) { AddComplaint("Queue angry", false); queueComplaintCooldown = 9f; }
                }
            }
            spawnTimer = CurrentSpawnDelay();
        }
        UpdateCustomerMotion(dt);
        foreach (var c in customers.ToList()) {
            // !c.leaving is critical: `seated` stays true through the ~0.72s stand-up animation, so
            // without it a single walkout re-fired AddComplaint every frame and failed the day in ~5 frames.
            if (!c.served && c.seated && !c.leaving) {
                c.patience -= dt * .70f;   // gentler drain — guests were still running out too fast
                if (c.patience <= 0) {
                    // a critic walkout is a SINGLE (weighted) complaint now — the old double hit was
                    // silent and second_wind could only forgive one of the pair.
                    AddComplaint(c.typeId == "critic" ? "Critic left!" : "Customer left");
                    missed++;
                    if (!HasPerk("showman")) combo = 0;   // SHOWMAN keeps the combo through walkouts
                    BeginCustomerDeparture(c, false);
                }
            }
        }
        SweepOrphanPlates(dt);
        UpdatePopups(dt);
        UpdateStations(dt);
        UpdateHazards(dt);
        if (fireFailed) return;         // day already ended by flashover
        UpdateWorkers(dt);
        rebuildTimer -= dt;
        if (rebuildTimer <= 0) {
            rebuildTimer = customers.Count > 0 ? .033f : .12f;   // ~30fps dynamic rebuild (static layer is free now)
            BuildPlayUI();
            RebuildWorld();
        }
        if (complaints >= 5) {
            // Offer the save BEFORE ending the day — once FinishDay runs, the shift state is gone
            // and "continue" would mean replaying, which is not the same product. One per shift.
            if (!secondChanceUsed && !adOverlayUp) { secondChanceUsed = true; OfferSecondChance(); }
            else if (!adOverlayUp) FinishDay(false);
        }
        else if (served >= goal || shiftTime <= 0) FinishDay(served >= goal);   // goal reached OR closing time
    }

    void ShowStationInfo(Appliance a, float dur)
    {
        if (a == null) return;
        if (infoAppliance != a) { infoAppliance = a; infoTimer = dur; }
        else infoTimer = Mathf.Max(infoTimer, dur);
    }

    // screen point -> the canvas-space rect of a ticket row (canvas Y runs from the TOP)
    Recipe TicketAt(Vector2 screenPoint)
    {
        if (Screen.width <= 0 || Screen.height <= 0) return null;
        float cx = screenPoint.x / Screen.width * W;
        float cy = (1f - screenPoint.y / Screen.height) * H;
        foreach (var row in ticketRows)
            if (row.rect.Contains(new Vector2(cx, cy))) return row.recipe;
        return null;
    }

    // the dish floating over a seated guest's head is a tap target too
    Recipe OrderIconAt(Vector2 screenPoint)
    {
        if (!cam || orderIcons.Count == 0) return null;
        Vector3 u0 = cam.WorldToScreenPoint(GameGroundPoint(Vector2.zero, .3f));
        Vector3 u1 = cam.WorldToScreenPoint(GameGroundPoint(new Vector2(1f, 0f), .3f));
        float unitPx = Mathf.Max(10f, Vector2.Distance((Vector2)u0, (Vector2)u1));   // screen px per world unit
        foreach (var o in orderIcons) {
            Vector3 sp = cam.WorldToScreenPoint(BillboardPoint(o.world, 44) + Vector3.up * o.lift);
            if (sp.z <= 0) continue;
            if (Vector2.Distance(new Vector2(sp.x, sp.y), screenPoint) < unitPx * o.size * .8f) return o.recipe;
        }
        return null;
    }

    bool MenuHotspot(Vector2 screenPoint)
    {
        // generous top-right zone that fully covers the MENU button, so a single tap opens pause
        // (the on-screen button is recreated every rebuild, so its own onClick can't be relied on)
        float width = Mathf.Max(150f, Screen.width * .30f);
        float height = Mathf.Max(96f, Screen.height * .12f);
        return screenPoint.x >= Screen.width - width && screenPoint.y >= Screen.height - height;
    }

    void UpdateCustomerMotion(float dt)
    {
        foreach (var c in customers.ToList()) {
            if (c.table == null) continue;
            c.animationClock += dt;

            if (c.served && !c.leaving) {
                c.eatTimer += dt;
                if (c.eatTimer < CustomerEatDuration) continue;
                BeginCustomerDeparture(c, true);
            }

            if (c.sittingDown) {
                c.transitionTime += dt;
                if (c.transitionTime >= CustomerSitDuration) {
                    c.transitionTime = CustomerSitDuration;
                    c.sittingDown = false;
                    c.seated = true;
                    c.facing = Vector2.down;
                }
                continue;
            }

            if (c.standingUp) {
                c.transitionTime += dt;
                if (c.transitionTime >= CustomerStandDuration) {
                    c.transitionTime = CustomerStandDuration;
                    c.standingUp = false;
                    c.seated = false;
                    if (c.table != null && c.table.customer == c) {
                        c.table.customer = null;
                        c.table.dirty = c.dirtyOnLeave;
                    }
                }
                continue;
            }

            if (c.leaving && c.seated) {
                c.standingUp = true;
                c.transitionTime = 0;
                c.facing = Vector2.down;
                continue;
            }
            if (!c.leaving && c.seated) continue;

            Vector2 target = c.leaving ? CustomerExitPoint() : CustomerSeatPosition(c);
            if (c.visualPos == Vector2.zero) c.visualPos = c.leaving ? CustomerSeatPosition(c) : CustomerEntryPoint(c.table);
            Vector2 way = SteerAlongPath(c.visualPos, target, .15f, c.table, ref c.pathWaypoint, ref c.pathTimer, dt);   // walk around tables/units
            Vector2 delta = way - c.visualPos;
            if (delta.sqrMagnitude > .002f) c.facing = delta.normalized;
            Vector2 next = Vector2.MoveTowards(c.visualPos, way, (c.leaving ? 1.55f : 1.25f) * dt);
            c.visualPos = MoveActorWithCollision(c.visualPos, next, .15f, c.table);
            // watchdog: if a guest can't reach their seat within ~6s (blocked path), force-seat them so
            // the table doesn't stay locked out of rotation forever with a frozen patience clock.
            if (!c.leaving) {
                c.seatTimer += dt;
                if (c.seatTimer > 6f) { c.visualPos = target; }
            }
            if (!c.leaving && Vector2.Distance(c.visualPos, target) < .045f) {
                c.visualPos = target;
                c.sittingDown = true;
                c.transitionTime = 0;
                c.facing = Vector2.down;
            }
            if (c.leaving && Vector2.Distance(c.visualPos, target) < .06f) {
                customers.Remove(c);
            }
        }
    }

    void UpdateStations(float dt)
    {
        foreach (var station in appliances.Where(a => (a.type == "hob" || a.type == "oven" || a.type == "espresso") && a.item != null).ToList()) {
            if (station.fire > 0) continue;                                    // a burning station isn't cooking anything
            string doneState = station.type == "hob" ? "cooked" : station.type == "oven" ? "baked" : "brewed";
            bool canBurn = station.type == "hob" || station.type == "oven";   // leave it on the heat too long and it chars
            float cook = CookDuration();
            float burnGrace = cook * 3.2f;                                     // generous window after it's done before it chars
            station.item.progress += dt;
            if (station.item.state == "burnt") {
                // left charred on the heat even longer -> it catches FIRE
                if (canBurn && station.item.progress >= cook + burnGrace * 2.4f) {
                    Ignite(station);
                    station.item = null;
                }
                continue;
            }
            if (station.item.state != doneState) {
                if (station.item.progress >= cook) {
                    station.item.state = doneState;
                    station.item.progress = cook;                             // hold; the burn clock keeps ticking from here
                    SetMessage(station.item.id.ToUpperInvariant() + " ready", .8f);
                    AddPopup(ApplianceCenter(station) + new Vector2(0, .5f), "READY!", mint, true);   // the best positive beat deserves a pop (#26)
                    Shake(.03f);
                    PlayEventSound("ready");
                }
            } else if (canBurn && station.item.progress >= cook + burnGrace) {  // ~two cooks of grace, then it burns
                station.item.state = "burnt";
                SetMessage(station.item.id.ToUpperInvariant() + " BURNT!", 1.3f);
                AddPopup(ApplianceCenter(station) + new Vector2(0, .5f), "BURNT!", red, true);
                PlayEventSound("bad");
                Shake(.05f);
            }
        }
    }

    void Ignite(Appliance a)
    {
        if (a == null || a.fire > 0) return;
        a.fire = .01f;
        a.nextSpread = 5f;      // first spread only after ~5s of established burning
        SetMessage("FIRE! grab the extinguisher", 1.6f);
        AddPopup(ApplianceCenter(a) + new Vector2(0, .5f), "FIRE!", red, true);
        PlayEventSound("bad");
        Shake(.14f);
        Buzz(60);
    }

    // fire grows, spreads to neighbours, and reaches flashover (day-ending) if never put out
    void UpdateHazards(float dt)
    {
        var burning = appliances.Where(a => a.fire > 0).ToList();
        if (burning.Count == 0) return;
        Shake(.02f + Mathf.Min(.06f, burning.Count * .015f));   // constant rumble while the kitchen burns
        foreach (var a in burning) {
            a.fire += dt;
            if (a.fire > 22f && !fireFailed) {                  // flashover -> the day is lost
                fireFailed = true;
                SetMessage("KITCHEN FIRE — service lost", 2f);
                Shake(.3f); Buzz(120);
                FinishDay(false);
                return;
            }
            if (a.fire >= a.nextSpread) {                       // established fire jumps to ONE adjacent station, then cools ~5s
                a.nextSpread = a.fire + 5f;
                foreach (var n in appliances) {
                    if (n.fire > 0 || n.type == "extinguisher") continue;
                    bool adjacent = Mathf.Abs(n.c - a.c) + Mathf.Abs(n.r - a.r) == 1;
                    if (adjacent && (n.type == "hob" || n.type == "oven" || n.type == "counter" || n.type == "prep" || n.type == "provider" || n.type == "plates")) {
                        Ignite(n);
                        break;
                    }
                }
            }
        }
    }

    void HandleHold(float dt, bool pressed)
    {
        // fire extinguisher: hold ACT near any burning station to knock its fire down (continuous, no bar)
        if (pressed && holding?.kind == "tool" && holding.id == "extinguisher") {
            var fire = NearestBurning(.9f);
            if (fire != null) {
                holdTarget = fire;
                holdProgress = 0;
                fire.fire = Mathf.Max(0f, fire.fire - dt * 8f);
                Shake(.015f);
                if (fire.fire <= 0f) { SetMessage("Fire out!", 1f); PlayEventSound("wash"); RebuildWorld(); }
                return;
            }
        }
        var nearest = NearestAppliance(.58f);
        if (!pressed || nearest == null || !HoldableAppliance(nearest)) {
            holdTarget = null;
            holdProgress = 0;
            return;
        }
        holdTarget = nearest;
        float duration = nearest.type == "sink" ? WashDuration() : PrepDuration();
        holdProgress += dt / duration;
        if (holdProgress < 1f) return;

        if (nearest.type == "sink" && holding?.kind == "plate" && holding.dirty) {
            holding.dirty = false;
            SetMessage("Plate washed", .8f);
            AddPopup(ApplianceCenter(nearest) + new Vector2(0, .4f), "WASHED", blue, true);   // completion juice (#27)
            Shake(.02f);
            PlayEventSound("wash");
        } else if ((nearest.type == "prep" || nearest.type == "counter") && nearest.item?.kind == "ingredient" && NeedsPrep(nearest.item.id)) {
            nearest.item.state = "ready";
            SetMessage(nearest.item.id.ToUpperInvariant() + " chopped", .8f);
            AddPopup(ApplianceCenter(nearest) + new Vector2(0, .4f), "CHOPPED", green, true);
            Shake(.02f);
            PlayEventSound("prep");
        }
        holdProgress = 0;
        holdTarget = null;
        RebuildWorld();
    }

    bool HoldableAppliance(Appliance a)
    {
        if (a == null) return false;
        if (a.type == "sink") return holding?.kind == "plate" && holding.dirty;
        // Chopping happens ON the prep counter: put the ingredient DOWN first (tap), then hold ACT
        // over it to chop. You can't chop something still in your hands.
        if (a.type == "prep" || a.type == "counter")
            return a.item?.kind == "ingredient" && NeedsPrep(a.item.id) && a.item.state != "ready";
        return false;
    }

    Appliance NearestAppliance(float maxDistance)
    {
        Appliance best = null;
        float bestD = maxDistance;
        foreach (var a in appliances) {
            float d = DistanceToAppliance(playerPos, a);
            if (d < bestD) {
                best = a;
                bestD = d;
            }
        }
        return best;
    }

    Appliance NearestBurning(float maxDistance)
    {
        Appliance best = null;
        float bestD = maxDistance;
        foreach (var a in appliances) {
            if (a.fire <= 0) continue;
            float d = DistanceToAppliance(playerPos, a);
            if (d < bestD) { best = a; bestD = d; }
        }
        return best;
    }

    void UpdateWorkers(float dt)
    {
        foreach (var w in workers) {
            Vector2 before = w.pos;
            Vector2 way = SteerAlongPath(w.pos, w.target, .15f, null, ref w.pathWaypoint, ref w.pathTimer, dt);   // staff route around the kitchen too
            Vector2 next = Vector2.MoveTowards(w.pos, way, (1.55f + save.speed * .05f) * dt);
            w.pos = MoveActorWithCollision(w.pos, next, .15f);
            // FACE THE WAY WE ARE HEADING, not the distance actually covered. The old test used the
            // ACHIEVED delta against a .0005 sq threshold — one frame of travel is only ~.026 at 60fps
            // (and far less at 120fps+, or when collision slides the move), so the facing frequently
            // never updated and the worker kept a stale direction: walking down while showing their
            // back. Steering direction is frame-rate independent and always correct.
            Vector2 heading = way - before;
            if (heading.sqrMagnitude > .0004f) w.facing = heading.normalized;
            if (!string.IsNullOrEmpty(w.task) && w.task != "IDLE") w.actionPulse = Mathf.Max(0, w.actionPulse - dt);

            if (w.pendingArrival) w.taskAge += dt; else w.taskAge = 0;
            // no-progress stuck detector: reset when the target changes, else track closest approach
            if (w.target != w.prevTarget) { w.prevTarget = w.target; w.bestDist = float.MaxValue; w.noProgress = 0; }
            float distToTarget = Vector2.Distance(w.pos, w.target);
            if (distToTarget < w.bestDist - .08f) { w.bestDist = distToTarget; w.noProgress = 0; } else w.noProgress += dt;
            bool moving = distToTarget > .045f;
            if (moving) {
                w.arrivalPause = 0;
                // truly stuck (no progress for ~5s): snap to the target so the pickup/place happens
                // AT the station on the next frame, never from across the room.
                if (w.pendingArrival && w.noProgress > 5f) { w.pos = w.target; w.noProgress = 0; }
                // an IDLE, uncommitted waiter re-decides while drifting home, so it reacts instantly
                // when a guest is seated or a plate becomes ready mid-walk.
                else if (w.role == "waiter" && !w.pendingArrival && w.transportKind == null && w.carry == null && (string.IsNullOrEmpty(w.task) || w.task == "IDLE")) {
                    w.timer -= dt;
                    if (w.timer <= 0) { w.timer = WorkerCycle(w.role); WorkerWaiter(w); }
                }
                continue;
            }

            if (w.pendingArrival) {
                if (w.arrivalPause <= 0) { w.arrivalPause = .3f; continue; }
                w.arrivalPause -= dt;
                if (w.arrivalPause > 0) continue;
                if (w.transportKind != null) ResolveTransport(w);   // cook/prepper two-leg
                else ResolveWaiterArrival(w);                       // waiter/washer
                w.timer = WorkerCycle(w.role);
                continue;
            }

            // legacy cosmetic-carry cleanup — but never delete a waiter's real held plate; redrop it
            if (w.carry != null || w.carryRecipe != null) {
                if (w.arrivalPause <= 0) { w.arrivalPause = .28f; continue; }
                w.arrivalPause -= dt;
                if (w.arrivalPause > 0) continue;
                w.arrivalPause = 0;
                if (w.role == "waiter" && w.carry?.kind == "plate") {
                    var free = appliances.FirstOrDefault(a => a.type == "counter" && a.item == null && a.fire <= 0);
                    if (free != null) { free.item = w.carry; w.carry = null; w.carryRecipe = null; }   // else keep holding, retry
                } else { w.carry = null; w.carryRecipe = null; }
            }

            w.timer -= dt;
            if (w.timer > 0) continue;
            w.timer = WorkerCycle(w.role);
            if (w.role == "waiter") WorkerWaiter(w);
            if (w.role == "cook") WorkerCook(w);
            if (w.role == "washer") WorkerWasher(w);
            if (w.role == "prepper") WorkerPrepper(w);
        }
    }

    // a station is spoken-for if any worker is fetching/placing/washing/picking-up at it
    bool StationReserved(Appliance a) => a != null && workers.Any(x => x.fetchFrom == a || x.placeTo == a || x.pickupCounter == a || x.washTable == a);

    // begin a two-leg transport: walk to `from`, pick up on arrival, walk to `to`, place on arrival.
    // The item stays on its origin until the worker physically reaches it — no teleport, no duplicate.
    void StartTransport(Worker w, string kind, Appliance from, Appliance to, string spawnId = null)
    {
        w.transportKind = kind; w.fetchFrom = from; w.placeTo = to; w.spawnId = spawnId;
        w.carry = null; w.carryRecipe = null;
        w.pendingArrival = true; w.arrivalPause = 0; w.taskAge = 0;
        if (from != null) { w.delivering = false; w.task = kind == "chop" ? "PREP" : "FETCH"; w.target = WorkerApproachPoint(w, from); }
        else { w.delivering = true; w.task = "COOK"; w.target = WorkerApproachPoint(w, to); }
    }

    void EndTransport(Worker w)
    {
        w.carry = null; w.carryRecipe = null; w.targetCustomer = null;
        w.transportKind = null; w.fetchFrom = null; w.placeTo = null; w.spawnId = null; w.delivering = false;
        w.pendingArrival = false; w.arrivalPause = 0; w.taskAge = 0;
        w.task = "IDLE"; w.target = w.pos;
    }

    void ResolveTransport(Worker w)
    {
        string k = w.transportKind;
        // ---- leg 1: at the source, pick up (or chop / assemble in place) ----
        if (!w.delivering && w.fetchFrom != null) {
            if (k == "chop") {
                if (w.fetchFrom.item?.kind == "ingredient" && NeedsPrep(w.fetchFrom.item.id)) {
                    w.fetchFrom.item.state = "ready";
                    AddPopup(ApplianceCenter(w.fetchFrom) + new Vector2(0, .4f), "CHOPPED", green, true);
                    PlayEventSound("prep");
                }
                EndTransport(w); return;
            }
            if (k == "assemble") {   // add the next plate part ON ARRIVAL (re-evaluated so it stays valid)
                var parts = w.targetCustomer?.orderParts ?? w.targetCustomer?.recipe.parts.ToList();
                if (parts != null && w.fetchFrom.item?.kind == "plate") { if (TryAddPrepperPart(parts, w.fetchFrom)) PlayEventSound("place"); }
                EndTransport(w); return;
            }
            w.carry = w.fetchFrom.item; w.fetchFrom.item = null; w.carryRecipe = null;
            if (w.carry == null || w.placeTo == null) { EndTransport(w); return; }
            w.delivering = true;
            w.task = k == "cook" ? "COOK" : "READY";
            w.pendingArrival = true; w.arrivalPause = 0; w.taskAge = 0;
            w.target = WorkerApproachPoint(w, w.placeTo);
            return;
        }
        // ---- spawn: no fetch leg, create the ingredient at the destination ----
        if (k == "spawn") {
            if (w.placeTo != null && w.placeTo.item == null && w.placeTo.fire <= 0) {
                w.placeTo.item = Item.Ingredient(w.spawnId, ProviderState(w.spawnId));
                w.placeTo.item.progress = 0; PlayEventSound("cook");
            }
            EndTransport(w); return;
        }
        // ---- leg 2: at the destination, place / bin the carried item ----
        var carried = w.carry;
        if (carried == null) { EndTransport(w); return; }
        if (k == "trash") { PlayEventSound("trash"); }
        else if (k == "plate") {
            if (w.placeTo?.item?.kind == "plate" && !w.placeTo.item.dirty && IngredientReady(carried)) w.placeTo.item.parts.Add(carried.id);
            else DropOnFreeCounter(carried);
        } else {   // cook / counter
            if (w.placeTo != null && w.placeTo.item == null && w.placeTo.fire <= 0) {
                w.placeTo.item = carried;
                if (k == "cook") w.placeTo.item.progress = 0;
                PlayEventSound(k == "cook" ? "cook" : "place");
            } else DropOnFreeCounter(carried);
        }
        EndTransport(w);
    }

    void DropOnFreeCounter(Item it)
    {
        var free = appliances.FirstOrDefault(a => a.type == "counter" && a.item == null && a.fire <= 0);
        if (free != null) free.item = it;
    }

    float WorkerCycle(string role)
    {
        if (role == "waiter") return 1.05f;
        if (role == "cook") return 1.45f;
        if (role == "prepper") return 1.9f;
        return 1.75f;
    }

    Vector2 ApplianceCenter(Appliance a) => CellRect(a.c, a.r, a.w, a.h).center;

    Vector2 WorkerApproachPoint(Worker w, Appliance a)
    {
        if (a == null) return w.pos;
        Rect rect = CellRect(a.c, a.r, a.w, a.h);
        Vector2 delta = w.pos - rect.center;
        if (delta.sqrMagnitude < .01f) delta = Vector2.down;
        Vector2 preferred = rect.center + delta.normalized * (Mathf.Max(rect.width, rect.height) * .5f + .2f);
        return SafeOpenPosition(preferred, .15f);
    }

    bool WorkerCustomerReserved(Customer customer)
    {
        return workers.Any(worker => worker.pendingArrival && worker.targetCustomer == customer);
    }

    void WorkerWaiter(Worker w)
    {
        // still holding an undelivered dish (all counters were full last cycle) — drop it as soon as one frees
        if (w.carry != null && w.carry.kind == "plate") {
            var free = appliances.FirstOrDefault(a => a.type == "counter" && a.item == null && a.fire <= 0);
            if (free != null) { free.item = w.carry; w.carry = null; w.carryRecipe = null; }
            else { w.task = "IDLE"; return; }
        }
        // most impatient guest first, so the guest about to leave gets served first
        var order = customers.Where(c => c.seated && !c.ordered && !c.leaving && !WorkerCustomerReserved(c))
                             .OrderBy(c => c.patience).FirstOrDefault();
        if (order != null) {
            if (order.table != null) w.target = WorkerApproachPoint(w, order.table);
            w.task = "ORDER";
            w.carry = null;
            w.carryRecipe = null;
            w.targetCustomer = order;
            w.pendingArrival = true;
            w.arrivalPause = 0;
            return;
        }
        // find a ready plate on a counter whose dish matches a waiting guest (lowest patience first).
        // The waiter WALKS TO THE COUNTER to pick it up — it is NOT teleported into their hands.
        foreach (var counter in appliances.Where(a => a.type == "counter" && a.fire <= 0 && a.item?.kind == "plate" && !a.item.dirty && !StationReserved(a))) {
            var table = customers.Where(c => c.ordered && !c.served && !c.mealServed && !c.leaving && !WorkerCustomerReserved(c) && PlateMatchesOrder(counter.item, c))
                                 .OrderBy(c => c.patience).FirstOrDefault();
            if (table == null) continue;
            w.pickupCounter = counter;               // grab it when we arrive
            w.target = WorkerApproachPoint(w, counter);
            w.task = "PICKUP";
            w.carry = null;                          // hands empty until we reach the counter
            w.carryRecipe = null;
            w.targetCustomer = table;
            w.pendingArrival = true;
            w.arrivalPause = 0;
            return;
        }
        var drinkStation = appliances.FirstOrDefault(a => a.type == "drink");
        var drinkTable = customers.Where(c => c.ordered && c.wantsDrink && !c.drinkServed && c.mealServed && !c.leaving && !WorkerCustomerReserved(c))
                                  .OrderBy(c => c.patience).FirstOrDefault();
        if (drinkStation != null && drinkTable != null) {
            w.pickupCounter = drinkStation;          // walk to the fridge, take a soda on arrival
            w.target = WorkerApproachPoint(w, drinkStation);
            w.task = "GETDRINK";
            w.carry = null;
            w.carryRecipe = null;
            w.targetCustomer = drinkTable;
            w.pendingArrival = true;
            w.arrivalPause = 0;
            return;
        }
        // nothing to do -> return to the kitchen and wait there
        w.task = "IDLE";
        w.carry = null;
        w.carryRecipe = null;
        w.targetCustomer = null;
        w.pickupCounter = null;
        w.pendingArrival = false;
        w.arrivalPause = 0;
        w.target = WaiterHome(w);
    }

    // a spot just inside the kitchen where an idle waiter waits (near the pass, not in the dining room)
    Vector2 WaiterHome(Worker w) => SafeOpenPosition(CellCenter(8, KitchenRow), .15f);

    void ResolveWaiterArrival(Worker w)
    {
        if (w.washTable != null) {                       // washer reached the dirty table -> clear it now
            if (w.washTable.customer == null) w.washTable.dirty = false;
            w.washTable = null;
            w.pendingArrival = false; w.arrivalPause = 0; w.taskAge = 0;
            w.task = "IDLE"; w.target = w.pos;
            PlayEventSound("wash");
            return;
        }
        // PHASE 1 — reached the counter/fridge: pick the item up, then set off for the customer.
        if (w.task == "PICKUP" || w.task == "GETDRINK") {
            var cust = w.targetCustomer;
            bool stillValid = cust != null && !cust.leaving && cust.ordered
                && (w.task == "GETDRINK" ? (cust.wantsDrink && !cust.drinkServed) : (!cust.served && !cust.mealServed));
            if (w.task == "PICKUP") {
                if (stillValid && w.pickupCounter?.item?.kind == "plate" && PlateMatchesOrder(w.pickupCounter.item, cust)) {
                    w.carry = w.pickupCounter.item;   // physically take the exact plate off the counter
                    w.pickupCounter.item = null;
                    w.carryRecipe = cust.recipe;
                    w.task = "SERVE";
                } else { w.pickupCounter = null; ReleaseWaiter(w); return; }
            } else {
                if (stillValid) { w.carry = Item.Drink(); w.task = "DRINK"; }
                else { ReleaseWaiter(w); return; }
            }
            w.pickupCounter = null;
            w.target = cust.table != null ? WorkerApproachPoint(w, cust.table) : w.pos;
            w.pendingArrival = true; w.arrivalPause = 0; w.taskAge = 0;
            return;
        }

        Customer customer = w.targetCustomer;
        bool servedMeal = false;
        if (customer != null && !customer.leaving) {
            if (w.task == "ORDER" && customer.seated && !customer.ordered) {
                customer.ordered = true;
                PlayEventSound("order");
            } else if (w.task == "SERVE" && customer.ordered && !customer.served && !customer.mealServed) {
                ServeMeal(customer);
                PlayEventSound("serve");
                servedMeal = true;
                if (CustomerComplete(customer)) CompleteCustomer(customer);
            } else if (w.task == "DRINK" && customer.wantsDrink && !customer.drinkServed) {
                ServeDrink(customer);
                PlayEventSound("serve");
                if (CustomerComplete(customer)) CompleteCustomer(customer);
            }
        }
        // couldn't deliver the finished dish (customer left / already served) -> put the real plate back
        bool keptPlate = false;
        if (w.task == "SERVE" && !servedMeal && (w.carry != null || w.carryRecipe != null)) {
            var free = appliances.FirstOrDefault(a => a.type == "counter" && a.item == null && a.fire <= 0);
            if (free != null) {
                if (w.carry != null && w.carry.kind == "plate") {
                    free.item = w.carry;                       // exact (possibly modified) dish preserved
                } else {
                    var plate = Item.Plate();
                    plate.parts.AddRange(w.carryRecipe.parts);
                    free.item = plate;
                }
            } else if (w.carry != null && w.carry.kind == "plate") {
                keptPlate = true;                              // no free counter — hold the dish and retry, don't destroy it
            }
        }
        w.pendingArrival = false;
        w.targetCustomer = null;
        w.arrivalPause = 0;
        w.pickupCounter = null;
        if (!keptPlate) { w.carry = null; w.carryRecipe = null; }
        w.task = "IDLE";
        w.target = keptPlate ? w.pos : WaiterHome(w);
    }

    // abort a waiter trip cleanly (guest left / plate gone) and send them back to the kitchen
    void ReleaseWaiter(Worker w)
    {
        // if we were already carrying a plate, drop it on a free counter rather than deleting it
        if (w.carry != null && w.carry.kind == "plate") {
            var free = appliances.FirstOrDefault(a => a.type == "counter" && a.item == null && a.fire <= 0);
            if (free != null) free.item = w.carry;
        }
        w.carry = null; w.carryRecipe = null; w.targetCustomer = null; w.pickupCounter = null;
        w.pendingArrival = false; w.arrivalPause = 0; w.taskAge = 0;
        w.task = "IDLE"; w.target = WaiterHome(w);
    }

    void WorkerCook(Worker w)
    {
        // scrape burnt food off the heat FIRST — nothing else can use that station, IngredientReady is
        // false for burnt so the cook could never lift it, and left alone it eventually ignites.
        // scrape burnt food off the heat FIRST (walk over, bin it on arrival)
        var burntStation = appliances.FirstOrDefault(a => IsCookStation(a) && a.fire <= 0 && a.item?.state == "burnt" && !StationReserved(a));
        if (burntStation != null) {
            var bin = appliances.FirstOrDefault(a => a.type == "trash" && !StationReserved(a)) ?? appliances.FirstOrDefault(a => a.type == "trash");
            StartTransport(w, "trash", burntStation, bin); return;
        }
        // carry a finished item from the heat to a free counter (physically, no teleport)
        var doneStation = appliances.FirstOrDefault(a => IsCookStation(a) && a.item != null && IngredientReady(a.item) && !StationReserved(a));
        if (doneStation != null) {
            var outputCounter = appliances.FirstOrDefault(a => a.type == "counter" && a.item == null && a.fire <= 0 && !StationReserved(a));
            if (outputCounter != null) { StartTransport(w, "cook", doneStation, outputCounter); return; }
            // no free counter — relocate it to the bin so the heat frees instead of igniting (audit #5)
            var bin = appliances.FirstOrDefault(a => a.type == "trash" && !StationReserved(a));
            if (bin != null) { StartTransport(w, "trash", doneStation, bin); return; }
        }
        // carry a raw ingredient from a counter to its cook station
        var source = appliances.FirstOrDefault(a => a.type == "counter" && a.item?.kind == "ingredient" && NeedsStation(a.item) && a.fire <= 0 && !StationReserved(a));
        if (source != null) {
            var station = appliances.FirstOrDefault(a => a.type == StationTypeFor(source.item.id) && a.item == null && a.fire <= 0 && !StationReserved(a));
            if (station != null) { StartTransport(w, "cook", source, station); return; }
        }
        // fetch a needed ingredient from a provider onto an empty cook station
        string needed = NextMissingStationPart();
        if (!string.IsNullOrEmpty(needed)) {
            var emptyStation = appliances.FirstOrDefault(a => a.type == StationTypeFor(needed) && a.item == null && a.fire <= 0 && !StationReserved(a));
            if (emptyStation != null) { StartTransport(w, "spawn", null, emptyStation, needed); return; }
        }
        w.task = "IDLE"; w.carry = null; w.carryRecipe = null;
    }

    void WorkerWasher(Worker w)
    {
        var table = appliances.FirstOrDefault(a => a.type == "table" && a.dirty && a.customer == null && !StationReserved(a));
        if (table != null) {
            // walk over FIRST, then clear on arrival (was cleared instantly from across the room)
            w.washTable = table;
            w.target = WorkerApproachPoint(w, table);
            w.task = "WASH";
            w.carry = null;
            w.carryRecipe = null;
            w.pendingArrival = true;
            w.arrivalPause = 0;
            return;
        }
        w.task = "IDLE";
        w.carry = null;
        w.carryRecipe = null;
    }

    void WorkerPrepper(Worker w)
    {
        // walk to an unchopped ingredient and chop it ON ARRIVAL (was chopped instantly from afar)
        var counter = appliances.FirstOrDefault(a => (a.type == "counter" || a.type == "prep") && a.fire <= 0 && a.item?.kind == "ingredient" && NeedsPrep(a.item.id) && a.item.state != "ready" && !StationReserved(a));
        if (counter != null) { StartTransport(w, "chop", counter, null); return; }

        // walk to a plate-in-progress and add its next part ON ARRIVAL
        foreach (var customer in customers.Where(c => c.ordered && !c.served && !c.mealServed && !c.leaving)) {
            var parts = customer.orderParts ?? customer.recipe.parts.ToList();
            var assembly = AssemblyCounterFor(parts);
            if (assembly == null || StationReserved(assembly)) continue;
            if (!CanAdvancePlate(parts, assembly)) continue;
            w.transportKind = "assemble"; w.fetchFrom = assembly; w.placeTo = null; w.spawnId = null;
            w.targetCustomer = customer; w.carry = null; w.carryRecipe = null; w.delivering = false;
            w.pendingArrival = true; w.arrivalPause = 0; w.taskAge = 0;
            w.task = "PLATE"; w.target = WorkerApproachPoint(w, assembly);
            return;
        }
        w.task = "IDLE";
        w.carry = null;
        w.carryRecipe = null;
    }

    // true if the plate on `counter` can take its next part right now (mirrors TryAddPrepperPart's
    // preconditions WITHOUT mutating), so the prepper never starts a dead trip.
    bool CanAdvancePlate(IList<string> parts, Appliance counter)
    {
        if (counter.item?.kind != "plate" || counter.item.dirty || counter.item.parts.Count >= parts.Count) return false;
        string part = parts[counter.item.parts.Count];
        if (StationPart(part)) return appliances.Any(a => a.type == "counter" && a != counter && a.item?.kind == "ingredient" && a.item.id == part && IngredientReady(a.item));
        return ItemUnlocked(part);
    }

    Appliance AssemblyCounterFor(IList<string> parts)
    {
        var partial = appliances
            .Where(a => a.type == "counter" && a.fire <= 0 && a.item?.kind == "plate" && !a.item.dirty && a.item.parts.Count < parts.Count && PartsPrefixMatches(a.item, parts))
            .OrderByDescending(a => a.item.parts.Count)
            .FirstOrDefault();
        if (partial != null) return partial;

        // start a fresh plate on an empty counter — but keep one free for the cook to offload a
        // finished patty while anything is on the heat, or all counters clog with partials and the
        // hob-bound patty burns (circular stall).
        var emptyCounters = appliances.Where(a => a.type == "counter" && a.item == null && a.fire <= 0).ToList();
        int reserve = appliances.Any(a => IsCookStation(a) && a.item != null) ? 1 : 0;
        if (emptyCounters.Count <= reserve) return null;
        var empty = emptyCounters[0];
        empty.item = Item.Plate();
        return empty;
    }

    bool TryAddPrepperPart(IList<string> parts, Appliance counter)
    {
        if (counter.item == null || counter.item.kind != "plate") return false;
        if (counter.item.parts.Count >= parts.Count) return false;
        string part = parts[counter.item.parts.Count];

        if (StationPart(part)) {
            var source = appliances.FirstOrDefault(a => a.type == "counter" && a != counter && a.item?.kind == "ingredient" && a.item.id == part && IngredientReady(a.item));
            if (source == null) return false;
            counter.item.parts.Add(part);
            source.item = null;
            return true;
        }

        if (!ItemUnlocked(part)) return false;
        counter.item.parts.Add(part);
        return true;
    }

    Item CloneItem(Item item)
    {
        if (item == null) return null;
        var clone = new Item {
            kind = item.kind,
            id = item.id,
            state = item.state,
            progress = item.progress,
            dirty = item.dirty
        };
        clone.parts = new List<string>(item.parts);
        return clone;
    }

    string CarryStateForPart(string part)
    {
        if (part == "patty") return "cooked";
        if (part == "dough") return "baked";
        if (part == "coffee") return "brewed";
        if (NeedsPrep(part)) return "ready";
        return "ready";
    }

    bool PartsPrefixMatches(Item plate, Recipe recipe) => PartsPrefixMatches(plate, recipe.parts);

    bool PartsPrefixMatches(Item plate, IList<string> parts)
    {
        if (plate == null || plate.kind != "plate" || plate.parts.Count > parts.Count) return false;
        for (int i = 0; i < plate.parts.Count; i++) if (plate.parts[i] != parts[i]) return false;
        return true;
    }

    string NextMissingStationPart()
    {
        foreach (var customer in customers.Where(c => c.ordered && !c.served && !c.mealServed && !c.leaving)) {
            foreach (string part in customer.orderParts ?? customer.recipe.parts.ToList()) {
                if (!StationPart(part)) continue;
                if (StaffStock(part) < PendingDemand(part)) return part;
            }
        }
        return "";
    }

    // Bin a COMPLETED plate that no longer matches any waiting guest (its guest left), after a short
    // grace. Otherwise orphaned modified-order plates clog counters until the cook can't offload and
    // food ignites (audit #5).
    void SweepOrphanPlates(float dt)
    {
        foreach (var a in appliances) {
            if (a.type != "counter" || a.item?.kind != "plate" || a.item.dirty || CompletedRecipeForPlate(a.item) == null) { a.orphanAge = 0; continue; }
            bool wanted = customers.Any(c => c.ordered && !c.served && !c.mealServed && !c.leaving && PlateMatchesOrder(a.item, c));
            if (wanted) { a.orphanAge = 0; continue; }
            a.orphanAge += dt;
            if (a.orphanAge > 7f) { a.item = null; a.orphanAge = 0; PlayEventSound("trash"); }
        }
    }

    int PendingDemand(string id)
    {
        // scale by OUTSTANDING meals: a party of N still owed M meals needs the part M times, not once,
        // else the cook idles after plate #1 and multi-person parties serve one plate at a time.
        return customers
            .Where(c => c.ordered && !c.served && !c.mealServed && !c.leaving)
            .Sum(c => (c.orderParts ?? c.recipe.parts.ToList()).Count(p => p == id) * Mathf.Max(1, c.partySize - c.mealsServed));
    }

    int StaffStock(string id)
    {
        int stock = 0;
        stock += appliances.Count(a => IsCookStation(a) && a.item?.id == id && a.item.state != "burnt");   // burnt is waste, not stock
        stock += appliances.Count(a => a.type == "counter" && a.item?.kind == "ingredient" && a.item.id == id && IngredientReady(a.item));
        stock += appliances
            .Where(a => a.type == "counter" && a.item?.kind == "plate")
            .Sum(a => a.item.parts.Count(p => p == id));
        return stock;
    }

    bool StationPart(string id)
    {
        return id == "patty" || (id == "dough" && save.theme == "pizza") || (id == "coffee" && save.theme == "coffee") || (id == "sausage" && save.theme == "hotdog");
    }

    bool IsCookStation(Appliance a)
    {
        return a.type == "hob" || a.type == "oven" || a.type == "espresso";
    }

    string StationTypeFor(string id)
    {
        if (id == "patty") return "hob";
        if (id == "sausage") return "hob";
        if (id == "dough") return "oven";
        return "espresso";
    }

    bool NeedsStation(Item item)
    {
        if (item == null) return false;
        if (item.id == "patty") return item.state == "raw";
        if (item.id == "sausage" && save.theme == "hotdog") return item.state == "raw";
        if (item.id == "dough" && save.theme == "pizza") return item.state == "raw";
        if (item.id == "coffee" && save.theme == "coffee") return item.state == "grounds";
        return false;
    }

    void InteractNearest()
    {
        var best = NearestAppliance(.58f);
        if (best != null) Interact(best);
    }

    void Interact(Appliance a)
    {
        if (a.fire > 0) { SetMessage("On fire — grab the extinguisher!", .9f); return; }
        if (a.type == "provider" && holding == null) {
            if (!ItemUnlocked(a.itemId)) {
                SetMessage("Unlocks day " + ItemDay(a.itemId), .9f);
                return;
            }
            string state = ProviderState(a.itemId);
            holding = Item.Ingredient(a.itemId, state);
            SetMessage("Took " + a.itemId.ToUpperInvariant(), .65f);
            PlayEventSound("pickup");
        } else if (a.type == "plates" && holding == null) {
            holding = Item.Plate();
            PlayEventSound("pickup");
        } else if (a.type == "extinguisher") {
            if (holding == null) { holding = Item.Tool("extinguisher"); SetMessage("Extinguisher — hold near a fire", .9f); PlayEventSound("pickup"); }
            else if (holding.kind == "tool") holding = null;   // rack it back
        } else if (a.type == "counter") {
            if (holding == null && a.item != null) {
                holding = a.item;
                a.item = null;
                PlayEventSound("pickup");
            } else if (holding != null && a.item == null) {
                a.item = holding;
                holding = null;
                PlayEventSound("place");
            } else if (holding != null && holding.kind == "ingredient" && a.item?.kind == "plate" && !a.item.dirty) {
                if (IngredientReady(holding)) {
                    a.item.parts.Add(holding.id);
                    holding = null;
                    PlayEventSound("place");
                }
            } else if (holding != null && holding.kind == "plate" && !holding.dirty && a.item?.kind == "ingredient" && IngredientReady(a.item)) {
                holding.parts.Add(a.item.id);
                a.item = null;
                PlayEventSound("place");
            }
        } else if (a.type == "hob") {
            if ((holding?.id == "patty" || (holding?.id == "sausage" && save.theme == "hotdog")) && holding.state == "raw" && a.item == null) {
                a.item = holding;
                a.item.progress = 0;
                holding = null;
                PlayEventSound("cook");
            } else if (holding == null && a.item != null && (a.item.state == "cooked" || a.item.state == "burnt")) {
                holding = a.item;
                a.item = null;
                PlayEventSound(holding.state == "burnt" ? "bad" : "pickup");
            }
        } else if (a.type == "oven") {
            if (holding?.id == "dough" && holding.state == "raw" && a.item == null) {   // never re-bake baked/burnt dough
                a.item = holding;
                a.item.progress = 0;
                holding = null;
                PlayEventSound("cook");
            } else if (holding == null && a.item != null && (a.item.state == "baked" || a.item.state == "burnt")) {
                holding = a.item;
                a.item = null;
                PlayEventSound(holding.state == "burnt" ? "bad" : "pickup");
            }
        } else if (a.type == "espresso") {
            if (holding?.id == "coffee" && a.item == null) {
                a.item = holding;
                a.item.progress = 0;
                holding = null;
                PlayEventSound("cook");
            } else if (holding == null && a.item != null && a.item.state == "brewed") {
                holding = a.item;
                a.item = null;
                PlayEventSound("pickup");
            }
        } else if (a.type == "prep") {
            if (HoldableAppliance(a)) SetMessage("Hold to prep", .8f);
        } else if (a.type == "sink") {
            if (HoldableAppliance(a)) SetMessage("Hold to wash", .8f);
        } else if (a.type == "drink") {
            if (holding == null) {
                holding = Item.Drink();
                PlayEventSound("pickup");
            }
        } else if (a.type == "table") {
            if (a.customer != null && !a.customer.seated) {
                SetMessage("Customer seating", .7f);
            } else if (a.dirty && holding == null) {
                holding = Item.Plate(true);
                a.dirty = false;
                PlayEventSound("pickup");
            } else if (a.customer != null && !a.customer.leaving && !a.customer.ordered) {
                a.customer.ordered = true;
                SetMessage("Order taken", .7f);
                PlayEventSound("order");
            } else if (a.customer != null && !a.customer.leaving && holding?.kind == "drink") {
                if (a.customer.wantsDrink && !a.customer.drinkServed) {
                    holding = null;
                    ServeDrink(a.customer);
                    SetMessage("Drink " + a.customer.drinkCount + "/" + a.customer.partySize, .7f);
                    PlayEventSound("serve");
                    if (CustomerComplete(a.customer)) CompleteCustomer(a.customer);
                }
            } else if (a.customer != null && !a.customer.leaving && holding?.kind == "plate" && PlateMatchesOrder(holding, a.customer)) {
                if (a.customer.mealServed) {
                    SetMessage("Meal already served", .8f);   // keep the spare correct plate in hand, no complaint
                } else {
                    holding = null;
                    ServeMeal(a.customer);
                    PlayEventSound("serve");
                    if (CustomerComplete(a.customer)) CompleteCustomer(a.customer);
                    else if (!a.customer.mealServed) SetMessage("Meal " + a.customer.mealsServed + "/" + a.customer.partySize, .8f);
                    else SetMessage("Needs drink", .8f);
                }
            } else if (a.customer != null && !a.customer.leaving && holding?.kind == "plate" && !holding.dirty
                       && holding.parts.Count >= (a.customer.orderParts?.Count ?? a.customer.recipe.parts.Length)) {
                // only a COMPLETE but incorrect dish is a wrong order — carrying a dirty or half-built
                // plate past a table used to destroy it and cost a complaint.
                wrongOrders++;
                holding = null;
                AddComplaint("Wrong order");
            } else if (a.customer != null && holding?.kind == "plate" && holding.dirty) {
                SetMessage("Wash that plate first", .8f);
            } else if (a.customer != null && !a.customer.leaving && holding?.kind == "plate" && !holding.dirty) {
                // a clean but UNFINISHED plate at the table: tell the player what's missing (was silent)
                var need = a.customer.orderParts ?? a.customer.recipe.parts.ToList();
                if (PartsPrefixMatches(holding, need))
                    SetMessage(!string.IsNullOrEmpty(a.customer.orderMod) && a.customer.orderMod[0] == 'E' ? "Needs " + a.customer.orderMod : "Dish incomplete", .8f);
                else SetMessage("Wrong order?", .8f);
            }
        } else if (a.type == "trash") {
            if (holding == null) { SetMessage("Nothing to bin", .7f); return; }
            SetMessage("Binned " + HoldingLabel(), .7f);
            holding = null;
            PlayEventSound("trash");
        } else if (holding != null && (a.type == "provider" || a.type == "plates" || a.type == "drink" || a.type == "extinguisher")) {
            SetMessage("Hands full", .6f);   // was a silent no-op
        }
        RebuildWorld();
    }

    void CompleteCustomer(Customer c)
    {
        if (c == null || c.served) return;
        c.served = true;
        c.eatTimer = 0;
        served++;
        combo++;
        bestCombo = Mathf.Max(bestCombo, combo);
        if (combo >= 5 && combo % 5 == 0) {
            PlayEventSound("combo");
            AddPopup((c.table != null ? ApplianceCenter(c.table) : playerPos) + new Vector2(0, 1.05f), "COMBO x" + combo + "!", gold, true);
        }
        float patienceRatio = Mathf.Clamp01(c.patience / Mathf.Max(.01f, c.maxPatience));
        int reward = (8 + c.recipe.value / 5 + c.bonus) * Mathf.Max(1, c.partySize);
        if (c.wantsDrink) reward += 3 * c.partySize;
        if (HasPerk("premium")) reward += 2 * Mathf.Max(1, c.partySize);
        if (HasPerk("investor")) reward = Mathf.RoundToInt(reward * 1.12f);
        float comboFactor = HasPerk("combo_king") ? .8f : .45f;
        float repBonus = 1f + Mathf.Min(save.reputation, 150) * .0022f;   // renowned restaurants earn better tips
        float eventTip = DayEvent() == "vip" ? 1.25f : DayEvent() == "happy" ? 1.3f : 1f;   // happy hour = fat tips
        int tip = Mathf.RoundToInt((2 + c.recipe.value / 20f + combo * comboFactor) * c.tipRate * patienceRatio * (1f + save.decor * .08f) * (HasPerk("big_tips") ? 1.3f : 1f) * eventTip * repBonus);
        tips += tip;
        BumpGoal("tips", tip);
        reward += tip;
        // A busier, more famous house genuinely earns more per guest, so MARKETING, a high STAR level
        // and BUSY BEE pay off instead of only raising arrival pressure (audit #21/#22).
        reward = Mathf.RoundToInt(reward * (1f + (save.stars - 3) * .06f) * (1f + save.marketing * .04f) * (HasPerk("busy_bee") ? 1.06f : 1f));
        bool isSpecial = specialRecipe != null && c.recipe.id == specialRecipe.id;
        if (isSpecial) reward += Mathf.RoundToInt(reward * .4f);
        earned += reward;
        save.coins += reward;
        BumpGoal("serve", 1);
        BumpGoal("earn", reward);
        if (c.partySize >= 2) BumpGoal("party", 1);
        Vector2 head = (c.table != null ? ApplianceCenter(c.table) : playerPos) + new Vector2(0, .7f);
        AddPopup(head, "+" + reward + "  x" + combo, tip > 0 ? gold : mint, true);
        AddPopup(head + new Vector2(.28f, .3f), "", combo >= 3 ? gold : new Color(1f, .55f, .68f), true);   // extra sparkle burst (juice)
        if (isSpecial) AddPopup((c.table != null ? ApplianceCenter(c.table) : playerPos) + new Vector2(.5f, .95f), "SPECIAL!", violet);
        PlayEventSound("coin");
        Shake(.045f + Mathf.Min(.055f, combo * .007f));   // satisfying pop, bigger on a hot combo
        Buzz();
        Persist();
    }

    void BeginCustomerDeparture(Customer c, bool dirtyTable)
    {
        if (c == null || c.leaving) return;
        c.leaving = true;
        c.dirtyOnLeave = dirtyTable;
        c.sittingDown = false;
        c.transitionTime = 0;
        if (c.seated) {
            c.standingUp = true;
        } else if (c.table != null && c.table.customer == c) {
            c.table.customer = null;
            c.table.dirty = dirtyTable;
        }
    }

    void ServeMeal(Customer c)
    {
        c.mealsServed = Mathf.Min(c.partySize, c.mealsServed + 1);
        c.mealServed = c.mealsServed >= c.partySize;
    }

    void ServeDrink(Customer c)
    {
        c.drinkCount = Mathf.Min(c.partySize, c.drinkCount + 1);
        drinksServed++;
        BumpGoal("drinks", 1);
        c.drinkServed = c.drinkCount >= c.partySize;
    }

    bool CustomerComplete(Customer c)
    {
        return c.mealServed && (!c.wantsDrink || c.drinkServed);
    }

    bool PlateMatches(Item plate, Recipe r)
    {
        // order-insensitive: a dish is defined by its ingredients, not the stacking order,
        // so a burger counts as complete however the player layered it.
        if (plate == null || plate.kind != "plate" || plate.dirty || plate.parts.Count != r.parts.Length) return false;
        var a = new List<string>(plate.parts); a.Sort();
        var b = new List<string>(r.parts); b.Sort();
        for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
        return true;
    }

    bool IngredientReady(Item item)
    {
        if (item.id == "patty") return item.state == "cooked";
        if (item.id == "sausage" && save.theme == "hotdog") return item.state == "cooked";
        if (item.id == "dough" && save.theme == "pizza") return item.state == "baked";
        if (item.id == "coffee" && save.theme == "coffee") return item.state == "brewed";
        if (NeedsPrep(item.id)) return item.state == "ready";
        return item.state == "ready";
    }

    bool NeedsPrep(string id) => id == "lettuce" || id == "tomato";

    bool SpawnCustomer()
    {
        var openTables = appliances.Where(a => a.type == "table" && a.customer == null && !a.dirty).ToList();
        if (openTables.Count == 0) return false;
        var table = openTables[UnityEngine.Random.Range(0, openTables.Count)];
        if (table == null) return false;
        var pool = AvailableRecipes();
        if (pool.Count == 0) return false;
        string type = PickCustomerType(table);
        int party = PartySizeFor(table, type);
        float patienceMul = CustomerTypePatience(type);
        float drinkChance = save.day < 2 ? 0f : save.theme == "coffee" ? .55f : save.theme == "pizza" ? .12f : .22f;
        if (type == "critic") drinkChance += .12f;
        if (type == "rush") drinkChance *= .65f;
        if (DayEvent() == "happy") drinkChance += .3f;   // happy hour: thirsty crowd
        bool wantsDrink = save.drink > 0 && UnityEngine.Random.value < drinkChance;
        // scale patience with the real workload: a party of N needs N plates, so add ~one order's grace per extra member
        float basePatience = CustomerPatience() * (1f + (party - 1) * .75f) + party * 1.4f;
        if (wantsDrink) basePatience *= 1.15f;   // a drink adds a fetch/serve loop — give time for it (#39)
        var c = new Customer {
            recipe = pool[UnityEngine.Random.Range(0, pool.Count)],
            partySize = party,
            maxPatience = basePatience * patienceMul,
            patience = basePatience * patienceMul,
            table = table,
            visualPos = CustomerEntryPoint(table),
            seated = false,
            leaving = false,
            walkSeed = UnityEngine.Random.Range(0f, 10f),
            animationClock = UnityEngine.Random.Range(0f, 10f),
            visualId = "customer" + UnityEngine.Random.Range(0, 6),
            wantsDrink = wantsDrink,
            drinkServed = !wantsDrink,
            typeId = type,
            typeLabel = CustomerTypeLabel(type),
            bonus = CustomerTypeBonus(type),
            tipRate = CustomerTypeTip(type)
        };
        ApplyOrderModifier(c);
        table.customer = c;
        customers.Add(c);
        return true;
    }

    static readonly string[] OrderToppings = { "lettuce", "tomato", "cheese", "onion", "sauce" };

    // give some single guests a "no X" / "extra X" twist on their order for depth (uses existing art)
    void ApplyOrderModifier(Customer c)
    {
        c.orderParts = new List<string>(c.recipe.parts);
        c.orderMod = "";
        if (save.day < 3 || c.partySize > 1 || c.typeId == "critic") return;
        if (specialRecipe != null && c.recipe.id == specialRecipe.id) return;
        if (UnityEngine.Random.value > .28f) return;
        var present = c.orderParts.Where(p => OrderToppings.Contains(p)).Distinct().ToList();
        if (present.Count == 0) return;
        bool canRemove = c.orderParts.Count > 2;
        if (canRemove && UnityEngine.Random.value < .5f) {
            string t = present[UnityEngine.Random.Range(0, present.Count)];
            c.orderParts.Remove(t);
            c.orderMod = "NO " + t.ToUpperInvariant();
        } else {
            string t = present[UnityEngine.Random.Range(0, present.Count)];
            c.orderParts.Add(t);
            c.orderMod = "EXTRA " + t.ToUpperInvariant();
            c.bonus += 4;                       // pays a little extra for the extra topping
        }
    }

    bool PlateMatchesOrder(Item plate, Customer c)
    {
        var parts = c.orderParts ?? c.recipe.parts.ToList();
        if (plate == null || plate.kind != "plate" || plate.dirty || plate.parts.Count != parts.Count) return false;
        var a = new List<string>(plate.parts); a.Sort();
        var b = new List<string>(parts); b.Sort();
        for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
        return true;
    }

    void EnsureOpeningCustomer()
    {
        if (customers.Any(c => !c.leaving)) return;
        if (SpawnCustomer()) return;
        var starter = appliances.FirstOrDefault(a => a.type == "table");
        if (starter == null) return;
        starter.dirty = false;
        starter.customer = null;
        SpawnCustomer();
    }

    Vector2 CustomerSeatPosition(Customer c)
    {
        if (c?.table == null) return CustomerExitPoint();
        var slots = CustomerSeatSlots(c.table);
        return slots.Count > 0 ? slots[0].pos : c.table != null ? CellRect(c.table.c, c.table.r, c.table.w, c.table.h).center : CustomerExitPoint();
    }

    // Chair anchors around a table (world position + facing toward the table), in fill order.
    // Tables span a 2x1 cell footprint, so the painted side chairs land near +-half the width.
    // 2-seat tables carry left/right chairs; family tables add a far and a near chair.
    List<(Vector2 pos, Vector2 face)> CustomerSeatSlots(Appliance table)
    {
        var slots = new List<(Vector2, Vector2)>();
        if (table == null) return slots;
        Rect r = CellRect(table.c, table.r, table.w, table.h);
        Vector2 vc = r.center + new Vector2(0, .13f);
        float w = r.width, h = r.height;
        if (table.seats >= 4) {
            // aligned with the pulled-out chairs in DrawFamilyTable: W, E, N(far), S(near)
            slots.Add((vc + new Vector2(-.94f, .02f), Vector2.right));
            slots.Add((vc + new Vector2(.94f, .02f), Vector2.left));
            slots.Add((vc + new Vector2(0f, .82f), Vector2.down));
            slots.Add((vc + new Vector2(0f, -.8f), Vector2.up));
        } else {
            // side chairs pull OUT when occupied; the guest sits on the pulled-out seat (matches the
            // occupied-chair position in DrawTableWithChairs so the body lands on the seat).
            var left = (vc + new Vector2(-.64f, -.03f), Vector2.right);
            var right = (vc + new Vector2(.64f, -.03f), Vector2.left);
            // alternate which side a lone guest takes so tables don't all seat on the same edge
            if (((table.c + table.r) & 1) == 0) { slots.Add(left); slots.Add(right); }
            else { slots.Add(right); slots.Add(left); }
        }
        return slots;
    }

    // 2-seat table drawn as a chairless top + separate chairs that pull OUT when a guest is on them
    void DrawTableWithChairs(Appliance a, Rect rect)
    {
        Vector2 vc = rect.center + new Vector2(0, .13f);   // sit the set a touch higher for a more top-down read
        // woven mat under each table so the dining area feels arranged
        float rug = a.seats >= 4 ? 2.5f : 2.2f;
        MakeBox3D(a.id + "-rug0", rect.center, new Vector2(Tile * rug, Tile * (rug - .3f)), .012f,
                  WorldMaterial("table-rug0", null, new Color(.21f, .045f, .065f), Vector2.one), .006f, false);
        MakeBox3D(a.id + "-rug1", rect.center, new Vector2(Tile * (rug - .28f), Tile * (rug - .58f)), .014f,
                  WorldMaterial("table-rug1", null, new Color(.46f, .13f, .12f), Vector2.one), .013f, false);
        MakeBox3D(a.id + "-rug2", rect.center, new Vector2(Tile * (rug - 1.1f), Tile * (rug - 1.3f)), .016f,
                  WorldMaterial("table-rug2", null, new Color(.62f, .21f, .16f), Vector2.one), .021f, false);
        var seated = customers.FirstOrDefault(c => c.table == a && c.seated);
        if (a.seats >= 4) { DrawFamilyTable(a, vc, seated); return; }
        int occ = seated != null ? Mathf.Clamp(seated.partySize, 1, 2) : 0;
        int[] sideOrder = ((a.c + a.r) & 1) == 0 ? new[] { 0, 1 } : new[] { 1, 0 };
        // chairs first so the table top tucks the empty seats under its edge (real 3D chair mesh + yaw)
        for (int k = 0; k < 2; k++) {
            int side = sideOrder[k];
            bool occupied = k < occ;
            float sign = side == 0 ? -1f : 1f;
            // Chairs must be BIG relative to a 1.46-tall guest (a real chair back reaches ~half a
            // person's height). At .36x.46 the mesh was ~3x narrower than the guest sprite, so a
            // seated body completely swallowed it and nobody looked like they were sitting.
            float chairX = occupied ? .64f : .56f;
            Vector2 chairPos = vc + new Vector2(sign * chairX, occupied ? -.03f : .04f);
            Make3DProp(a.id + "-chair" + side, "chair",
                       chairPos, new Vector2(.46f, .6f), side == 0 ? ChairYaw[0] : ChairYaw[1], occupied ? a.r + 2 : a.r);
        }
        Make3DProp(a.id, "table", vc, new Vector2(.82f, .66f), 0f, a.r + 1);   // a genuinely SMALL two-top
    }

    // chair yaw (around up) so the seat faces the table, per seat: W, E, N(far), S(near). Tuned so the
    // 3D chair model opens toward the centre from each side.
    static readonly float[] ChairYaw = { 90f, 270f, 180f, 0f };

    // family (4-top): fresh table + 4 chairs (W,E,N,S) that pull out when a guest sits on them
    void DrawFamilyTable(Appliance a, Vector2 vc, Customer seated)
    {
        int occ = seated != null ? Mathf.Clamp(seated.partySize, 1, 4) : 0;
        Vector2[] tuck = { new Vector2(-.86f, .02f), new Vector2(.86f, .02f), new Vector2(0f, .74f), new Vector2(0f, -.72f) };
        Vector2[] pull = { new Vector2(-.94f, .02f), new Vector2(.94f, .02f), new Vector2(0f, .82f), new Vector2(0f, -.8f) };
        Vector2[] size = { new Vector2(.46f, .6f), new Vector2(.46f, .6f), new Vector2(.56f, .52f), new Vector2(.56f, .52f) };
        // N/S chairs sit BEHIND the table top, W/E beside it; occupied chairs come forward
        int[] baseOrder = { a.r, a.r, a.r - 1, a.r - 1 };
        for (int i = 0; i < 4; i++)
            Make3DProp(a.id + "-fc" + i, "chair", vc + (i < occ ? pull[i] : tuck[i]), size[i], ChairYaw[i], i < occ ? a.r + 2 : baseOrder[i]);
        Make3DProp(a.id, "bigtable", vc, new Vector2(1.42f, 1.1f), 0f, a.r + 1);   // clearly BIGGER than a two-top
    }

    Vector2 CustomerEntryPoint(Appliance table)
    {
        float x = table != null ? Mathf.Clamp(CellCenter(table.c, 0).x, CellCenter(2, 0).x, CellCenter(7, 0).x) : CellCenter(4, 0).x;
        return new Vector2(x, CellRect(0, 0, Cols, Rows).yMax - .18f);
    }

    Vector2 CustomerExitPoint()
    {
        return new Vector2(CellCenter(4, 0).x, CellRect(0, 0, Cols, Rows).yMax - .16f);
    }

    string PickCustomerType(Appliance table)
    {
        if (save.day < 2) return "regular";
        string ev = DayEvent();
        if (ev == "rush" && UnityEngine.Random.value < .4f) return "rush";
        if (ev == "vip" && save.day >= 5 && UnityEngine.Random.value < .4f) return "critic";
        if (ev == "critic" && UnityEngine.Random.value < .5f) return "critic";   // reviewer night
        if (rushActive && UnityEngine.Random.value < .46f) return "rush";
        if (save.day >= 8 && UnityEngine.Random.value < (HasPerk("vip_magnet") ? .16f : .09f)) return "vip";
        if (save.day >= 5 && UnityEngine.Random.value < (HasPerk("vip_magnet") ? .2f : .13f)) return "critic";
        if (save.day >= 4 && table.seats >= 4 && UnityEngine.Random.value < .34f) return "family";
        if (save.day >= 7 && table.seats >= 2 && UnityEngine.Random.value < .18f) return "bulk";
        if (UnityEngine.Random.value < .2f) return "patient";
        return "regular";
    }

    int PartySizeFor(Appliance table, string type)
    {
        int cap = Mathf.Clamp(table.seats, 1, 4);
        int dayCap = save.day < 3 ? 1 : save.day < 5 ? 2 : 4;
        int max = Mathf.Min(cap, dayCap);
        if (type == "family") return Mathf.Min(4, Mathf.Max(2, max));
        if (type == "bulk") return Mathf.Min(max, UnityEngine.Random.value < .55f ? 3 : 2);
        if (max <= 1) return 1;
        if (max >= 4 && UnityEngine.Random.value < .22f) return 4;
        if (max >= 3 && UnityEngine.Random.value < .18f) return 3;
        if (max >= 2 && UnityEngine.Random.value < .46f) return 2;
        return 1;
    }

    float CustomerTypePatience(string type)
    {
        if (type == "vip") return .78f;
        if (type == "rush") return .72f;
        if (type == "patient") return 1.38f;
        if (type == "family") return 1.18f;
        if (type == "critic") return 1.05f;
        if (type == "bulk") return .96f;
        return 1f;
    }

    int CustomerTypeBonus(string type)
    {
        if (type == "vip") return 28;
        if (type == "rush") return 8;
        if (type == "critic") return 18;
        if (type == "family") return 10;
        if (type == "bulk") return 12;
        return 0;
    }

    float CustomerTypeTip(string type)
    {
        if (type == "vip") return 2.2f;
        if (type == "critic") return 1.6f;
        if (type == "rush") return 1.25f;
        if (type == "patient") return .8f;
        return 1f;
    }

    string CustomerTypeLabel(string type)
    {
        if (type == "vip") return "VIP";
        if (type == "rush") return "FAST";
        if (type == "patient") return "CALM";
        if (type == "family") return "FAMILY";
        if (type == "critic") return "CRITIC";
        if (type == "bulk") return "BULK";
        return "";
    }

    void PickDailySpecial()
    {
        var pool = recipes.Where(r => r.theme == save.theme && r.day <= save.day).ToList();
        specialRecipe = pool.Count > 0 ? pool[UnityEngine.Random.Range(0, pool.Count)] : null;
    }

    void GenerateDailyGoals()
    {
        dailyGoals.Clear();
        dailyGoals.Add(new DailyGoal { id = "serve", label = "Serve groups", target = Mathf.Max(2, Mathf.Min(goal, 2 + save.day / 2)), reward = 18 + save.day * 4 });
        dailyGoals.Add(new DailyGoal { id = "earn", label = "Earn coins", target = 24 + save.day * 12, reward = 20 + save.day * 5 });
        // rotating third goal for variety
        var pool = new List<DailyGoal>();
        if (save.drink > 0 && save.day >= 2) pool.Add(new DailyGoal { id = "drinks", label = "Serve drinks", target = Mathf.Clamp(1 + save.day / 3, 1, 5), reward = 22 + save.day * 4 });
        if (save.day >= 3) pool.Add(new DailyGoal { id = "party", label = "Seat parties", target = 1 + save.day / 6, reward = 26 + save.day * 3 });
        if (save.day >= 2) pool.Add(new DailyGoal { id = "tips", label = "Earn tips", target = 8 + save.day * 3, reward = 22 + save.day * 4 });
        pool.Add(new DailyGoal { id = "clean", label = "No complaints", target = 1, progress = 1, reward = 24 + save.day * 2 });
        dailyGoals.Add(pool[UnityEngine.Random.Range(0, pool.Count)]);
    }

    void BumpGoal(string id, int amount)
    {
        foreach (var g in dailyGoals.Where(g => g.id == id && !g.done)) {
            g.progress = Mathf.Min(g.target, g.progress + amount);
            if (g.progress >= g.target) {
                g.done = true;
                AddPopup(playerPos + new Vector2(0, .9f), "GOAL +" + g.reward, gold);
            }
        }
    }

    void FailGoal(string id)
    {
        foreach (var g in dailyGoals.Where(g => g.id == id)) {
            g.progress = 0;
            g.done = false;
        }
    }

    void EvaluateEndGoals()
    {
        foreach (var g in dailyGoals) {
            // the clean goal is defined solely by a spotless service — don't let the generic
            // progress>=target check below clobber it back to done after a forgiven wrong order.
            if (g.id == "clean") g.done = complaints == 0 && wrongOrders == 0 && missed == 0;
            else if (g.progress >= g.target) g.done = true;
        }
        goalBonus = dailyGoals.Where(g => g.done).Sum(g => g.reward);
        if (goalBonus > 0) {
            earned += goalBonus;
            save.coins += goalBonus;
        }
    }

    int CalculateStars(bool success)
    {
        if (!success) return Mathf.Clamp(served > 0 ? 1 : 0, 0, 3);
        int stars = 1;
        if (served >= goal) stars++;
        if (complaints == 0 && dailyGoals.Count(g => g.done) >= 2) stars++;
        return Mathf.Clamp(stars, 1, 3);
    }

    string GoalLine(DailyGoal g)
    {
        // no "[x]" prefix — DrawGoalHud draws a real tick box beside each line
        if (g.id == "clean") return g.label + "  +" + g.reward;
        return g.label + " " + Mathf.Min(g.progress, g.target) + "/" + g.target + "  +" + g.reward;
    }

    void AddPopup(Vector2 pos, string value, Color color, bool burst = false)
    {
        popups.Add(new Popup { pos = pos, text = value, color = color, life = 1.35f, maxLife = 1.35f, burst = burst });
    }

    void UpdatePopups(float dt)
    {
        foreach (var p in popups) {
            p.life -= dt;
            p.pos += Vector2.up * dt * .22f;
        }
        popups.RemoveAll(p => p.life <= 0);
    }

    float CurrentSpawnDelay()
    {
        float delay = SpawnDelayForDay(save.day);
        if (rushActive) delay *= .56f;
        if (DayEvent() == "rush") delay *= .78f;
        if (HasPerk("busy_bee")) delay *= .88f;
        delay *= Mathf.Max(.72f, 1f - save.marketing * .045f);
        delay *= StarFlowFactor();   // famous restaurants pull a crowd; 1-star rooms stay quiet
        if (save.day == 1) delay = Mathf.Max(delay, 12f);
        return delay;
    }

    float StarFlowFactor()
    {
        switch (Mathf.Clamp(save.stars, 1, 5)) {
            case 1: return 1.3f;
            case 2: return 1.12f;
            case 4: return .88f;
            case 5: return .76f;
            default: return 1f;
        }
    }

    bool IsRushWindow()
    {
        if (save.day < 4) return false;
        float ratio = shiftElapsed / Mathf.Max(1f, maxShiftTime);
        return ratio > .36f && ratio < .66f && served < goal;
    }

    string DayEvent()
    {
        if (save.day < 3) return "";
        if (save.day % 5 == 0) return "vip";
        if (save.day % 7 == 0) return "critic";     // a reviewer is in — mistakes cost double
        if (save.day % 4 == 0) return "happy";      // happy hour — thirsty crowd, fat tips
        if (save.day % 3 == 0) return "rush";
        return "";
    }

    string DayEventLabel()
    {
        switch (DayEvent()) {
            case "rush": return "RUSH DAY";
            case "vip": return "VIP NIGHT";
            case "critic": return "CRITIC IN!";
            case "happy": return "HAPPY HOUR";
            default: return "";
        }
    }

    string DayEventHint()
    {
        switch (DayEvent()) {
            case "rush": return "Crowds pour in - keep the line moving";
            case "vip": return "Big spenders tonight - tips run high";
            case "critic": return "A reviewer is watching - don't slip up";
            case "happy": return "Thirsty crowd - drinks and tips flow";
            default: return "";
        }
    }

    void AddComplaint(string reason, bool failsCleanGoal = true)
    {
        if (HasPerk("second_wind") && !complaintForgivenUsed) {
            complaintForgivenUsed = true;
            AddPopup(playerPos + new Vector2(0, .72f), "SECOND WIND", green);
            SetMessage("Complaint forgiven", 1f);
            return;
        }
        complaints++;
        if (!HasPerk("showman")) {
            if (combo >= 3) AddPopup(playerPos + new Vector2(0, 1.0f), "COMBO LOST", red);
            combo = 0;
        }
        if (failsCleanGoal) FailGoal("clean");   // a full queue is a capacity issue, not a service mistake
        SetMessage(reason + " " + complaints + "/5", 1.1f);
        AddPopup(playerPos + new Vector2(0, .72f), reason.ToUpperInvariant(), red);
        PlayEventSound("bad");
        Shake(.09f);
        Buzz(50);
    }

    void FinishDay(bool success)
    {
        EvaluateEndGoals();
        starRating = CalculateStars(success);
        save.reputation = Mathf.Max(0, save.reputation + (success ? starRating * 3 + dailyGoals.Count(g => g.done) : -2));
        save.totalStars += starRating;
        // Tokens are also earned by PLAYING WELL, not only by watching ads. A cosmetic economy
        // reachable purely through ads reads as a paywall.
        //
        // The payout curve is measured, not guessed: SimulateEconomy ran a fully-staffed
        // restaurant for 10 days and the old star-only rule paid 0.30 tokens/day, which put the
        // cheapest outfit 20 days away and the most expensive 70+. Paying for FINISHING the day
        // (not just for excelling at it) is what makes the wardrobe reachable by playing.
        tokensEarnedToday = success ? 1 + (starRating >= 2 ? 1 : 0) + (starRating >= 3 ? 2 : 0) : 0;
        save.tokens += tokensEarnedToday;
        // restaurant STAR LEVEL (1..5): a perfect day earns a star, a weak or failed day loses one.
        // More stars = guests arrive faster; fewer = slower (see CurrentSpawnDelay).
        int prevStars = save.stars;
        if (!success) save.stars = Mathf.Max(1, save.stars - 1);
        else if (starRating >= 3 && complaints == 0) save.stars = Mathf.Min(5, save.stars + 1);
        else if (starRating <= 1) save.stars = Mathf.Max(1, save.stars - 1);
        starDelta = save.stars - prevStars;
        save.bestDay = Mathf.Max(save.bestDay, save.day);
        if (success) {
            save.day++;
            GenerateMarketOffers();
            MaybeOfferPerks();
        } else {
            marketOffers.Clear();
            pendingPerks.Clear();   // a failed day offers no perk — clear any stale choice so Result can't show it
        }
        Persist();
        PlayEventSound(success ? "win" : "bad");
        screen = ScreenMode.Result;
        ClearWorld();
        ClearStaticWorld();
        DrawResultScreen(success);
    }

    void DrawResultScreen(bool success)
    {
        lastResultSuccess = success;
        ClearUI();
        AddPanel(0, 0, W, H, new Color(.03f, .035f, .05f, 1), uiRoot, "result-bg");
        if (pendingPerks.Count > 0) { DrawPerkChoice(); return; }
        Color tone = success ? mint : red;
        if (messageTimer > 0) AddText(message, 360, 60, 13, gold, FontStyle.Bold);
        // hero: outcome + the day's star rating as pips (a "***" string reads as debris at this size)
        AddText(success ? "DAY COMPLETE" : "SERVICE FAILED", 360, 104, 38, tone, FontStyle.Bold);
        AddText("DAY " + (success ? save.day - 1 : save.day), 360, 142, 14, muted, FontStyle.Bold);
        for (int i = 0; i < 3; i++) {
            bool on = i < starRating;
            int px = 300 + i * 42;
            AddPanel(px, 168, 32, 32, on ? new Color(1f, .8f, .25f, .95f) : new Color(1, 1, 1, .1f), uiRoot, "res-star");
            if (on) AddPanel(px + 6, 174, 20, 20, new Color(1f, .93f, .62f, 1f), uiRoot, "res-star-in");
        }

        const int cx = 56, cw = 608, pad = 20;
        int ry = 224;
        AddCard(cx, ry, cw, 46 + 8 * 32 + 12, tone, "res-stats");
        AddText("SHIFT REPORT", cx + pad, ry + 26, 13, gold, FontStyle.Bold, TextAnchor.MiddleLeft);
        int rowY = ry + 52;
        void Row(string k, string v, Color vc)
        {
            AddText(k, cx + pad, rowY + 14, 14, muted, FontStyle.Bold, TextAnchor.MiddleLeft, "result-row");
            AddText(v, cx + cw - pad, rowY + 14, 15, vc, FontStyle.Bold, TextAnchor.MiddleRight, "result-row");
            rowY += 32;
        }
        Row("SERVED", served + " / " + goal, text);
        Row("COINS EARNED", "+" + earned, gold);
        Row("TIPS", "+" + tips, gold);
        Row("BEST COMBO", "x" + bestCombo, bestCombo >= 3 ? gold : text);
        Row("RESTAURANT STARS", save.stars + "/5" + (starDelta > 0 ? "   UP" : starDelta < 0 ? "   DOWN" : ""), starDelta > 0 ? mint : starDelta < 0 ? red : gold);
        Row("REPUTATION", save.reputation.ToString(), mint);
        Row("CAREER STARS", save.totalStars.ToString(), gold);
        Row("WARDROBE TOKENS", (tokensEarnedToday > 0 ? "+" + tokensEarnedToday : "-") + "   (" + save.tokens + ")",
            tokensEarnedToday > 0 ? violet : muted);
        int y = ry + 46 + 8 * 32 + 12 + 16;
        AddText("bonus " + goalBonus + "   drinks " + drinksServed + "   wrong " + wrongOrders + "   missed " + missed + "   queue " + queueComplaints,
                360, y + 10, 11, muted, FontStyle.Bold);
        y += 30;

        // goals as a tick list, same language as the in-play GOALS card
        AddCard(cx, y, cw, 44 + dailyGoals.Count * 28 + 10, gold, "res-goals");
        AddText("DAILY GOALS", cx + pad, y + 26, 13, gold, FontStyle.Bold, TextAnchor.MiddleLeft);
        int gy = y + 48;
        foreach (var g in dailyGoals) {
            AddPanel(cx + pad, gy + 4, 13, 13, g.done ? mint : new Color(1, 1, 1, .14f), uiRoot, "res-tick");
            if (g.done) AddPanel(cx + pad + 3, gy + 7, 7, 7, new Color(.05f, .07f, .1f, 1f), uiRoot, "res-tick-in");
            AddText(GoalLine(g), cx + pad + 26, gy + 11, 12, g.done ? mint : muted, FontStyle.Bold, TextAnchor.MiddleLeft);
            gy += 28;
        }
        y += 44 + dailyGoals.Count * 28 + 10 + 20;

        if (success && marketOffers.Count > 0) {
            AddText("DAILY MARKET", cx + pad, y + 12, 18, gold, FontStyle.Bold, TextAnchor.MiddleLeft);
            AddText("1 GOOD or up to 2 STANDARD", cx + cw - pad, y + 13, 11, muted, FontStyle.Bold, TextAnchor.MiddleRight);
            y += 34;
            int cardW = (cw - 16) / marketOffers.Count;
            for (int i = 0; i < marketOffers.Count; i++) {
                var offer = marketOffers[i];
                int idx = i, x = cx + i * (cardW + 8);
                bool locked = MarketChoiceLocked(offer);
                Color oc = offer.bought ? mint : locked ? muted : offer.color;
                // hit area FIRST so the card art and its labels draw on top of it (a button added last
                // painted over the whole card and the offer text vanished)
                if (!offer.bought && !locked) AddButton("", x, y, cardW, 116, oc, () => BuyMarketOffer(idx), "market-card");
                AddCard(x, y, cardW, 116, oc, "mk" + i);
                AddPanel(x + 10, y + 14, 62, 20, new Color(oc.r, oc.g, oc.b, .22f), uiRoot, "mk-tier" + i);
                AddText(offer.tier, x + 41, y + 24, 10, oc, FontStyle.Bold);
                AddText(offer.label, x + 12, y + 52, 14, text, FontStyle.Bold, TextAnchor.MiddleLeft, "market-label");
                AddText(offer.desc, x + 12, y + 74, 9, muted, FontStyle.Bold, TextAnchor.MiddleLeft, "market-desc");
                AddText(offer.bought ? "OWNED" : locked ? "LOCKED" : offer.cost + " COINS", x + 12, y + 98, 12, offer.bought ? mint : locked ? muted : gold, FontStyle.Bold, TextAnchor.MiddleLeft, "market-cost");
            }
            y += 136;
        }
        int footY = Mathf.Max(y + 12, H - 130);
        if (success && tips > 0 && !tipsDoubled) {
            // Offered only where it is real: a losing day has no tips worth doubling.
            footY = Mathf.Max(y + 12, H - 216);
            AddAdButton("WATCH AD:  DOUBLE +" + tips + " TIPS", cx, footY, cw, 74, "tips",
                () => DrawResultScreen(lastResultSuccess), "res-adtips");
            footY += 86;
        }
        AddIconButton("CONTINUE", "ic_open", cx, footY, cw, 78, mint, ShowMenu, "res-continue");
    }

    bool HasPerk(string id)
    {
        if (string.IsNullOrEmpty(save.perks)) return false;
        foreach (var p in save.perks.Split(',')) if (p == id) return true;
        return false;
    }

    int OwnedPerkCount()
    {
        return string.IsNullOrEmpty(save.perks) ? 0 : save.perks.Split(',').Count(s => !string.IsNullOrEmpty(s));
    }

    string PerkSummary()
    {
        var owned = allPerks.Where(p => HasPerk(p.id)).Select(p => p.label).ToList();
        if (owned.Count == 0) return "";
        if (owned.Count <= 4) return string.Join("  ", owned);
        return string.Join("  ", owned.Take(4)) + "  +" + (owned.Count - 4);
    }

    void GrantPerk(string id)
    {
        if (HasPerk(id)) return;
        save.perks = string.IsNullOrEmpty(save.perks) ? id : save.perks + "," + id;
    }

    void MaybeOfferPerks()
    {
        pendingPerks.Clear();
        perkRerollUsed = false;   // a fresh perk offer earns a fresh reroll
        if (OwnedPerkCount() >= allPerks.Count) return;
        if (save.day % 2 != 0) return;                 // offer entering an even day (2,4,6,...)
        pendingPerks.AddRange(allPerks.Where(p => !HasPerk(p.id))
            .OrderBy(_ => UnityEngine.Random.value).Take(3).Select(p => p.id));
    }

    void DrawPerkChoice()
    {
        // modern "choose one" sheet: headline, then one clean card per perk
        AddText("NEW PERK", 360, 150, 40, gold, FontStyle.Bold);
        AddText("Pick ONE permanent upgrade — it lasts every future shift", 360, 194, 13, muted, FontStyle.Bold);
        const int px = 56, pw = 608, cardH = 140, gap = 18;
        int listTop = 240;
        for (int i = 0; i < pendingPerks.Count; i++) {
            var perk = allPerks.First(p => p.id == pendingPerks[i]);
            int y = listTop + i * (cardH + gap);
            int idx = i;
            Color accent = i == 0 ? gold : i == 1 ? mint : violet;
            // hit area under the card art, so labels stay visible and the whole card is tappable
            AddButton("", px, y, pw, cardH, accent, () => ChoosePerk(idx), "perk-card" + i);
            AddCard(px, y, pw, cardH, accent, "perk" + i, .93f);
            AddPanel(px + 22, y + 34, 72, 72, new Color(1, 1, 1, .07f), uiRoot, "perk-med" + i);
            AddText((i + 1).ToString(), px + 58, y + 70, 32, accent, FontStyle.Bold, TextAnchor.MiddleCenter, "perk-num" + i);
            AddText(perk.label, px + 112, y + 50, 23, text, FontStyle.Bold, TextAnchor.MiddleLeft, "perk-l" + i);
            AddText(perk.desc, px + 112, y + 82, 14, accent, FontStyle.Bold, TextAnchor.MiddleLeft, "perk-d" + i);
            AddText("TAP TO CHOOSE", px + 112, y + 112, 11, muted, FontStyle.Bold, TextAnchor.MiddleLeft, "perk-cta" + i);
        }
        int footY = listTop + pendingPerks.Count * (cardH + gap) + 14;
        if (!perkRerollUsed) {
            AddAdButton("WATCH AD:  REROLL THESE", px, footY, 608, 66, "reroll",
                () => { perkRerollUsed = true; DrawResultScreen(lastResultSuccess); }, "perk-reroll");
            footY += 76;
        }
        AddIconButton("SKIP", "ic_back", px + 174, footY, 260, 62, muted,
            () => { pendingPerks.Clear(); DrawResultScreen(lastResultSuccess); }, "perk-skip");
    }

    void ChoosePerk(int idx)
    {
        if (idx < 0 || idx >= pendingPerks.Count) return;
        GrantPerk(pendingPerks[idx]);
        pendingPerks.Clear();
        Persist();
        PlayEventSound("perk");
        DrawResultScreen(lastResultSuccess);
    }

    void GenerateMarketOffers()
    {
        // Market = a stable DISCOUNT off the Studio catalogue (so players can plan/save toward a purchase)
        marketOffers.Clear();
        string goodId = save.room < 2 ? "room" : LowestUpgrade();
        marketOffers.Add(new MarketOffer { id = goodId, label = OfferLabel(goodId), tier = "GOOD", desc = OfferDesc(goodId), cost = Mathf.Max(60, Mathf.RoundToInt(StudioCostFor(goodId) * .78f)), color = gold });
        string equipmentId = BestEquipmentOffer();
        marketOffers.Add(new MarketOffer { id = equipmentId, label = OfferLabel(equipmentId), tier = "STANDARD", desc = OfferDesc(equipmentId), cost = Mathf.Max(45, Mathf.RoundToInt(StudioCostFor(equipmentId) * .82f)), color = blue });
        string staffId = LowestStaff();
        marketOffers.Add(new MarketOffer { id = staffId, label = staffId.ToUpperInvariant(), tier = "STANDARD", desc = "staff +1", cost = Mathf.Max(45, Mathf.RoundToInt(StudioCostFor(staffId) * .82f)), color = green });
    }

    int StudioCostFor(string id)
    {
        switch (id) {
            case "room": return Cost(220, save.room);
            case "speed": return Cost(90, save.speed);
            case "grill": return Cost(110, save.grill);
            case "prepUpgrade": return Cost(85, save.prepUpgrade);
            case "patience": return Cost(100, save.patience);
            case "sinkUpgrade": return Cost(95, save.sinkUpgrade);
            case "decor": return Cost(130, save.decor);
            case "marketing": return Cost(140, save.marketing);
            case "waiter": return StaffCost(save.waiter);
            case "cook": return StaffCost(save.cook);
            case "washer": return StaffCost(save.washer);
            case "prepper": return StaffCost(save.prepper);
            default: return EquipmentCost(id);
        }
    }

    string LowestUpgrade()
    {
        var levels = new Dictionary<string, int> {
            ["speed"] = save.speed, ["grill"] = save.grill, ["prepUpgrade"] = save.prepUpgrade,
            ["patience"] = save.patience, ["sinkUpgrade"] = save.sinkUpgrade,
            ["decor"] = save.decor, ["marketing"] = save.marketing
        };
        return levels.OrderBy(kv => kv.Value).First().Key;
    }

    string BestEquipmentOffer()
    {
        string[] ids = { "table", "counter", "drink", "sink", "hob" };
        return ids.FirstOrDefault(id => OwnedCount(id) < MaxOwned(id)) ?? "counter";
    }

    string LowestStaff()
    {
        var levels = new Dictionary<string, int> {
            ["waiter"] = save.waiter, ["cook"] = save.cook, ["prepper"] = save.prepper, ["washer"] = save.washer
        };
        return levels.OrderBy(kv => kv.Value).First().Key;
    }

    string OfferLabel(string id)
    {
        if (id == "prepUpgrade") return "PREP";
        if (id == "sinkUpgrade") return "WATER";
        if (id == "marketing") return "ADS";
        if (id == "room") return "BAY";
        return id.ToUpperInvariant();
    }

    string OfferDesc(string id)
    {
        if (id == "room") return "new bay";
        if (id == "speed") return "move +1";
        if (id == "grill") return "cook +1";
        if (id == "prepUpgrade") return "prep +1";
        if (id == "patience") return "patience +1";
        if (id == "sinkUpgrade") return "wash +1";
        if (id == "decor") return "tips +1";
        if (id == "marketing") return "flow +1";
        return "unit +1";
    }

    bool MarketChoiceLocked(MarketOffer offer)
    {
        if (offer.bought) return false;
        bool goodBought = marketOffers.Any(o => o.bought && o.tier == "GOOD");
        int standardBought = marketOffers.Count(o => o.bought && o.tier == "STANDARD");
        if (offer.tier == "GOOD") return standardBought > 0;
        return goodBought || standardBought >= 2;
    }

    void BuyMarketOffer(int index)
    {
        if (index < 0 || index >= marketOffers.Count) return;
        var offer = marketOffers[index];
        if (offer.bought || MarketChoiceLocked(offer)) return;
        if (save.coins < offer.cost) {
            SetMessage("Not enough coins", 1f);
            DrawResultScreen(lastResultSuccess);
            return;
        }
        save.coins -= offer.cost;
        if (offer.id == "room") save.room = Mathf.Min(2, save.room + 1);
        else if (offer.id == "speed") save.speed++;
        else if (offer.id == "grill") save.grill++;
        else if (offer.id == "prepUpgrade") save.prepUpgrade++;
        else if (offer.id == "patience") save.patience++;
        else if (offer.id == "sinkUpgrade") save.sinkUpgrade++;
        else if (offer.id == "decor") save.decor++;
        else if (offer.id == "marketing") save.marketing++;
        else if (offer.id == "waiter") save.waiter++;
        else if (offer.id == "cook") save.cook++;
        else if (offer.id == "washer") save.washer++;
        else if (offer.id == "prepper") save.prepper++;
        else if (OwnedCount(offer.id) < MaxOwned(offer.id)) SetOwned(offer.id, OwnedCount(offer.id) + 1);
        else {
            save.coins += offer.cost;
            SetMessage("No floor space", 1f);
            DrawResultScreen(lastResultSuccess);
            return;
        }
        offer.bought = true;
        Persist();
        DrawResultScreen(lastResultSuccess);
    }

    void BuildLayoutData()
    {
        appliances.Clear();
        // Front "pass" row (row 10, just under the divider): plates | 6 ingredient providers | 2 counters | bin
        int[] providerCols = { 1, 2, 3, 4, 5, 6 };
        var providers = ThemeProviders().Where(ItemUnlocked).ToList();
        for (int i = 0; i < providers.Count; i++) appliances.Add(App(providers[i], "provider", providerCols[Mathf.Min(i, providerCols.Length - 1)], 10, 1, 1, providers[i]));
        appliances.Add(App("plates", "plates", 0, 10));
        appliances.Add(App("trash", "trash", 9, 10));
        appliances.Add(App("extinguisher", "extinguisher", 0, 13));   // wall-mounted on the left kitchen wall
        // Dining room (rows 0-8): two columns of tables with a central + side aisles
        AddOwned("table", "table", new[] { V2(2, 2), V2(6, 2), V2(2, 5), V2(6, 5), V2(2, 8), V2(6, 8) }, 2, 1);
        // Kitchen (rows 11-13, roomier): hobs on the heat row, counters/prep/sink spread deeper
        AddOwned("hob", "hob", new[] { V2(0, 11), V2(1, 11), V2(2, 11), V2(3, 11) });
        // The separate "prep table" unit is gone — every counter is a PREP COUNTER (tap = stack,
        // hold = chop), so the old prep slots simply become more counters.
        AddOwned("counter", "counter", new[] { V2(7, 10), V2(8, 10), V2(4, 12), V2(5, 12), V2(6, 12), V2(7, 12),
                                               V2(0, 12), V2(1, 12), V2(2, 13), V2(3, 13), V2(4, 13), V2(5, 13), V2(6, 13) });
        AddOwned("sink", "sink", new[] { V2(7, 13), V2(8, 13), V2(9, 13) });
        AddOwned("drink", "drink", new[] { V2(8, 12), V2(9, 12) });
        if (save.theme == "pizza") AddOwned("oven", "oven", new[] { V2(8, 11), V2(9, 11) });
        if (save.theme == "coffee") AddOwned("espresso", "espresso", new[] { V2(8, 11), V2(9, 11) });
        ApplySavedLayout();
        var chairs = ChairMap();
        int tableIndex = 0;
        foreach (var table in appliances.Where(a => a.type == "table")) {
            int fallback = save.room >= 1 && tableIndex % 3 == 1 ? 4 : save.room >= 2 && tableIndex % 3 == 2 ? 1 : 2;
            table.seats = chairs.TryGetValue(table.id, out int savedSeats) ? Mathf.Clamp(savedSeats, 1, 4) : fallback;
            table.tableKind = table.seats <= 1 ? "bar" : table.seats >= 4 ? "family" : "small";
            tableIndex++;
        }
    }

    void SpawnWorkers()
    {
        workers.Clear();
        for (int i = 0; i < save.waiter; i++) workers.Add(new Worker { role = "waiter", pos = CellCenter(2, 14), target = CellCenter(2, 14), timer = .7f });
        for (int i = 0; i < save.cook; i++) workers.Add(new Worker { role = "cook", pos = CellCenter(4, 14), target = CellCenter(4, 14), timer = 1f });
        for (int i = 0; i < save.washer; i++) workers.Add(new Worker { role = "washer", pos = CellCenter(6, 14), target = CellCenter(6, 14), timer = 1.4f });
        for (int i = 0; i < save.prepper; i++) workers.Add(new Worker { role = "prepper", pos = CellCenter(8, 14), target = CellCenter(8, 14), timer = 1.6f });
    }

    void AddOwned(string id, string type, Vector2Int[] positions, int w = 1, int h = 1)
    {
        int count = Mathf.Min(OwnedCount(id), positions.Length);
        for (int i = 0; i < count; i++) appliances.Add(App(id + (i + 1), type, positions[i].x, positions[i].y, w, h));
    }

    Appliance App(string id, string type, int c, int r, int w = 1, int h = 1, string itemId = "")
    {
        return new Appliance { id = id, type = type, c = c, r = r, w = w, h = h, itemId = itemId };
    }

    Dictionary<string, Vector2Int> LayoutMap()
    {
        var map = new Dictionary<string, Vector2Int>();
        if (string.IsNullOrWhiteSpace(save.layout)) return map;
        foreach (var entry in save.layout.Split(';')) {
            if (string.IsNullOrWhiteSpace(entry)) continue;
            var parts = entry.Split(':');
            if (parts.Length < 3) continue;
            if (int.TryParse(parts[1], out int c) && int.TryParse(parts[2], out int r)) map[parts[0]] = new Vector2Int(c, r);
        }
        return map;
    }

    Dictionary<string, int> ChairMap()
    {
        var map = new Dictionary<string, int>();
        if (string.IsNullOrWhiteSpace(save.chairs)) return map;
        foreach (var entry in save.chairs.Split(';')) {
            if (string.IsNullOrWhiteSpace(entry)) continue;
            var parts = entry.Split(':');
            if (parts.Length != 2) continue;
            if (int.TryParse(parts[1], out int seats)) map[parts[0]] = seats;
        }
        return map;
    }

    void ApplySavedLayout()
    {
        var map = LayoutMap();
        var rot = LayoutRotMap();
        foreach (var a in appliances) {
            if (map.TryGetValue(a.id, out var cell)) {
                a.c = Mathf.Clamp(cell.x, 0, Cols - a.w);
                a.r = Mathf.Clamp(cell.y, 0, Rows - a.h);
            }
            if (rot.TryGetValue(a.id, out var rr)) a.rotation = rr & 3;
        }
    }

    Dictionary<string, int> LayoutRotMap()
    {
        var map = new Dictionary<string, int>();
        if (string.IsNullOrWhiteSpace(save.layout)) return map;
        foreach (var entry in save.layout.Split(';')) {
            var parts = entry.Split(':');
            if (parts.Length >= 4 && int.TryParse(parts[3], out int rr)) map[parts[0]] = rr;
        }
        return map;
    }

    void RotateSelected()
    {
        if (selectedLayout == null) { SetMessage("Tap a unit, then ROTATE", 1f); return; }
        selectedLayout.rotation = (selectedLayout.rotation + 1) & 3;
        SaveLayoutFromAppliances();
        RebuildWorld();
    }

    // Deliberate seat-count cycle for the selected table (1 → 2 → 4 if the room is expanded → 1)
    void CycleSeats()
    {
        if (selectedLayout == null || selectedLayout.type != "table") { SetMessage("Tap a table, then SEATS", 1f); return; }
        var t = selectedLayout;
        if (t.seats <= 1) t.seats = 2;
        else if (t.seats == 2 && save.room >= 1) t.seats = 4;
        else t.seats = 1;
        t.tableKind = t.seats <= 1 ? "bar" : t.seats >= 4 ? "family" : "small";
        SaveLayoutFromAppliances();
        RebuildWorld();
    }

    void SaveLayoutFromAppliances()
    {
        save.layout = string.Join(";", appliances.Select(a => a.id + ":" + a.c + ":" + a.r + ":" + a.rotation));
        save.chairs = string.Join(";", appliances.Where(a => a.type == "table").Select(a => a.id + ":" + a.seats));
        Persist();
        SetMessage("Floorplan saved", .85f);
    }

    void ResetLayout()
    {
        save.layout = "";
        save.chairs = "";
        Persist();
        ShowLayout();
    }

    List<string> ThemeProviders()
    {
        if (save.theme == "pizza") return new List<string> { "dough", "sauce", "cheese", "tomato", "lettuce" };
        if (save.theme == "coffee") return new List<string> { "coffee", "milk", "sauce" };
        if (save.theme == "hotdog") return new List<string> { "bun", "sausage", "sauce", "cheese", "onion" };
        if (save.theme == "bowl") return new List<string> { "rice", "patty", "lettuce", "tomato", "cheese", "onion" };
        return new List<string> { "bun", "patty", "lettuce", "tomato", "cheese", "sauce" };
    }

    List<Recipe> AvailableRecipes()
    {
        return recipes.Where(r => r.theme == save.theme && r.day <= save.day).ToList();
    }

    int GoalForDay(int day)
    {
        int[] goals = { 2, 3, 4, 5, 5, 6, 7, 7, 8, 9 };
        if (day > goals.Length) return goals[goals.Length - 1] + (day - goals.Length);
        return goals[Mathf.Clamp(day - 1, 0, goals.Length - 1)];
    }

    float SpawnDelayForDay(int day)
    {
        float[] spawn = { 24f, 16.5f, 11.5f, 8.75f, 7.25f, 6.3f, 5.65f, 5.25f, 4.95f, 4.7f };
        float pace = save.theme == "coffee" ? .92f : save.theme == "pizza" ? 1.1f : 1f;
        float baseDelay = day > spawn.Length
            ? Mathf.Max(3.6f, spawn[spawn.Length - 1] - (day - spawn.Length) * .12f)
            : spawn[Mathf.Clamp(day - 1, 0, spawn.Length - 1)];
        return baseDelay * pace;
    }

    int QueueMaxForDay(int day)
    {
        int[] max = { 2, 3, 4, 5, 5, 5, 5, 6, 6, 6 };
        if (day > max.Length) return Mathf.Min(9, max[max.Length - 1] + (day - max.Length) / 3);
        return max[Mathf.Clamp(day - 1, 0, max.Length - 1)];
    }

    float CustomerPatience()
    {
        // gentler ramp, especially the opening week
        float[] dayFactor = { 2.1f, 1.85f, 1.62f, 1.42f, 1.28f, 1.18f, 1.1f, 1.04f, 1f, .97f };
        float themeFactor = save.theme == "coffee" ? .95f : save.theme == "pizza" ? 1.06f : 1f;
        return (50f + save.patience * 5.2f + save.decor * 1.6f) * dayFactor[Mathf.Clamp(save.day - 1, 0, dayFactor.Length - 1)] * themeFactor * (HasPerk("regulars") ? 1.15f : 1f) * (DayEvent() == "rush" ? .92f : 1f);
    }

    float CookDuration()
    {
        float theme = save.theme == "pizza" ? -.35f : save.theme == "coffee" ? .2f : 0f;
        // slower, more readable cooking — you should have time to fetch the next ingredient
        return Mathf.Max(1.8f, (4.1f + theme - save.grill * .28f) * (HasPerk("grill_master") ? .78f : 1f));
    }

    float PrepDuration()
    {
        return Mathf.Max(1.05f, (2.55f - save.prepUpgrade * .34f - save.prep * .08f) * (HasPerk("sharp") ? .65f : 1f));
    }

    float WashDuration()
    {
        return Mathf.Max(.8f, (1.8f - save.sinkUpgrade * .22f) * (HasPerk("clean_sweep") ? .6f : 1f));
    }

    int ItemDay(string id)
    {
        if (save.theme == "pizza") {
            if (id == "dough" || id == "sauce" || id == "cheese") return 1;
            if (id == "lettuce") return 2;
            if (id == "tomato") return 3;
        }
        if (save.theme == "coffee") {
            if (id == "coffee") return 1;
            if (id == "milk") return 2;
            if (id == "sauce") return 4;
        }
        if (save.theme == "hotdog") {
            if (id == "bun" || id == "sausage" || id == "sauce") return 1;
            if (id == "cheese") return 2;
            if (id == "onion") return 3;
        }
        if (id == "lettuce") return 2;
        if (id == "tomato") return 3;
        if (id == "cheese") return 4;
        if (id == "sauce") return 5;
        return 1;
    }

    bool ItemUnlocked(string id) => save.day >= ItemDay(id);

    string ProviderState(string id)
    {
        if (id == "patty") return "raw";
        if (id == "sausage") return "raw";
        if (id == "dough" && save.theme == "pizza") return "raw";
        if (id == "coffee" && save.theme == "coffee") return "grounds";
        return NeedsPrep(id) ? "whole" : "ready";
    }

    void SetMessage(string value, float duration = 1.35f)
    {
        message = value;
        messageTimer = duration;
    }

    void PlayEventSound(string eventId)
    {
        if (!audioSource || (save != null && !save.sfxOn)) return;   // SFX toggle (#23)
        if (eventId == "pickup") PlayTone("pickup", 520f, .045f, .16f);
        else if (eventId == "place") PlayTone("place", 360f, .055f, .15f);
        else if (eventId == "cook") PlayTone("cook", 250f, .07f, .13f);
        else if (eventId == "ready") PlayTone("ready", 740f, .075f, .18f);
        else if (eventId == "serve") PlayTone("serve", 620f, .07f, .18f);
        else if (eventId == "coin") {
            PlayTone("coin-a", 860f, .06f, .18f);
            PlayTone("coin-b", 1180f, .05f, .13f);
        } else if (eventId == "bad") PlayTone("bad", 150f, .12f, .2f);
        else if (eventId == "wash") PlayTone("wash", 440f, .09f, .14f);
        else if (eventId == "prep") PlayTone("prep", 560f, .07f, .14f);
        else if (eventId == "order") PlayTone("order", 680f, .05f, .14f);
        else if (eventId == "start") PlayTone("start", 500f, .08f, .14f);
        else if (eventId == "trash") PlayTone("trash", 190f, .08f, .13f);
        else if (eventId == "combo") {                 // celebratory chord
            PlayTone("combo-a", 659f, .09f, .16f);
            PlayTone("combo-b", 784f, .1f, .14f);
            PlayTone("combo-c", 988f, .12f, .12f);
        } else if (eventId == "win") {                 // day-complete fanfare (chord stack)
            PlayTone("win-a", 523f, .16f, .16f);
            PlayTone("win-b", 659f, .18f, .14f);
            PlayTone("win-c", 784f, .2f, .13f);
            PlayTone("win-d", 1046f, .22f, .11f);
        } else if (eventId == "perk") {
            PlayTone("perk-a", 587f, .1f, .16f);
            PlayTone("perk-b", 880f, .14f, .13f);
        }
    }

    void PlayTone(string key, float frequency, float duration, float volume)
    {
        string cacheKey = key + ":" + frequency + ":" + duration;
        if (!toneCache.TryGetValue(cacheKey, out var clip) || !clip) {
            int sampleRate = 22050;
            int samples = Mathf.Max(64, Mathf.RoundToInt(sampleRate * duration));
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++) {
                float t = i / (float)sampleRate;
                float env = Mathf.Sin(Mathf.PI * i / Mathf.Max(1, samples - 1));
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * env * .75f;
            }
            clip = AudioClip.Create(cacheKey, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            toneCache[cacheKey] = clip;
        }
        audioSource.PlayOneShot(clip, volume);
    }

    void UpdateLayoutDrag()
    {
        if (Input.GetMouseButtonDown(0) && !PointerOverUI()) {
            // Tap = SELECT + start dragging. It must NOT change seats — that fired on every touch and
            // made tables impossible to reposition ("masaları düzgün kontrol edemiyoruz"). Seat count
            // is now a deliberate SEATS button (CycleSeats), like ROTATE.
            selectedLayout = ApplianceAtScreen(Input.mousePosition);   // pick the prop you clicked
            draggingLayout = selectedLayout != null;
            if (selectedLayout != null) ShowStationInfo(selectedLayout, 3f);   // bubble = what this unit is
            else { infoTimer = 0; infoAppliance = null; }                      // empty tap closes the balloon
            RebuildWorld();
        }
        if (Input.GetMouseButton(0) && draggingLayout && selectedLayout != null) {
            Vector2 p = ScreenToGamePoint(Input.mousePosition);
            Vector2Int cell = WorldToCell(p);
            int nc = Mathf.Clamp(cell.x, 0, Cols - selectedLayout.w);
            int nr = Mathf.Clamp(cell.y, 0, Rows - selectedLayout.h);
            // only move onto FREE cells — units can't be dropped on top of each other any more
            if (CellsFree(nc, nr, selectedLayout.w, selectedLayout.h, selectedLayout)) {
                selectedLayout.c = nc;
                selectedLayout.r = nr;
            }
            rebuildTimer -= Time.deltaTime;
            if (rebuildTimer <= 0) {
                rebuildTimer = .08f;
                RebuildWorld();
            }
        }
        if (Input.GetMouseButtonUp(0)) {
            if (draggingLayout) SaveLayoutFromAppliances();
            draggingLayout = false;
        }
    }

    Appliance ApplianceAt(Vector2 world)
    {
        for (int i = appliances.Count - 1; i >= 0; i--) if (CellRect(appliances[i].c, appliances[i].r, appliances[i].w, appliances[i].h).Contains(world)) return appliances[i];
        return null;
    }

    // Pick the appliance whose MESH you actually clicked. Projecting the tap onto the floor selected
    // the wrong cell because the 3D prop stands tall above its footprint; instead we compare the tap
    // to each prop's on-screen body position, which is correct at any camera height or rotation.
    Appliance ApplianceAtScreen(Vector2 screenPoint)
    {
        if (!cam) return null;
        // screen size of one tile (sample two cell centres) → acceptance radius
        Vector3 s0 = cam.WorldToScreenPoint(GameGroundPoint(CellCenter(0, 0), .3f));
        Vector3 s1 = cam.WorldToScreenPoint(GameGroundPoint(CellCenter(1, 0), .3f));
        float cellPx = Mathf.Max(24f, Vector2.Distance((Vector2)s0, (Vector2)s1));
        Appliance best = null; float bestD = float.MaxValue;
        foreach (var a in appliances) {
            Rect r = CellRect(a.c, a.r, a.w, a.h);
            Vector3 sp = cam.WorldToScreenPoint(GameGroundPoint(r.center, .34f));   // mesh body height
            if (sp.z <= 0) continue;
            float d = Vector2.Distance(new Vector2(sp.x, sp.y), screenPoint);
            // accept out to roughly the unit's own half-extent, so both ends of a 2-wide table hit
            float accept = cellPx * (.4f * Mathf.Max(a.w, a.h) + .45f);
            if (d < accept && d < bestD) { bestD = d; best = a; }
        }
        return best;
    }

    // true if the w×h block at (c,r) doesn't overlap any appliance except `except` (drag anti-overlap)
    bool CellsFree(int c, int r, int w, int h, Appliance except)
    {
        foreach (var o in appliances) {
            if (o == except) continue;
            bool apart = c + w <= o.c || o.c + o.w <= c || r + h <= o.r || o.r + o.h <= r;
            if (!apart) return false;
        }
        return true;
    }

    void BuyUpgrade(string id, bool stayInLayout = false)
    {
        if (id == "room" && save.room >= 2) {
            SetMessage("Room fully expanded", .9f);
            if (stayInLayout) ShowLayout(); else ShowMenu();
            return;
        }
        int level = UpgradeLevel(id);
        int cost = Cost(id == "room" ? 220 : id == "marketing" ? 140 : id == "decor" ? 130 : id == "grill" ? 110 : id == "prep" ? 85 : id == "sink" ? 95 : id == "patience" ? 100 : 90, level);
        if (save.coins < cost) {
            SetMessage("Not enough coins", .9f);
            if (stayInLayout) ShowLayout(); else ShowMenu();
            return;
        }
        save.coins -= cost;
        if (id == "speed") save.speed++; else if (id == "grill") save.grill++; else if (id == "prep") save.prepUpgrade++; else if (id == "patience") save.patience++; else if (id == "sink") save.sinkUpgrade++; else if (id == "decor") save.decor++; else if (id == "marketing") save.marketing++; else save.room++;
        Persist();
        SetMessage(id == "room" ? "Bay renovated" : "Upgrade bought", .9f);
        if (stayInLayout) ShowLayout(); else ShowMenu();
    }

    int UpgradeLevel(string id)
    {
        if (id == "speed") return save.speed;
        if (id == "grill") return save.grill;
        if (id == "prep") return save.prepUpgrade;
        if (id == "patience") return save.patience;
        if (id == "sink") return save.sinkUpgrade;
        if (id == "decor") return save.decor;
        if (id == "marketing") return save.marketing;
        return save.room;
    }

    void BuyEquipment(string id, bool stayInLayout = false)
    {
        if (OwnedCount(id) >= MaxOwned(id)) {
            SetMessage("No floor space", .9f);
            if (stayInLayout) ShowLayout(); else ShowMenu();
            return;
        }
        int cost = EquipmentCost(id);
        if (save.coins < cost) {
            SetMessage("Not enough coins", .9f);
            if (stayInLayout) ShowLayout(); else ShowMenu();
            return;
        }
        save.coins -= cost;
        SetOwned(id, OwnedCount(id) + 1);
        Persist();
        SetMessage(LayoutBuyTitle(id) + " bought", .9f);
        if (stayInLayout) ShowLayout(); else ShowMenu();
    }

    void BuyStaff(string id)
    {
        int count = id == "waiter" ? save.waiter : id == "cook" ? save.cook : id == "washer" ? save.washer : save.prepper;
        int cost = StaffCost(count);
        if (save.coins < cost) {
            SetMessage("Not enough coins", .9f);
            ShowMenu();
            return;
        }
        save.coins -= cost;
        if (id == "waiter") save.waiter++; else if (id == "cook") save.cook++; else if (id == "washer") save.washer++; else save.prepper++;
        Persist();
        SetMessage(id.ToUpperInvariant() + " hired", .9f);
        PlayEventSound("coin");
        ShowMenu();
    }

    void CycleTheme()
    {
        save.theme = save.theme == "burger" ? "pizza" : save.theme == "pizza" ? "coffee"
            : save.theme == "coffee" ? "hotdog" : save.theme == "hotdog" ? "bowl" : "burger";
        Persist();
        ShowMenu();
    }

    void ResetSave()
    {
        PlayerPrefs.DeleteKey("rushhouse-unity-save");
        save = new SaveData();
        shopTab = "upgrades";
        message = "Fresh save";
        messageTimer = 1.2f;
        ShowMenu();
    }

    // Pricing curve: a good day nets roughly 250-450 coins, so a first upgrade should land in one or
    // two days and later tiers scale smoothly instead of spiking out of reach.
    int Cost(int baseCost, int level) => baseCost + level * 70;
    int StaffCost(int level) => 180 + level * 120;
    int EquipmentCost(string id) => (id == "table" ? 150 : id == "hob" ? 165 : id == "sink" ? 140 : id == "drink" ? 130 : 110) + OwnedCount(id) * 55;

    int MaxOwned(string id)
    {
        if (id == "counter") return 7 + save.room * 2;   // absorbed the old prep-table slots
        if (id == "table") return 4 + save.room;   // bigger dining room holds more tables
        if (id == "hob") return 2 + save.room;
        if (id == "prep") return 0;                // retired unit type
        if (id == "sink") return 3;
        if (id == "drink") return 2;
        if (id == "oven") return 2;
        return 2;
    }

    int OwnedCount(string id)
    {
        if (id == "counter") return save.counter + save.prep;   // legacy prep tables became counters
        if (id == "table") return save.table;
        if (id == "hob") return save.hob;
        if (id == "prep") return 0;
        if (id == "sink") return save.sink;
        if (id == "drink") return save.drink;
        if (id == "oven") return save.oven;
        return save.espresso;
    }

    void SetOwned(string id, int value)
    {
        if (id == "counter") save.counter = value;
        else if (id == "table") save.table = value;
        else if (id == "hob") save.hob = value;
        else if (id == "prep") save.prep = value;
        else if (id == "sink") save.sink = value;
        else if (id == "drink") save.drink = value;
        else if (id == "oven") save.oven = value;
        else save.espresso = value;
    }

    static readonly string[] ProviderItems = { "bun", "patty", "lettuce", "tomato", "cheese", "sauce", "dough", "coffee", "milk", "sausage", "onion", "rice" };
    IEnumerable<string> ProviderSpriteNames() => ProviderItems.Select(ProviderSpriteName);
    string ProviderSpriteName(string itemId) => string.IsNullOrEmpty(itemId) ? "provider" : "provider" + char.ToUpperInvariant(itemId[0]) + itemId.Substring(1);

    string ApplianceSpriteName(Appliance a)
    {
        if (a.type == "table") return a.seats >= 4 ? "familyTable" : "table";
        if (a.type == "provider") {
            string n = ProviderSpriteName(a.itemId);
            return objectSprites.ContainsKey(n) ? n : "provider";
        }
        return a.type;
    }

    Vector2 ApplianceVisualSize(Appliance a, Vector2 baseSize)
    {
        // Realistic relative scale: work units read chest-high, cold chests slightly bulkier, small
        // hand props small. Neighbours may kiss (counter runs SHOULD look continuous) but not overlap.
        if (a.type == "table") return a.seats >= 4 ? new Vector2(baseSize.x * 1.14f, baseSize.y * 1.95f) : new Vector2(baseSize.x * 1.08f, baseSize.y * 1.55f);
        if (a.type == "provider") {
            bool cold = a.itemId == "patty" || a.itemId == "cheese" || a.itemId == "milk" || a.itemId == "sauce" || a.itemId == "sausage";
            return cold ? new Vector2(baseSize.x * 1.14f, baseSize.y * 1.18f) : new Vector2(baseSize.x * 1.0f, baseSize.y * 1.04f);
        }
        if (a.type == "extinguisher") return new Vector2(baseSize.x * .5f, baseSize.y * .72f);   // small wall unit, not a monolith
        if (a.type == "hob") return new Vector2(baseSize.x * .98f, baseSize.y * 1.0f);
        if (a.type == "sink") return new Vector2(baseSize.x * 1.34f, baseSize.y * 1.4f);
        if (a.type == "counter" || a.type == "prep") return new Vector2(baseSize.x * 1.3f, baseSize.y * 1.36f);
        if (a.type == "plates") return baseSize * .72f;
        if (a.type == "trash") return baseSize * .9f;
        if (a.type == "oven" || a.type == "espresso" || a.type == "drink") return new Vector2(baseSize.x * 1.08f, baseSize.y * 1.12f);
        return new Vector2(baseSize.x * 1.0f, baseSize.y * 1.04f);
    }

    Vector2 ApplianceVisualOffset(Appliance a)
    {
        if (a.type == "table") return new Vector2(0, .08f);
        if (a.type == "provider") return new Vector2(0, .07f);   // lift the taller container so it sits on its cell
        // wall-mounted in PLAY; in LAYOUT sit it ON its cell so it lines up with the drag hit-box (movable)
        if (a.type == "extinguisher") return screen == ScreenMode.Layout ? new Vector2(0, .04f) : new Vector2(-.44f, .22f);
        return new Vector2(0, .035f);
    }

    string FoodSpriteName(string id, string state)
    {
        if (id == "patty") return state == "cooked" || state == "burnt" ? "pattyCooked" : "pattyRaw";
        if (id == "sausage") return state == "cooked" || state == "burnt" ? "sausageCooked" : "sausageRaw";
        if (id == "dough") return state == "baked" || state == "burnt" ? "doughBaked" : "dough";
        if ((id == "lettuce" || id == "tomato") && state == "ready") return id + "Ready";
        return id;
    }

    Sprite ObjectSprite(string name)
    {
        if (objectSprites.TryGetValue(name, out var sprite) && sprite) return sprite;
        if (!objectCells.ContainsKey(name)) return whiteSprite;
        return AtlasSprite(objectAtlas, 4, 3, objectCells[name], objectCrops[name]);
    }

    Sprite FoodSprite(string name)
    {
        if (foodSprites.TryGetValue(name, out var sprite) && sprite) return sprite;
        if (!foodCells.ContainsKey(name)) return whiteSprite;
        return AtlasSprite(foodAtlas, 4, 3, foodCells[name], foodCrops[name]);
    }

    Sprite CharacterSprite(string name)
    {
        if (characterSprites.TryGetValue(name, out var sprite) && sprite) return sprite;
        if (!charCells.ContainsKey(name)) return whiteSprite;
        return AtlasSprite(characterAtlas, 4, 2, charCells[name], charCrops[name]);
    }

    Sprite FinalDishSprite(string name)
    {
        if (finalDishSprites.TryGetValue(name, out var sprite) && sprite) return sprite;
        return null;
    }

    Sprite CarrySprite(string name)
    {
        if (!string.IsNullOrEmpty(name) && carrySprites.TryGetValue(name, out var sprite) && sprite) return sprite;
        if (finalDishSprites.TryGetValue(name, out var dish) && dish) return dish;
        if (foodSprites.TryGetValue(name, out var food) && food) return food;
        if (objectSprites.TryGetValue(name, out var obj) && obj) return obj;
        return whiteSprite;
    }

    Sprite FloorSprite(int col, int row)
    {
        string key = col == 0 ? "wood" : "tile";
        if (floorSprites.TryGetValue(key, out var sprite) && sprite) return sprite;
        return AtlasSprite(floorAtlas, 2, 2, new Vector2Int(col, row), new Rect(0, 0, floorAtlas.width / 2f, floorAtlas.height / 2f));
    }

    Sprite MenuSprite(string name)
    {
        if (menuSprites.TryGetValue(name, out var sprite) && sprite) return sprite;
        return whiteSprite;
    }

    Sprite ShopIconSprite(string title)
    {
        switch (title) {
            case "SHOES": return CharacterSprite("player");
            case "HOB": return ObjectSprite("hob");
            case "PREP": return ObjectSprite("prep");
            case "COMFORT": return ObjectSprite("table");
            case "WATER":
            case "SINK":
            case "WASHER": return ObjectSprite("sink");
            case "BAY":
            case "ROOM": return FloorSprite(1, 0);
            case "DECOR": return ObjectSprite("familyTable");
            case "ADS": return ObjectSprite("provider");
            case "COUNTER": return ObjectSprite("counter");
            case "TABLE": return ObjectSprite("table");
            case "DRINK": return ObjectSprite("drink");
            case "WAITER": return CharacterSprite("waiter");
            case "COOK": return CharacterSprite("cook");
            case "PREPPER": return CharacterSprite("prepper");
            default: return ObjectSprite("counter");
        }
    }

    string ShopCardSprite(string title, Color stroke)
    {
        // tab_gold/tab_dark are flat dark pills meant for tabs — never use them as cards.
        // Every upgrade/equipment/staff card uses one of the three vivid gradient plates.
        if (title == "HOB" || title == "RESET") return "danger_button";
        if (title == "SINK" || title == "WASHER" || title == "COMFORT" || title == "BAY" || title == "ROOM") return "secondary_button";
        return "primary_button";
    }

    Sprite AtlasSprite(Texture2D tex, int cols, int rows, Vector2Int cell, Rect cropTopLeft)
    {
        if (tex == null) return whiteSprite;
        float sw = tex.width / (float)cols;
        float sh = tex.height / (float)rows;
        float x = cell.x * sw + cropTopLeft.x;
        float y = tex.height - ((cell.y + 1) * sh) + (sh - cropTopLeft.y - cropTopLeft.height);
        x = Mathf.Clamp(x, 0, tex.width - 1);
        y = Mathf.Clamp(y, 0, tex.height - 1);
        float w = Mathf.Clamp(cropTopLeft.width, 1, tex.width - x);
        float h = Mathf.Clamp(cropTopLeft.height, 1, tex.height - y);
        return Sprite.Create(tex, new Rect(x, y, w, h), new Vector2(.5f, .5f), 100);
    }

    // Game logic stays in its original XY grid. Rendering maps logical Y onto the ground's Z axis;
    // the compensation keeps one logical Y unit equal to one screen unit at CameraPitch.
    Vector3 GameGroundPoint(Vector2 p, float height = 0f)
    {
        return new Vector3(p.x, height, p.y / Mathf.Max(.01f, GroundProjection));
    }

    Vector3 BillboardPoint(Vector2 p, int order)
    {
        Vector3 basePoint = GameGroundPoint(p, .025f);
        // spriteDepthBoost pulls a sprite far toward the camera. Under an ORTHO camera that does not
        // move it on screen at all — it only wins the depth test, so overlays (the station card) sit
        // in front of every 3D mesh instead of being swallowed by a counter standing closer.
        return cam ? basePoint - cam.transform.forward * (order * SpriteDepthStep + spriteDepthBoost) : basePoint;
    }

    Quaternion BillboardRotation(float angle = 0f)
    {
        Quaternion face = cam ? cam.transform.rotation : Quaternion.Euler(CameraPitch, 0f, 0f);
        return face * Quaternion.Euler(0f, 0f, angle);
    }

    void SetBillboardAngle(GameObject go, float angle)
    {
        if (go) go.transform.rotation = BillboardRotation(angle);
    }

    Vector2 ScreenToGamePoint(Vector2 screenPoint)
    {
        if (!cam) return Vector2.zero;
        Ray ray = cam.ScreenPointToRay(screenPoint);
        var plane = new Plane(Vector3.up, Vector3.zero);
        if (!plane.Raycast(ray, out float distance)) return Vector2.zero;
        Vector3 hit = ray.GetPoint(distance);
        return new Vector2(hit.x, hit.z * GroundProjection);
    }

    GameObject MakeSprite(string name, Sprite sprite, Vector2 center, Vector2 targetSize, Color color, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(ActiveRoot, false);
        go.transform.position = BillboardPoint(center, order) + Vector3.up * spriteLift;
        go.transform.rotation = BillboardRotation();
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        Vector2 size = sr.bounds.size;
        if (size.x > 0 && size.y > 0) {
            float scale = Mathf.Min(targetSize.x / size.x, targetSize.y / size.y);
            go.transform.localScale = Vector3.one * scale;
        }
        return go;
    }

    GameObject MakeRect(string name, Vector2 center, Vector2 size, Color color, int order)
    {
        return MakeSprite(name, whiteSprite, center, size, color, order);
    }

    // MakeSprite scales UNIFORMLY (aspect-preserving) — useless for non-square bars. whiteSprite is
    // exactly 1 world unit, so set localScale to the size directly for a true rectangle.
    GameObject MakeBar(string name, Vector2 center, Vector2 size, Color color, int order)
    {
        var go = MakeRect(name, center, size, color, order);
        go.transform.localScale = new Vector3(size.x, size.y, 1);
        return go;
    }

    // like MakeSprite but stretches NON-uniformly to exactly fill `size` (for the angled wall panels).
    GameObject MakeStretch(string name, Sprite sprite, Vector2 center, Vector2 size, Color color, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(ActiveRoot, false);
        go.transform.position = BillboardPoint(center, order);
        go.transform.rotation = BillboardRotation();
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        Vector2 b = sr.bounds.size;
        if (b.x > 0 && b.y > 0) go.transform.localScale = new Vector3(size.x / b.x, size.y / b.y, 1);
        return go;
    }

    Material WorldMaterial(string key, Texture2D texture, Color tint, Vector2 tiling)
    {
        if (worldMatCache.TryGetValue(key, out var cached) && cached) return cached;
        Shader shader = Shader.Find("Standard");
        if (!shader) shader = Shader.Find("Legacy Shaders/Diffuse");
        var mat = new Material(shader) { color = tint };
        if (texture) {
            texture.wrapMode = TextureWrapMode.Repeat;
            mat.mainTexture = texture;
            mat.mainTextureScale = tiling;
        }
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", .12f);
        worldMatCache[key] = mat;
        return mat;
    }

    Mesh sharedCubeMesh;

    GameObject MakeBox3D(string name, Vector2 center, Vector2 logicalSize, float height, Material material, float baseHeight, bool castShadow)
    {
        // build the cube directly (shared mesh, no collider) instead of CreatePrimitive, which adds a
        // BoxCollider we immediately Destroy — that create+destroy churn ran ~11×/frame (door+rugs).
        if (!sharedCubeMesh) { var t = GameObject.CreatePrimitive(PrimitiveType.Cube); sharedCubeMesh = t.GetComponent<MeshFilter>().sharedMesh; if (Application.isPlaying) Destroy(t); else DestroyImmediate(t); }
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.GetComponent<MeshFilter>().sharedMesh = sharedCubeMesh;
        go.transform.SetParent(ActiveRoot, false);
        go.transform.position = GameGroundPoint(center, baseHeight);
        go.transform.localScale = new Vector3(logicalSize.x, height, logicalSize.y / Mathf.Max(.01f, GroundProjection));
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = castShadow ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = true;
        return go;
    }

    GameObject MakeCylinder3D(string name, Vector2 center, float radius, float height, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(ActiveRoot, false);
        go.transform.position = GameGroundPoint(center, height * .5f);
        go.transform.localScale = new Vector3(radius * 2f, height * .5f, radius * 2f);
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        renderer.receiveShadows = true;
        var collider = go.GetComponent<Collider>();
        if (collider) { if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider); }
        return go;
    }

    // Architectural assets have a local X span, local Y height and local Z thickness.  Fitting all
    // three dimensions independently lets the modular walls meet cleanly without shrinking a door
    // or window to the shallow wall thickness (the old generic prop fitter did exactly that).
    GameObject MakeArchitecturalModel(string name, string folder, Vector2 center,
        float logicalSpan, float logicalDepth, float targetHeight, float yaw)
    {
        var prefab = LoadModelPrefab(folder);
        if (!prefab) return null;
        var go = Instantiate(prefab);
        go.name = name + "-mesh";
        // Keep the FBX root's imported axis-correction (wiping it laid the wall kit on its back and
        // turned the top of the room into a row of "cabinets"). Because the corrected root can be
        // rotated, the span/height/thickness fit must happen on a WORLD-axis pivot wrapper.
        var pivot = new GameObject(name);
        pivot.transform.SetParent(ActiveRoot, false);
        go.transform.SetParent(pivot.transform, false);
        var renderers = go.GetComponentsInChildren<Renderer>();
        var material = ModelMaterial(folder, prefab);
        foreach (var renderer in renderers) {
            int slots = Mathf.Max(1, renderer.sharedMaterials.Length);
            var materials = new Material[slots];
            for (int i = 0; i < slots; i++) materials[i] = material;
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        Bounds raw = CombinedBounds(renderers);   // upright (import-corrected) world bounds
        bool sideWall = Mathf.Abs(Mathf.Sin(yaw * Mathf.Deg2Rad)) > .5f;
        float spanWorld = logicalSpan / (sideWall ? Mathf.Max(.01f, GroundProjection) : 1f);
        float depthWorld = logicalDepth / (sideWall ? 1f : Mathf.Max(.01f, GroundProjection));
        pivot.transform.localScale = new Vector3(
            spanWorld / Mathf.Max(.001f, raw.size.x),
            targetHeight / Mathf.Max(.001f, raw.size.y),
            depthWorld / Mathf.Max(.001f, raw.size.z));
        pivot.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        Bounds placed = CombinedBounds(renderers);
        Vector3 target = GameGroundPoint(center, .01f);
        pivot.transform.position += new Vector3(target.x - placed.center.x, target.y - placed.min.y, target.z - placed.center.z);
        return pivot;
    }

    GameObject MakeStatic3DModel(string name, string folder, Vector2 center, Vector2 logicalFootprint, float yaw, float maxHeight)
    {
        var prefab = LoadModelPrefab(folder);
        if (!prefab) return null;
        var go = Instantiate(prefab);
        go.name = name;
        go.transform.SetParent(ActiveRoot, false);
        go.transform.position = Vector3.zero;
        // keep the FBX root's imported axis-correction (wiping it laid models on their backs)
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * prefab.transform.localRotation;
        var renderers = go.GetComponentsInChildren<Renderer>();
        var material = ModelMaterial(folder, prefab);
        foreach (var renderer in renderers) {
            int slots = Mathf.Max(1, renderer.sharedMaterials.Length);
            var materials = new Material[slots];
            for (int i = 0; i < slots; i++) materials[i] = material;
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
        Bounds bounds = CombinedBounds(renderers);
        float targetX = Mathf.Max(.04f, logicalFootprint.x);
        float targetZ = Mathf.Max(.04f, logicalFootprint.y / Mathf.Max(.01f, GroundProjection));
        float scale = Mathf.Min(targetX / Mathf.Max(.001f, bounds.size.x), targetZ / Mathf.Max(.001f, bounds.size.z));
        if (maxHeight > 0f) scale = Mathf.Min(scale, maxHeight / Mathf.Max(.001f, bounds.size.y));
        go.transform.localScale = go.transform.localScale * scale;
        bounds = CombinedBounds(renderers);
        Vector3 target = GameGroundPoint(center, .01f);
        go.transform.position += new Vector3(target.x - bounds.center.x, target.y - bounds.min.y, target.z - bounds.center.z);
        return go;
    }

    // ---------- real 3D prop meshes (the user's FBX models) ----------
    void BuildPropModelMap()
    {
        propModelMap = new Dictionary<string, string> {
            { "hob", "cooktop" }, { "oven", "countertopoven" }, { "espresso", "coffeemaker" },
            { "drink", "soda" }, { "sink", "sink" }, { "trash", "bin" }, { "prep", "prepcounter" }, { "counter", "prepcounter" },
            { "plates", "plates" }, { "singlePlate", "plate" }, { "dirtyPlate", "dirtyplate" },
            { "extinguisher", "firedist" },
            { "providerBun", "buns" }, { "providerPatty", "patties" }, { "providerLettuce", "lettuce" },
            { "providerTomato", "tomato" }, { "providerCheese", "cheese" }, { "providerSauce", "sauces" },
            { "providerDough", "dough" }, { "providerRice", "rice" }, { "providerMilk", "milk" },
            { "providerCoffee", "roastedcoffee" }, { "providerSausage", "sausage" },
            { "providerOnion", "singleonion" }, { "provider", "prepcounter" },
            { "dtable", "table" }, { "familyTable", "bigtable" },
            { "dchair", "chair" }, { "dchairR", "chair" }, { "dchairF", "chair" }, { "dchairB", "chair" },
        };
    }

    string PropModelFolder(string sprite) => (propModelMap != null && sprite != null && propModelMap.TryGetValue(sprite, out var f)) ? f : null;

    GameObject LoadModelPrefab(string folder)
    {
        if (modelPrefabCache.TryGetValue(folder, out var p)) return p;
        var pf = Resources.Load<GameObject>("Models3D/" + folder + "/base");
        modelPrefabCache[folder] = pf;
        return pf;
    }

    Material ModelMaterial(string folder, GameObject prefab)
    {
        if (modelMatCache.TryGetValue(folder, out var m) && m) return m;
        // prefer Shader.Find, but fall back to the FBX's own imported material shader (guaranteed in
        // the build, since the prefab references it) so meshes never turn magenta in a player build
        var sh = Shader.Find("Standard");
        if (!sh && prefab) { var pr = prefab.GetComponentInChildren<Renderer>(); if (pr && pr.sharedMaterial) sh = pr.sharedMaterial.shader; }
        if (!sh) sh = Shader.Find("Legacy Shaders/Diffuse");
        var mat = new Material(sh);
        var tex = Resources.Load<Texture2D>("Models3D/" + folder + "/texture_diffuse");
        if (tex) mat.mainTexture = tex;
        // One compressed diffuse map is enough at this camera distance.  Per-prop normal,
        // metallic, roughness and emissive maps added hundreds of MB to a mobile-sized game.
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", .16f);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", .02f);
        modelMatCache[folder] = mat;
        return mat;
    }

    static Bounds CombinedBounds(Renderer[] rends)
    {
        var b = new Bounds(); bool has = false;
        foreach (var r in rends) { if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); }
        return b;
    }

    // Instantiate the real FBX on the XZ floor. The former renderer physically tilted each model
    // toward a flat camera, which made furniture look as if it were staring into the sky. The camera
    // is now angled instead, so meshes keep an upright Y axis and their true base touches the floor.
    // models authored facing AWAY from the camera (freezer chests, counters) get a 180 so their
    // fronts/contents show by default; applied on top of the in-game ROTATE yaw
    static readonly Dictionary<string, float> ModelYawFix = new Dictionary<string, float> {
        { "patties", 180f }, { "cheese", 180f }, { "milk", 180f }, { "sauces", 180f }, { "prepcounter", 180f },
    };

    GameObject Make3DProp(string name, string folder, Vector2 center, Vector2 footprint, float yaw, int order, float lift = 0f)
    {
        var prefab = LoadModelPrefab(folder);
        if (!prefab) return null;
        propUsed.Add(name);
        if (ModelYawFix.TryGetValue(folder, out float yawFix)) yaw += yawFix;
        // POOL: instantiate a mesh once per id, then just re-pose it each rebuild (instantiating full
        // FBX hierarchies every 33ms would stutter). Meshes live under the persistent propRoot.
        if (!propPool.TryGetValue(name, out var go) || !go) {
            go = Instantiate(prefab);
            go.name = name;
            go.transform.SetParent(propRoot.transform, false);
            var mat = ModelMaterial(folder, prefab);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) {
                int slots = Mathf.Max(1, r.sharedMaterials.Length);
                var materials = new Material[slots];
                for (int i = 0; i < slots; i++) materials[i] = mat;
                r.sharedMaterials = materials;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }
            propPool[name] = go;
        }
        if (!go.activeSelf) go.SetActive(true);
        if (!propRends.TryGetValue(name, out var rends) || rends == null || rends.Length == 0)
            propRends[name] = rends = go.GetComponentsInChildren<Renderer>();
        foreach (var r in rends) r.sortingOrder = order;
        // COMPOSE the yaw with the prefab root's imported rotation. Rodin FBX roots carry the
        // Z-up -> Y-up axis correction; overwriting it laid every model flat on its back
        // ("her şey yukarı bakıyor") — the audit sheets z-audit-imported/identity.png prove it.
        Quaternion importRot = prefab.transform.localRotation;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * importRot;
        // The fit (uniform scale + where the mesh sits relative to its own origin) depends only on
        // the model, the cell footprint and the yaw — never on where the prop stands — so it is
        // solved once and replayed. Cached as (scale, offsetX, baseY, offsetZ).
        string fitKey = folder + "|" + footprint.x.ToString("F3") + "|" + footprint.y.ToString("F3") + "|" + Mathf.RoundToInt(yaw);
        if (!propFit.TryGetValue(fitKey, out var fit)) {
            go.transform.position = Vector3.zero;
            // 1) fit the scale at CANONICAL yaw 0 — rotating a unit must TURN it, never RESIZE it
            //    (the sink used to change height when rotated because the fit re-ran per orientation)
            go.transform.localRotation = importRot;
            go.transform.localScale = Vector3.one;
            Bounds b0 = CombinedBounds(rends);
            float targetX = Mathf.Max(.05f, footprint.x * .96f);
            float targetZ = Mathf.Max(.05f, footprint.y / Mathf.Max(.01f, GroundProjection) * .96f);
            float scale = Mathf.Min(targetX / Mathf.Max(1e-4f, b0.size.x), targetZ / Mathf.Max(1e-4f, b0.size.z));
            // 2) apply the real yaw with that same scale, then measure centre + base
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * importRot;
            go.transform.localScale = Vector3.one * scale;
            Bounds b = CombinedBounds(rends);
            fit = new Vector4(scale, b.center.x, b.min.y, b.center.z);
            propFit[fitKey] = fit;
        }
        go.transform.localScale = Vector3.one * fit.x;
        Vector3 target = GameGroundPoint(center, .015f + lift);
        go.transform.position = new Vector3(target.x - fit.y, target.y - fit.z, target.z - fit.w);
        return go;
    }

    // deactivate pooled meshes no appliance used this rebuild (removed/moved units, or a screen change)
    void PrunePropPool()
    {
        foreach (var kv in propPool)
            if (kv.Value && kv.Value.activeSelf && !propUsed.Contains(kv.Key)) kv.Value.SetActive(false);
    }

    void HideAllProps()
    {
        foreach (var kv in propPool) if (kv.Value) kv.Value.SetActive(false);
        propUsed.Clear();
    }

    // Leaving a screen is the safe moment to actually free meshes (#33): the pool is keyed by
    // "prop:cell", so every seat re-cycle and every sold unit strands an entry that will never be
    // asked for again. Hidden meshes still hold their FBX instance, so a long session leaks.
    void TrimPropPool()
    {
        var stale = new List<string>();
        foreach (var kv in propPool)
            if (!kv.Value || !kv.Value.activeSelf) stale.Add(kv.Key);
        foreach (var key in stale) {
            if (propPool.TryGetValue(key, out var go) && go) Destroy(go);
            propPool.Remove(key);
            propRends.Remove(key);
        }
    }

    void DrawWorldLabel(string value, Vector2 center, Vector2 size, Color color)
    {
        MakeRect("label-bg", center, size, new Color(.03f, .04f, .06f, .76f), 40);
        var go = new GameObject("label");
        go.transform.SetParent(ActiveRoot, false);
        go.transform.position = BillboardPoint(center, 41) + Vector3.up * spriteLift;
        go.transform.rotation = BillboardRotation();
        var tm = go.AddComponent<TextMesh>();
        tm.text = value;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontSize = 36;
        tm.characterSize = .0165f;
        tm.color = color;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sortingOrder = 41;
    }

    void DrawWorldText(string value, Vector2 center, float characterSize, Color color, int order)
    {
        var go = new GameObject("world-text");
        go.transform.SetParent(ActiveRoot, false);
        go.transform.position = BillboardPoint(center, order) + Vector3.up * spriteLift;
        go.transform.rotation = BillboardRotation();
        var tm = go.AddComponent<TextMesh>();
        tm.text = value;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontSize = 42;
        tm.characterSize = characterSize;
        tm.color = color;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sortingOrder = order;
    }

    void DrawWorldBar(Vector2 center, float width, float ratio, Color color)
    {
        // MUST use MakeBar: MakeRect goes through MakeSprite, which scales UNIFORMLY, so a .52x.045 bar
        // collapsed to a .045 SQUARE — patience and cook-progress bars were rendering as tiny dots.
        float h = .07f;
        MakeBar("bar-bg", center, new Vector2(width, h), new Color(.02f, .025f, .035f, .85f), 42);
        float r = Mathf.Clamp01(ratio);
        if (r > .01f) MakeBar("bar-fill", center + new Vector2((r - 1f) * width * .5f, 0), new Vector2(width * r, h * .74f), color, 43);
    }

    Button AddButton(string label, int x, int y, int w, int h, Color color, Action action, string name = "button")
    {
        var go = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(uiRoot, false);
        Place(go.GetComponent<RectTransform>(), x, y, w, h);
        var img = go.GetComponent<Image>();
        img.sprite = whiteSprite;
        img.type = Image.Type.Simple;
        Color fill = Color.Lerp(new Color(.065f, .078f, .105f, .96f), color, .34f);
        fill.a = .96f;
        img.color = fill;
        img.raycastTarget = true;
        var b = go.GetComponent<Button>();
        var colors = b.colors;
        colors.normalColor = fill;
        colors.highlightedColor = Color.Lerp(fill, color, .38f);
        colors.pressedColor = Color.Lerp(fill, color, .58f);
        colors.selectedColor = colors.highlightedColor;
        b.colors = colors;
        b.onClick.AddListener(() => action?.Invoke());
        AddPanel(x + 4, y + 5, w, h, new Color(.005f, .008f, .014f, .42f), uiRoot, name + "-shadow");
        AddPanel(x, y, w, h, Color.Lerp(fill, color, .32f), uiRoot, name + "-border");
        AddPanel(x + 2, y + 2, w - 4, h - 4, fill, uiRoot, name + "-skin");
        AddPanel(x + 8, y + 7, Mathf.Max(8, w - 16), 3, Color.Lerp(color, Color.white, .18f), uiRoot, name + "-shine");
        if (!string.IsNullOrEmpty(label)) AddText(label, x + w / 2, y + h / 2, 17, text, FontStyle.Bold, TextAnchor.MiddleCenter, name + "-text");
        return b;
    }

    // An invisible tap target. A zero-alpha Image still receives raycasts, so this makes a whole
    // card or row clickable without AddButton's rim/face/shine painting over the content on top.
    Button AddHitArea(int x, int y, int w, int h, Action action, string name)
    {
        var go = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(uiRoot, false);
        Place(go.GetComponent<RectTransform>(), x, y, w, h);
        var img = go.GetComponent<Image>();
        img.sprite = whiteSprite;
        img.color = new Color(1, 1, 1, 0f);
        img.raycastTarget = true;
        var b = go.GetComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(() => action?.Invoke());
        return b;
    }

    Button AddTouchButton(string label, int x, int y, int w, int h, Color color, Action action, string name)
    {
        var go = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(uiRoot, false);
        Place(go.GetComponent<RectTransform>(), x, y, w, h);
        var img = go.GetComponent<Image>();
        img.sprite = whiteSprite;
        img.type = Image.Type.Simple;
        img.color = Color.Lerp(new Color(.04f, .052f, .074f, .7f), color, .38f);
        img.raycastTarget = true;
        var b = go.GetComponent<Button>();
        var colors = b.colors;
        colors.normalColor = img.color;
        colors.highlightedColor = Color.Lerp(img.color, color, .34f);
        colors.pressedColor = Color.Lerp(img.color, color, .56f);
        colors.selectedColor = colors.highlightedColor;
        b.colors = colors;
        b.onClick.AddListener(() => action?.Invoke());
        AddPanel(x + 5, y + 6, w, h, new Color(.004f, .006f, .01f, .36f), uiRoot, name + "-shadow");
        AddPanel(x, y, w, h, new Color(color.r, color.g, color.b, .58f), uiRoot, name + "-rim");
        AddPanel(x + 3, y + 3, w - 6, h - 6, img.color, uiRoot, name + "-face");
        AddPanel(x + 10, y + 9, Mathf.Max(8, w - 20), 3, new Color(1, 1, 1, .2f), uiRoot, name + "-shine");
        AddText(label, x + w / 2, y + h / 2, 15, text, FontStyle.Bold, TextAnchor.MiddleCenter, name + "-text");
        return b;
    }

    void AddHoldButton(int x, int y, int w, int h)
    {
        var button = AddTouchButton("HOLD", x, y, w, h, blue, () => { }, "play-hold");
        var trigger = button.gameObject.AddComponent<EventTrigger>();
        var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        down.callback.AddListener(_ => holdPressed = true);
        trigger.triggers.Add(down);
        var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        up.callback.AddListener(_ => holdPressed = false);
        trigger.triggers.Add(up);
        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => holdPressed = false);
        trigger.triggers.Add(exit);
    }

    string lastAnimToken = "";
    bool suppressAnim;      // set by in-screen redraws (a toggle flip) so the screen does not re-enter

    /// <summary>
    /// Staggered entrance for everything currently in uiRoot.
    ///
    /// Done here rather than at each call site because the UI is immediate-mode and flat: every
    /// screen is a few dozen siblings created in whatever order the drawing code happens to run.
    /// Sorting by descending y and stepping the delay turns that arbitrary order into a top-down
    /// cascade, so the header lands first and the eye is led down the screen. Elements at the same
    /// height get near-identical delays, which is what keeps a card and its own labels together
    /// without needing a real hierarchy.
    ///
    /// `token` identifies the screen: re-entering the same one (a shop tab switch, a toggle) must
    /// not replay the whole animation.
    /// </summary>
    void AnimateUIIn(string token, float stagger = .022f, float maxDelay = .34f)
    {
        if (uiRoot == null) return;
        if (suppressAnim) { suppressAnim = false; lastAnimToken = token; return; }
        if (lastAnimToken == token) return;
        lastAnimToken = token;

        var kids = new List<RectTransform>();
        foreach (Transform c in uiRoot) if (c is RectTransform rt) kids.Add(rt);
        // A CanvasGroup per element is cheap for a third of a second but not for a hundred of them;
        // past that the entrance would cost more than it is worth, so skip rather than stutter.
        if (kids.Count > 90) return;
        kids.Sort((a, b) => b.anchoredPosition.y.CompareTo(a.anchoredPosition.y));
        for (int i = 0; i < kids.Count; i++) {
            RushhouseUIPop.Play(kids[i].gameObject, Mathf.Min(maxDelay, i * stagger), new Vector2(0f, 24f));
            if (kids[i].GetComponent<Button>()) RushhouseUIPress.Attach(kids[i].gameObject);
        }
    }

    void AddPanel(int x, int y, int w, int h, Color color, RectTransform parent, string name)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = whiteSprite;
        img.type = Image.Type.Simple;
        img.color = color;
        img.raycastTarget = false;
        Place(go.GetComponent<RectTransform>(), x, y, w, h);
    }

    void AddMenuImage(string name, string spriteKey, int x, int y, int w, int h, Color color, bool preserveAspect = true)
    {
        AddUIImage(name, MenuSprite(spriteKey), x, y, w, h, color, preserveAspect);
    }

    // ---------- modern UI primitives ----------
    // Flat "surface" card: drop shadow, dark surface, hairline border and a coloured accent rail at the
    // top. Replaces the ornate fixed-aspect frames, which couldn't grow with their content.
    void AddCard(int x, int y, int w, int h, Color accent, string name = "card", float surfaceAlpha = .97f)
    {
        AddPanel(x + 4, y + 8, w, h, new Color(0, 0, 0, .38f), uiRoot, name + "-shadow");
        AddPanel(x, y, w, h, new Color(.16f, .19f, .25f, .9f), uiRoot, name + "-border");
        AddPanel(x + 2, y + 2, w - 4, h - 4, new Color(.055f, .07f, .1f, surfaceAlpha), uiRoot, name + "-surface");
        AddPanel(x + 2, y + 2, w - 4, 5, accent, uiRoot, name + "-accent");
    }

    // Centred status pill (daily special / day event / rush) that stays legible over the 3D room.
    // Soft filled row/pill used for list items and stat chips.
    void AddChip(int x, int y, int w, int h, Color tint, string name = "chip", float alpha = .1f)
    {
        AddPanel(x, y, w, h, new Color(tint.r, tint.g, tint.b, alpha), uiRoot, name);
        AddPanel(x, y, 4, h, new Color(tint.r, tint.g, tint.b, .85f), uiRoot, name + "-rail");
    }

    Button AddImageButton(string label, string spriteKey, int x, int y, int w, int h, Action action, string name = "image-button", int fontSize = 13, Color? labelColor = null)
    {
        var go = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(uiRoot, false);
        Place(go.GetComponent<RectTransform>(), x, y, w, h);
        var img = go.GetComponent<Image>();
        img.sprite = MenuSprite(spriteKey);
        img.type = Image.Type.Simple;
        img.preserveAspect = false;
        img.color = Color.white;
        img.raycastTarget = true;
        var b = go.GetComponent<Button>();
        var colors = b.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1);
        colors.pressedColor = new Color(.84f, .88f, .92f, 1);
        colors.selectedColor = colors.highlightedColor;
        b.colors = colors;
        b.onClick.AddListener(() => action?.Invoke());
        if (!string.IsNullOrEmpty(label)) AddText(label, x + w / 2, y + h / 2, fontSize, labelColor ?? text, FontStyle.Bold, TextAnchor.MiddleCenter, name + "-text");
        return b;
    }

    Sprite UIIcon(string name) => uiIconSprites.TryGetValue(name, out var s) ? s : null;

    // Filled button with an icon glyph on the left and a label beside it (floorplan controls).
    Button AddIconButton(string label, string icon, int x, int y, int w, int h, Color color, Action action, string name)
    {
        var b = AddButton("", x, y, w, h, color, action, name);
        int iconSz = Mathf.Min(h - 12, 34);
        bool labelled = !string.IsNullOrEmpty(label);
        int iconX = labelled ? x + 12 : x + (w - iconSz) / 2;
        AddUIImage(name + "-ic", UIIcon(icon), iconX, y + (h - iconSz) / 2, iconSz, iconSz, Color.white, true);
        if (labelled) AddText(label, x + 12 + iconSz + 6 + (w - 12 - iconSz - 6) / 2 - 6, y + h / 2, 13, text, FontStyle.Bold, TextAnchor.MiddleCenter, name + "-lbl");
        return b;
    }

    void AddUIImage(string name, Sprite sprite, int x, int y, int w, int h, Color color, bool preserveAspect = true)
    {
        if (!sprite) return;
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(uiRoot, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = preserveAspect;
        img.color = color;
        img.raycastTarget = false;
        Place(go.GetComponent<RectTransform>(), x, y, w, h);
    }

    void AddGlassPanel(int x, int y, int w, int h, string name, Color accent, float alpha = .92f)
    {
        AddPanel(x + 5, y + 7, w, h, new Color(.003f, .005f, .009f, .42f), uiRoot, name + "-shadow");
        AddPanel(x, y, w, h, new Color(.34f, .43f, .5f, .18f), uiRoot, name + "-rim");
        AddPanel(x + 2, y + 2, w - 4, h - 4, new Color(.035f, .045f, .065f, alpha), uiRoot, name + "-glass");
        AddPanel(x + 8, y + 7, w - 16, 3, new Color(1f, 1f, 1f, .14f), uiRoot, name + "-shine");
        AddPanel(x + 2, y + h - 6, w - 4, 4, Color.Lerp(accent, Color.white, .08f), uiRoot, name + "-accent");
    }

    Text AddText(string value, int x, int y, int size, Color color, FontStyle style, TextAnchor anchor = TextAnchor.MiddleCenter, string name = "text")
    {
        var go = new GameObject(name, typeof(Text));
        go.transform.SetParent(uiRoot, false);
        var rt = go.GetComponent<RectTransform>();
        int width = 500;
        int left = x - width / 2;
        if (anchor == TextAnchor.MiddleLeft) left = x;
        if (anchor == TextAnchor.MiddleRight) left = x - width;
        Place(rt, left, y - size, width, size * 2 + 8);
        var t = go.GetComponent<Text>();
        t.text = value;
        t.font = uiFont;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = anchor;
        t.resizeTextForBestFit = false;
        t.raycastTarget = false;
        return t;
    }

    void Place(RectTransform rt, int x, int y, int w, int h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
        rt.pivot = new Vector2(.5f, .5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x + w / 2f - W / 2f, H / 2f - y - h / 2f);
    }

    Vector2 CellCenter(int c, int r)
    {
        return new Vector2((c - Cols / 2f + .5f) * Tile, GridCenterY + (Rows / 2f - r - .5f) * Tile);
    }

    Rect CellRect(int c, int r, int w, int h)
    {
        Vector2 center = CellCenter(c, r) + new Vector2((w - 1) * Tile * .5f, -(h - 1) * Tile * .5f);
        return new Rect(center - new Vector2(Tile * w, Tile * h) / 2f, new Vector2(Tile * w, Tile * h));
    }

    Vector2Int WorldToCell(Vector2 p)
    {
        int c = Mathf.FloorToInt(p.x / Tile + Cols / 2f);
        int r = Mathf.FloorToInt(Rows / 2f - ((p.y - GridCenterY) / Tile));
        return new Vector2Int(c, r);
    }

    Vector2 ApproachPoint(Appliance a)
    {
        Rect r = CellRect(a.c, a.r, a.w, a.h);
        Vector2 delta = playerPos - r.center;
        if (delta.sqrMagnitude < .01f) delta = Vector2.down;
        Vector2 preferred = r.center + delta.normalized * (Mathf.Max(r.width, r.height) * .5f + .22f);
        return SafeOpenPosition(preferred, .16f);
    }

    float InteractionRange(Appliance a)
    {
        if (a == null) return .42f;
        if (a.type == "table") return .56f;
        return .48f;
    }

    float DistanceToAppliance(Vector2 p, Appliance a)
    {
        if (a == null) return 999f;
        Rect r = CellRect(a.c, a.r, a.w, a.h);
        float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
        float dy = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    Vector2 MoveActorWithCollision(Vector2 current, Vector2 target, float radius, Appliance ignore = null)
    {
        if (IsWalkablePosition(target, radius, ignore)) return target;
        Vector2 xOnly = new Vector2(target.x, current.y);
        if (IsWalkablePosition(xOnly, radius, ignore)) return xOnly;
        Vector2 yOnly = new Vector2(current.x, target.y);
        if (IsWalkablePosition(yOnly, radius, ignore)) return yOnly;
        // Both axes refused, so this is a corner — most often a door jamb approached slightly
        // off-centre. Freezing here is what made actors press into the kitchen doorway forever.
        // Slip sideways instead, at the same speed the step would have been, so the body slides
        // along the frame until it lines up with the opening.
        Vector2 dir = target - current;
        float stepLen = dir.magnitude;
        if (stepLen > 1e-5f) {
            Vector2 perp = new Vector2(-dir.y, dir.x) / stepLen * stepLen;
            if (IsWalkablePosition(current + perp, radius, ignore)) return current + perp;
            if (IsWalkablePosition(current - perp, radius, ignore)) return current - perp;
        }
        return current;
    }

    // ---------------- pathfinding ----------------
    // Everyone (player, staff, guests) routes around furniture instead of grinding into it. A* over
    // the cell grid, then the caller just steers toward the next waypoint each frame.
    static readonly Vector2Int[] StepDirs = {
        new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
        new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
    };

    bool CellWalkable(int c, int r, float radius, Appliance ignore)
    {
        if (c < 0 || r < 0 || c >= Cols || r >= Rows) return false;
        return IsWalkablePosition(CellCenter(c, r), radius, ignore);
    }

    // Steer along a CACHED path: only recompute A* every ~0.3s (or when we reach/lose the waypoint).
    // Running the full search every frame for every actor caused stutter and made bodies jitter
    // between two waypoints. If a straight line is clear we ignore the path entirely.
    Vector2 SteerAlongPath(Vector2 from, Vector2 to, float radius, Appliance ignore, ref Vector2 wp, ref float timer, float dt)
    {
        if (PathClear(from, to, radius, ignore)) return to;             // cheap common case
        timer -= dt;
        bool stale = timer <= 0f || wp == Vector2.zero
            || Vector2.Distance(from, wp) < .14f || !IsWalkablePosition(wp, radius, ignore);
        if (stale) {
            wp = NextPathStep(from, to, radius, ignore);
            timer = .28f + Mathf.Abs(from.x * 13.1f % .14f);            // desync actors so they don't all recompute together
        }
        return wp;
    }

    // Returns the next waypoint to steer toward, or `to` when a straight line is already clear.
    Vector2 NextPathStep(Vector2 from, Vector2 to, float radius, Appliance ignore = null)
    {
        if (PathClear(from, to, radius, ignore)) return to;
        Vector2Int start = WorldToCell(from), goal = WorldToCell(to);
        if (start == goal) return to;
        if (!CellWalkable(goal.x, goal.y, radius, ignore)) {
            // aim at the closest reachable neighbour of a blocked goal (e.g. standing at a counter)
            Vector2Int best = goal; float bestD = float.MaxValue;
            foreach (var d in StepDirs) {
                var n = goal + d;
                if (!CellWalkable(n.x, n.y, radius, ignore)) continue;
                float dd = Vector2.Distance(CellCenter(n.x, n.y), from);
                if (dd < bestD) { bestD = dd; best = n; }
            }
            if (best == goal) return to;
            goal = best;
        }
        var came = new Dictionary<Vector2Int, Vector2Int>();
        var gScore = new Dictionary<Vector2Int, float> { [start] = 0f };
        var open = new List<Vector2Int> { start };
        var closed = new HashSet<Vector2Int>();
        int guard = 0;
        while (open.Count > 0 && guard++ < 900) {
            int bi = 0; float bf = float.MaxValue;
            for (int i = 0; i < open.Count; i++) {
                float f = gScore[open[i]] + Vector2Int.Distance(open[i], goal);
                if (f < bf) { bf = f; bi = i; }
            }
            Vector2Int cur = open[bi];
            open.RemoveAt(bi);
            if (cur == goal) break;
            closed.Add(cur);
            foreach (var d in StepDirs) {
                var n = cur + d;
                if (closed.Contains(n) || !CellWalkable(n.x, n.y, radius, ignore)) continue;
                // no cutting through a diagonal gap between two blocked cells
                if (d.x != 0 && d.y != 0 &&
                    (!CellWalkable(cur.x + d.x, cur.y, radius, ignore) || !CellWalkable(cur.x, cur.y + d.y, radius, ignore))) continue;
                float ng = gScore[cur] + (d.x != 0 && d.y != 0 ? 1.414f : 1f);
                if (gScore.TryGetValue(n, out float old) && old <= ng) continue;
                gScore[n] = ng;
                came[n] = cur;
                if (!open.Contains(n)) open.Add(n);
            }
        }
        if (!came.ContainsKey(goal)) {
            // Unreachable, or the search ran out of budget. Returning `to` here steered the actor
            // straight at whatever was in the way and pinned it there — the classic "walks into the
            // wall and stays". Aim for the explored cell that got CLOSEST to the goal instead: a
            // partial route still makes real progress, and the next recompute finishes the job from
            // nearer in.
            Vector2Int bestNode = start; float bestDist = float.MaxValue;
            foreach (var kv in gScore) {
                if (kv.Key == start || !came.ContainsKey(kv.Key)) continue;
                float d = Vector2Int.Distance(kv.Key, goal);
                if (d < bestDist) { bestDist = d; bestNode = kv.Key; }
            }
            if (bestNode == start) return to;
            goal = bestNode;
        }
        Vector2Int step = goal;
        while (came.TryGetValue(step, out var prev) && prev != start) step = prev;
        return CellCenter(step.x, step.y);
    }

    // sample a straight line for blockers
    bool PathClear(Vector2 from, Vector2 to, float radius, Appliance ignore)
    {
        float dist = Vector2.Distance(from, to);
        int steps = Mathf.CeilToInt(dist / (Tile * .45f));
        for (int i = 1; i <= steps; i++)
            if (!IsWalkablePosition(Vector2.Lerp(from, to, i / (float)steps), radius, ignore)) return false;
        return true;
    }

    Vector2 SafeOpenPosition(Vector2 preferred, float radius)
    {
        if (IsWalkablePosition(preferred, radius)) return preferred;
        Vector2Int cell = WorldToCell(preferred);
        int bestC = Mathf.Clamp(cell.x, 0, Cols - 1);
        int bestR = Mathf.Clamp(cell.y, 0, Rows - 1);
        float best = float.MaxValue;
        Vector2 bestPos = CellCenter(bestC, bestR);
        for (int range = 1; range <= 4; range++) {
            for (int dc = -range; dc <= range; dc++) {
                for (int dr = -range; dr <= range; dr++) {
                    int c = Mathf.Clamp(bestC + dc, 0, Cols - 1);
                    int r = Mathf.Clamp(bestR + dr, 0, Rows - 1);
                    Vector2 pos = CellCenter(c, r);
                    if (!IsWalkablePosition(pos, radius)) continue;
                    float d = Vector2.Distance(preferred, pos);
                    if (d < best) {
                        best = d;
                        bestPos = pos;
                    }
                }
            }
            if (best < float.MaxValue) return bestPos;
        }
        return CellCenter(4, 15);
    }

    bool IsWalkablePosition(Vector2 p, float radius, Appliance ignore = null)
    {
        Rect all = CellRect(0, 0, Cols, Rows);
        if (p.x < all.xMin + radius || p.x > all.xMax - radius || p.y < all.yMin + radius || p.y > all.yMax - radius) return false;
        if (BlockedByDivider(p, radius)) return false;
        foreach (var a in appliances) {
            if (a == ignore) continue;
            if (!SolidAppliance(a)) continue;
            Rect solid = SolidRect(a, radius);
            if (solid.Contains(p)) return false;
        }
        return true;
    }

    // The kitchen divider is real architecture, not decoration: everyone must use the DOORWAY.
    // Previously the wall was drawn but had no collision, so actors walked straight through it.
    bool BlockedByDivider(Vector2 p, float radius)
    {
        float divY = DividerY();
        const float wallHalf = .16f;
        if (Mathf.Abs(p.y - divY) > wallHalf + radius) return false;   // not near the divider line
        return Mathf.Abs(p.x) > DoorHalfGap - radius;                  // the central doorway is open
    }

    bool SolidAppliance(Appliance a)
    {
        if (a == null) return false;
        return a.type == "table" || a.type == "counter" || a.type == "hob" || a.type == "oven" ||
            a.type == "espresso" || a.type == "prep" || a.type == "sink" || a.type == "trash" ||
            a.type == "provider" || a.type == "plates" || a.type == "drink";
    }

    Rect SolidRect(Appliance a, float radius)
    {
        // The prop is solid across its WHOLE cell footprint (that's how much floor the 3D mesh sits
        // on), plus the actor radius so bodies stop at the edge instead of clipping in. A tiny inset
        // keeps flush neighbours from sealing a diagonal you should be able to slip through.
        Rect r = CellRect(a.c, a.r, a.w, a.h);
        float inset = .02f;
        r = new Rect(r.xMin + inset, r.yMin + inset, r.width - inset * 2f, r.height - inset * 2f);
        float margin = radius;
        return new Rect(r.xMin - margin, r.yMin - margin, r.width + margin * 2f, r.height + margin * 2f);
    }

    bool WorldInsideGrid(Vector2 p)
    {
        Rect all = CellRect(0, 0, Cols, Rows);
        return all.Contains(p);
    }

    string FormatTime(float seconds)
    {
        int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return (s / 60) + ":" + (s % 60).ToString("00");
    }

    string HoldingLabel()
    {
        if (holding == null) return "";
        if (holding.kind == "drink") return "DRINK";
        if (holding.kind == "plate") return holding.dirty ? "DIRTY PLATE" : "PLATE " + holding.parts.Count;
        return holding.id.ToUpperInvariant();
    }

    string ActionHint()
    {
        var a = NearestAppliance(.58f);
        if (a == null) return "Tap station or use WASD + SPACE";
        if (HoldableAppliance(a)) return a.type == "sink" ? "Hold SPACE / HOLD to wash" : "Hold SPACE / HOLD to prep";
        return ApplianceLabel(a);
    }

    string ApplianceLabel(Appliance a)
    {
        if (a == null) return "STATION";
        if (a.type == "provider") {
            if (holding != null) return "HANDS FULL";
            return ItemUnlocked(a.itemId) ? "TAKE " + a.itemId.ToUpperInvariant() : "LOCKED DAY " + ItemDay(a.itemId);
        }
        if (a.type == "plates") return holding == null ? "TAKE PLATE" : "HANDS FULL";
        if (a.type == "counter") {
            if (holding == null) return a.item == null ? "EMPTY COUNTER" : "PICK UP";
            if (a.item == null) return "PLACE ON COUNTER";
            if (holding.kind == "ingredient" && a.item.kind == "plate" && IngredientReady(holding)) return "ADD TO PLATE";
            if (holding.kind == "plate" && a.item.kind == "ingredient" && IngredientReady(a.item)) return "ADD TO PLATE";
            return "COUNTER BLOCKED";
        }
        if (a.type == "hob") {
            if (holding?.id == "patty" || holding?.id == "sausage") return "COOK " + holding.id.ToUpperInvariant();
            if (a.item != null && a.item.state == "cooked" && holding == null) return "TAKE " + a.item.id.ToUpperInvariant();
            if (a.item != null && a.item.state == "burnt" && holding == null) return "TAKE BURNT";
            return "HOB";
        }
        if (a.type == "oven") {
            if (holding?.id == "dough") return "BAKE DOUGH";
            if (a.item != null && a.item.state == "baked" && holding == null) return "TAKE DOUGH";
            return "OVEN";
        }
        if (a.type == "espresso") {
            if (holding?.id == "coffee") return "BREW COFFEE";
            if (a.item != null && a.item.state == "brewed" && holding == null) return "TAKE COFFEE";
            return "ESPRESSO";
        }
        if (a.type == "prep") return "PREP";
        if (a.type == "drink") return "POUR DRINK";
        if (a.type == "sink") return "SINK";
        if (a.type == "trash") return "TRASH";
        if (a.type == "extinguisher") return holding == null ? "TAKE EXTINGUISHER" : holding.kind == "tool" ? "RACK IT" : "HANDS FULL";
        if (a.type == "table") {
            if (a.dirty) return "CLEAR TABLE";
            if (a.customer != null && !a.customer.ordered) return "TAKE ORDER";
            if (a.customer != null) {
                if (a.customer.mealServed && a.customer.wantsDrink && !a.customer.drinkServed) {
                    return holding?.kind == "drink" ? "SERVE DRINK" : "NEEDS DRINK";
                }
                if (holding?.kind == "plate") return PlateMatchesOrder(holding, a.customer) ? "SERVE " + a.customer.recipe.label : "WRONG ORDER?";
                return "BUILD " + a.customer.recipe.label;
            }
            return "TABLE";
        }
        return a.type.ToUpperInvariant();
    }

    bool PointerOverUI() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    void LoadSave()
    {
        // a partial write (app killed mid-Persist) or tampered blob makes FromJson THROW — catch it and
        // fall back to a fresh save instead of crashing to a black screen on launch (#24).
        try {
            if (PlayerPrefs.HasKey("rushhouse-unity-save")) save = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString("rushhouse-unity-save"));
        } catch (Exception e) { Debug.LogWarning("Rushhouse: corrupt save discarded — " + e.Message); save = null; }
        if (save == null) save = new SaveData();
        SanitizeSave();
    }

    void SanitizeSave()
    {
        save.day = Mathf.Max(1, save.day);
        save.coins = Mathf.Max(0, save.coins);
        if (string.IsNullOrWhiteSpace(save.theme)) save.theme = "burger";
        save.counter = Mathf.Max(3, save.counter);
        save.stars = Mathf.Clamp(save.stars == 0 ? 3 : save.stars, 1, 5);   // older saves predate stars
        save.table = Mathf.Max(1, save.table);
        save.hob = Mathf.Max(2, save.hob);
        save.prep = Mathf.Max(1, save.prep);
        save.sink = Mathf.Max(1, save.sink);
        save.oven = Mathf.Max(1, save.oven);
        save.espresso = Mathf.Max(1, save.espresso);
        save.bestDay = Mathf.Max(save.bestDay, save.day);
        if (save.layoutVersion < 2) {
            save.layout = "";
            save.chairs = "";
            save.layoutVersion = 2;
            Persist();
        }
    }

    void Persist()
    {
        PlayerPrefs.SetString("rushhouse-unity-save", JsonUtility.ToJson(save));
        PlayerPrefs.Save();
    }

    static Rect Crop(float x, float y, float w, float h) => new Rect(x, y, w, h);
    static Vector2Int V2(int x, int y) => new Vector2Int(x, y);

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var c);
        return c;
    }
}
