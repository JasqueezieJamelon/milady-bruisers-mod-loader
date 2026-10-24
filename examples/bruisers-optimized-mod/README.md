# BruisersOptimizedMod

A mod for **Bruisers 2D Boxing**, made for Milady Bruisers Mod Loader.

- **Career screen:** an ATE TODAY / TRAINED TODAY (xN) box. Green = done, red = not yet. Training count is saved with your game.
- **Saving:** the game saves when you return to Career Management after eating or training, so quitting doesn't lose it.
- **Pro opponent fix:** picking an opponent no longer wipes that day's ate/trained status.
- **Meal Optimizer:** on the Meal Plan screen press O (keyboard) or Triangle / Y (controller). Modes (left/right):
  - **NUTRIENTS** - fills the most of what you're missing, then gets closest to your goal weight.
  - **BEST VALUE** - nutrients + closeness to your goal weight, for the least cash.
  - **CHEAPEST** - all about weight: gets as close to your goal weight as possible for the least cash,
    using only plans you could afford every day until fight night (your cash divided by the days left,
    Saturday included). Nutrients only decide between plans with the same weight and price.

  All modes keep you at or under GOAL WEIGHT, which follows the fight's target weight; change it with left/right
  (L1/R1 = 10 lb) or press Select on it to go back to the target. It fills the 4 slots - press ORDER MEAL yourself.

## Install

Copy this `bruisers-optimized-mod` folder into `MiladyLoader/mods/`, close the game and run `MiladyLoader.exe`.

## Changelog

```
v0.3.2
- Now a Milady Bruisers Mod Loader mod (works alongside other loader mods).
- Meal Optimizer: added Cheapest mode - gets as close to your goal weight as possible for the least
  cash you can afford every day until fight night. It doesn't buy extra food just for nutrients.
- Saved values renamed with a bom_ prefix (old saves carry over automatically).

v0.3.1
- Renamed to BruisersOptimizedMod.
- Meal Optimizer: removed Weight mode; both modes now stay at or under goal weight.
- Goal weight shows (TARGET) whenever it matches the fight's target weight.
- Nutrients mode now aims for your goal weight once nutrients are covered.
- Meal Optimizer menu box now fits its text.

v0.3
- Added Meal Optimizer (O / Triangle / Y on the Meal Plan screen) with
  Nutrients / Weight / Best Value modes and a custom goal weight.
- Fixed Ate Today / Trained Today resetting after picking an opponent.

v0.2
- Training count is now saved in your save file.
- Game now saves when you return to Career Management after eating or training.
- Status box made smaller and moved up.
- Added one-click installer and uninstaller.

v0.1
- Added "Ate Today" and "Trained Today (xN)" box to the Career Management screen.
```
