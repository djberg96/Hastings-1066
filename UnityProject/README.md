# Hastings 1066 Unity game

Open this folder with Unity 6000.3.6f1. Open `Assets/Scenes/Hastings.unity` and press Play, or use the built Mac app at `../Builds/Mac/Hastings 1066.app`. Use **Hastings → Create Main Scene** if the scene needs to be regenerated. To rebuild the Mac app, run `ProjectBuilder.BuildMac` from the Unity command line.

The first release is a solo desktop game. The Norman player chooses strategies, facings, movement, fire, and melee. The Saxon AI plays its own segments. The phase button advances the turn, and the event log records dice and results. Rolling orders opens a readable summary of both armies' rolls, orders, durations, accumulated strategy effects, and practical rule consequences; the current summary is saved with the game and can be reopened from the phase panel. The game opens on a tapestry-inspired title screen; the battlefield appears after New Game or Load Game. The larger parchment-colored battle panel and reference charts scale with the display. In a battle, Escape opens the menu for Resume, New Game, Save Game, and Load Game. The battle panel's Hide units button clears the counters from the map for terrain inspection; Show units restores them. Save files are versioned JSON in Unity's `Application.persistentDataPath/Saves` folder.

## Controls

- Click a Norman counter to select it; Alt-click to select a leader stacked with a combat unit.
- Shift-click additional Norman counters to combine fire or melee. During melee, Shift-click enemy counters to select several defenders, then use **Resolve selected melee**.
- Click a highlighted hex to move, or click an enemy counter to fire or melee. The panel shows the current phase and available decisions.
- Press Q/E to turn a selected counter, Space to finish a segment, and Escape for the menu. Hold WASD to move the view; right drag also pans, and the wheel zooms.
- Hex numbers are drawn over the map from the SVG's hex IDs. The upper-right hex is `0101`, and numbers increase to the left in each row. The map grows with the game window. **Battle view** centers the fighting area; **Full map** fits the entire board.

The bundled PDF is available through **Rulebook PDF**. The Melee, Missile, and Morale reference tabs render readable tables from the same rule data used by combat and rally. The Terrain tab summarizes movement costs, combat modifiers, and related restrictions from the supplied terrain effects chart and rules. The SVG map is converted into `Assets/Resources/Art/Map/hex_map.png`, and its terrain data into `Assets/Resources/Data/Map.json`, by `../Tools/generate_assets.py`. That script uses `rsvg-convert` to preserve the SVG's stream turbulence, turns the marked woods and marsh hexes into connected terrain areas using the source art in `../Tools/terrain_art/`, and uses Python's Pillow package to rasterize textured ridges and add a subtle printed-paper finish. `../Tools/generate_counters.py` creates the slightly muted, printed-cardboard unit artwork used by Unity while retaining the supplied counter PNGs unchanged. Neither script uses Inkscape. The generated textures and data are checked in, so rebuilding them is unnecessary for normal Unity use.

The project uses only Unity's built-in modules. A Windows build can be exported after adding Windows Build Support through Unity Hub. The Mac build and Editor run have been checked in Unity; Windows export has not been run here.

The Vassal scenario places the Norman counters and Housecarls but leaves the randomly drawn Saxon Fyrd and Thegn counters in a pool. This project uses a fixed front line on the hill for those 32 counters, then draws their types from the rules' pool. Those individual Saxon setup hexes are an interpretation of the supplied map and should be reviewed against a printed copy before using the game for exact rules adjudication.

## Saxon AI

The AI uses the same legal action methods as the Norman player. It favors higher elevation (+2 per level), road control (+3), the upper hill (+2), and woods (+1), and penalizes marshes (-3) and moving beyond the hill (+7 penalty). Missile units favor a distance of two or three hexes (+2) and avoid adjacent enemies (-3). Other units favor adjacent or near enemies (+2 or +1); an Attack and Pursue order adds 1.5 per hex closed. Reactions prefer elevation, then road control, and will preserve a road hex if retreat would lose elevation. Fire prefers stronger odds, a target's reduced side, and William's guard. The code breaks ties by hex number so the saved random state determines every subsequent die result.
