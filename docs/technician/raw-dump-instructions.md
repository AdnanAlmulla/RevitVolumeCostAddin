# Raw dump of one tower model: instructions for the BIM technician

**What this is.** A one-off read of one real tower model. It writes every model element, with its
materials, volumes and every parameter, to one file. It shows what our models already carry, so the
quantity tool is built on what is really there. **It changes nothing in the model** (Revit runs it in
read-only mode), and nothing is uploaded anywhere: the file stays on your PC until you send it.

**Which model.** A real tower that is well advanced; the largest one we have is best. Use the Revit year
the model is already in (this package has **Revit 2024** and **Revit 2026**). If Revit says the model must
be upgraded, **stop and cancel**: that is the wrong Revit year, and upgrading a cloud model affects everyone.

## 1. Install (2 minutes)

1. Close every Revit window.
2. Save the .zip on your own PC (for example your Desktop), not on a network drive. Right-click it >
   **Properties**, tick **Unblock** at the bottom (if it is there) > **OK**. Then right-click > **Extract All**.
3. In the extracted folder, double-click **Install.cmd**. It installs for each of Revit 2024 and 2026 that your
   PC has, and says so. If it asks whether to run a script, type **R** and press Enter.

## 2. Run the dump

1. Start Revit. If it asks about an **unsigned add-in**, choose **Load Once**.
2. Open the tower model the way you normally do. You don't need to sync.
3. Go to **Add-Ins** tab > **External Tools** > **RevitVolumeCost: raw dump**.
4. Revit is busy while it reads; on a large tower this can take several minutes. Please wait.
5. A box says **Raw dump finished**, with the file's location (your **Documents\RevitVolumeCost\Raw dumps**
   folder). The file ends in **.rawdump.json.gz**. Click "Show the file in File Explorer" to find it.

## 3. Export Revit's own quantities (for checking the dump)

In the same model:

1. **View** tab > **Schedules** > **Material Takeoff**. Category: **<Multi-Category>**. OK.
2. **Fields**: add **Category**, **Material: Name**, **Material: Volume** and **Count**, in that order.
3. **Sorting/Grouping**: sort by **Category**, then by **Material: Name**. Untick **Itemize every instance**.
   Tick **Grand totals**.
4. **Formatting**: pick **Material: Volume**, choose **Calculate totals**, then **Field Format...**: untick
   **Use project settings**, set Units to **Cubic meters** and Rounding to **3 decimal places**.
5. With the schedule open on screen, **File** > **Export** > **Reports** > **Schedule**. Keep the default
   settings and save the .txt file.

These steps were written without Revit open. If a step looks different in your Revit, do the nearest thing,
send whatever export you get, and say which step differed.

You don't need to keep the schedule in the model: close the model **without saving** (or delete the
schedule first) so nothing is added for anyone else.

## 4. Send back to Adnan

- The **.rawdump.json.gz** file, as it is (don't unzip or rename it).
- The schedule **.txt** file.
- One line with: the model's name, the Revit year and build (**?** > **About Autodesk Revit**), and roughly
  how long the dump took.
- If anything failed, a screenshot of the message.

The file holds the model's element data and parameter values, so send it to Adnan only.

## Afterwards

You can leave it installed. To remove it, close Revit and delete **RevitVolumeCost.addin** and the
**RevitVolumeCost** folder from `%AppData%\Autodesk\Revit\Addins\2024` (and `...\2026`).
