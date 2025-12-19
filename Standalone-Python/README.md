# Internal Wind Machine

A software-only solution for a SimRacing wind simulator.  
It allows standard PC fans (connected directly to a motherboard fan header) to be controlled via live telemetry data from racing simulations — without any external microcontrollers (e.g., Arduino).

The system is based on **SimHub**, **SimHub Property Server Plugin**, **Fan Control**, and costsists of a single **Python Script**.

---

## 🌟 Features

- **No external hardware controllers required**: Uses standard PC fans and motherboard fan headers.
- **Dynamic 3D Wind Support**: Supports Left, Center, and Right channels for directional wind (requires multiple fans).
- **Auto-Update System**: Stay up to date with the latest features directly from GitHub (with numeric version protection).
- **Robust Config Management**: Fully configurable via `config.txt` with automatic backup and template migration.
- **Low Latency**: Real-time telemetry processing for an immersive experience.


---

Check out my [Twitch](https://www.twitch.tv/therealkarle), I plan on streaming a bit with the Wind sim, so it's a good chance to see it in action and ask me questions about it.

---

## 🛠️ Dependencies

The following software must be installed:

- [**SimHub**](https://www.simhubdash.com/)
- [**Sim Hub Property Server Plugin**](https://github.com/pre-martin/SimHubPropertyServer) by pre-martin
- [**Fan Control**](https://getfancontrol.com/)
- [**Python 3.x**](https://www.python.org/downloads/) (No additional packages/pip installs required!)

---

## 🚀 Installation

1. Install **SimHub**, **Fan Control**, and **Martin Renner’s Property Server**.
2. Make sure all required plugins and services are **enabled and running**.
3. Download or clone this repository.
4. Copy **all files from the repository into a single folder** (e.g., `C:\Programs\InternalWindMachine`).
5. Connect your **PC fan(s)** to your **motherboard fan header(s)**.
6. Run **`InternalWindMachineStart.bat`** to initialize the folder structure.

---

## ⚙️ Configuration (`config.txt`)

The system is controlled via the `config.txt` file. You can adjust:
- **`auto_update`**: Set to `ask` (default), `true` (auto), or `false`.
- **`use_3d_wind`**: Set to `true` to enable Left/Right fan support. (Default: `false`)
- **`host` / `port`**: (Advanced) Connection settings for SimHub Property Server. 
- **Individual Toggles**: (Advanced) Enable or disable specific fans (Center, Left, Right).

---

## 💨 Fan Control Setup

1. Open **Fan Control**.
2. Create a **Custom Sensor** (File).
3. Point to the sensor file(s) in the `Sensors` subdirectory:
   - `Sensors\WindPercentageCenter(default).sensor`
   - `Sensors\WindPercentageLeft.sensor` (if 3D enabled)
   - `Sensors\WindPercentageRight.sensor` (if 3D enabled)
4. Create a **new control curve** for each fan you want to use:


   - Minimum temperature: `-1`


   - Maximum temperature: `100`


   - Recommended curve: a linear line from `0 C° → The Lowest Percentage at which the Fan stops spinning (for me it was 33%)`





     to `100 C° → 100 %`


     - Ad an adtional Pont at `-1C° → 0% ` to ensure the fan(s) fully power down


   - Adjust the curve to your personal preference if needed.

![Fan Curve Example](../Media/FanCurve.png)

5. Assign the fan(s) to the corresponding control curve(s).

---

## 🎮 Running the System

1. Start **Fan Control** (Recommended: Set it as a startup program, especially if it also manages your system fans).
2. Start **SimHub**.
3. Start the Python Script by runnin the provided **`InternalWindMachineStart.bat`**.
   But at the end of the day you just need to start the `InternalWindMachine.py` howevery you want.

4. **Testing**: Enable "Idle Wind" in SimHub. If the fan reacts, you are ready!
5. **Shutting down**: Type **`stop`** in the console or press **`Ctrl+C`**. This ensures all sensors are reset to **-1** to turn off the fans.

> ### 🛠️ Troubleshooting Commands
> - **`reset`**: Type this in the console to manually reset all sensor files to -1.00.
> - **`update`**: Type this to manually check for script updates.
> - **`ResetSensorFiles.bat`**: A standalone tool to reset all sensors if the script crashed.

---

## 📝 Troubleshooting

- **No fan response**: Check if the Property Server port in SimHub matches `config.txt` (Default: 18082).
- **Idle Wind works, but no wind in-game**: Verify that telemetry output is enabled in your game settings.
- **Fan takes a while to spin up**: Adjust your Fan Control curve so that 0% sensor value equals the lowest percentage at which your fan **stops** spinning.

---

## 📦 Notes & 3D Prints

The control latency is extremely low. For the best result, I recommend using a nozzle for your fan.
[**Here is the 3D-printable nozzle I use**](https://www.thingiverse.com/thing:6845650).

---

## 📜 License

Use at your own risk. This project is intended for personal and experimental use. For commercial use or embedding this into a commercial project, please contact me.

---

## 📢 Shameless Self Promotion

Stay connected and see the Wind Machine in action:

- [**Linktree (All Links)**](https://linktr.ee/therealkarle)
- [Youtube](https://www.youtube.com/@therealkarle?sub_confirmation=1)
- [Twitch](https://www.twitch.tv/therealkarle)
- [TikTok](https://www.tiktok.com/@therealkarle)
- [Instagram](https://www.youtube.com/@therealkarle?sub_confirmation=1)


