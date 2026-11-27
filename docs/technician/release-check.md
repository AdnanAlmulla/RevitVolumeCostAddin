# Telltale 0.7: the release check, for one BIM technician

One technician runs this once, on a PC that has an **earlier version (RevitVolumeCost, any version)** installed, before 0.7
goes to everyone. It takes about 20 minutes. It checks the rename, the new buttons (Parameter standard and Help), (for
the first time in Revit) the 3D shapes sent after a Synchronize with Central, and the automatic update: Adnan has already
released a test version, **0.7.1**, so the PC moves from 0.7.0 to 0.7.1 by itself within a couple of minutes of
installing. Section F (the "restart Revit" message) waits for the next version Adnan releases. Write down what you see at each step and send it to Adnan with the
files listed at the end.

## A. Before installing: leave one snapshot unsent

1. Open Task Manager (Ctrl + Shift + Esc) > **Details**, right-click **RevitVolumeCost.Uploader.exe** > **End task** (if it is there).
2. In Revit, open any project model and press **Sync to website** on the **RevitVolumeCost** tab. Close Revit.
3. Open `%LocalAppData%\RevitVolumeCost\Outbox` and write down the name of the newest **.snapshot.json.gz** file.

## B. Install 0.7

4. Extract the 0.7 zip and double-click **Install.cmd** (see "READ ME FIRST" for the usual steps).
   **Expected:** lines saying it removed the old RevitVolumeCost files and moved its folders, and **no** question
   about the website address or password. If it asks for them, write that down, then type them in.
5. **Expected** afterwards:
   - `%AppData%\Autodesk\Revit\Addins\2026` holds **Telltale.addin** and a **Telltale** folder (with a **0.7.0** folder
     inside), and no
     RevitVolumeCost.addin or RevitVolumeCost folder. The same in `...\2024` if the PC has Revit 2024.
   - Task Manager > **Startup apps** lists **Telltale uploader** (enabled). The Startup folder (Windows key + R,
     `shell:startup`) has no RevitVolumeCost uploader.
   - Task Manager > Details shows **Telltale.Uploader.exe** and no RevitVolumeCost.Uploader.exe.
   - Within two minutes, the file from step 3 is in `%LocalAppData%\Telltale\Outbox\Sent`.
   - Within two minutes, the **Telltale** folder in `...\Addins\2026` also holds a **0.7.1** folder (the automatic
     update), and Task Manager > **Details**, right-click **Telltale.Uploader.exe** > **Open file location** opens
     `...\Telltale\Uploader\0.7.1`. Write down how long each took, and whether antivirus or Windows warned about
     anything.

## C. The ribbon, in Revit 2026

6. Start Revit 2026 with **no model open**. **Expected:** one question about an unsigned add-in, naming Telltale
   (choose Always Load; write down if it asks more than once); a **Telltale** tab with **Sync to website**, **Parameter standard** and **Help**, each with an icon; no RevitVolumeCost
   tab. Click the small arrow beside the **Quantities** panel's name: **Raw dump** shows, with its icon. Take a
   screenshot of the tab with the arrow opened.
7. Switch Revit to the dark theme (File > Options > Colors, the theme setting: Dark > OK; the wording may differ a
   little, as this path was not checked in Revit). **Expected:** the icons turn
   light and stay easy to see. Take a screenshot, then switch back to Light.
8. Press **Help** (still no model open). **Expected:** it opens (the button is not greyed out) and shows **0.7.1** (the update), "Snapshot
   after Synchronize with Central: on" and "Waiting to be sent: none". Try each of its four links in turn: the website
   opens in the browser, the instructions sheet opens, then the outbox folder, then the logs folder.
9. Press **Parameter standard** (still no model open). **Expected:** a window listing the five parameters as agreed on
   27 Nov, with **Roof** among the Division_Shared values and **Upstand** among the Wall Function_Shared values, then the
   settings, rules and examples. The line under the title should say "From the website" (write down what it says). Check every name and value against the Excel sheet and note any difference. Select a
   value, copy it (Ctrl + C) and paste it into a parameter in Revit to check it pastes exactly. Close the window with
   Close or Esc.

## D. A real sync, with its 3D shapes

10. Open a tagged structural model on ACC and make a small change, then **Synchronize with Central**. Revit is busy for a
   few seconds after the sync. **Expected:** within two minutes, a new .snapshot.json.gz and a .shapes.json.gz with the
   same name are in `%LocalAppData%\Telltale\Outbox\Sent`, and the website shows the model's new snapshot and its 3D view.
11. Press **Sync to website**. **Expected:** "Snapshot taken" with the element count and the time.

## E. Revit 2024 (only if the PC has it)

12. Repeat steps 6, 8, 9 and 10 in Revit 2024 with a model of that year. Revit 2024 has not run this add-in yet.

## F. The "restart Revit" message (when Adnan releases the next version)

13. Adnan tells you when he releases the next version. Before he does, open Revit 2026 with any model and keep it open.
    Once it is released, make the uploader check at once (it also checks every hour by itself): Task Manager >
    **Details**, right-click **Telltale.Uploader.exe** > **End task**, then double-click the **Telltale.Uploader.exe** in
    the newest folder of `%LocalAppData%\Telltale\Uploader\` (nothing shows; it runs in the background).
14. Go back to Revit and leave it idle for up to 10 minutes. **Expected:** a message
    **"New version of Telltale Addin ready, restart Revit please"**, once. Write down how long it took to appear and
    whether it came back after you closed it (it should not).
15. Close Revit and start it again. **Expected:** Help shows the new version.

## Send back to Adnan

- What you saw at each step, especially anything that differed from "Expected".
- The screenshots from steps 6 and 7.
- `addin.log` and `uploader.log` (Help > Show the logs folder).
- Revit's exact build for each year used (in Revit: the ? menu > About).
