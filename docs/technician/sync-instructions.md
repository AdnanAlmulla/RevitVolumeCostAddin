# First snapshot test: instructions for the BIM technician

**What this is.** Version 0.2 of the add-in. It adds a **RevitVolumeCost** tab to Revit with two buttons:
**Sync to website** (takes a snapshot of the model's quantities) and **Raw dump** (as before). **It changes nothing in the model** (it only reads), and nothing is uploaded yet: each
snapshot stays in a folder on your PC until you send it. The automatic snapshot after Synchronize with Central is
built in but **switched off** in this version, so your syncs are not affected; it gets switched on after this test.

## 1. Install (2 minutes)

1. Close every Revit window.
2. Save the .zip on your own PC, not on a network drive. Right-click it > **Properties**, tick **Unblock** (if it is
   there) > **OK**. Then right-click > **Extract All**.
3. Double-click **Install.cmd**. It replaces the earlier version for each of Revit 2024 and 2026 your PC has.
   If it asks whether to run a script, type **R** and press Enter.

## 2. Take a snapshot by hand

1. Start Revit. If it asks about an **unsigned add-in**, choose **Always Load** (or Load Once).
2. Open **the same detached copy of the tower you used for the raw dump**, in Revit 2026. You don't need to sync.
3. Go to the new **RevitVolumeCost** tab > **Sync to website**.
4. A box says **Snapshot taken**, with how many seconds it took. Please note the seconds.
5. Click **Show the file in File Explorer**. The file ends in **.snapshot.json.gz**, in
   `%LocalAppData%\RevitVolumeCost\Outbox`.

## 3. Send back to Adnan

- The **.snapshot.json.gz** file from the **Outbox** folder, as it is.
- The log `%LocalAppData%\RevitVolumeCost\Logs\addin.log` (it records each snapshot's time).
- One line: the seconds from step 2.
- If anything failed, a screenshot of the message.

The files hold the model's element data, so send them to Adnan only.

## Afterwards

Leave it installed: with the automatic snapshot switched off, it does nothing until a button is pressed. To
remove the add-in, close Revit and delete **RevitVolumeCost.addin** and the **RevitVolumeCost** folder from
`%AppData%\Autodesk\Revit\Addins\2024` (and `...\2026`).
