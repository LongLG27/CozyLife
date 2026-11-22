# CozyLife — 2D Multiplayer Life Simulation & Social Town Game

**CozyLife** is a 2D life-simulation and multiplayer social town game built with **Unity 6 (`6000.6.0f1`)** in C#. Players can customize their character, choose a profession, explore an interactive Japanese-inspired cozy town, and participate in multiplayer minigames and social activities over LAN or Cloud Relay.

---

## Key Features & Technical Highlights

### 1. Custom Networking Architecture (LAN & Cloud Relay)
- **Authoritative Host & Client System** (`CozyNetworkServer`, `CozyNetworkClient`, `CozyNetworkManager`): Built custom TCP/UDP state synchronization for player movement, appearance, chat bubbles, emotes, and interactive world states.
- **Cloud Relay Support** (`CozyCloudRelay`): Room code generation and matchmaking support so players can connect across networks without manual port forwarding.
- **Remote Player Interpolation** (`RemoteNetworkPlayer`): Smooth position interpolation and visual state syncing for remote players.

### 2. World Systems & Interactive Minigames
- **Board & Strategy Minigames**:
  - **Chess (`CozyChessSystem`)** & **Xiangqi / Chinese Chess (`CozyXiangqiSystem`)**: Full rule validation, turn synchronization, and interactive board UI for 2-player matches.
  - **Campfire Werewolf (`CozyCampfireWerewolfSystem`)**: Social deduction minigame integrated directly into the town's campfire gathering spot.
- **Sports & Action Minigames (`CozySportsMinigames`)**: Interactive town sports activities and mini-challenges.
- **Fishing & Economy (`CozyFishingSystem`, `ProfessionAndInventory`)**: Resource gathering, inventory management, and profession progression.
- **Cozy Town Venues**:
  - **Japanese Cafe (`CozyJapaneseCafeSystem`)** & **Street Food Carts (`TownStreetFoodCartInteractable`)**
  - **Onsen Hot Spring (`CozyOnsenHotSpringSystem`)** with stamina/stat regeneration
  - **Town Bulletin Board (`CozyTownBulletinBoardSystem`)** & **Personal Diary (`CozyDiarySystem`)**

### 3. Character, Companions & Dynamic Environment
- **Modular Character Customization (`CharacterAppearanceCompositor`, `CharacterAppearanceData`)**: Dynamic layered sprite composition for hairstyles, outfits, and accessories.
- **Pet Companion System (`CozyPetCompanionSystem`)**: AI-driven pet companions that follow and interact with the player.
- **Dynamic Weather, Day/Night & Ambiance (`CozyWeatherAmbianceSystem`, `CozyAmbientAndVehicleSystems`)**: Real-time weather transitions, lighting ambiance, and background vehicle/town life simulation.
- **Isometric Depth Sorting (`IsometricSorting`)**: Dynamic Y-axis sorting for 2D sprites and props across the town map.

---

## Project Structure

```text
Assets/
├── PlayerController.cs                  # Core player movement, interaction & local input handling
├── Resources/                           # Runtime-loaded sprites, audio, and prefabs
├── Scenes/                              # Unity game scenes
└── Scripts/
    ├── Core/                            # Save system, Audio manager, Diary, Item/World databases
    ├── Editor/                          # Custom Unity Editor tools & world setup utilities
    ├── Network/                         # Client/Server networking, Cloud Relay, Packet serialization
    ├── Player/                          # Appearance compositor, Character stats, Inventory & Professions
    ├── UI/                              # Main Menu, HUD, notifications & interactive dialogs
    └── World/                           # Procedural/Modular town builder, Minigames, Weather & Interactables
```

---

## Getting Started

### Prerequisites
- **Unity Hub**
- **Unity 6 (`6000.6.0f1` or newer)** with **Universal Render Pipeline (URP) / 2D** support

### Running the Project
1. Clone this repository:
   ```bash
   git clone https://github.com/LongLG27/CozyLife.git
   ```
2. Open **Unity Hub**, click **Add -> Add project from disk**, and select the `CozyLife` folder.
3. Open the main scene under `Assets/Scenes/` and press **Play** in the Unity Editor.
