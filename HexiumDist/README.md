<div align="center">
  
# 🍖 FATTY: The Valheim Culinary Overhaul 🍻

![Valheim Mod](https://img.shields.io/badge/Valheim-Food_Overhaul-orange.svg)
[![Multiplayer Compatible](https://img.shields.io/badge/Multiplayer-Server--Synced-blue.svg)]()
[![Framework](https://img.shields.io/badge/Requires-BepInEx-red.svg)]()
[![Version](https://img.shields.io/badge/Version-1.1.5-lightgrey.svg)]()

*Stop surviving. Start feasting.* 

Fatty is a massive overhaul to Valheim's food system: it stops food stats from decaying, grows your food bar from 3 slots to 5 (a new **4th food slot** *and* a dedicated **5th drink slot**), lets you stack the same food for bonus stats, rewards a varied diet with powerful synergy buffs, gives meads and drinks their own buffs, and permanently makes you tougher the more you eat. Every buff comes with its own custom icon.

---
</div>

## ⚠️ Upgrading

> **From 1.1.x?** Getting Fat's rewards changed shape: the five diet categories no longer grant health directly, they each grant their own stat instead, and health now comes from the new Fatness tally (total food eaten). Your existing per-category counts carry over untouched and immediately start feeding the new stats — you don't lose progress, but your current health bonus will move to whichever stats your diet has actually been feeding.
>
> **From 1.0.x?** Your food data file gains the new Eitr category and drink flag, so Fatty runs a **one-time migration** on first launch and re-derives every category. Your old file is backed up to **`Fatty_FoodData.json.bak`** — restore any hand-edits from there afterwards.

---

## 🍗 Guide to Being a Fatty

Fatty changes a lot at once. If you've opened your food bar, seen five slots doing five different things, and gone *"...huh?"* — this is for you. Everything below happens **automatically**; there's nothing to activate. You just eat.

### The 60-second version
- **Eat food. It doesn't decay.** Whatever a food gives you, it keeps giving until its timer hits zero, then it's gone — no gradual fade like vanilla.
- **You have 5 slots: 4 for solid food, 1 just for drinks.** A drink can never get pushed out by food, and food can never steal the drink slot.
- **Eat the same food again to stack it into the same slot**, for more stats out of that one slot (up to 4 copies by default). Different foods take different slots.
- **Eating a good spread of food triggers free buffs** (below) and slowly, permanently makes you tougher (also below) — no crafting, no activation, just eating what's in front of you.

### Stacking, and why a slot's timer might surprise you
Stack the same food and each extra copy is worth **less** than the last — so stacking is strong, but spamming one food isn't a full replacement for a varied diet:

| Copy | Value |
| :--- | :--- |
| 1st | 100% |
| 2nd | 30% |
| 3rd | 20% |
| 4th+ | 10% |

The **"×N" badge** on a slot tells you how many copies are stacked there.

**Only the most-recently-eaten item in a stack actually counts down.** Every older copy in that same slot is held in reserve, frozen, until the active one runs out — then the next-most-recent copy picks up automatically and starts its own countdown. So a slot showing "×4, 75m" means *the item you just ate* empties in 75 minutes, and the three behind it are banked and waiting their turn, not also draining in the background. The thin bar under the icon shows you each individual item and dims them one at a time as each takes its turn, so you can watch the stack empty in eaten order.

If you want the full picture — every item's own time left, its stack position, and exactly what it's contributing — click the gold **▲ FEAST** tab (or press **F8**) to open The Feast Ledger.

### Reading your food bar at a glance

| What you see | What it means |
| :--- | :--- |
| Icon | The food (or drink) occupying that slot |
| **×N** badge (top-right) | How many times you've stacked that food |
| Number under the icon | When the **active item** (the one you most recently ate) runs out — the next copy in the stack then takes over automatically |
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

**Fill your drink slot with Tasty Mead** and you get **Drunk** — harder hitting, harder to stagger, but a much weaker guard. It's the one drink you can chug back-to-back, specifically so this is reachable.

### The buffs that never go away
Separately from all of the above, Fatty is quietly counting **every food you've ever eaten, by category**, for the life of your character. Cross a threshold in a category and you get a small slice of a **permanent** stat matched to that category's flavour — Meat → carry weight, Sweet → stamina, Eitr → max eitr, Fish → swim speed, Vegetable → all-round regen — and none of it has anything to do with your current stomach; it's never lost, even if you starve. Meat/Sweet/Eitr/Fish don't repeat the same reward three times either: the *2nd* tier swaps in a different permanent perk (Sword & Axe skill, stamina regen, eitr regen, or reduced swim stamina cost) before the 3rd tier goes back to the flat bonus. On top of all that, your **total** food eaten across every category (a separate "Fatness" tally, which alternates the same way — max health, then health regen, then max health again) permanently raises your max health. It's deliberately per-category, so it rewards actually eating a varied diet over a long playthrough rather than spamming one cheap food. Check your progress any time in the **Getting Fat** section of the ▲ FEAST panel.

### A worked example
You eat 3 Cooked Meat, 1 Carrot Soup, and a Stamina Mead:
- **Meat slot:** shows "×3" — 1st Meat at 100%, 2nd at 30%, 3rd at 20% of its stats, stacked together.
- **Veggie slot:** Carrot Soup on its own, full value.
- **Drink slot:** the Stamina Mead — health/stamina from it feed your bars like any food, *and* because it's a Sweet-category drink it also grants the **Reveler's Draught** for as long as it holds the slot.
- You now have **1 Meat + 1 Veggie** on the board → **Balanced Diet** kicks in automatically, with its own countdown ticking on the icon.
- Every one of those four foods also just counted toward its category's lifetime "Getting Fat" tally (and the overall Fatness tally) — permanently, whether or not you ever eat that exact meal again.

That's the whole loop: eat → stack → qualify for buffs → get a little more permanent, forever, in the background.

---

## ✨ Core Features

### 🛑 No Food Decay
Hate watching your health and stamina slowly tick down to nothing? **Fatty** disables vanilla food decay. When you eat, you keep **100% of a food's stats until its timer fully expires** — no gradual fade. *(Configurable — you can turn decay back on and keep the rest of the mod.)*

### 🥞 Stat Stacking
If you really love Lox Meat, why stop at one? Fatty lets you eat the **same food multiple times** to pile on stats.
- By default you can stack a food up to **4 times**.
- To keep it balanced, extra stacks give **diminishing returns** (configurable).
- *Default:* 1st serving = 100%, 2nd = 30%, 3rd = 20%, 4th and beyond = 10%.
- The HUD shows an **"×N" badge** so you always know how many times a food is stacked.

### 🔍 The Feast Ledger

A stacked slot's timer shows exactly when the **active item** empties (the most recently eaten copy - older copies are held in reserve and take over one at a time), and a thin segmented bar under each icon shows every individual item in the stack, dimming one at a time as each takes its turn.

Click the small gold **▲ FEAST** tab, or press **F8** (rebindable in the config), to open The Feast Ledger: every stacked item's own remaining time, its position in the diminishing-returns ladder, its actual icon, and exactly how much HP/Stamina/Eitr it's contributing right now. A stack of the same food (e.g. "Tasty Mead ×4") collapses behind one line — click it to expand the individual items instead of repeating the food's name over and over. Inside the same panel, two more sections expand on click:
- **Getting Fat** — your progress toward all six permanent milestone perks.
- **Lifetime Feast Log** — every distinct food you've ever eaten on this character, and how many times, independent of what's currently in your stomach.

The panel is **draggable** (grab the title bar), **resizable** (drag the corner grip), and framed in a full gilt picture frame — a polished double-rail gold border with mitred acanthus-flourish corners and a crest top and bottom, over a near-black leather panel. Text runs a couple of points larger than a typical Fatty panel by default; adjust or disable that in "10 - HUD Panels" → Feast Ledger Font Size Delta. It remembers where you left it, even across a full game restart.

### 🍺 A 4th Food Slot + a 5th Drink Slot
Vanilla only gives you **3** food slots. Fatty expands the food bar to **5**: a brand-new **4th solid-food slot** *plus* a dedicated **5th slot just for drinks** — so you can run 4 foods and a drink at once. Icons get a **gold trim** for a cleaner look.

> Note: the drink slot covers **every** drink — including vanilla/modded meads that just grant a timed status effect with no food stats (Frost Resistance, Tasty Mead, etc.), not only drinks that carry food stats. A mead holds the slot for its own effect's duration or 20 minutes, whichever is longer, so a health mead's draught doesn't vanish a few seconds after you drink it. (That floor is 10 minutes scaled by the Food Duration Multiplier, which now ships at 2.0 — see below.)
>
> Drinking is never **blocked**. If your drink slot is already busy, the mead still works exactly as it does in vanilla — it just doesn't get a Fatty drink buff. You can never be locked out of a health mead mid-fight.
>
> Fatty also never lets you drink *more often* than vanilla does. The game's cooldown between meads is untouched — that's what stops potion spam, and Fatty has no business removing it. Tasty Mead is the single exception, because its effect does nothing at all; see Drunk below.

### ⚡ Regen That Keeps Up With Your Bars
Valheim regenerates stamina and eitr at a **flat rate** that completely ignores how big your bars are — so any mod that enlarges your pools secretly makes them refill *slower*. Fatty scales regeneration to match the pools it gives you, keeping refill times at vanilla parity no matter how much you stack. *(Configurable, and it will never make regen worse than vanilla.)*

### 🍔 Getting Fat Is a Reward

Fatty counts every food you eat, **per category**, for the lifetime of your character. Pass a threshold and you permanently gain a stat matched to that category's flavour — small awards, well spaced out, and capped so it stays sane. Meat, Sweet, Eitr, Fish and Fatness don't just grant more of the same thing three times over, either: **tier 2 swaps in a different permanent perk** before tier 3 goes back to the flat reward.

| Category | Tier 1 & 3 | Tier 2 (the alternate) |
| :--- | :--- | :--- |
| 🥩 **Meat** | Carry weight | Sword & Axe skill |
| 🍬 **Sweet** | Base stamina | Stamina regen |
| 🔮 **Eitr** | Max eitr | Eitr regen |
| 🐟 **Fish** | Swim speed | Reduced swim stamina cost |
| 🥕 **Vegetable** | Health, stamina & eitr regen, all 3 tiers | *(no alternate - it's the regen category already)* |
| 🍔 **Fatness** (total eaten, every category) | Base max health | Health regen |

- Because the five diet counts are per category, a **varied diet** is what pays off — you can't just spam the cheapest berry to max out everything.
- Each of those five runs its **own independent ladder**, and its own cap for the flat tiers. **Fatness** is separate again: it's your *total* food eaten across every category (including food Fatty couldn't identify), so it climbs faster and needs bigger thresholds — that's what grants max health now, not any single category.
- Progress lives on your character, so it survives relogs and follows you between worlds.
- *Default:* 50 / 200 / 500 of a single category earns its 3 tiers; 150 / 600 / 1500 total food eaten earns Fatness' 3 tiers. Every threshold, award and cap — including each tier-2 alternate's own amount — is configurable in "8 - Milestones".

### 🥗 Dietary Synergies
Eating the right combination of food categories triggers a unique, powerful **Status Effect** — complete with its own **custom icon** — applied and removed automatically as your diet changes:

| Synergy | Recipe | Effect |
| :--- | :--- | :--- |
| **🟢 Balanced Diet** | 1 Meat + 1 Veggie | Greatly increased Health & Stamina regen |
| **🩷 Sugar Rush** | 3 Sweets + 1 Drink | Big movement-speed boost |
| **🔵 Fisherman's Friend** | 2 Fish + 1 Veggie | Improved swim speed & reduced swim stamina use |
| **🟤 Lumberjack's Feast** | 3 Meat + 1 Drink | Bonus carry weight & woodcutting damage |

Its status icon shows a **live countdown** to when your diet will actually stop qualifying — eat more of what's about to run out and the timer pushes back out. Banked stacks of the same food are counted **in sequence**, matching how they actually burn down, so the countdown reflects your real reserve rather than a single slot's timer.

**⏳ Max Effect Duration.** Between the 2× food duration and four banked stacks, one activation could otherwise sit on you for well over an hour, so every food-driven Fatty effect — the four synergies, the four draughts and Drunk — is capped at **30 minutes by default** (*4 - Synergies*). When an effect hits the ceiling it ends even if your diet still qualifies, and it won't return until its recipe stops being met and is met again. Set it to **0** for no ceiling. Milestone perks are permanent progression and are never affected.

### 🍻 Mead & Drink Buffs

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

**Fill your drink slot with Tasty Mead** (configurable) and you get **Drunk**. It's a real trade, not a freebie: you hit harder and are much harder to stagger, but your blocking skill craters and blocking burns far more stamina. Skal!

Tasty Mead is the one drink you're allowed to knock back one after another, because unlike every other mead its effect does nothing — so letting you re-drink it costs the game nothing. Health, stamina and every other mead still respect the vanilla cooldown exactly as before.

The requirement is capped by your **Max Food Stacks**, and ships set deliberately above it — so Drunk always means "a drink slot full of mead" rather than a fixed number that quietly gets easier the moment you raise your stack limit. At the defaults that's **4 Tasty Meads**.

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

Fatty has its own **Food Duration Multiplier** (section *7 - Duration*) — it ships at **2.0**, so food lasts twice as long as vanilla out of the box. Set it to 1.0 for vanilla timings, or higher for longer still. No extra mod required.

If you *do* have **blacks7ar's FoodDurationMultiplier** installed, Fatty detects it and **defers to that mod's value entirely** rather than multiplying the two together — so running both can never silently double up on you. Fatty also reads that mod's **Health / Stamina / Eitr Benefits** sliders and applies them itself, so your settings there keep working.

### 🤝 Food-Mod Compatibility
Fatty is built to play nicely with **any** food mod:
- **Automatic, open support:** Fatty scans every food in the game — from the base game or *any* mod — and sorts each one into a category. New foods just work.
- **Eitr food is identified by its stats, not its name.** "Mushroom Omelette" is mage food, not a vegetable, and Fatty files it accordingly.
- **Valheim Cuisine:** specifically recognized. Fatty pre-sorts its meats, fish, sweets and drinks out of the box using Valheim Cuisine's own recipe ingredients as ground truth — not guessing from the name.
- **Full manual control:** anything the auto-sorter gets wrong, you fix in one JSON file (below).

### 🏷️ Food Categories

Every food has a **category** (its diet) and, separately, a flag for whether it **takes the drink slot**. Keeping those apart means an eitr mead can count as mage food for synergies *while still* being a drink.

| Category | What it is |
| :--- | :--- |
| **Meat** / **Fish** / **Vegetable** / **Sweet** | The classic four |
| **Eitr** | Mage food — anything whose eitr outweighs its health or its stamina |
| **Unknown** | Fatty couldn't identify it. Always treated as **food, never a drink**, so it can never steal your drink slot |

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
  <i>Created with ❤️ by Wubarrk with design input and testing by Jaga</i>
</div>

---
join the Mists of Avalor Open BETA find it only on [Hexium ![Hexium Logo](data:image/png;base64,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)](https://valheim.hexium.gg/mods/Wubarrk/Mists_of_Avalor_BETA)
