using System;
using System.IO;
using Gtk;
using Glade;

namespace FieldEditor {

public class MainWindow {
	[Widget] private Window window;
	[Widget] private ScrolledWindow scrolledWindow;
	[Widget] private FileChooserDialog openFileDialog, saveFileDialog;
	[Widget] private HPaned splitPane;
	[Widget] private Widget infoTable;
	[Widget] private SpinButton widthEdit, heightEdit;
	[Widget] private Label currentCoord, currentBlockType, zoomLevelLabel, selectedCoord;
	[Widget] private MenuItem saveMenu, saveAsMenu;
	[Widget] private ToolButton saveAsButton, zoomInButton, zoomOutButton;
	[Widget] private ComboBox selectedBlockType;

	private FieldView fieldView;
	/** The currently open file. */
	private string filename = null;
	private bool selectedBlockTypeChanging;

	public MainWindow() {
		initUI();
	}

	private void initUI() {
		Glade.XML xml = new Glade.XML(DataFiles.Glade("MainWindow.glade"), null, null);
		xml.Autoconnect(this);
		setupSelectedBlockType();

		fieldView = new FieldView();
		fieldView.OnMouseMove += OnFieldMouseMove;
		fieldView.OnSelectionChanged += OnFieldSelectionChanged;
		scrolledWindow.AddWithViewport(fieldView);
		fieldView.Show();

		splitPane.Position = 320;

		window.Show();
	}

	private void setupOpenDialog() {
		FileFilter filter;
		Glade.XML xml = new Glade.XML(DataFiles.Glade("OpenDialog.glade"), null, null);

		xml.Autoconnect(this);

		filter = new FileFilter();
		filter.Name = "All support files (*.fld, *.gat)";
		filter.AddPattern("*.fld");
		filter.AddPattern("*.gat");
		openFileDialog.AddFilter(filter);

		filter = new FileFilter();
		filter.Name = "OpenKore Field Files (*.fld)";
		filter.AddPattern("*.fld");
		openFileDialog.AddFilter(filter);

		filter = new FileFilter();
		filter.Name = "Ragnarok Online Ground Files (*.gat)";
		filter.AddPattern("*.gat");
		openFileDialog.AddFilter(filter);

		filter = new FileFilter();
		filter.Name = "All Files (*)";
		filter.AddPattern("*");
		openFileDialog.AddFilter(filter);
	}

	private void setupSaveDialog() {
		FileFilter filter;
		Glade.XML xml = new Glade.XML(DataFiles.Glade("SaveDialog.glade"), null, null);

		xml.Autoconnect(this);

		filter = new FileFilter();
		filter.Name = "OpenKore Field Files (*.fld)";
		filter.AddPattern("*.fld");
		saveFileDialog.AddFilter(filter);
	}

	private void setupSelectedBlockType() {
		selectedBlockTypeChanging = true;
		((ListStore) selectedBlockType.Model).Clear();
		foreach (object o in Enum.GetValues(((Enum) BlockType.Walkable).GetType())) {
			selectedBlockType.AppendText(Field.BlockTypeToString((BlockType) o));
		}
		selectedBlockTypeChanging = false;
	}

	/********************************/

	/**
	 * Open a field file.
	 */
	private void Open(string filename) {
		Field field = null;
		string extension;

		extension = Path.GetExtension(filename.ToLower());
		switch (extension) {
		case ".fld":
			field = new FldField(filename);
			break;
		case ".gat":
			string rswFile = Path.ChangeExtension(filename, ".rsw");
			if (File.Exists(rswFile)) {
				field = new GatField(filename, rswFile);
			} else {
				field = new GatField(filename, null);
				ShowWarning("You are loading a .gat file. For optimal results, OpenKore Field " +
					"Editor needs the file " + Path.GetFileName(rswFile) +
					", which doesn't exist.\n\n" +
					"Please put " + Path.GetFileName(rswFile) + " in the same folder as " +
					Path.GetFileName(filename) + " if you know how to do that.");
			}
			break;
		default:
			ShowError("Unknown field file format.");
			break;
		}

		if (field != null) {
			fieldView.Field = field;
			this.filename = filename;
			Update();
		}
	}

	/**
	 * Save the current field file.
	 *
	 * @require fieldView.Field != null
	 */
	private void Save(string filename) {
		Field field;
		string extension;

		extension = Path.GetExtension(filename).ToLower();
		if ((extension == ".fld" && fieldView.Field is FldField)
		 || (extension == ".gat" && fieldView.Field is GatField)) {
			field = fieldView.Field; 
		} else {
			field = new FldField(fieldView.Field);
		}

		try {
			field.Save(filename);
			fieldView.Field = field;
			this.filename = filename;
			Update();
		} catch (SaveNotSupportedException e) {
			ShowError(e.Message);
		}
	}

	/**
	 * Called when a file has been opened or saved.
	 */
	private void Update() {
		window.Title = Path.GetFileName(filename) + " - OpenKore Field Editor";
		infoTable.Sensitive = saveMenu.Sensitive = saveAsMenu.Sensitive
			= saveAsButton.Sensitive = true;
		zoomInButton.Sensitive = fieldView.ZoomLevel < 20;
		zoomOutButton.Sensitive = fieldView.ZoomLevel > 1;
		widthEdit.Value = fieldView.Field.Width;
		heightEdit.Value = fieldView.Field.Height;
	}

	/**
	 * Show an error dialog.
	 */
	private void ShowError(string msg) {
		Dialog d = new MessageDialog(window, DialogFlags.Modal,
			MessageType.Error, ButtonsType.Ok,
			"{0}", msg);
		d.Resizable = false;
		d.Run();
		d.Destroy();
	}

	/**
	 * Show a warning dialog.
	 */
	private void ShowWarning(string msg) {
		Dialog d = new MessageDialog(window, DialogFlags.Modal,
			MessageType.Warning, ButtonsType.Ok,
			"{0}", msg);
		d.Resizable = false;
		d.Run();
		d.Destroy();
	}

	/**
	 * Show or hide the "(Mixed)" type in the 'Selected region' type combo box.
	 */
	private void ShowMixedType(bool show, bool activate) {
		int len;
		int count = 1;
		TreeIter iter;

		len = Enum.GetValues(((Enum) BlockType.Walkable).GetType()).Length;
		selectedBlockType.Model.GetIterFirst(out iter);
		while (selectedBlockType.Model.IterNext(ref iter)) {
			count++;
		}

		if (show && len == count) {
			selectedBlockType.AppendText("(Mixed)");
			if (activate) {
				selectedBlockType.Active = (int) count;
			}
		} else if (!show && len != count) {
			selectedBlockType.RemoveText(count - 1);
		}
	}

	/********* Callbacks *********/

	protected void OnDelete(object o, DeleteEventArgs args) {
		OnQuit(null, null);
	}

	protected void OnQuit(object o, EventArgs args) {
		Application.Quit();
	}

	protected void OnOpen(object o, EventArgs args) {
		if (openFileDialog == null) {
			setupOpenDialog();
		}

		ResponseType response = (ResponseType) openFileDialog.Run();
		openFileDialog.Hide();

		if (response == ResponseType.Ok) {
			Open(openFileDialog.Filename);
		}
	}

	protected void OnSave(object o, EventArgs args) {
		if (filename == null) {
			OnSaveAs(null, null);
		} else {
			Save(filename);
		}
	}

	protected void OnSaveAs(object o, EventArgs args) {
		if (saveFileDialog == null) {
			setupSaveDialog();
		}

		if (filename != null) {
			saveFileDialog.CurrentName = Path.GetFileName(filename);
		}
		ResponseType response = (ResponseType) saveFileDialog.Run();
		saveFileDialog.Hide();

		if (response == ResponseType.Ok) {
			string fn = saveFileDialog.Filename;
			if (Path.GetExtension(fn).ToLower() != ".fld") {
				fn += ".fld";
			}
			Save(fn);
		}
	}

	protected void OnAbout(object o, EventArgs args) {
		AboutBox.Present(window);
	}

	protected void OnWidthChanged(object o, EventArgs args) {
		fieldView.Field.Width = (uint) widthEdit.ValueAsInt;
	}

	protected void OnHeightChanged(object o, EventArgs args) {
		fieldView.Field.Height = (uint) heightEdit.ValueAsInt;
	}

	private void OnFieldMouseMove(FieldView sender, int x, int y) {
		if (x == -1 && y == -1) {
			currentCoord.Text = currentBlockType.Text = "-";
		} else {
			currentCoord.Text = String.Format("{0:d}, {1:d}", x, y);
			currentBlockType.Text = Field.BlockTypeToString(
				fieldView.Field.GetBlock((uint) x, (uint) y)
			);
		}
	}

	private void OnFieldSelectionChanged(FieldView sender, FieldSelection selection) {
		if (selection != null) {
			selectedBlockType.Sensitive = true;

			if (selection.Left != selection.Right || selection.Top != selection.Bottom) {
				// More than 1 block has been selected.
				selectedCoord.Text = String.Format("({0}, {1}) - ({2}, {3})",
					selection.Left, selection.Top,
					selection.Right, selection.Bottom);

				BlockType type = sender.Field.GetBlock(selection.Left, selection.Top);
				bool same = true;
				for (uint x = selection.Left; x <= selection.Right && same; x++) {
					for (uint y = selection.Bottom; y <= selection.Top && same; y++) {
						same = sender.Field.GetBlock(x, y) == type;
					}
				}

				selectedBlockTypeChanging = true;
				ShowMixedType(!same, !same);
				if (same) {
					selectedBlockType.Active = (int) type;
				}
				selectedBlockTypeChanging = false;

			} else {
				// Only 1 block is selected.
				selectedCoord.Text = String.Format("({0}, {1})",
					selection.Left, selection.Top);
				selectedBlockTypeChanging = true;
				ShowMixedType(false, false);
				selectedBlockType.Active = (int) sender.Field.GetBlock(selection.Left, selection.Top);
				selectedBlockTypeChanging = false;
			}

		} else {
			selectedCoord.Text = "-";
			selectedBlockType.Sensitive = false;
		}
	}

	protected void OnZoomIn(object o, EventArgs args) {
		fieldView.ZoomLevel++;
		zoomInButton.Sensitive = fieldView.ZoomLevel < 20;
		zoomOutButton.Sensitive = true;
		zoomLevelLabel.Text = String.Format("{0:d}x", fieldView.ZoomLevel);
	}

	protected void OnZoomOut(object o, EventArgs args) {
		fieldView.ZoomLevel--;
		zoomInButton.Sensitive = true;
		zoomOutButton.Sensitive = fieldView.ZoomLevel > 1;
		zoomLevelLabel.Text = String.Format("{0:d}x", fieldView.ZoomLevel);
	}
	
	protected void OnSelectedBlockTypeChanged(object o, EventArgs args) {
		int len = Enum.GetValues(((Enum) BlockType.Walkable).GetType()).Length;
		// Make sure we do nothing when user selected "(Mixed)",
		// or when the combo boxed is being automaticallychanged.
		if (selectedBlockType.Active < len && !selectedBlockTypeChanging) {
			BlockType type = (BlockType) selectedBlockType.Active;
			FieldSelection selection = fieldView.Selection;

			for (uint x = selection.Left; x <= selection.Right; x++) {
				for (uint y = selection.Bottom; y <= selection.Top; y++) {
					fieldView.Field.SetBlock(x, y, type);
				}
			}
			OnFieldSelectionChanged(fieldView, selection);
		}
	}
}

} // namespace FieldEditor