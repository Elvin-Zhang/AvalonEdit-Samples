namespace ThemedDemo.ViewModels
{
    using HL.Interfaces;
    using HL.Manager;
    using ICSharpCode.AvalonEdit;
    using ICSharpCode.AvalonEdit.Document;
    using ICSharpCode.AvalonEdit.Highlighting;
    using ICSharpCode.AvalonEdit.Utils;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Text;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Media;
    using TextEditLib.Enums;
    using ThemedDemo.ViewModels.Base;
    using UnitComboLib.Models.Unit;
    using UnitComboLib.Models.Unit.Screen;
    using UnitComboLib.ViewModels;

    public class DocumentRootViewModel : Base.ViewModelBase
    {
        #region fields
        private string _FilePath;

        private bool _IsDirty;
        private bool _IsReadOnly;
        private string _IsReadOnlyReason = string.Empty;

        private ICommand _HighlightingChangeCommand;
        private IHighlightingDefinition _HighlightingDefinition;
						
		private int _SynchronizedColumn, _SynchronizedLine;
		
		private Encoding _FileEncoding;
		private bool _WordWrap, _ShowLineNumbers;
        private ICommand _toggleEditorOptionCommand;
        private bool _IsContentLoaded;
        private ICommand _DisableHighlightingCommand;
		
		private readonly TextEditorOptions _TextOptions;
        private readonly TextDocument _Document;
        #endregion fields

        #region ctors
        /// <summary>
        /// Class constructor
        /// </summary>
        public DocumentRootViewModel()
        {
            _Document = new TextDocument(string.Empty);

            var items = new ObservableCollection<UnitComboLib.Models.ListItem>(GenerateScreenUnitList());
            SizeUnitLabel = UnitComboLib.UnitViewModeService.CreateInstance(items, new ScreenConverter(), 0);
			_FileEncoding = Encoding.Default;

			_TextOptions = new TextEditorOptions();
            _TextOptions.AllowToggleOverstrikeMode = true;
        }
        #endregion ctors

        #region properties
        /// <summary>
        /// Gets/sets the AvalonEdit document object that contains the text edit
		/// information being displayed in the editor control (text backend storage).
        /// </summary>		
        public TextDocument Document
        {
            get { return _Document; }
        }

        /// <summary>
        /// Gets/sets whether the file has been changed (edited) and should
		/// therefore, be saved on exit, or not.
        /// </summary>		
        public bool IsDirty
        {
            get { return _IsDirty; }
            set
            {
                if (_IsDirty != value)
                {
                    _IsDirty = value;
                    NotifyPropertyChanged(() => IsDirty);
                }
            }
        }

        /// <summary>
        /// Gets whether the document content (text) is pesent or not.
        /// </summary>
        public bool IsContentLoaded
        {
            get { return _IsContentLoaded; }

            protected set
            {
                if (_IsContentLoaded != value)
                {
                    _IsContentLoaded = value;
                    NotifyPropertyChanged(() => IsContentLoaded);
                }
            }
        }

        /// <summary>
        /// Gets/sets the path of the current file.
        /// </summary>		
        public string FilePath
        {
            get { return _FilePath; }
            set
            {
                if (_FilePath != value)
                {
                    _FilePath = value;

                    NotifyPropertyChanged(() => FilePath);
                }
            }
        }

        /// <summary>
        /// Gets/sets whether a file is readonly or not (can be edit and saved to).
        /// </summary>		
        public bool IsReadOnly
        {
            get
            {
                return _IsReadOnly;
            }

            protected set
            {
                if (_IsReadOnly != value)
                {
                    _IsReadOnly = value;
                    NotifyPropertyChanged(() => IsReadOnly);
                }
            }
        }

        /// <summary>
        /// Gets/sets a humanreadle string that describe why a file may not be edible
		/// if it appears to be available in readonly mode, only.
        /// </summary>		
        public string IsReadOnlyReason
        {
            get
            {
                return _IsReadOnlyReason;
            }

            protected set
            {
                if (_IsReadOnlyReason != value)
                {
                    _IsReadOnlyReason = value;
                    NotifyPropertyChanged(() => IsReadOnlyReason);
                }
            }
        }

        #region Highlighting Definition
        /// <summary>
        /// Gets a copy of all highlightings.
        /// </summary>
        public ReadOnlyCollection<IHighlightingDefinition> HighlightingDefinitions
        {
            get
            {
                var hlManager = GetService<IThemedHighlightingManager>();

                if (hlManager != null)
                    return hlManager.HighlightingDefinitions;

                return null;
            }
        }

        /// <summary>
        /// AvalonEdit exposes a Highlighting property that controls whether keywords,
        /// comments and other interesting text parts are colored or highlighted in any
        /// other visual way. This property exposes the highlighting information for the
        /// text file managed in this viewmodel class.
        /// </summary>
        public IHighlightingDefinition HighlightingDefinition
        {
            get
            {
                return _HighlightingDefinition;
            }

            set
            {
                if (_HighlightingDefinition != value)
                {
                    _HighlightingDefinition = value;
                    NotifyPropertyChanged(() => HighlightingDefinition);
                }
            }
        }

        /// <summary>
        /// Gets a command that changes the currently selected syntax highlighting in the editor.
        /// </summary>
        public ICommand HighlightingChangeCommand
        {
            get
            {
                if (_HighlightingChangeCommand == null)
                {
                    _HighlightingChangeCommand = new RelayCommand<object>((p) =>
                    {
                        var parames = p as object[];

                        if (parames == null)
                            return;

                        if (parames.Length != 1)
                            return;

                        var param = parames[0] as IHighlightingDefinition;
                        if (param == null)
                            return;

                        HighlightingDefinition = param;
                    });
                }

                return _HighlightingChangeCommand;
            }
        }

        /// <summary>
        /// Gets a command that turns off editors syntax highlighting.
        /// </summary>
        public ICommand DisableHighlightingCommand
        {
            get
            {
                if (_DisableHighlightingCommand == null)
                {
                    _DisableHighlightingCommand = new RelayCommand<object>(
                        (p) => { HighlightingDefinition = null; },
                        (p) =>
                        {
                            if (HighlightingDefinition != null)
                                return true;

                            return false;
                        }
                        );
                }

                return _DisableHighlightingCommand;
            }
        }
        #endregion Highlighting Definition


        /// <summary>
        /// Gets a scale viewmodel of the text in percentages of the font size
		/// See https://github.com/Dirkster99/UnitComboLib for more details.
        /// </summary>
        public IUnitViewModel SizeUnitLabel { get; }
		
        #region Synchronized Caret Position
        /// <summary>
        /// Gets/sets the caret positions column from the last time when the
        /// caret position in the left view has been synchronzied with the right view (or vice versa).
        /// </summary>
        public int SynchronizedColumn
        {
            get
            {
                return _SynchronizedColumn;
            }

            set
            {
                if (_SynchronizedColumn != value)
                {
                    _SynchronizedColumn = value;
                    NotifyPropertyChanged(() => SynchronizedColumn);
                }
            }
        }

        /// <summary>
        /// Gets/sets the caret positions line from the last time when the
        /// caret position in the left view has been synchronzied with the right view (or vice versa).
        /// </summary>
        public int SynchronizedLine
        {
            get
            {
                return _SynchronizedLine;
            }

            set
            {
                if (_SynchronizedLine != value)
                {
                    _SynchronizedLine = value;
                    NotifyPropertyChanged(() => SynchronizedLine);
                }
            }
        }
        #endregion Synchronized Caret Position

        /// <summary>
        /// Get/set file encoding of current text file.
        /// </summary>
        public Encoding FileEncoding
        {
            get { return _FileEncoding; }

            protected set
            {
                if (!Equals(_FileEncoding, value))
                {
                    _FileEncoding = value;
                    NotifyPropertyChanged(() => FileEncoding);
                    NotifyPropertyChanged(() => FileEncodingDescription);
                }
            }
        }

        /// <summary>
        /// Gets descriptive and human readable string of the file encoding used to load the data.
        /// </summary>
        public string FileEncodingDescription
        {
            get
            {
                return
                    string.Format("{0}, Header: {1} Body: {2}",
                    _FileEncoding.EncodingName, _FileEncoding.HeaderName, _FileEncoding.BodyName);
            }
        }

        /// <summary>
        /// Gets a command to toggle common editor options (such as WordWrap, Show Line Numbers).
        /// </summary>
        public ICommand ToggleEditorOptionCommand
        {
            get
            {
                return _toggleEditorOptionCommand ??
					(_toggleEditorOptionCommand = new RelayCommand<ToggleEditorOption>
					   ((p) => OnToggleEditorOption(p),
						(p) => { return OnToggleEditorOptionCanExecute(p); }
					   )
					);
            }
        }

        /// <summary>
        /// Get/set whether word wrap is currently activated or not.
        /// </summary>
        public bool WordWrap
        {
            get { return _WordWrap; }

            set
            {
                if (_WordWrap != value)
                {
                    _WordWrap = value;
                    NotifyPropertyChanged(() => WordWrap);
                }
            }
        }

        /// <summary>
        /// Get/set whether line numbers are currently shown or not.
        /// </summary>
        public bool ShowLineNumbers
        {
            get { return _ShowLineNumbers; }

            set
            {
                if (_ShowLineNumbers != value)
                {
                    _ShowLineNumbers = value;
                    NotifyPropertyChanged(() => ShowLineNumbers);
                }
            }
        }

        /// <summary>
        /// Get/set whether the end of each line is currently shown or not.
        /// </summary>
        public bool ShowEndOfLine               // Toggle state command
        {
            get { return TextOptions.ShowEndOfLine; }

            set
            {
                if (TextOptions.ShowEndOfLine != value)
                {
                    TextOptions.ShowEndOfLine = value;
                    NotifyPropertyChanged(() => ShowEndOfLine);
                }
            }
        }

        /// <summary>
        /// Get/set whether the spaces are highlighted or not.
        /// </summary>
        public bool ShowSpaces               // Toggle state command
        {
            get { return TextOptions.ShowSpaces; }

            set
            {
                if (TextOptions.ShowSpaces != value)
                {
                    TextOptions.ShowSpaces = value;
                    NotifyPropertyChanged(() => ShowSpaces);
                }
            }
        }

        /// <summary>
        /// Get/set whether the tabulator characters are highlighted or not.
        /// </summary>
        public bool ShowTabs               // Toggle state command
        {
            get { return TextOptions.ShowTabs; }

            set
            {
                if (TextOptions.ShowTabs != value)
                {
                    TextOptions.ShowTabs = value;
                    NotifyPropertyChanged(() => ShowTabs);
                }
            }
        }

        /// <summary>
        /// Get/Set texteditor options from <see cref="AvalonEdit"/> editor as <see cref="TextEditorOptions"/> instance.
        /// </summary>
        public TextEditorOptions TextOptions
        {
            get { return _TextOptions; }
        }
        #endregion properties

        #region methods
        /// <summary>
        /// Loads a text document from the persistance of the file system
		/// and updates all corresponding states in this viewmodel.
        /// </summary>
		/// <param name="paramFilePath"></param>
        /// <returns></returns>
        internal bool LoadDocument(string paramFilePath)
        {
            if (File.Exists(paramFilePath))
            {
                IsDirty = false;
                IsReadOnly = false;

                // Check file attributes and set to read-only if file attributes indicate that
                if ((System.IO.File.GetAttributes(paramFilePath) & FileAttributes.ReadOnly) != 0)
                {
                    IsReadOnly = true;
                    IsReadOnlyReason = "This file cannot be edit because another process is currently writting to it.\n" +
                                       "Change the file access permissions or save the file in a different location if you want to edit it.";
                }

				try
				{
                    var fileEncoding = GetEncoding(paramFilePath);
                    
                    using (FileStream fs = new FileStream(paramFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        using (StreamReader reader = FileReader.OpenStream(fs, fileEncoding))
                        {
                            Document.Text = reader.ReadToEnd();
                            FileEncoding = reader.CurrentEncoding; // assign encoding after ReadToEnd() so that the StreamReader can autodetect the encoding
                        }
                    }

                    FilePath = paramFilePath;
                    IsContentLoaded = true;

                    // Setting this to null and then to some useful value ensures that the Foldings work
                    // Installing Folding Manager is invoked via HighlightingChange
                    // (so this works even when changing from test.XML to test1.XML)
                    HighlightingDefinition = null;
                    var hlManager = GetService<IThemedHighlightingManager>();
                    string extension = System.IO.Path.GetExtension(paramFilePath);
                    HighlightingDefinition = hlManager.GetDefinitionByExtension(extension);
                }
                catch (System.Exception exc)
				{
                    IsReadOnly = true;
                    IsReadOnlyReason = exc.Message;
					Document.Text = string.Empty;

                    FilePath = string.Empty;
                    IsContentLoaded = false;
                    HighlightingDefinition = null;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines a text file's encoding by analyzing its byte order mark (BOM).
        /// Defaults to ASCII when detection of the text file's endianness fails.
        /// 
        /// source: https://stackoverflow.com/questions/3825390/effective-way-to-find-any-files-encoding
        /// </summary>
        /// <param name="filename">The text file to analyze.</param>
        /// <returns>The detected encoding.</returns>
        public static Encoding GetEncoding(string filename)
        {
            // Read the BOM
            var bom = new byte[4];
            using (var file = new FileStream(filename, FileMode.Open, FileAccess.Read))
            {
                file.Read(bom, 0, 4);
            }

            // Analyze the BOM
            if (bom[0] == 0x2b && bom[1] == 0x2f && bom[2] == 0x76)
                return Encoding.UTF7;

            if (bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf)
                return Encoding.UTF8;

            if (bom[0] == 0xff && bom[1] == 0xfe)
                return Encoding.Unicode; //UTF-16LE

            if (bom[0] == 0xfe && bom[1] == 0xff)
                return Encoding.BigEndianUnicode; //UTF-16BE

            if (bom[0] == 0 && bom[1] == 0 && bom[2] == 0xfe && bom[3] == 0xff)
                return Encoding.UTF32;

            return Encoding.Default;
        }

        /// <summary>
        /// Initialize Scale View with useful units in percent and font point size
        /// </summary>
        /// <returns></returns>
        private IEnumerable<UnitComboLib.Models.ListItem> GenerateScreenUnitList()
        {
            List<UnitComboLib.Models.ListItem> unitList = new List<UnitComboLib.Models.ListItem>();

            var percentDefaults = new ObservableCollection<string>() { "25", "50", "75", "100", "125", "150", "175", "200", "300", "400", "500" };
            var pointsDefaults = new ObservableCollection<string>() { "3", "6", "8", "9", "10", "12", "14", "16", "18", "20", "24", "26", "32", "48", "60" };

            unitList.Add(new UnitComboLib.Models.ListItem(Itemkey.ScreenPercent, "Percent", "%", percentDefaults));
            unitList.Add(new UnitComboLib.Models.ListItem(Itemkey.ScreenFontPoints, "Point", "pt", pointsDefaults));

            return unitList;
        }
		
		#region ToggleEditorOption
        private void OnToggleEditorOption(object parameter)
        {
            if (parameter == null)
                return;

            if ((parameter is ToggleEditorOption) == false)
                return;

            ToggleEditorOption t = (ToggleEditorOption)parameter;

            switch (t)
            {
                case ToggleEditorOption.WordWrap:
                    this.WordWrap = !this.WordWrap;
                    break;

                case ToggleEditorOption.ShowLineNumber:
                    this.ShowLineNumbers = !this.ShowLineNumbers;
                    break;

                case ToggleEditorOption.ShowSpaces:
                    this.TextOptions.ShowSpaces = !this.TextOptions.ShowSpaces;
                    break;

                case ToggleEditorOption.ShowTabs:
                    this.TextOptions.ShowTabs = !this.TextOptions.ShowTabs;
                    break;

                case ToggleEditorOption.ShowEndOfLine:
                    this.TextOptions.ShowEndOfLine = !this.TextOptions.ShowEndOfLine;
                    break;

                default:
                    break;
            }
        }

        private bool OnToggleEditorOptionCanExecute(object parameter)
        {
            if (parameter == null)
                return false;

            if (parameter is ToggleEditorOption)
                return true;

            return false;
        }
        #endregion ToggleEditorOption

        /// <summary>
        /// Invoke this method to apply a change of theme to the content of the document
        /// (eg: Adjust the highlighting colors when changing from "Dark" to "Light"
        ///      WITH current text document loaded.)
        /// </summary>
        internal void OnAppThemeChanged(IThemedHighlightingManager hlManager)
        {
            if (hlManager == null)
                return;

            // Does this highlighting definition have an associated highlighting theme?
            if (hlManager.CurrentTheme.HlTheme != null)
            {
                // A highlighting theme with GlobalStyles?
                // Apply these styles to the resource keys of the editor
                foreach (var item in hlManager.CurrentTheme.HlTheme.GlobalStyles)
                {
                    switch (item.TypeName)
                    {
                        case "DefaultStyle":
                            ApplyToDynamicResource(TextEditLib.Themes.ResourceKeys.EditorBackground, item.backgroundcolor);
                            ApplyToDynamicResource(TextEditLib.Themes.ResourceKeys.EditorForeground, item.foregroundcolor);
                            break;

                        case "CurrentLineBackground":
                            ApplyToDynamicResource(TextEditLib.Themes.ResourceKeys.EditorCurrentLineBackgroundBrushKey, item.backgroundcolor);
                            ApplyToDynamicResource(TextEditLib.Themes.ResourceKeys.EditorCurrentLineBorderBrushKey, item.bordercolor);
                            break;

                        case "LineNumbersForeground":
                            ApplyToDynamicResource(TextEditLib.Themes.ResourceKeys.EditorLineNumbersForeground, item.foregroundcolor);
                            break;

                        case "Selection":
                            ApplyToDynamicResource(TextEditLib.Themes.ResourceKeys.EditorSelectionBrush, item.backgroundcolor);
                            ApplyToDynamicResource(TextEditLib.Themes.ResourceKeys.EditorSelectionBorder, item.bordercolor);
                            break;

                        case "Hyperlink":
                            ApplyToDynamicResource(TextEditLib.Themes.ResourceKeys.EditorLinkTextBackgroundBrush, item.backgroundcolor);
                            ApplyToDynamicResource(TextEditLib.Themes.ResourceKeys.EditorLinkTextForegroundBrush, item.foregroundcolor);
                            break;

                        case "NonPrintableCharacter":
                            ApplyToDynamicResource(TextEditLib.Themes.ResourceKeys.EditorNonPrintableCharacterBrush, item.foregroundcolor);
                            break;

                        default:
                            throw new System.ArgumentOutOfRangeException("GlobalStyle named '{0}' is not supported.", item.TypeName);
                    }
                }
            }

            // 1st try: Find highlighting based on currently selected highlighting
            // The highlighting name may be the same as before, but the highlighting theme has just changed
            if (HighlightingDefinition != null)
            {
                // Reset property for currently select highlighting definition
                HighlightingDefinition = hlManager.GetDefinition(HighlightingDefinition.Name);

                if (HighlightingDefinition != null)
                    return;
            }

            // 2nd try: Find highlighting based on extension of file currenlty being viewed
            if (string.IsNullOrEmpty(FilePath))
                return;

            string extension = System.IO.Path.GetExtension(FilePath);

            if (string.IsNullOrEmpty(extension))
                return;

            // Reset property for currently select highlighting definition
            HighlightingDefinition = hlManager.GetDefinitionByExtension(extension);
        }

        /// <summary>
        /// Re-define an existing <seealso cref="SolidColorBrush"/> and backup the originial color
        /// as it was before the application of the custom coloring.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="newColor"></param>
        private void ApplyToDynamicResource(ComponentResourceKey key, Color? newColor)
        {
            if (Application.Current.Resources[key] == null || newColor == null)
                return;

            // Re-coloring works with SolidColorBrushs linked as DynamicResource
            if (Application.Current.Resources[key] is SolidColorBrush)
            {
                //backupDynResources.Add(resourceName);

                var newColorBrush = new SolidColorBrush((Color)newColor);
                newColorBrush.Freeze();

                Application.Current.Resources[key] = newColorBrush;
            }
        }
        #endregion methods
    }
}

