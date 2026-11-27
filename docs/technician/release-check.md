# Telltale 0.7: the release check, for one BIM technician

One technician runs this once, on a PC that has the **previous version (RevitVolumeCost 0.6)** installed, before 0.7
goes to everyone. It takes about 20 minutes. It checks the rename, the new buttons, and (for the first time in Revit) the
3D shapes sent after a Synchronize with Central. Write down what you see at each step and send it to Adnan with the
files listed at the end.

## A. Before installing: leave one snapshot unsent

1. Open Task Manager (Ctrl + Shift + Esc) > **Details**, right-click **RevitVolumeCost.Uploader.exe** > **End task**.
2. In Revit, open any project model and press **Sync to website** on the **RevitVolumeCost** tab. Close Revit.
3. Open `%LocalAppData%\RevitVolumeCost\Outbox` and write down the name of the newest **.snapshot.json.gz** file.

## B. Install 0.7

4. Extract the 0.7 zip and double-click **Install.cmd** (see "READ ME FIRST" for the usual steps).
   **Expected:** lines saying it removed the old RevitVolumeCost files and moved its folders, and **no** question
   about the website address or password. If it asks for them, write that down, then type them in.
5. **Expected** afterwards:
   - `%AppData%\Autodesk\Revit\Addins\2026` holds **Telltale.addin** and a **Telltale** folder, and no
     RevitVolumeCost.addin or RevitVolumeCost folder. The same in `...\2024` if the PC has Revit 2024.
   - The Startup folder (Windows key + R, `shell:startup`) holds **Telltale uploader** and no RevitVolumeCost uploader.
   - Task Manager > Details shows **Telltale.Uploader.exe** and no RevitVolumeCost.Uploader.exe.
   - Within two minutes, the file from step 3 is in `%LocalAppData%\Telltale\Outbox\Sent`.

## C. The ribbon, in Revit 2026

6. Start Revit 2026 with **no model open**. **Expected:** one question about an unsigned add-in, naming Telltale
   (choose Always Load); a **Telltale** tab with **Sync to website**, **Raw dump** and **Help**, each with an icon; no
   RevitVolumeCost tab. Take a screenshot of the tab.
7. Switch Revit to the dark theme (File > Options > Colors, the theme setting: Dark > OK). **Expected:** the icons turn
   light and stay easy to see. Take a screenshot, then switch back to Light.
8. Press **Help** (still no model open). **Expected:** it opens (the button is not greyed out) and shows 0.7.0, "Snapshot
   after Synchronize with Central: on" and "Waiting to be sent: none". Try each of its four links in turn: the website
   opens in the browser, this sheet opens, then the outbox folder, then the logs folder.

## D. A real sync, with its 3D shapes

9. Open a tagged structural model on ACC and make a small change, then **Synchronize with Central**. Revit is busy for a
   few seconds after the sync. **Expected:** within two minutes, a new .snapshot.json.gz and a .shapes.json.gz with the
   same name are in `%LocalAppData%\Telltale\Outbox\Sent`, and the website shows the model's new snapshot and its 3D view.
10. Press **Sync to website**. **Expected:** "Snapshot taken" with the element count and the time.

## E. Revit 2024 (only if the PC has it)

11. Repeat steps 6, 8 and 9 in Revit 2024 with a model of that year. Revit 2024 has not run this add-in yet.

## Send back to Adnan

- What you saw at each step, especially anything that differed from "Expected".
- The screenshots from steps 6 and 7.
- `addin.log` and `uploader.log` (Help > Show the logs folder).
- Revit's exact build for each year used (in Revit: the ? menu > About).
