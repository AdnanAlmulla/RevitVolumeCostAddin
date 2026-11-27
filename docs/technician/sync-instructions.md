# Telltale add-in 0.7: instructions for the BIM technicians

**New in 0.7:** the add-in is now called **Telltale** (it was RevitVolumeCost). Its tab in Revit is **Telltale**, its
buttons have icons, a **Parameter standard** button shows the parameters to fill in and their values, and a **Help** button shows whether snapshots are waiting to be sent and opens this sheet. Installing
0.7 removes the old RevitVolumeCost add-in and uploader by itself and keeps your settings: you are not asked for the
password again, and snapshots not yet sent are sent.

**From 0.7, new versions install themselves.** The uploader fetches each new version Adnan releases and installs it
in the background. When one is ready, Revit shows **"New version of Telltale Addin ready, restart Revit please"**:
finish what you are doing, close Revit and start it again. If Revit then asks about an unsigned add-in, choose **Always
Load**. You won't need to run Install.cmd again.

The add-in takes a snapshot of the model's quantities, with its 3D shapes, **by itself after every successful
Synchronize with Central** on a cloud (ACC) model. Nothing pops up. Revit is busy for a few seconds after each sync while
it reads (about 5 to 12 seconds on a large tower). **It changes nothing in the model.** A small program, the
**uploader**, starts when you sign in to Windows, has no window, and sends each snapshot to the website within a minute;
if the website can't be reached, it keeps trying.

Install it only on the PCs of people who model **structural** models: it takes a snapshot of every cloud model synced
on that PC.

## 1. Install (3 minutes)

The first time on a PC, get two things from Adnan: the **website address** and the **site password**.

1. Close every Revit window.
2. Save the .zip on your own PC, not on a network drive. Right-click it > **Properties**, tick **Unblock** (if it is
   there) > **OK**. Then right-click > **Extract All**.
3. Double-click **Install.cmd**. It replaces the earlier version for each of Revit 2024 and 2026 your PC has, and the
   uploader. If it asks whether to run a script, type **R** and press Enter.
4. The first time on a PC, it asks for the **website address** and the **site password**: type what Adnan gave you (the
   password does not show as you type) and press Enter after each. It then starts the uploader.
5. Start Revit. If it asks about an **unsigned add-in**, choose **Always Load**.

## 2. Work as usual

Synchronize with Central as you always do. On the **Telltale** tab:

- **Sync to website** takes a snapshot now, on any project model (a detached copy too).
- **Parameter standard** shows the shared parameters to fill in on each element (Division_Shared, Concrete Grade_Shared,
  Structural System_Shared, Wall Function_Shared, Steel Grade_Shared), the values to type, spelled exactly, and the
  rules, as agreed with Adnan. You can copy a value from it and paste it into Revit.
- **Raw dump** writes every element with all its parameters to one file, for checking. Use it only when Adnan asks. It
  is out of sight: click the small arrow beside the **Quantities** panel's name to show it.
- **Help** shows the add-in's version, whether this PC takes the snapshot after each sync, and how many snapshots are
  still **waiting to be sent**. From there you can open the website, this sheet, the outbox folder and the logs folder.

After each sync two files appear in the outbox, `%LocalAppData%\Telltale\Outbox`, one ending in **.snapshot.json.gz**
and one in **.shapes.json.gz**. Within a minute the uploader moves them to the **Sent** folder beside them, which keeps
them for 90 days (the website keeps its own copy) and then deletes them.

## 3. If something seems wrong

- **Help says snapshots are waiting, and they stay:** open `uploader.log` (Help > **Show the logs folder**).
  "will retry ... 403" means the site password is wrong: correct it in `%AppData%\Telltale\uploader.json` (Notepad), and
  the uploader uses it within a minute. Otherwise send the log to Adnan. Adnan's PC must be on for the website to be up.
- **A file in Outbox\Rejected:** the website refused it; send the file and its `.reason.txt` to Adnan.
- **Sync to website says "No snapshot was taken":** send `addin.log` (Help > **Show the logs folder**) to Adnan.
- If a sync feels noticeably slower than the few seconds above, tell Adnan and send `addin.log` (it records each
  snapshot's time).
- To switch the automatic snapshot off on one PC: create the file `%AppData%\Telltale\addin.json` containing
  `{ "snapshotAfterSync": false }` and restart Revit. Delete the file to switch it back on. Help shows which it is.
- "The uploader needs .NET 8" when installing: the PC has only Revit 2024. Tell Adnan; the snapshots wait in the
  Outbox, and this PC gets no new versions by itself.
- The **Parameter standard** window says at the top whether it shows the website's copy or the one built into the
  add-in. It shows the built-in one until the uploader has fetched the website's copy (within an hour of the uploader
  starting).

The snapshot and shapes files hold the model's data, so send them to Adnan only. Keep the site password to yourself.

## Afterwards

To remove the add-in, close Revit and delete **Telltale.addin** and the **Telltale** folder from
`%AppData%\Autodesk\Revit\Addins\2024` (and `...\2026`). To remove the uploader, open Task Manager (Ctrl + Shift + Esc) >
**Startup apps**, right-click **Telltale uploader** > **Disable**, sign out and back in, then delete
`%LocalAppData%\Telltale\Uploader`.
