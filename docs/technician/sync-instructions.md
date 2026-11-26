# Add-in 0.3: instructions for the BIM technicians

**What changes.** From this version, the add-in takes a snapshot of the model's quantities **by itself after every
successful Synchronize with Central** on a cloud (ACC) model. Nothing pops up. Revit is busy for a few seconds
after each sync while it reads (about 4 to 11 seconds on a large tower). **It changes nothing in the model.** The
**Sync to website** and **Raw dump** buttons on the **RevitVolumeCost** tab work as before.

Install it only on the PCs of people who model **structural** models: it takes a snapshot of every cloud model synced
on that PC.

## 1. Install (2 minutes)

1. Close every Revit window.
2. Save the .zip on your own PC, not on a network drive. Right-click it > **Properties**, tick **Unblock** (if it is
   there) > **OK**. Then right-click > **Extract All**.
3. Double-click **Install.cmd**. It replaces the earlier version for each of Revit 2024 and 2026 your PC has.
   If it asks whether to run a script, type **R** and press Enter.
4. Start Revit. If it asks about an **unsigned add-in**, choose **Always Load**.

## 2. Work as usual

Synchronize with Central as you always do. After each sync, a new file ending in **.snapshot.json.gz** appears in
`%LocalAppData%\RevitVolumeCost\Outbox`. They collect there until the website can receive them over the network;
Adnan will ask for them until then.

## 3. If something seems wrong

- If a sync feels noticeably slower than the few seconds above, tell Adnan and send
  `%LocalAppData%\RevitVolumeCost\Logs\addin.log` (it records each snapshot's time).
- To switch the automatic snapshot off on one PC: create the file `%AppData%\RevitVolumeCost\addin.json` containing
  `{ "snapshotAfterSync": false }` and restart Revit. Delete the file to switch it back on.

The snapshot files hold the model's element data, so send them to Adnan only.

## Afterwards

To remove the add-in, close Revit and delete **RevitVolumeCost.addin** and the **RevitVolumeCost** folder from
`%AppData%\Autodesk\Revit\Addins\2024` (and `...\2026`).
