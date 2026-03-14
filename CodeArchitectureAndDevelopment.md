# Code Architecture and Development Plan

**Date:** 14.03.2026 19:04

---

## 🏗️ Development Plan
We partition the program into a set of classes to enable efficiently working in a team of 4-6 developers. Each person is responsible for a subset of the classes. This way, developers can easily work in parallel as dependencies between workstreams are minimized and some level of structure is enforced upon the whole codebase.

### 👥 Assignments
| Developer | Role Color | Primary Focus |
| :--- | :--- | :--- |
| **Lassi** | 🔵 Blue | Smart pathfinding & Systems |
| **Murezi** | 🟢 Green | Map & Infrastructure |
| **Eli** | 🔴 Red | Entities & Combat |
| **Ruchitraa** | 🟡 Yellow | Cards & UI |

---

## 🕹️ Overview
On a high-level, the game consists of a main loop with two steps:
* **An Update step**, where the state of the game is updated using any game logic.
* **A Draw step**, where the game is drawn in its current state.

---

## ⚙️ Update Step
Here are the classes that get called in the update step:

### 🔵 GamePhaseManager [Lassi]
* **"highest-level manager"**
* Start the day by calling `SanityManage`, `MapManager` and `CardManager` at the beginning of the day.
* Start the night by calling `CombatManager` at the beginning of the night.
* Check win/loss conditions.

### 🟢 MapManager [Murezi]
* Tracks the state of the map (holds all the entities and tiles).
    * **list-form** for rendering.
    * **graph-form** for pathfinding.
* Other classes will call the `MapManager` to access the state of the map.
* Gets called to move or place entities.
* Spawn void tiles at the beginning of the day.

### 🟢 Tile [Murezi]
* Static map tile; doesn't have health, doesn't move, etc.

### 🟢 Entity (baseclass/interface only) [Murezi]
* Owned by the `MapManager`, kept in its data structures.
* Baseclass implemented by monsters, defense towers; stores health.
* Stores tile-wise and exact position.
    * Tile-wise position is also implicitly stored by the `MapManager`.
* Stores animation:
    * What it looks like.
    * Which version of an animated sprite (which region of the sprite sheet) should be shown and in which sequence, how frequently should it change.
* Provides `takeDamage()` and `isDestroyed()` methods.

### 🔴 Monster (implements/inherits from Entity) [Eli]
* Provides everything the baseclass `Entity` provides, and more:
* Stores the type of targets it is able to hit.
* Stores the exact target it intends to hit and the path that it is going to follow to reach said target.
* The `Monster` gets the exact target and the path by calling the `PathfindingManager`.
* **Offers the following methods:**
    * `move()`: updates the monster's exact position and, when moving across a tile-boundary, tile-wise position.
        * The exact position refers to the position where the monster is drawn on the map.
        * The monster moves smoothly from one tile to the next, and thus there are many intermediate exact positions when changing tile-wise position.
    * `attack()`: method that imposes damage to the Monster's target if the target is located in range.

### 🔴 Other Entities [Eli]
* **DefenceTower** (implements/inherits from Entity)
* **Sheep**
* **Player**

### 🔵 Sanity Manager [Lassi]
* Gets called at the beginning of a day.
* Calculates sanity-income from meadows.
* Gives a sanity-bonus for surviving the night.
* Gets called for any sanity-related operations:
    * `deduct_sanity()` (like when moving sheep).
* Stores sanity-amount.

### 🟡 CardManager [Ruchitraa]
* Manage the deck.
* Pick the two cards at the start of each round.
* Execute card actions.
* Use `SanityManager` to reduce sanity when buying cards or doing actions that cost sanity.

### 🔴 CombatManager [Eli]
* Spawns the monsters.
* Loops through monsters to call `move()` on each.
* Loops through monsters to call `attack()` on each.
    * (The monsters will take care to only attack if they can).

### 🔵 PathfindingManager [Lassi]
* Monsters call the `PathfindingManager` with the types of targets they are attracted to, and the `PathfindingManager` responds with a target and a path to the target.
* Offers **"smart pathfinding"** by coordinating across monsters/requests in some way.
* A request for finding targets and paths for multiple monsters may be made in one call in the future, otherwise the class can have some internal state for coordinating.

---

## 🖌️ Draw Step
During the Draw step, there are multiple Renderer-classes that get called in the following order:

1.  **🟢 MapRenderer [Murezi]:** draws anything stationary, mainly all tiles (e.g. meadows, void tiles, fences, etc, and any static defence tiles).
2.  **🔴 EntityRenderer [Eli]:** draws all entities (e.g. the player, monsters, sheep, rats) and anything else that moves (attack projectiles, health lost by monsters, dynamic defences, effects etc).
3.  **🟡 UIRenderer [Ruchitraa]:** draws the cards and anything else UI-related, like current sanity, monster's spawn directions, visual aids for tile placement/heatmap of defence strengths.