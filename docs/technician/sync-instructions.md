# Add-in 0.4: instructions for the BIM technicians

**What changes.** The snapshots now **reach the website by themselves**. A small program, the **uploader**, is
installed with the add-in: it starts when you sign in to Windows, has no window, and sends each new snapshot to the
website within a minute. If the website can't be reached, it keeps trying.

As in 0.3, the add-in takes a snapshot of the model's quantities **by itself after every successful Synchronize with
Central** on a cloud (ACC) model. Nothing pops up. Revit is busy for a few seconds after each sync while it reads
(about 4 to 11 seconds on a large tower). **It changes nothing in the model.** The **Sync to website** and **Raw dump**
buttons on the **RevitVolumeCost** tab work as before.

Install it only on the PCs of people who model **structural** models: it takes a snapshot of every cloud model synced
on that PC.

## 1. Install (3 minutes)

Before you start, get two things from Adnan: the **website address** and the **site password**.

1. Close every Revit window.
2. Save the .zip on your own PC, not on a network drive. Right-click it > **Properties**, tick **Unblock** (if it is
   there) > **OK**. Then right-click > **Extract All**.
3. Double-click **Install.cmd**. It replaces the earlier version for each of Revit 2024 and 2026 your PC has.
   If it asks whether to run a script, type **R** and press Enter.
4. The first time, it asks for the **website address** and the **site password**: type what Adnan gave you (the
   password does not show as you type) and press Enter after each. It then starts the uploader.
5. Start Revit. If it asks about an **unsigned add-in**, choose **Always Load**.

## 2. Work as usual

Synchronize with Central as you always do. After each sync a file ending in **.snapshot.json.gz** appears in
`%LocalAppData%\RevitVolumeCost\Outbox`, and within a minute the uploader moves it to the **Sent** folder beside it.
Files that stay in the Outbox have not reached the website yet.

## 3. If something seems wrong

- **Files stay in the Outbox:** open `%LocalAppData%\RevitVolumeCost\Logs\uploader.log`. "will retry ... 403" means
  the site password is wrong: correct it in `%AppData%\RevitVolumeCost\uploader.json` (Notepad), and the uploader
  uses it within a minute. Otherwise send the log to Adnan. Adnan's PC must be on for the website to be up.
- **A file in Outbox\Rejected:** the website refused it; send the file and its `.reason.txt` to Adnan.
- If a sync feels noticeably slower than the few seconds above, tell Adnan and send
  `%LocalAppData%\RevitVolumeCost\Logs\addin.log` (it records each snapshot's time).
- To switch the automatic snapshot off on one PC: create the file `%AppData%\RevitVolumeCost\addin.json` containing
  `{ "snapshotAfterSync": false }` and restart Revit. Delete the file to switch it back on.
- "The uploader needs .NET 8" when installing: the PC has only Revit 2024. Tell Adnan; the snapshots wait in the
  Outbox.

The snapshot files hold the model's element data, so send them to Adnan only. Keep the site password to yourself.

## Afterwards

To remove the add-in, close Revit and delete **RevitVolumeCost.addin** and the **RevitVolumeCost** folder from
`%AppData%\Autodesk\Revit\Addins\2024` (and `...\2026`). To remove the uploader, delete **RevitVolumeCost uploader**
from your Startup folder (Windows key + R, type `shell:startup`), sign out and back in, then delete
`%LocalAppData%\RevitVolumeCost\Uploader`.
