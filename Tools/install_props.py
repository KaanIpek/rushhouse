"""Copy staged prop renders (work/props_rendered/<folder>.png) onto the game's sprite
names, keeping the existing .png.meta so GUIDs/wiring survive. Render-once, copy-many."""
import os, shutil

STAGE = r"C:\Users\RLD_R\Documents\Codex\2026-07-06\imdi-bana-ok-iyi-ve-sadece\work\props_rendered"
ART = r"C:\Users\RLD_R\Documents\Codex\2026-07-06\imdi-bana-ok-iyi-ve-sadece\outputs\rushhouse-unity\Assets\Resources\Art"

# staging basename -> list of "Subdir/spriteName" targets (game sprite names, no extension)
MAP = {
    # appliances
    "cooktop": ["Objects/hob"], "countertopoven": ["Objects/oven"], "espresso": ["Objects/espresso"],
    "sink": ["Objects/sink"], "bin": ["Objects/trash"], "prepcounter": ["Objects/prep", "Objects/counter"],
    "plates": ["Objects/plates"], "plate": ["Objects/singlePlate"], "dirtyplate": ["Objects/dirtyPlate"],
    "firedist": ["Objects/extinguisher"], "fireee": ["Objects/flame"],
    # providers
    "buns": ["Objects/providerBun"], "patties": ["Objects/providerPatty"], "lettuce": ["Objects/providerLettuce"],
    "tomato": ["Objects/providerTomato"], "cheese": ["Objects/providerCheese"], "sauces": ["Objects/providerSauce"],
    "dough": ["Objects/providerDough"], "rice": ["Objects/providerRice"], "milk": ["Objects/providerMilk"],
    "roastedcoffee": ["Objects/providerCoffee", "Foods/coffee"], "sausage": ["Objects/providerSausage"],
    # furniture
    "table": ["Objects/dtable"], "bigtable": ["Objects/familyTable"],
    # ingredients
    "topbun": ["Foods/bun"], "bottombun": ["Foods/bunBottom"], "singlerawbeef": ["Foods/pattyRaw"],
    "cookedbeef": ["Foods/pattyCooked"], "singlerawsausage": ["Foods/sausageRaw"], "cookedsausage": ["Foods/sausageCooked"],
    "singlelettuce": ["Foods/lettuce"], "preppedlettuce": ["Foods/lettuceReady"], "singletomato": ["Foods/tomato"],
    "preppedtomato": ["Foods/tomatoReady"], "preppedcheese": ["Foods/cheese"], "singleonion": ["Foods/onion"],
    "singlesauce2": ["Foods/sauce"], "rawdough": ["Foods/dough"], "bakeddough": ["Foods/doughBaked"],
    "singlemilk": ["Foods/milk"], "ricecooked": ["Foods/rice"], "singlesoda": ["Foods/drink"],
    # final dishes
    "basicburger": ["FinalDishes/basic"], "burgerlettuce": ["FinalDishes/green"], "tomatolettuceburger": ["FinalDishes/fresh"],
    "cheeseburger": ["FinalDishes/cheese", "FinalDishes/saucy"], "cheeselettucetomatoburger": ["FinalDishes/deluxe"],
    "towerburger": ["FinalDishes/tower"], "baconburger": ["FinalDishes/bacon"], "doublecheesburger": ["FinalDishes/double"],
    "margaritapizza": ["FinalDishes/margherita"], "gardenpizza": ["FinalDishes/garden"], "rossopizza": ["FinalDishes/rosso"],
    "deluxepizza": ["FinalDishes/supreme"], "bianca": ["FinalDishes/bianca"],
    "coffee": ["FinalDishes/espresso", "FinalDishes/double_shot"], "latte": ["FinalDishes/latte", "FinalDishes/sweet_latte"],
    "cappucino": ["FinalDishes/cappuccino"], "basichotdog": ["FinalDishes/classic"], "cheesehotdog": ["FinalDishes/cheesy"],
    "onionsaucehotdog": ["FinalDishes/loaded"], "ricebowl": ["FinalDishes/ricebowl"], "greenbowl": ["FinalDishes/greenbowl"],
    "gardenbowl": ["FinalDishes/gardenbowl"], "cheddarbowl(maybeuseasapatatochipsbowl)": ["FinalDishes/cheddarbowl"],
    "fiestabowl": ["FinalDishes/fiesta"],
    # chairs: 4 azimuth renders -> seat sprites, mapped so each SEAT FACES THE TABLE.
    # chair_0(az0)=backrest-top/seat-down=NORTH(dchairF); chair_1(az90)=seat-right=WEST(dchair);
    # chair_2(az180)=seat-up/from-behind=SOUTH(dchairB); chair_3(az270)=seat-left=EAST(dchairR).
    "chair_0": ["Objects/dchairF"], "chair_1": ["Objects/dchair"], "chair_2": ["Objects/dchairB"], "chair_3": ["Objects/dchairR"],
}

installed, missing = 0, []
for stem, targets in MAP.items():
    src = os.path.join(STAGE, stem + ".png")
    if not os.path.exists(src):
        missing.append(stem); continue
    for t in targets:
        dst = os.path.join(ART, t.replace("/", os.sep) + ".png")
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        shutil.copy2(src, dst)
        installed += 1
print("INSTALLED", installed, "MISSING_SRC", missing)
