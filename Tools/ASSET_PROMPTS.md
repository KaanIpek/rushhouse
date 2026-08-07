# Rushhouse — 3D asset prompt pack

Goal: replace the hand-drawn / faked-3D art with real stylized-3D renders that all match each
other **and** match the characters (which are fixed at a high 3/4 top-down angle and cannot be
re-rendered). Everything below is written to be pasted into an AI image generator.

---

## 0. THE STYLE BLOCK — paste this in FRONT of every single prompt

> Stylized 3D game asset render, single object, centered. Chunky rounded low-poly-ish forms with
> soft bevels, clean readable silhouette, smooth matte surfaces, subtle material texture — the
> look of a modern cozy cooking game (PlateUp / Overcooked style), NOT photorealistic, NOT flat
> 2D vector, NOT pixel art. Camera: high three-quarter top-down view, about 50 degrees above the
> horizon, straight on (no rotation/skew). Lighting: soft warm key light from the upper-left,
> gentle fill, mild ambient occlusion in the crevices, no harsh specular hotspots, no rim light.
> Warm saturated but natural colors, high readability at small size. **Fully transparent
> background (PNG alpha). NO ground shadow, NO drop shadow, NO floor, NO backdrop, no text, no
> watermark.** Object fills ~85% of the frame with even padding.

**Critical:** the 50° top-down camera angle must be IDENTICAL for every asset. If assets come back
at different angles they will not sit together in the scene. Generate one asset first, approve the
angle, then reuse that exact seed/style for the rest.

**Output:** PNG with alpha. Square canvas unless noted.

---

## 1. FURNITURE — `Assets/Resources/Art/Objects/` — 512×512
Highest priority (this is what looks worst in-game today). All wood must be the SAME warm oak, and
chairs must clearly match the table's material.

| File | Prompt subject |
|---|---|
| `dtable.png` | A small square 2-person restaurant dining table. Thick warm-oak top with a visible chamfered edge showing the slab thickness, four sturdy tapered wooden legs. |
| `dchair.png` | A wooden dining chair seen in profile **facing RIGHT** — backrest on the LEFT side, seat surface extending to the right. Same warm oak as the table, slatted backrest, thick seat. |
| `dchairR.png` | The exact same chair **mirrored — facing LEFT** (backrest on the RIGHT). |
| `dchairF.png` | The same wooden chair seen **from the front**: backrest at the back/away from camera, seat surface facing the viewer. |
| `dchairB.png` | The same wooden chair seen **from behind**: the tall slatted backrest faces the camera and hides most of the seat. |
| `familyTable.png` | A larger rectangular 4-person version of `dtable`, same oak, same thickness and legs. Canvas 640×512. |

## 2. INGREDIENT STATIONS (providers) — `Art/Objects/` — 512×640 (portrait)
Two families. Keep the crate/freezer body IDENTICAL across each family so only the contents change.

**Wooden crate family** — *"An open-topped rustic wooden crate, slatted sides, filled to the brim with X, the contents mounded just above the rim."*

| File | X = |
|---|---|
| `providerBun.png` | glossy golden brioche burger buns |
| `providerDough.png` | pale raw pizza dough balls |
| `providerLettuce.png` | crisp green lettuce heads |
| `providerTomato.png` | ripe red tomatoes |
| `providerOnion.png` | purple-white onions |
| `providerRice.png` | an open burlap sack of white rice instead of a crate (same size/footprint) |

**Freezer/chiller family** — *"A small white chest freezer with a raised lid, chrome trim, a little green LED on the front panel, cold blue frosty haze inside, packed with X."*

| File | X = |
|---|---|
| `providerPatty.png` | raw red beef patties |
| `providerSausage.png` | raw pink sausages |
| `providerCheese.png` | bright yellow cheese slices/blocks |
| `providerMilk.png` | glass milk bottles |
| `providerSauce.png` | squeeze bottles of red sauce |
| `providerCoffee.png` | bags of dark coffee beans |

## 3. KITCHEN APPLIANCES — `Art/Objects/` — 512×512
Stainless steel + warm accents, all the same metal.

| File | Prompt subject |
|---|---|
| `hob.png` | A stainless-steel 4-burner gas cooktop, black cast-iron grates, red control knobs at the front. |
| `oven.png` | A stainless-steel countertop oven with a dark glass door and a chrome handle. |
| `espresso.png` | A chrome espresso machine with a portafilter, steam wand and a small cup tray. |
| `counter.png` | A plain stainless-steel prep counter with an empty flat top. |
| `prep.png` | A prep station: stainless counter with a thick wooden chopping board and a chef's knife on it. |
| `sink.png` | A stainless double-basin sink with a tall curved chrome tap. |
| `trash.png` | A dark grey swing-lid kitchen bin. |
| `plates.png` | A neat stack of clean round white ceramic plates. |
| `drink.png` | A soda fountain / drinks dispenser with two taps and a small drip tray. |
| `extinguisher.png` | A red fire extinguisher on a **wall bracket**, black valve and hose. Canvas 384×512. |

## 4. RAW INGREDIENTS — `Art/Foods/` — 320×320
Single hero item, no plate, no board.

`bun.png` (top half of a sesame brioche bun) · `bunBottom.png` (bottom half) ·
`pattyRaw.png` (raw red beef patty) · `pattyCooked.png` (seared browned patty with grill marks) ·
`sausageRaw.png` (raw pink sausage) · `sausageCooked.png` (browned grilled sausage) ·
`lettuce.png` (whole lettuce head) · `lettuceReady.png` (loose shredded lettuce leaves) ·
`tomato.png` (whole tomato) · `tomatoReady.png` (a fan of tomato slices) ·
`cheese.png` (a folded slice of yellow cheese) · `onion.png` (whole purple onion) ·
`sauce.png` (a red squeeze bottle) · `dough.png` (raw dough ball) · `doughBaked.png` (baked golden pizza base) ·
`coffee.png` (a heap of dark coffee beans) · `milk.png` (a glass milk bottle) ·
`rice.png` (a mound of cooked white rice) · `drink.png` (a cold soda cup with a lid and straw)

## 5. FINAL DISHES — `Art/FinalDishes/` — 512×512
**Always plated on a round white ceramic plate** (except drinks/coffee, which are in a cup),
viewed from the same 50° angle so the stack is readable.

**Burgers** (stacked, in a bun):
| File | Contents |
|---|---|
| `basic.png` | bun + patty + bun |
| `green.png` | bun + patty + lettuce + bun |
| `fresh.png` | bun + patty + lettuce + tomato + bun |
| `cheese.png` | bun + patty + melted cheese + bun |
| `deluxe.png` | bun + patty + cheese + lettuce + tomato + bun |
| `saucy.png` | bun + patty + cheese + a drizzle of red sauce + bun |
| `tower.png` | bun + patty + cheese + a second patty + lettuce + bun (tall) |
| `bacon.png` | bun + patty + crispy bacon strips + cheese + bun |
| `double.png` | bun + two patties + two cheese slices + bun |

**Pizzas** (whole round pizza on the plate):
`margherita.png` (sauce + mozzarella) · `garden.png` (sauce + cheese + green leaves) ·
`rosso.png` (sauce + cheese + tomato slices) · `supreme.png` (sauce + cheese + tomato + greens) ·
`bianca.png` (white pizza: no red sauce, cheese + herbs)

**Coffees** (in a ceramic cup on a saucer, no plate):
`espresso.png` (small dark shot) · `latte.png` (tall, milky, leaf latte-art) ·
`sweet_latte.png` (latte with a caramel drizzle) · `double_shot.png` (two dark shots + a little milk) ·
`cappuccino.png` (thick milk foam cap, cocoa dust)

**Hot dogs** (in a long split bun on the plate):
`classic.png` (bun + sausage) · `cheesy.png` (bun + sausage + melted cheese) ·
`loaded.png` (bun + sausage + diced onion + red sauce zig-zag)

**Bowls** (in a deep ceramic bowl, top-down-ish, sections visible):
`ricebowl.png` (rice + sliced beef) · `greenbowl.png` (rice + beef + lettuce) ·
`gardenbowl.png` (rice + lettuce + tomato + onion) · `cheddarbowl.png` (rice + beef + cheese) ·
`fiesta.png` (rice + beef + cheese + tomato + onion)

## 6. MISC — `Art/Objects/`
`singlePlate.png` (one clean empty white plate, 384×384) ·
`dirtyPlate.png` (a used plate with food smears, 384×384) ·
`flame.png` (a single stylized fire flame, orange-to-yellow, transparent bg, 320×320 — used for the fire hazard) ·
`walldoor.png` (a wooden double swing door, front-on) · `wallwindow.png` (a window with a daylight view) ·
`wallpic.png` (a small framed picture)

---

## 7. HOW TO DROP THEM IN
1. Save each PNG with **exactly** the filename above (case-sensitive) into the folder listed.
2. Overwrite the existing file — **keep the `.png.meta` file that's already there** (don't delete
   it; Unity reuses it, so the sprite keeps its GUID and nothing unwires).
3. Tell me when they're in and I'll re-import, re-tune the in-game sizes/offsets, verify with a
   capture and rebuild.

## 8. PRIORITY ORDER (if you don't want to generate all ~90)
1. **Furniture** (§1) — 6 files. Biggest visible win.
2. **Appliances** (§3) — 10 files.
3. **Providers** (§2) — 12 files.
4. Ingredients (§4) and Final dishes (§5) — these are already decent painterly art; upgrade last.
