# Duty of War ⚔️

A tactical Real-Time Strategy (RTS) game built in **Unity (C#)** where players command military units using natural language orders interpreted in real time by **Google Gemini AI**.

---

## ✨ Key Features

* **Natural Language Commanding (LLM Commander):** Issue tactical orders in plain text (e.g., *"Cavalry, flank them from the east"*, *"Archers, take the northern hill"*, *"Everyone form a turtle"*). The AI model translates player intent into structured JSON commands (`MOVE`, `ATTACK`, `FORM`, `STOP`) based on the live battlefield context and currently selected units.
* **Tactical Enemy AI (Enemy General):** Opposing forces operate in two modes — **Historical** (driven by scripted battle phases and triggers such as elapsed time, enemy distance, or casualty percentage) and **Dynamic** (adapting tactics in real time based on the general's personality: *Aggressive*, *Defensive*, *Balanced*, *Tactician*).
* **Automated Terrain Analysis (Terrain Scanner):** Scans map heightmaps and terrain layers to automatically detect strategic landmarks (hills, forests, rivers) and calculate tactical alignment vectors (*Front*, *Back*, *Top*).
* **Formations & Flanking Maneuvers:** Supports multiple combat formations (*Line*, *Double Line*, *Wedge*, *Turtle*, *Box*, *Square*, *Loose*, *Column*) as well as multi-stage flanking sequences.

---

## 🎮 How to Play (Quick Start without Unity)

A pre-compiled Windows build is available in the **[Releases](../../releases)** tab on the right side of this repository:

1. Download the latest `.zip`.
2. Extract the entire archive into a folder.
3. Run the game executable (`.exe`).

### Example Commands (in Polish, as configured in the system prompt):
* *"Wszyscy do ataku!"* (All units, attack!)
* *"Jazda, oflankować ich od zachodu!"* (Cavalry, flank them from the west!)
* *"Łucznicy zająć wzgórze na północy!"* (Archers, take the northern hill!)
* *"Wybrani, formacja żółwia!"* (Selected units, form a turtle!)
* *"Piechota 50 metrów do przodu!"* (Infantry, advance 50 meters!)

---

## 🛠️ Running from Source (Unity)

To open and run the project in the Unity Editor:

1. Clone the repository:
   ```bash
   git clone [https://github.com/B0mbel1/Duty-of-War.git](https://github.com/B0mbel1/Duty-of-War.git)
