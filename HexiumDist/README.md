<div align="center">
  
# 🍖 FATTY: The Valheim Culinary Overhaul 🍻

*Stop surviving. Start feasting.* 

Fatty is a massive overhaul to Valheim's food system: it stops food stats from decaying, grows your food bar from 3 slots to 5 (a new **4th food slot** *and* a dedicated **5th drink slot**), lets you stack the same food for bonus stats, rewards a varied diet with powerful synergy buffs, gives meads and drinks their own buffs, and permanently makes you tougher the more you eat. Every buff comes with its own custom icon.

---
</div>

## 🆕 New in 1.1.0

| | What's new |
| :--- | :--- |
| 🍔 **Getting Fat Is a Reward** | Every food you eat is counted **per category** for the life of your character. Pass a threshold and you permanently gain **base max health**. A varied diet pays; spamming one cheap berry doesn't. |
| 🍻 **Mead & Drink Buffs** | Whatever's in your drink slot now grants a buff for as long as it lasts — **Arcane**, **Warrior's**, **Reveler's** or **Seafarer's Draught**, chosen by what the drink actually does. Each also grants bonus max health that scales with your stacks. |
| 🍯 **Drunk** | Drink three Tasty Meads and you get Drunk — hit harder, stagger less, but your guard falls apart. Skal! |
| ⏱️ **Built-in Food Timer Extension** | Fatty now has its **own** food duration multiplier. No extra mod needed. |
| 🔮 **A Real Eitr Category** | Mage food is finally recognised as mage food. Eitr foods are identified by their **stats**, not their name — so "Mushroom Omelette" is no longer filed as a vegetable. |
| 🏷️ **Smarter Categories** | "Drink" is now a *property*, not a category — an eitr mead counts for mage synergies **and** takes the drink slot. Unidentified food goes to a real `Unknown` category instead of being silently called a vegetable. |
| 🔍 **The Feast Ledger** | A standalone panel with a full breakdown of every stacked item's own timer and stats, plus your **Getting Fat** progress and a **Lifetime Feast Log** of everything you've ever eaten. Click its small gold **▲ FEAST** tab, or press **F8** (configurable), to open it. It drags and resizes freely, and remembers where you put it. |
| ⏳ **Buffs Show a Real Countdown** | Every synergy and drink draught now shows exactly how long it has left — calculated from your actual stomach, not a fake timer. |

**Also fixed:** Lumberjack's Feast was *reducing* your woodcutting damage by 55% instead of increasing it. It now works as advertised.

> ⚠️ **Upgrading from 1.0.x?** Your food data file gains the new Eitr category and drink flag, so Fatty runs a **one-time migration** on first launch and re-derives every category. Your old file is backed up to **`Fatty_FoodData.json.bak`** — restore any hand-edits from there afterwards.

---

## 🍗 Guide to Being a Fatty

Fatty changes a lot at once. If you've opened your food bar, seen five slots doing five different things, and gone *"...huh?"* — this is for you. Everything below happens **automatically**; there's nothing to activate. You just eat.

### The 60-second version
- **Eat food. It doesn't decay.** Whatever a food gives you, it keeps giving until its timer hits zero, then it's gone — no gradual fade like vanilla.
- **You have 5 slots: 4 for solid food, 1 just for drinks.** A drink can never get pushed out by food, and food can never steal the drink slot.
- **Eat the same food again to stack it into the same slot**, for more stats out of that one slot (up to 5 copies by default). Different foods take different slots.
- **Eating a good spread of food triggers free buffs** (below) and slowly, permanently makes you tougher (also below) — no crafting, no activation, just eating what's in front of you.

### Stacking, and why a slot's timer might surprise you
Stack the same food and each extra copy is worth **less** than the last — so stacking is strong, but spamming one food isn't a full replacement for a varied diet:

| Copy | Value |
| :--- | :--- |
| 1st | 100% |
| 2nd | 50% |
| 3rd | 25% |
| 4th+ | 5% |

The **"×N" badge** on a slot tells you how many copies are stacked there.

Here's the part that trips people up: **every item in a stack counts down at the same time**, not one after another. So a slot showing "×7, 75m" doesn't mean *"75 minutes, then 6 more chunks after that"* — it means **the whole slot is gone in 75 minutes**, when the longest-lasting item in it runs out. The thin bar under the icon shows you each individual item and dims them as they run low, so you can see the stack thinning out in real time even though the slot number itself only tracks the survivor.

If you want the full picture — every item's own time left, its stack position, and exactly what it's contributing — click the gold **▲ FEAST** tab (or press **F8**) to open The Feast Ledger.

### Reading your food bar at a glance

| What you see | What it means |
| :--- | :--- |
| Icon | The food (or drink) occupying that slot |
| **×N** badge (top-right) | How many times you've stacked that food |
| Number under the icon | When this slot **actually empties** (its longest-lasting item) |
| Segmented bar under the icon | One segment per stacked item, dimming as each runs down |

The gold **▲ FEAST** tab (drag it anywhere; press **F8** to toggle it too) opens The Feast Ledger — see below.

### The buffs that just... happen
Eat the right mix and a **Status Effect** appears with no input from you — keep eating that mix and it stays up; let it lapse and it fades. Its icon shows a live countdown to the moment your diet stops qualifying, so you know exactly how long you have to top it back up.

| You're eating | You get |
| :--- | :--- |
| 1 Meat + 1 Veggie | 🟢 Balanced Diet — big Health & Stamina regen |
| 3 Sweets + 1 Drink | 🩷 Sugar Rush — big movement speed |
| 2 Fish + 1 Veggie | 🔵 Fisherman's Friend — faster, cheaper swimming |
| 3 Meat + 1 Drink | 🟤 Lumberjack's Feast — more carry weight & woodcutting damage |

Whatever's in your **drink slot** grants its own buff too, chosen by what that drink actually does (an eitr mead → Arcane Draught, a stamina mead → Reveler's, and so on) — see the [Mead & Drink Buffs](#-mead--drink-buffs) table below for the full list. You don't pick a draught; the drink you're holding picks it for you.

Drink **three Tasty Meads** and you get **Drunk** — harder hitting, harder to stagger, but a much weaker guard. It's the one drink you can chug back-to-back, specifically so this is reachable.

### The buff that never goes away
Separately from all of the above, Fatty is quietly counting **every food you've ever eaten, by category**, for the life of your character. Cross a threshold in a category and you get a small slice of **permanent** max health — this has nothing to do with your current stomach and is never lost, even if you starve. It's deliberately per-category, so it rewards actually eating a varied diet over a long playthrough rather than spamming one cheap food. Check your progress any time in the **Getting Fat** section of the ▲ FEAST panel.

### A worked example
You eat 3 Cooked Meat, 1 Carrot Soup, and a Stamina Mead:
- **Meat slot:** shows "×3" — 1st Meat at 100%, 2nd at 50%, 3rd at 25% of its stats, stacked together.
- **Veggie slot:** Carrot Soup on its own, full value.
- **Drink slot:** the Stamina Mead — health/stamina from it feed your bars like any food, *and* because it's a Sweet-category drink it also grants the **Reveler's Draught** for as long as it holds the slot.
- You now have **1 Meat + 1 Veggie** on the board → **Balanced Diet** kicks in automatically, with its own countdown ticking on the icon.
- Every one of those four foods also just counted toward its category's lifetime "Getting Fat" tally — permanently, whether or not you ever eat that exact meal again.

That's the whole loop: eat → stack → qualify for buffs → get a little more permanent, forever, in the background.

---

## ✨ Core Features

### 🛑 No Food Decay
Hate watching your health and stamina slowly tick down to nothing? **Fatty** disables vanilla food decay. When you eat, you keep **100% of a food's stats until its timer fully expires** — no gradual fade. *(Configurable — you can turn decay back on and keep the rest of the mod.)*

### 🥞 Stat Stacking
If you really love Lox Meat, why stop at one? Fatty lets you eat the **same food multiple times** to pile on stats.
- By default you can stack a food up to **5 times**.
- To keep it balanced, extra stacks give **diminishing returns** (configurable).
- *Default:* 1st serving = 100%, 2nd = 50%, 3rd = 25%, 4th and beyond = 5%.
- The HUD shows an **"×N" badge** so you always know how many times a food is stacked.

### 🔍 The Feast Ledger
> 🆕 **New in 1.1.0**

A stacked slot's timer shows exactly when that slot will **actually empty**, and a thin segmented bar under each icon shows every individual item in the stack, dimming as it runs down.

Click the small gold **▲ FEAST** tab, or press **F8** (rebindable in the config), to open The Feast Ledger: every stacked item's own remaining time, its position in the diminishing-returns ladder, and exactly how much HP/Stamina/Eitr it's contributing right now. A stack of the same food (e.g. "Tasty Mead ×7") collapses behind one line — click it to expand the individual items instead of repeating the food's name over and over. Inside the same panel, two more sections expand on click:
- **Getting Fat** — your per-category progress toward permanent health bonuses.
- **Lifetime Feast Log** — every distinct food you've ever eaten on this character, and how many times, independent of what's currently in your stomach.

The panel is **draggable** (grab the title bar) and **resizable** (drag the corner grip), and remembers where you left it, even across a full game restart. It's a transparent-black, gold-on-black panel in the same style as the rest of Fatty's HUD.

### 🍺 A 4th Food Slot + a 5th Drink Slot
Vanilla only gives you **3** food slots. Fatty expands the food bar to **5**: a brand-new **4th solid-food slot** *plus* a dedicated **5th slot just for drinks** — so you can run 4 foods and a drink at once. Icons get a **gold trim** for a cleaner look.

> Note: the drink slot covers **every** drink — including vanilla/modded meads that just grant a timed status effect with no food stats (Frost Resistance, Tasty Mead, etc.), not only drinks that carry food stats. A mead holds the slot for its own effect's duration or 10 minutes, whichever is longer, so a health mead's draught doesn't vanish a few seconds after you drink it.
>
> Drinking is never **blocked**. If your drink slot is already busy, the mead still works exactly as it does in vanilla — it just doesn't get a Fatty drink buff. You can never be locked out of a health mead mid-fight.
>
> Fatty also never lets you drink *more often* than vanilla does. The game's cooldown between meads is untouched — that's what stops potion spam, and Fatty has no business removing it. Tasty Mead is the single exception, because its effect does nothing at all; see Drunk below.

### ⚡ Regen That Keeps Up With Your Bars
Valheim regenerates stamina and eitr at a **flat rate** that completely ignores how big your bars are — so any mod that enlarges your pools secretly makes them refill *slower*. Fatty scales regeneration to match the pools it gives you, keeping refill times at vanilla parity no matter how much you stack. *(Configurable, and it will never make regen worse than vanilla.)*

### 🍔 Getting Fat Is a Reward
> 🆕 **New in 1.1.0**

Fatty counts every food you eat, **per category**, for the lifetime of your character. Pass a threshold and you permanently gain **base max health** — small awards, well spaced out, and capped so it stays sane.

- Because counts are per category, a **varied diet** is what pays off — you can't just spam the cheapest berry.
- Each category runs its **own independent ladder** — Meat, Vegetable, Sweet, Fish and Eitr each grant their own +5 / +5 / +10 as you pass their thresholds, all at once if you've genuinely eaten a bit of everything. The **+50 overall cap** only limits the sum, not any single category.
- Progress lives on your character, so it survives relogs and follows you between worlds.
- *Default:* 50 / 200 / 500 of a category grants +5 / +5 / +10 health, capped at +50 overall.

### 🥗 Dietary Synergies
Eating the right combination of food categories triggers a unique, powerful **Status Effect** — complete with its own **custom icon** — applied and removed automatically as your diet changes:

| Synergy | Recipe | Effect |
| :--- | :--- | :--- |
| **🟢 Balanced Diet** | 1 Meat + 1 Veggie | Greatly increased Health & Stamina regen |
| **🩷 Sugar Rush** | 3 Sweets + 1 Drink | Big movement-speed boost |
| **🔵 Fisherman's Friend** | 2 Fish + 1 Veggie | Improved swim speed & reduced swim stamina use |
| **🟤 Lumberjack's Feast** | 3 Meat + 1 Drink | Bonus carry weight & woodcutting damage |

Its status icon shows a **live countdown** to when your diet will actually stop qualifying — eat more of what's about to run out and the timer pushes back out.

### 🍻 Mead & Drink Buffs
> 🆕 **New in 1.1.0**

Vikings love their mead. Whatever sits in your **drink slot** now grants a buff for exactly as long as that drink lasts, chosen by **what the drink actually does** — an eitr mead gives the Arcane Draught, a stamina mead the Reveler's, a swimming mead the Seafarer's — rather than by guessing from its name:

| Draught | Drink type | Effect |
| :--- | :--- | :--- |
| **🟣 Arcane Draught** | Eitr drinks | Improved eitr regen + Elemental & Blood Magic skill |
| **🔴 Warrior's Draught** | Meat drinks | Improved Swords & Axes skill |
| **🟠 Reveler's Draught** | Sweet drinks | Improved stamina regen + Run & Jump skill |
| **🩵 Seafarer's Draught** | Fish drinks | Improved Swim & Fishing skill, faster swimming |

Every drink buff **also grants bonus max health** that scales with how many stacks of that drink you commit — on the same diminishing-returns ladder as food.

Every mead in the game grants *something*, including the resistance and repellent meads, so committing your one drink slot is never a wasted choice. Its status icon counts down to the moment your drink slot actually runs dry.

### 🍯 Drunk
> 🆕 **New in 1.1.0**

Drink **three Tasty Meads** (configurable) and you get **Drunk**. It's a real trade, not a freebie: you hit harder and are much harder to stagger, but your blocking skill craters and blocking burns far more stamina. Skal!

Tasty Mead is the one drink you're allowed to knock back one after another, because unlike every other mead its effect does nothing — so letting you re-drink it costs the game nothing. Health, stamina and every other mead still respect the vanilla cooldown exactly as before.

*Every synergy, draught and Drunk value is configurable and server-synced.*

---

## 🛠️ Configuration & Data-Driven Modding

### ⚙️ ServerSync
Fatty is fully ServerSync integrated. Server admins have authority over **all gameplay values** — no-decay, max stacks, diminishing returns, regen scaling, food duration, milestones, and every synergy and drink buff. *(Only the personal debug-logging toggle stays local to each client.)*

Config sections: **2 - Core**, **3 - Stacking**, **4 - Synergies**, **6 - Regen**, **7 - Duration**, **8 - Milestones**, **9 - Drink Buffs**.

Every numeric setting is **range-checked**, so a mistyped value — or one pushed by a server — is clamped instead of corrupting your health bar. Each description states its unit (multiplier vs. flat vs. fraction), what vanilla is, and what the default works out to.

### 🔧 Something in the wrong category?
Set **`Rebuild Food Data = true`** in *2 - Core* and launch once. Fatty backs up your `Fatty_FoodData.json` to `.bak`, re-sorts every food from scratch, and turns the switch back off. Use this whenever a food is filed wrong — no need to delete files by hand.

### ⏱️ Food Duration
> 🆕 **New in 1.1.0**

Fatty has its own **Food Duration Multiplier** (section *7 - Duration*) — set it above 1.0 to make food last longer. No extra mod required.

If you *do* have **blacks7ar's FoodDurationMultiplier** installed, Fatty detects it and **defers to that mod's value entirely** rather than multiplying the two together — so running both can never silently double up on you. Fatty also reads that mod's **Health / Stamina / Eitr Benefits** sliders and applies them itself, so your settings there keep working.

### 🤝 Food-Mod Compatibility
Fatty is built to play nicely with **any** food mod:
- **Automatic, open support:** Fatty scans every food in the game — from the base game or *any* mod — and sorts each one into a category. New foods just work.
- **Eitr food is identified by its stats, not its name.** "Mushroom Omelette" is mage food, not a vegetable, and Fatty files it accordingly.
- **Valheim Cuisine:** specifically recognized. Fatty pre-sorts its meats, fish, sweets and drinks out of the box using Valheim Cuisine's own recipe ingredients as ground truth — not guessing from the name.
- **Full manual control:** anything the auto-sorter gets wrong, you fix in one JSON file (below).

### 🏷️ Food Categories
> 🆕 **Reworked in 1.1.0**

Every food has a **category** (its diet) and, separately, a flag for whether it **takes the drink slot**. Keeping those apart means an eitr mead can count as mage food for synergies *while still* being a drink.

| Category | What it is |
| :--- | :--- |
| **Meat** / **Fish** / **Vegetable** / **Sweet** | The classic four |
| **Eitr** | 🆕 Mage food — anything whose eitr outweighs its health or its stamina |
| **Unknown** | 🆕 Fatty couldn't identify it. Always treated as **food, never a drink**, so it can never steal your drink slot |

The drink flag is set from the item's **name** (mead, ale, tea, brew, smoothie, wine…), so potions, scrolls and boss-power items are never mistaken for drinks. Anything the name test misses is one `IsDrink` edit away in the JSON below.

### 📁 The JSON Data File
The **first** time you run Fatty, it writes every food's real stats, category and drink flag to `BepInEx/config/Fatty_FoodData.json`. After that it **only rewrites the file when it discovers new foods** (for example, after you add another food mod) — so a normal launch never touches it and **your manual edits are preserved**.

**Want to change a food's category?** Edit that JSON file — set any food to `Meat`, `Vegetable`, `Sweet`, `Fish`, `Eitr` or `Unknown`, and set `IsDrink` to control whether it uses the drink slot. Fatty will respect it.

---

## 🧩 Dependencies
Fatty needs just two things at runtime — **Gale** installs them automatically:

| Dependency | Why |
| :--- | :--- |
| **[BepInExPack Valheim](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/)** | The mod loader (also provides HarmonyX). |
| **[JsonDotNET](https://valheim.thunderstore.io/package/ValheimModding/JsonDotNET/)** | Newtonsoft.Json, used to read/write `Fatty_FoodData.json`. |

*(ServerSync is compiled into Fatty and needs no separate download.)*

## 📥 Installation
**With Gale (recommended):** just install Fatty — the dependencies above are pulled in for you.

**Manual install:**
1. Install **BepInExPack Valheim** and **JsonDotNET**.
2. Drop `Fatty.dll` into `BepInEx/plugins`.
3. Eat up! 

<div align="center">
  <i>Created with ❤️ by Wubarrk</i>
</div>
