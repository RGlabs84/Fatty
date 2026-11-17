<!-- README schema v2 -->
# 🍖 Fatty

![FATTY](https://img.shields.io/badge/FATTY-STOP_SURVIVING%2C_START_FEASTING-c9a227?style=for-the-badge&labelColor=1b1b1b)
![VERSION](https://img.shields.io/badge/VERSION-1.2.0-8b1e1e?style=for-the-badge&labelColor=1b1b1b)
![CLIENT SIDE](https://img.shields.io/badge/CLIENT--SIDE-SERVER_OPTIONAL-1f4e79?style=for-the-badge&labelColor=1b1b1b)

> *The skald gave up counting the plates at forty. "He eats as if Fenrir were coming for the leftovers," muttered the jarl's wife. The Viking reached for a fifth bowl of stew, belched loudly enough to scatter the goats, and said that Fenrir would have to wait his turn like everybody else.*

### Eat. Keep every point of it. Get bigger forever.

Fatty rebuilds Valheim's food. Stats stop fading, the food bar grows to **four food slots and a drink slot of its own**, and the same food stacks into one slot for more. Food lasts twice as long, and stamina and eitr refill as fast as vanilla however big your bars get.

Then it rewards *how* you eat. The right mix of foods switches on a buff, whatever is in your drink slot picks a draught, a slot full of Tasty Mead gets you **Drunk**, and every meal counts toward permanent stats for the life of your character. Every buff has its own icon, every food mod's food is sorted for you, and **The Feast Ledger** shows exactly what is in your stomach.

---

![NEW IN 1.2.0](https://img.shields.io/badge/NEW_IN_1.2.0-THE_PUKE_BUTTON-8b1e1e?style=for-the-badge&labelColor=1b1b1b)

**Ate the wrong thing? Bring it all back up.** The Feast Ledger now has a **Puke** button.

| You | It |
|---|---|
| **Open The Feast Ledger** — the ▲ FEAST tab or **F8** | the **Puke** button sits at the top, whenever there is something to bring up |
| **Click Puke** | it arms and asks **Really? Click again** |
| **Click again** within a few seconds | every food slot and the drink slot empty — every stack — with vanilla's retching |
| **Leave it** | it disarms; nothing happens |
| **Your buffs** | diet synergies, draughts and Drunk end on their own — they follow what is in your stomach |
| **Your history** | stays. Lifetime counts, Fatness and milestones are never refunded: what you ate stays eaten |

🌀 **Drunk motion blur** — heavy ghosting trails that fade in as you get drunk and out as you sober up, even with Motion Blur off in Valheim's graphics settings, and your own setting comes back afterwards · 🔒 **Admins** can take the Puke button away from everyone with `Allow Puke Button`.

---

![BEFORE YOU INSTALL](https://img.shields.io/badge/BEFORE_YOU_INSTALL-READ_THIS-b8860b?style=for-the-badge&labelColor=1b1b1b)

| | If… | Then… |
|---|---|---|
| 🤝 | you play on a **server** | server and players update together — it only accepts the same version, and its gameplay settings replace yours. |
| ⏱️ | you run **blacks7ar's FoodDurationMultiplier** | Fatty uses that mod's food duration instead of its own. The two never multiply. |
| 🍖 | you want **vanilla food timers** | food lasts **twice as long** out of the box — set `Food Duration Multiplier` to 1.0. |
| 📁 | you **hand-edit** `Fatty_FoodData.json` | `Rebuild Food Data` overwrites your edits. The old file is kept as `Fatty_FoodData.json.bak`. |
| 🌀 | screen blur makes you **queasy** | switch `Drunk Motion Blur` off, or turn `Drunk Blur Strength` down. |

---

![I](https://img.shields.io/badge/I-THE_BIGGER_STOMACH-c9a227?style=for-the-badge&labelColor=1b1b1b)

**Five slots, no decay, and the same food stacks.**

| | |
|---|---|
| 🛑 **No decay** | a food gives every point until its timer ends — no slow fade |
| 🍽️ **Four food slots + a drink slot** | four foods and a drink at once; food can't take the drink slot, and a drink can't push out food |
| 🥞 **Stacking** | eat the same food again and it stacks in its slot, up to four — each copy worth less: 100% · 30% · 20% · 10% |
| ⏳ **Twice as long** | food lasts double out of the box, no second mod needed |
| ⚡ **Regen keeps up** | stamina and eitr refill as fast as vanilla however big Fatty makes the bars — never slower |
| ✨ **Gold trim** | the food icons get a gold frame, and a **×N** badge shows each stack |

<details>
<summary>📖 <b>Read the saga</b> — stacks, timers and the drink slot</summary>

**Only the newest copy in a stack counts down.** Older copies wait in reserve, frozen, until the active one runs out; then the next one takes over. A slot showing "×4, 75m" means *the one you just ate* empties in 75 minutes, and the three behind it are banked, not draining. Prefer a picture? `Use Segmented Stack Bar` swaps the badge for a thin bar under the icon, one segment per item, dimming as each takes its turn.

| On the food bar | |
|---|---|
| **The icon** | the food or drink in that slot |
| **×N** | how many copies are stacked there |
| **The number under it** | when the active copy runs out — the next one then takes over |

**The drink slot takes every drink**, including meads that only give a timed effect, like Frost Resistance or Tasty Mead. A mead holds the slot for its own effect's length or twenty minutes, whichever is longer, so a health mead's draught doesn't vanish seconds after you drink it.

**Drinking is never blocked.** If the drink slot is busy, the mead works exactly as in vanilla — it just gives no Fatty draught, so you can never be locked out of a health mead mid-fight. **Nor can you drink more often than vanilla**: the cooldown between meads is untouched. Tasty Mead is the one exception, because its effect does nothing at all — see Drunk.

**Regen scaling exists because vanilla refills stamina and eitr at a flat rate** that ignores how big the bar is, so any mod that enlarges your pools quietly makes them refill slower. Fatty scales the rate with the pool to keep refill *time* at vanilla's — capped, and never below vanilla.

</details>

---

![II](https://img.shields.io/badge/II-THE_FEAST_LEDGER-5b2c83?style=for-the-badge&labelColor=1b1b1b)

**Everything in your stomach, on one gilt page.**

| You | It |
|---|---|
| **▲ FEAST tab** or **F8** | opens The Feast Ledger |
| **Click a stack** — "Tasty Mead ×4" | opens it out: each item's time left, its place on the stacking ladder, and the health, stamina and eitr it gives right now |
| **Getting Fat** | your progress toward every permanent perk |
| **Lifetime Feast Log** | every food you've ever eaten on this character, and how many times |
| **Puke** | empties your stomach — click twice |
| **Drag the title** · **the corner grip** | moves it · resizes it; it remembers, even after a restart |
| **Close the inventory or the ESC menu** | the Ledger closes with it; the ▲ FEAST tab stays to bring it back |

<details>
<summary>📖 <b>Read the saga</b> — the frame, the text and the tab</summary>

The Ledger sits in a full gilt picture frame: a polished double gold rail with mitred acanthus corners and a crest top and bottom, over near-black leather. Its text runs a couple of points larger than the theme's base size; `Feast Ledger Font Size Delta` makes it bigger, smaller or plain.

The **▲ FEAST** tab can be dragged anywhere and never leaves the screen — a position saved on a monitor you no longer have is pulled back into view, so the Ledger can't get lost. The F8 key is rebindable with `Breakdown Panel Hotkey`.

</details>

---

![III](https://img.shields.io/badge/III-DIET_SYNERGIES-2e5e3a?style=for-the-badge&labelColor=1b1b1b)

**Eat the right mix and a buff appears. Nothing to craft, nothing to click.**

| You're eating | You get |
|---|---|
| **1 Meat + 1 Produce** | 🟢 **Balanced Diet** — double health and stamina regen |
| **3 Sweets + 1 Drink** | 🩷 **Sugar Rush** — a big burst of movement speed |
| **2 Fish + 1 Produce** | 🔵 **Fisherman's Friend** — faster swimming for a fraction of the stamina |
| **3 Meat + 1 Drink** | 🟤 **Lumberjack's Feast** — more carry weight, and an axe that bites deeper into trees |

<details>
<summary>📖 <b>Read the saga</b> — countdowns, the half-hour ceiling, and a worked meal</summary>

**Each buff's icon counts down to the moment your diet stops qualifying** — eat more of what is about to run out and the timer pushes back out. Banked copies of a food count in the order they will actually burn, so the countdown shows your real reserve, not one slot's timer.

**One activation lasts half an hour at most.** Between doubled food time and four banked stacks, a buff could otherwise sit on you for well over an hour, so every food-driven effect — the four synergies, the four draughts and Drunk — ends at `Max Effect Duration` even if your diet still qualifies, and comes back only once its recipe stops being met and is met again. Set it to 0 for no ceiling. Getting Fat's perks are permanent and never touched by it.

**A worked meal.** You eat 3 Cooked Meat, a Carrot Soup and a Stamina Mead.

- The meat stacks in one slot as **×3** — 100%, then 30%, then 20% of its stats.
- The soup takes its own slot at full value.
- The mead takes the drink slot, feeds your bars like any food and, being a sweet drink, grants **Reveler's Draught**.
- 1 Meat + 1 Produce is on the board, so **Balanced Diet** switches on, its countdown ticking.
- All five just counted toward Getting Fat — for good.

</details>

---

![IV](https://img.shields.io/badge/IV-MEAD_%26_DRUNKENNESS-8b1e1e?style=for-the-badge&labelColor=1b1b1b)

**Your drink picks your draught. A slot full of Tasty Mead picks a fight.**

| In your drink slot | You get |
|---|---|
| **An eitr drink** | 🟣 **Arcane Draught** — eitr regen, Elemental and Blood Magic |
| **A meat drink** | 🔴 **Warrior's Draught** — Swords and Axes |
| **A sweet drink** | 🟠 **Reveler's Draught** — stamina regen, Run and Jump |
| **A fish drink** | 🩵 **Seafarer's Draught** — Swim and Fishing, faster swimming |
| **A slot full of Tasty Mead** | 🍯 **Drunk** — you hit harder and barely stagger, your guard falls apart, and the world starts to smear |

<details>
<summary>📖 <b>Read the saga</b> — draughts, Drunk, and the blur</summary>

**The draught is chosen by what the drink does**, not guessed from its name: an eitr mead gives Arcane, a stamina mead Reveler's, a swimming mead Seafarer's. Every mead grants something — the resistance and repellent meads included — so your one drink slot is never wasted. Each draught also adds **bonus max health** that grows with the stacks you commit, on the same diminishing ladder as food, and its icon counts down to the moment the slot runs dry.

**Drunk is a trade, not a freebie.** You deal more damage and are far harder to stagger, but your blocking skill craters and blocking burns much more stamina. Skål! Tasty Mead is the one drink you can knock back one after another, because its own effect does nothing — letting you re-drink it costs the game nothing. It takes a drink slot full of mead, four at the default stack limit, and the requirement follows `Max Food Stacks`, so raising the limit never makes Drunk cheaper.

**The room swims.** While you are Drunk the screen takes on heavy motion blur with ghosting trails, fading in as you get drunk and out as you sober up. It works even with Motion Blur switched off in Valheim's graphics settings, and puts your own setting back afterwards. `Drunk Blur Strength` sets how hard it hits; `Drunk Motion Blur` turns it off.

</details>

---

![V](https://img.shields.io/badge/V-GETTING_FAT-c9a227?style=for-the-badge&labelColor=1b1b1b)

**Every meal counts, forever — and a varied diet counts most.**

| Eat a lot of… | Tiers 1 and 3 | Tier 2 |
|---|---|---|
| 🥩 **Meat** | carry weight | Swords and Axes skill |
| 🍬 **Sweet** | base stamina | stamina regen |
| 🔮 **Eitr** | max eitr | eitr regen |
| 🐟 **Fish** | swim speed | cheaper swimming |
| 🥕 **Produce** — vegetables and fruit | health, stamina and eitr regen | *(regen on every tier)* |
| 🍔 **Everything** — your Fatness | base max health | health regen |

<details>
<summary>📖 <b>Read the saga</b> — tiers, Fatness, and why nothing is ever lost</summary>

Fatty counts every food you eat, by category, for the life of your character. Cross a threshold and a small, permanent bonus matched to that food's flavour is yours — not tied to what is in your stomach, and never lost, even if you starve. By default a category's three tiers come at **50, 200 and 500** eaten.

**Each category climbs its own ladder**, so spamming the cheapest berry maxes nothing. The flat rewards are capped so it stays sane, and tier 2 swaps in a different perk before tier 3 returns to the flat one.

**Fatness is everything you have ever eaten**, every category together, food Fatty couldn't identify included. It climbs faster, so its tiers come at **150, 600 and 1,500** — and it is what raises your max health.

**Your progress lives on your character**, so it survives relogs and follows you between worlds. Turning milestones off removes the bonuses but keeps the tally, so switching them back on picks up exactly where you left off. When Fatty's sorting improves and a food moves to a better category, what you have already eaten of it moves too — no progress is stranded. And the Puke button never takes any of it back.

Check it any time in the **Getting Fat** section of The Feast Ledger.

</details>

---

![VI](https://img.shields.io/badge/VI-ANY_FOOD_MOD-2e5e3a?style=for-the-badge&labelColor=1b1b1b)

**Every food in the game sorted — from any mod — with no list to keep up.**

| | |
|---|---|
| 🔎 **Automatic** | every food from the base game or any mod is scanned and given a category; new foods just work |
| 🔮 **Mage food by its stats** | a food is Eitr when its eitr outweighs its health or its stamina — Mushroom Omelette is mage food, whatever its name says |
| 🍲 **Valheim Cuisine** | pre-sorted from its own recipe ingredients, not guessed from names |
| 🫐 **Produce is vegetables and fruit** | raw berries are Produce; a berry *pie* is Sweet |
| 🍺 **Drinks by name** | mead, ale, tea, brew, smoothie, wine… — potions and scrolls are never mistaken for drinks |
| 📁 **Your word is final** | anything filed wrong, you fix in one JSON file |

<details>
<summary>📖 <b>Read the saga</b> — the six categories and the data file</summary>

Every food has a **category** — its diet — and, separately, a flag for whether it **takes the drink slot**. Keeping them apart lets an eitr mead count as mage food for synergies while still being a drink.

| Category | What it is |
|---|---|
| **Meat · Fish · Produce · Sweet** | the classic four |
| **Eitr** | mage food |
| **Unknown** | Fatty couldn't tell. Always food, never a drink, so it can't steal your drink slot. It feeds Fatness but earns no category tier |

The first time it runs, Fatty writes every food's real stats, category and drink flag to `BepInEx/config/Fatty_FoodData.json`. After that it rewrites the file only when it finds **new** foods — after you add a food mod, say — so your hand edits are kept.

</details>

---

![VII](https://img.shields.io/badge/VII-THE_FEAST_RECORDS-1f4e79?style=for-the-badge&labelColor=1b1b1b)

**Who is the biggest glutton on the server? Now a Discord bot can tell you.**

| | |
|---|---|
| 📊 **One file on the server** | everyone's lifetime food counts for a Discord bot or web dashboard — favourite food, most Serpent Stew, biggest glutton |
| 🔒 **Off by default** | off means nothing is collected, nothing is sent, and no file exists |
| 📜 **History counts** | a player's whole Lifetime Feast Log arrives the first time they play there; nobody starts from zero |
| 🚪 **Logging off keeps your place** | records survive relogs and server restarts |
| ⚡ **No restart** | only the server's `Enable Feast Export` matters, and it takes effect at once |

<details>
<summary>📖 <b>Read the saga</b> — what gets sent, and why the server asks</summary>

**For players:** nothing to install and nothing to switch on. This is the only part of Fatty that sends anything anywhere, and what it sends is exactly what your own Lifetime Feast Log shows you — food names and counts, nothing else — to the server you are playing on, every five minutes, and only when that server asks.

**Why the server asks.** Your food history lives in your own character file on your own machine, and a dedicated server has no players of its own to read it from. So the server asks each player for a snapshot and their game answers. The direction matters: the server owns the switch, so off means it never asks, and no player's setting can disagree with it. A player on an older Fatty simply doesn't answer, and shows up with no record rather than breaking anything.

The file format is in the Toolbox below.

</details>

---

![THE TOOLBOX](https://img.shields.io/badge/THE_TOOLBOX-QUICK_START_%C2%B7_KEYS_%C2%B7_HOW--TO-1b1b1b?style=for-the-badge&labelColor=8b1e1e)

**First ten minutes:** install with your mod manager → eat a Cooked Meat and a Carrot Soup and watch **Balanced Diet** appear → eat two more Cooked Meat and see **×3** on the slot → press **F8** and click the stack → drink a mead and meet its draught → open **Getting Fat**.

| Key | | Setting | |
|---|---|---|---|
| **F8** | The Feast Ledger | `Rebuild Food Data` | re-sort every food, once |
| **▲ FEAST** | The Feast Ledger — drag the tab anywhere | `Food Duration Multiplier` | 1.0 for vanilla food timers |
| **Puke**, twice | empty your stomach | `Enable Feast Export` | the server's feast records |
| | | `Enable Debug Mode` | log how each food was sorted |

<details>
<summary>🔧 <b>Fix a food in the wrong category</b></summary>

**Let Fatty re-sort everything:** set `Rebuild Food Data` to true in *2 - Core* and launch once. Fatty backs up `Fatty_FoodData.json` to `Fatty_FoodData.json.bak`, sorts every food from scratch, and switches the setting back off. Hand edits are overwritten — copy them back from the `.bak`.

**Or fix one food by hand:** open `BepInEx/config/Fatty_FoodData.json`, find the food by its prefab name, set its category to `Meat`, `Fish`, `Produce`, `Sweet`, `Eitr` or `Unknown`, and set `IsDrink` to decide whether it takes the drink slot. Fatty respects it from the next launch.

</details>

<details>
<summary>📊 <b>Read the feast records from a bot</b> — for server admins and bot authors</summary>

Turn on `Enable Feast Export` in *11 - Feast Export* on the **server**. Two settings beside it set the pace: `Feast Export Interval Seconds` (how often the file is rewritten — every minute by default) and `Feast Sync Interval Seconds` (how often each player is asked for fresh counts — every five minutes; a player who has just joined is asked straight away).

Two things are written under `BepInEx/config/Fatty/`:

- **`BarrkBOT_feasts.json`** — the whole server in one file. **Read this one.**
- **`feasts/<player>.json`** — one file per player, so a bad write costs one player rather than the server's whole history. Bots should ignore these: the main file has the same fields, and one read beats thirty-five.

**This file is an interface.** A program on another machine reads it and has no way to notice a renamed field, so fields are added, never renamed. `source` is Fatty's name and version.

```json
{
  "generated_at": "2026-08-22T06:12:44.0000000Z",
  "source": "Fatty <version>",
  "players": {
    "Fatty": {
      "name": "Fatty",
      "player_id": 1234567890123,
      "last_updated": "2026-08-22T06:12:40.0000000Z",
      "total_meals": 261,
      "distinct_foods": 3,
      "favourite_food": "CookedMeat",
      "food_counts":     { "CookedMeat": 143, "Sausages": 30, "Cloudberry": 88 },
      "category_counts": { "Meat": 173, "Produce": 88 }
    }
  }
}
```

**What a reader can rely on:**

- **`favourite_food` is `null` on a tie, and `null` when `total_meals` is 0.** Never an arbitrary pick and never a placeholder string — silently choosing one of a tie invents a fact.
- **`food_counts` keys are raw prefab names** (`CookedMeat`, not "Cooked Meat"). Make them readable at your end; two different prettifiers drift apart.
- **`total_meals` and `distinct_foods` are given, not derived.** Don't recompute them — a number both sides calculate is a number that eventually disagrees.
- **A player missing from `players` has never been seen.** That is not the same as having eaten nothing, and a leaderboard that mixes the two tells someone they've never eaten. Offline players are never dropped.
- **Counts are absolute lifetime totals, never deltas.** They self-correct after a dropped message, and they jump on a player's first sync after updating — that's their back history arriving. Don't diff two files for "meals since yesterday".
- **Written atomically** (a temp file, then renamed over the old one), so a reader never catches a half-written file — and **with no byte-order mark**, so any standard JSON parser reads it.
- **`category_counts` uses Fatty's six categories.** It says **`Produce`** (vegetables and fruit), not the old `Vegetable` — don't hardcode the old name.

</details>

---

![MULTIPLAYER](https://img.shields.io/badge/MULTIPLAYER-YOUR_STOMACH_%C2%B7_THE_SERVER%27S_RULES-1f4e79?style=for-the-badge&labelColor=1b1b1b)

| Works with vanilla players and servers | Needs Fatty on the server | Needs Fatty on both ends |
|---|---|---|
| everything you eat — slots, stacking, synergies, draughts, Drunk, Getting Fat, The Feast Ledger, Puke | the server's gameplay settings replace yours, the Puke switch included; `Lock Configuration` keeps them admin-only | the feast records — the server asks, your game answers |

> ⚠️ **Versions must match.** A server running Fatty accepts only the same version. Update the server and its players together. Single player and vanilla servers do not care.

---

![COMPATIBILITY](https://img.shields.io/badge/COMPATIBILITY-EATS_WELL_WITH_OTHERS-2e5e3a?style=for-the-badge&labelColor=1b1b1b)

| Mod | |
|---|---|
| **blacks7ar's FoodDurationMultiplier** | Fatty uses its duration instead of its own — never both multiplied together — and applies its Health, Stamina and Eitr Benefits sliders itself, so they keep working. |
| **Valheim Cuisine** | Recognised: its meats, fish, sweets and drinks are pre-sorted from its own recipe ingredients. |
| **Any other food mod** | Scanned and sorted automatically. Anything filed wrong is one JSON edit away. |
| **VikingOS** | The Feast Ledger wears the same gilt frame as VikingOS's windows. |

---

![SETTINGS](https://img.shields.io/badge/SETTINGS-F1_IN_GAME-b8860b?style=for-the-badge&labelColor=1b1b1b)

All safe to leave alone. **Configuration Manager** (`F1`) in game, or `BepInEx/config/wubarrk.fatty.cfg`.

<details>
<summary>🔒 <b>Server-synced</b> — set on the server, applied to everyone</summary>

| Section | Setting | What it does |
|---|---|---|
| 1 - General | `Lock Configuration` | only admins change synced settings |
| 2 - Core | `No Food Decay` | full stats until the timer ends; off = vanilla's fade |
| 2 - Core | `Max Food Stacks` | copies of one food a slot can hold |
| 2 - Core | `Allow Puke Button` | the Puke button in The Feast Ledger |
| 3 - Stacking | `Stack 2 Multiplier` · `Stack 3 Multiplier` · `Stack 4+ Multiplier` | what each extra copy is worth |
| 4 - Synergies | `Enable Synergies` | the four diet buffs |
| 4 - Synergies | `Max Effect Duration` | the ceiling on one activation, in minutes; 0 = none |
| 4 - Synergies | `Balanced Diet Regen Multiplier` · `Sugar Rush Speed Multiplier` · `Fisherman Swim Stamina Reduction` · `Lumberjack Carry Weight Bonus` · `Lumberjack Woodcutting Damage` | how strong each buff is |
| 6 - Regen | `Scale Regen With Pool` · `Max Regen Scale` | stamina and eitr refill keeps pace with bigger bars, up to a cap |
| 7 - Duration | `Food Duration Multiplier` | how long food lasts; 1.0 = vanilla |
| 8 - Milestones | `Enable Milestones` | Getting Fat's rewards — the tally always runs |
| 8 - Milestones | `Tier 1 Count` · `Tier 2 Count` · `Tier 3 Count` | meals of one category per tier |
| 8 - Milestones | `Fatness Tier 1 Count` · `Fatness Tier 2 Count` · `Fatness Tier 3 Count` · `Fatness Tier 1 Health` · `Fatness Tier 2 Health Regen` · `Fatness Tier 3 Health` · `Fatness Health Cap` | Fatness |
| 8 - Milestones | `Meat Tier 1 Carry Weight` · `Meat Tier 2 Skill Bonus` · `Meat Tier 3 Carry Weight` · `Meat Carry Weight Cap` | Meat |
| 8 - Milestones | `Sweet Tier 1 Stamina` · `Sweet Tier 2 Stamina Regen` · `Sweet Tier 3 Stamina` · `Sweet Stamina Cap` | Sweet |
| 8 - Milestones | `Eitr Tier 1 Eitr` · `Eitr Tier 2 Eitr Regen` · `Eitr Tier 3 Eitr` · `Eitr Eitr Cap` | Eitr |
| 8 - Milestones | `Fish Tier 1 Swim Speed` · `Fish Tier 2 Swim Stamina Reduction` · `Fish Tier 3 Swim Speed` | Fish |
| 8 - Milestones | `Produce Tier 1 Regen Multiplier` · `Produce Tier 2 Regen Multiplier` · `Produce Tier 3 Regen Multiplier` | Produce |
| 9 - Drink Buffs | `Enable Drink Buffs` | the four draughts |
| 9 - Drink Buffs | `Skill Level Bonus` · `Arcane Eitr Regen Multiplier` · `Reveler Stamina Regen Multiplier` · `Max Health Per Drink` | how strong each draught is |
| 9 - Drink Buffs | `Default Drink Duration` | the least time a drink holds the slot |
| 9 - Drink Buffs | `Enable Drunk` · `Drunk Stacks Required` | Drunk, and how many Tasty Meads it takes |
| 9 - Drink Buffs | `Drunk Damage Bonus` · `Drunk Stagger Resistance` · `Drunk Blocking Penalty` · `Drunk Block Stamina Penalty` | Drunk's trade |
| 11 - Feast Export | `Enable Feast Export` | the server's feast records — off by default |
| 11 - Feast Export | `Feast Export Interval Seconds` · `Feast Sync Interval Seconds` | how often the file is written · how often players are asked |

</details>

<details>
<summary>🖥️ <b>Local to your game</b> — yours alone</summary>

| Section | Settings |
|---|---|
| **2 - Core** | `Rebuild Food Data` — runs once, then switches itself off |
| **5 - Debug** | `Enable Debug Mode` |
| **10 - HUD Panels** | `Breakdown Panel Hotkey` · `Feast Ledger Font Size Delta` · `Use Segmented Stack Bar` · `Food Breakdown Panel X` · `Food Breakdown Panel Y` · `Food Breakdown Panel Width` · `Food Breakdown Panel Height` — the last four are set by dragging |
| **12 - Effects** | `Drunk Motion Blur` · `Drunk Blur Strength` |

</details>

---

![CREDITS](https://img.shields.io/badge/CREDITS-THANK_YOU-1b1b1b?style=for-the-badge&labelColor=c9a227)

**Needs:** [BepInExPack Valheim](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/) · [JsonDotNET](https://valheim.thunderstore.io/package/ValheimModding/JsonDotNET/) — your mod manager installs both. ServerSync is built in; there is nothing else to download. **Design input and testing** by Jaga.

> 🔗 Also by me: **VikingOS**, which rebuilds Valheim's interface; **TortalPortal**, a complete overhaul of Valheim's portal network; and **Mists of Avalor**, in open beta on [Hexium](https://valheim.hexium.gg/mods/Wubarrk/Mists_of_Avalor_BETA).

*Made by Wubarrk. Bug reports and abuse welcome on [Discord](https://discord.com/invite/GTTCnGS6Ym).* **License:** MIT — use, change and share it freely, commercially too; just keep the copyright notice. Full text in `LICENSE.md`.
