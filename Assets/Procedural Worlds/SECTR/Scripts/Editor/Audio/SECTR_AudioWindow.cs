// Copyright (c) 2014 Make Code Now! LLC

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class SECTR_AudioWindow : SECTR_Window 
{

    private int scaledScreenWidth;
    private int scaledScreenHeight;

    #region Private Details
    private class TreeItem
	{
		#region Private Details
		private string path;
		private SECTR_AudioBus bus;
		private SECTR_AudioCue cue;
		private AudioClip clip;
		private AudioImporter importer;
		#endregion

		#region Public Interface
		public Rect ScrollRect;
		public Rect WindowRect;
		public bool Expanded = true;
		public bool Rename = false;
		public string Name;

		public TreeItem(SECTR_AudioWindow window, SECTR_AudioCue cue, string path)
		{
			this.cue = cue;
			this.path = path;
			this.Name = cue.name;
			window.hierarchyItems.Add(AsObject, this);
		}

		public TreeItem(SECTR_AudioWindow window, SECTR_AudioBus bus, string path)
		{
			this.bus = bus;
			this.path = path;
			this.Name = bus.name;
			this.Expanded = EditorPrefs.GetBool(expandedPrefPrefix + this.path, true);
			window.hierarchyItems.Add(AsObject, this);
		}

		public TreeItem(AudioImporter importer, string path, string name)
		{
			this.importer = importer;
			this.path = path;
			this.Name = name;
		}

		public string Path 				{ get { return path; } }
		public SECTR_AudioBus Bus 		{ get { return bus; } }
		public SECTR_AudioCue Cue 		{ get { return cue; } }
		public AudioImporter Importer 	{ get { return importer; } }
		public AudioClip Clip 			
		{
			get 
			{
				if(importer && !clip)
				{
					clip = (AudioClip)AssetDatabase.LoadAssetAtPath(path, typeof(AudioClip));
				}
				return clip;
			}
		}

		public string DefaultName
		{
			get
			{
				if(bus) return bus.name;
				if(cue) return cue.name;
				if(importer) return System.IO.Path.GetFileName(path);
				return "";
			}
		}

		public Object AsObject
		{
			get
			{
				if(bus) return bus;
				if(cue) return cue;
				if(importer) return importer;
				return null;
			}
		}
		#endregion
	}

	private class AudioClipFolder
	{
		public List<TreeItem> items = new List<TreeItem>(32);
		public bool expanded = true;
	}

	private class Splitter
	{
		public int thickness = 5;
		public int pos = 0;
		public bool vertical = true;
		public Rect rect;
		public bool dragging = false;

		public Splitter(int pos, bool vertical)
		{
			this.pos = pos;
			this.vertical = vertical;
		}

		public void Draw(SECTR_AudioWindow window)
		{
			if(vertical)
			{
				rect = new Rect(pos, 0f, thickness, window.showClipList ? window.bottomSplitter.pos : Screen.height);
			}
			else
			{
				rect = new Rect(0f, pos, Screen.width, thickness);
			}

			GUI.Box(rect, GUIContent.none);
		}

		public bool HandleEvents(SECTR_AudioWindow parent)
		{
			bool handledEvent = false;
			switch(Event.current.type)
			{
			case EventType.MouseDown:
				if(rect.Contains(Event.current.mousePosition)) 
				{
					dragging = true;
					handledEvent = true;
				}
				break;
			case EventType.MouseDrag:
				if(dragging)
				{
					pos += vertical ? (int)Event.current.delta.x :  (int)Event.current.delta.y;
					handledEvent = true;
				}
				break;
			case EventType.MouseUp:
				dragging = false;
				break;
			}

			if(dragging || rect.Contains(Event.current.mousePosition))
			{
				EditorGUIUtility.AddCursorRect(new Rect(Event.current.mousePosition.x - 16, Event.current.mousePosition.y - 16, 32, 32), 
				                               vertical ? MouseCursor.ResizeHorizontal : MouseCursor.ResizeVertical);
			}

			return handledEvent;
		}

        public void UpdateCursor()
        {
            if (Event.current != null)
            {
                if (rect.Contains(Event.current.mousePosition))
                {
                    EditorGUIUtility.AddCursorRect(new Rect(Event.current.mousePosition.x - 16, Event.current.mousePosition.y - 16, 32, 32),
                                                   vertical ? MouseCursor.ResizeHorizontal : MouseCursor.ResizeVertical);
                }
            }
        }

	}

	private enum SampleBehavior
	{
		PreserveRate,
		OptimizeRate,
		Force8k,
		Force11k,
		Force22k,
		Force44k,
		Force48k,
		Force96k,
		Force192k,
	}

	private Vector2 treeScrollPos;
	private Vector2 propertyScrollPos;
	private Vector2 clipScrollPos;
	private Vector2 busScrollPos;

	private string treeSearchString = "";
	private string clipSearchString = "";
	private string busSearchString = "";

	private Splitter leftSplitter = new Splitter(-1, true);
	private Splitter bottomSplitter = new Splitter(-1, false);

	private int lastWidth = 0;
	private int lastHeight = 0;
	private int indent = 0;

	private TreeItem selectedHierarchyItem = null;
	private List<TreeItem> selectedHierarchyItems = new List<TreeItem>();
	private List<TreeItem> displayedHierarchyItems = new List<TreeItem>();
	private TreeItem selectedClipItem = null;
	private List<TreeItem> selectedClipItems = new List<TreeItem>();
	private List<TreeItem> displayedClipItems = new List<TreeItem>();
	private TreeItem dragHoverItem = null;
	private TreeItem dragHierarchyItem = null;
	private TreeItem dragClipItem = null;
	private bool lastSelectedHierarchy = true;
	private bool changedAudioClip = false;
	private Rect clipScrollRect;
	private SECTR_Editor propertyEditor = null;
	
	private Dictionary<string, AudioClipFolder> clipItems = new Dictionary<string, AudioClipFolder>(256);
	private Dictionary<Object, TreeItem> hierarchyItems = new Dictionary<Object, TreeItem>(256);
	private List<TreeItem> deadItems = new List<TreeItem>();
	private List<SECTR_AudioBus> newBuses = new List<SECTR_AudioBus>();
	private List<SECTR_AudioCue> newCues = new List<SECTR_AudioCue>();

	// Editor state vars
	private bool showFullClipDetails = false;
	private bool showProperties = true;
	private bool showHierarchy = true;
	private bool showClipList = true;
	private string audioRootPath = null;

	// Styles and Icons
	private GUIStyle busSliderStyle = null;
	private GUIStyle busFieldStyle = null;
	private Texture2D playIcon;
	private Texture2D cueIcon;
	private Texture2D busIcon;
	private Texture2D expandedIcon;
	private Texture2D collapsedIcon;
	private Texture2D folderIcon;
	private Texture2D soloOnIcon;
	private Texture2D soloOffIcon;
	private Texture2D muteOnIcon;
	private Texture2D muteOffIcon;
	private static Dictionary<string, Texture2D> iconCache = new Dictionary<string, Texture2D>(12);

	// baking
	SECTR_ComputeRMS bakeMaster = null;
	bool wasBaking = false;

	private const string rootPrefPrefix = "SECTR_Audio_Root_";
	private const string expandedPrefPrefix = "SECTR_Audio_Expanded_";
	private const string showPrefPrefix = "SECTR_Audio_Show_";
	private const string fullDetailsPref = "SECTR_Audio_FullClip";
	#endregion

	#region Public Interface
	public static Texture2D LoadIcon(string iconName)
	{
		if(iconCache.ContainsKey(iconName))
		{
			return iconCache[iconName];
		}

        // Look for each icon first in the default path, only do a full search if we don't find it there.
        // Full search would be required if someone imports the library into a non-standard place.
        string path = SECTR_SectorUtils.GetSectrDirectory() + SECTR_Constants.PATH_AudioIcons + iconName;
        Texture2D icon = (Texture2D)AssetDatabase.LoadAssetAtPath(path, typeof(Texture2D));
		if(!icon)
		{
			icon = SECTR_Asset.Find<Texture2D>(iconName);
		}
		iconCache[iconName] = icon;
		return icon;
	}
	#endregion

	#region Unity Interface
	void OnEnable()
	{
		playIcon = LoadIcon("PlayIcon.psd");
		cueIcon = LoadIcon("CueIcon.psd");
		busIcon = LoadIcon("BusIcon.psd");
		expandedIcon = LoadIcon("ExpandedIcon.psd");
		collapsedIcon = LoadIcon("CollapsedIcon.psd");
		folderIcon = LoadIcon("FolderIcon.psd");
		soloOnIcon = LoadIcon("SoloOnIcon.psd");
		soloOffIcon = LoadIcon("SoloOffIcon.psd");
		muteOnIcon = LoadIcon("MuteOnIcon.psd");
		muteOffIcon = LoadIcon("MuteOffIcon.psd");

		showFullClipDetails = UnityEditor.EditorPrefs.GetBool(fullDetailsPref, false);
		showHierarchy = UnityEditor.EditorPrefs.GetBool(showPrefPrefix + "Hierarchy", true);
		showProperties = UnityEditor.EditorPrefs.GetBool(showPrefPrefix + "Properties", true);
		showClipList = UnityEditor.EditorPrefs.GetBool(showPrefPrefix + "ClipList", true);

		audioRootPath = UnityEditor.EditorPrefs.GetString(rootPrefPrefix + SECTR_Asset.GetProjectName(), "");
		if(string.IsNullOrEmpty(audioRootPath))
		{
			if(EditorUtility.DisplayDialog("Welcome to SECTR AUDIO!", 
			                               "Do you store all of your audio files beneath a single folder? If so, please tell us which one. If not, don't worry, we can search your whole project quickly. You can always set a new root audio folder in the Audio Window right click menu.",
			                               "Yes",
			                               "No"))
			{
				_SelectAudioRoot();
			}

			if(string.IsNullOrEmpty(audioRootPath))			
			{
				audioRootPath = Application.dataPath;
				UnityEditor.EditorPrefs.SetString(rootPrefPrefix + SECTR_Asset.GetProjectName(), audioRootPath);
			}
		}


		if(hierarchyItems.Count == 0)
		{
			_RefreshAssets();
		}
	}

	void OnDestroy()
	{
		hierarchyItems.Clear();
		clipItems.Clear();
		SECTR_AudioSystem.StopSoloing();
	}

	void Update()
	{
		int numDeadItems = deadItems.Count;
		for(int itemIndex = 0; itemIndex < numDeadItems; ++itemIndex)
		{
			hierarchyItems.Remove(deadItems[itemIndex].AsObject);
		}
		
		int numNewBuses = newBuses.Count;
		for(int busIndex = 0; busIndex < numNewBuses; ++busIndex)
		{
			SECTR_AudioBus bus = newBuses[busIndex];
			hierarchyItems[bus] = new TreeItem(this, bus, AssetDatabase.GetAssetPath(bus)); 
		}
		
		int numNewCues = newCues.Count;
		for(int cueIndex = 0; cueIndex < numNewCues; ++cueIndex)
		{
			SECTR_AudioCue cue = newCues[cueIndex];
			hierarchyItems[cue] = new TreeItem(this, cue, AssetDatabase.GetAssetPath(cue)); 
		}
		
		if(numDeadItems > 0 || numNewBuses > 0 || numNewCues > 0)
		{
			Repaint();
		}

		deadItems.Clear();
		newBuses.Clear();
		newCues.Clear();

		if(bakeMaster)
		{
			Repaint();
		}

        leftSplitter.UpdateCursor();
        bottomSplitter.UpdateCursor();

	}

	protected override void OnGUI()
	{
        base.OnGUI();

        //96 is a default value in WPF for 100% DPI scaling.
        scaledScreenHeight = (int)(Screen.height * (96 / Screen.dpi));
        scaledScreenWidth = (int)(Screen.width * (96 / Screen.dpi));


        if (busSliderStyle == null)
		{
			busSliderStyle = new GUIStyle(GUI.skin.verticalSlider);
			busSliderStyle.alignment = TextAnchor.MiddleCenter;
		}

		if(busFieldStyle == null)
		{
			busFieldStyle = new GUIStyle(EditorStyles.textField);
			busFieldStyle.alignment = TextAnchor.MiddleCenter;
		}

		if(leftSplitter.pos == -1)
		{
			leftSplitter.pos = (int)(position.width * 0.3f);
		}
		if(bottomSplitter.pos == -1)
		{
			bottomSplitter.pos = (int)(position.height * 0.6f);
		}

		if(lastWidth == 0 && lastHeight == 0)
		{
			lastWidth = (int)position.width;
			lastHeight = (int)position.height;
		}
		else if(position.width != lastWidth || position.height != lastHeight)
		{
			float leftFrac = leftSplitter.pos / (float)lastWidth;
			float bottomFrac = bottomSplitter.pos / (float)position.height;
			leftSplitter.pos = (int)(position.width * leftFrac);
            bottomSplitter.pos = (int)(position.height * bottomFrac);
            lastWidth = (int)position.width;
			lastHeight = (int)position.height;
		}

		displayedHierarchyItems.Clear();
		displayedClipItems.Clear();

		if(showHierarchy)
		{
			_DrawHierarchy();
			if(showProperties)
			{
				leftSplitter.Draw(this);
			}
		}
		if(showProperties)
		{
			_DrawProperties();
		}
		if(showClipList)
		{
			if(showProperties || showHierarchy)
			{
				bottomSplitter.Draw(this);
			}
			_DrawClipList();
		}

		if(bakeMaster)
		{
			float progress = bakeMaster.Progress;
			EditorUtility.DisplayProgressBar("Baking HDR Audio Data", "Please don't leave the Editor window during HDR baking.", progress);
			wasBaking = true;
		}
		else if(wasBaking)
		{
			EditorUtility.ClearProgressBar();
			wasBaking = false;
		}

		_HandleEvents();
	}
	#endregion

	#region Private Methods
	private void _RefreshAssets()
	{
		clipItems.Clear();
		hierarchyItems.Clear();

		List<string> assetExtensions = new List<string>()	{ ".asset", };
		List<string> clipExtensions = new List<string>()	{ ".wav", ".aif", ".aiff", ".ogg", ".mp3", ".xm", ".mod", ".it", ".xm", };
		List<string> paths = new List<string>(128);

		// Add all Buses
		List<SECTR_AudioBus> buses = SECTR_Asset.GetAll<SECTR_AudioBus>(audioRootPath, assetExtensions, ref paths, false);
		for(int busIndex = 0; busIndex < buses.Count; ++busIndex)
		{
			SECTR_AudioBus bus = buses[busIndex];
			if(bus != null)
			{
				new TreeItem(this, bus, paths[busIndex]);
			}
		}

		// Add all Cues
		List<SECTR_AudioCue> cues = SECTR_Asset.GetAll<SECTR_AudioCue>(audioRootPath, assetExtensions, ref paths, false);
		for(int cueIndex = 0; cueIndex < cues.Count; ++cueIndex)
		{
			SECTR_AudioCue cue = cues[cueIndex];
			if(cue != null)
			{
				new TreeItem(this, cue, paths[cueIndex]);
			}
		}

		// Build the list of AudioClips
		SECTR_Asset.GetAll<AudioClip>(audioRootPath, clipExtensions, ref paths, true);
		for(int pathIndex = 0; pathIndex < paths.Count; ++pathIndex)
		{
			string path = paths[pathIndex];
			if(!string.IsNullOrEmpty(path))
			{
				string dirPath = "";
				string fileName = "";
				SECTR_Asset.SplitPath(path, out dirPath, out fileName);

				TreeItem item = new TreeItem((AudioImporter)AssetImporter.GetAtPath(path), path, fileName);

				AudioClipFolder folder;
				if(!clipItems.TryGetValue(dirPath, out folder))
				{
					folder = new AudioClipFolder();
					bool userExpanded = EditorPrefs.GetBool(expandedPrefPrefix + dirPath, false);
					folder.expanded = userExpanded;
					clipItems.Add(dirPath, folder);
				}
				folder.items.Add(item);
			}
		}
	}
	
	private void _DrawHierarchy()
	{
		GUILayout.BeginArea(new Rect(0f,
		                             0f,
		                             showProperties ? leftSplitter.pos : scaledScreenWidth,
		                             showClipList ? bottomSplitter.pos : scaledScreenHeight));

		Rect headerRect = DrawHeader("HIERARCHY", ref treeSearchString, leftSplitter.pos / 2, false);

		treeScrollPos = EditorGUILayout.BeginScrollView(treeScrollPos);

		// Back up initial indent b/c DrawTreeItem will start by indenting.
		string searchString = treeSearchString.ToLower();
		indent = 0;

		foreach(TreeItem item in hierarchyItems.Values)
		{
			if(item.Cue != null || item.Bus != null)
			{
				if((item.Cue && item.Cue.Bus == null) || (item.Bus && item.Bus.Parent == null))
				{
					_DrawHierarchyItem(item, headerRect.height, searchString);
				}
			}
			else
			{
				deadItems.Add(item);
			}
		}

		EditorGUILayout.EndScrollView();
		GUILayout.EndArea();
	}

	private void _DrawHierarchyItem(TreeItem item, float initialOffset, string searchString)
	{
		if(item != null && (item.Bus || item.Cue) && _ChildVisible(item, searchString))
		{
			item.ScrollRect = EditorGUILayout.BeginHorizontal();
			bool selected = selectedHierarchyItems.Contains(item);
			if(selected && lastSelectedHierarchy)
			{
				Rect selectionRect = item.ScrollRect;
				selectionRect.y += 3;
				GUI.Box(selectionRect, "", selectionBoxStyle);
			}
			item.WindowRect = item.ScrollRect;
			item.WindowRect.y += initialOffset;
			item.WindowRect.y -= treeScrollPos.y;
			GUILayout.Label(GUIContent.none, GUILayout.Width(indent), GUILayout.MaxWidth(indent), GUILayout.MinWidth(indent), GUILayout.ExpandWidth(false));

			elementStyle.alignment = TextAnchor.MiddleLeft;
			if(item == dragHoverItem || selected)
			{
				elementStyle.normal.textColor = Color.white;
			}
			else
			{
				if(item.Cue && item.Cue.IsTemplate)
				{
					elementStyle.normal.textColor = Color.blue;
				}
				else if(item.Cue && item.Cue.Template != null)
				{
					elementStyle.normal.textColor = Color.yellow;
				}
				else
				{
					elementStyle.normal.textColor = UnselectedItemColor;
				}
			}

			int iconSize = lineHeight;
			if(item.Bus && (item.Bus.Children.Count > 0 || item.Bus.Cues.Count > 0))
			{
				if(GUILayout.Button(item.Expanded ? expandedIcon : collapsedIcon, elementStyle, GUILayout.Width(iconSize), GUILayout.Height(iconSize)))
				{
					item.Expanded = !item.Expanded;
					EditorPrefs.SetBool(expandedPrefPrefix + item.Path, item.Expanded);
				}
			}
			else
			{
				GUILayout.Button(GUIContent.none, elementStyle, GUILayout.Width(iconSize), GUILayout.Height(iconSize));
			}

			bool wasEnabled = GUI.enabled;
			GUI.enabled &= SECTR_VC.IsEditable(item.Path);

			Texture typeIcon = null;
			if(item.Bus != null) typeIcon = busIcon;
			if(item.Cue != null) typeIcon = cueIcon;
			if(item.Importer != null) typeIcon = playIcon;
			if(typeIcon)
			{
				GUILayout.Label(typeIcon, elementStyle, GUILayout.Width(iconSize), GUILayout.Height(iconSize));
			}
			if(item.Rename)
			{
				string focusName = "RenamingItem";
				GUI.SetNextControlName(focusName);
				item.Name = GUILayout.TextField(item.Name);
				GUI.FocusControl(focusName);
			}
			else
			{
				GUILayout.Label(item.Name, elementStyle);
			}
			EditorGUILayout.EndHorizontal();
			GUI.enabled = wasEnabled;

			displayedHierarchyItems.Add(item);

			if(item.Expanded && item.Bus)
			{
				indent += iconSize / 2;
				foreach(SECTR_AudioBus bus in item.Bus.Children)
				{
					if(bus != null)
					{
						if(hierarchyItems.ContainsKey(bus))
						{
							_DrawHierarchyItem(hierarchyItems[bus], initialOffset, searchString);
						}
						else if(!newBuses.Contains(bus))
						{
							newBuses.Add(bus);
						}
					}
				}
				foreach(SECTR_AudioCue cue in item.Bus.Cues)
				{
					if(cue != null)
					{
						if(hierarchyItems.ContainsKey(cue))
						{
							_DrawHierarchyItem(hierarchyItems[cue], initialOffset, searchString);
						}
						else if(!newCues.Contains(cue))
						{
							newCues.Add(cue);
						}
					}
				}
				indent -= iconSize / 2;
			}
		}
	}

	private bool _ChildVisible(TreeItem item, string searchString)
	{
		if(item == null)
		{
			return false;
		}
		else if(string.IsNullOrEmpty(treeSearchString) || item.Name.ToLower().Contains(searchString))
		{
			return true;
		}
		else if(item.Expanded && item.Bus)
		{
			foreach(SECTR_AudioBus bus in item.Bus.Children)
			{
				if(_ChildVisible(hierarchyItems[bus], searchString))
				{
					return true;
				}
			}
			foreach(SECTR_AudioCue cue in item.Bus.Cues)
			{
				if(_ChildVisible(hierarchyItems[cue], searchString))
				{
					return true;
				}
			}
		}

		return false;
	}

	private void _DrawProperties()
	{
		int width = showHierarchy ? scaledScreenWidth - leftSplitter.pos - leftSplitter.thickness :scaledScreenWidth;
		GUILayout.BeginArea(new Rect(showHierarchy ? leftSplitter.pos + leftSplitter.thickness : 0f,
		                             0f,
		                             width,
		                             showClipList ? bottomSplitter.pos : scaledScreenHeight));

		bool allCues = selectedHierarchyItems.Count > 0;
		bool allBuses = selectedHierarchyItems.Count > 0;
		foreach(TreeItem item in selectedHierarchyItems)
		{
			allCues &= item.Cue != null;
			allBuses &= item.Bus != null;
		}

		if(allBuses)
		{
			DrawHeader("BUSES", ref busSearchString, (scaledScreenWidth - leftSplitter.pos) / 4, true);
			EditorGUILayout.Space();
			busScrollPos = EditorGUILayout.BeginScrollView(busScrollPos);
			EditorGUILayout.BeginHorizontal();
			string searchString = busSearchString.ToLower();

			List<TreeItem> drawnBuses = new List<TreeItem>(selectedHierarchyItems.Count);
			foreach(TreeItem item in hierarchyItems.Values)
			{
				if(selectedHierarchyItems.Contains(item) && !drawnBuses.Contains(item))
				{
					_DrawBus(item, searchString, drawnBuses);
				}
			}
			
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.Space();
			EditorGUILayout.EndScrollView();
		}
		else if(allCues)
		{
			string nullSearch = null;
			if(selectedHierarchyItems.Count > 1)
			{
				DrawHeader("PROPERTIES (" + selectedHierarchyItems.Count + " SELECTED)", ref nullSearch, 0, true);
			}
			else
			{
				DrawHeader("PROPERTIES (" + selectedHierarchyItem.Cue.name + ")", ref nullSearch, 0, true);
			}

			propertyScrollPos = EditorGUILayout.BeginScrollView(propertyScrollPos);
			bool wasEnabled = GUI.enabled;
			GUI.enabled &= SECTR_VC.IsEditable(selectedHierarchyItem.Path);
			if(selectedHierarchyItem.Cue && (propertyEditor == null || propertyEditor.target != selectedHierarchyItem.Cue))
			{
				int numSelected = selectedHierarchyItems.Count;
				SECTR_AudioCueEditor cueEditor = null;
				if(numSelected > 1)
				{
					SECTR_AudioCue[] cues = new SECTR_AudioCue[selectedHierarchyItems.Count];
					for(int selectedIndex = 0; selectedIndex < numSelected; ++selectedIndex)
					{
						cues[selectedIndex] = selectedHierarchyItems[selectedIndex].Cue;
					}
					cueEditor = (SECTR_AudioCueEditor)Editor.CreateEditor(cues);
				}
				else
				{
					cueEditor = (SECTR_AudioCueEditor)Editor.CreateEditor(selectedHierarchyItem.Cue);
				}
				cueEditor.DrawBus = false;
				propertyEditor = cueEditor;
			}
			else if(selectedHierarchyItem.Bus && (propertyEditor == null || propertyEditor.target != selectedHierarchyItem.Bus))
			{
				propertyEditor = (SECTR_Editor)Editor.CreateEditor(selectedHierarchyItem.Bus);
			}
			if(propertyEditor)
			{
				propertyEditor.WidthOverride = width;
				propertyEditor.OnInspectorGUI();
			}
			GUI.enabled = wasEnabled;
			EditorGUILayout.EndScrollView();
		}
		else if(selectedHierarchyItems.Count > 0)
		{
			string nullSearch = null;
			DrawHeader("MIXED SELECTION", ref nullSearch, 0, true);
			bool wasEnabled = GUI.enabled;
			GUI.enabled = false;
			GUILayout.Button("Cannot display Buses and Cues simultaneously.", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
			GUI.enabled = wasEnabled;
		}
		else
		{
			string nullSearch = null;
			DrawHeader("NO SELECTION", ref nullSearch, 0, true);
			bool wasEnabled = GUI.enabled;
			GUI.enabled = false;
			GUILayout.Button("Nothing Selected in Hierarchy View.", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
			GUI.enabled = wasEnabled;
		}
		GUILayout.EndArea();
	}
	
	private void _DrawBus(TreeItem item, string searchString, List<TreeItem> drawnBuses)
	{
		if(item != null && item.Bus)
		{
			string name = item.Bus.name;
			SECTR_AudioBus parent = item.Bus.Parent;
			while(parent != null)
			{
				name = parent.name + "/" + name;
				parent = parent.Parent;
			}

			if(string.IsNullOrEmpty(busSearchString) || name.ToLower().Contains(searchString))
			{
				bool wasEnabled = GUI.enabled;
				GUI.enabled &= SECTR_VC.IsEditable(item.Path);

				SECTR_AudioBusEditor.DrawBusControls(name, 100, item.Bus, muteOnIcon, muteOffIcon, soloOnIcon, soloOffIcon, elementStyle, busSliderStyle, busFieldStyle);
				GUILayout.Box(GUIContent.none, GUILayout.Width(5), GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(true));

				GUI.enabled = wasEnabled;
				drawnBuses.Add(item);
			}

			foreach(SECTR_AudioBus bus in item.Bus.Children)
			{
				if(bus != null)
				{
					_DrawBus(hierarchyItems[bus], searchString, drawnBuses);
				}
			}
		}
	}
	
	private void _DrawClipList()
	{
		//int minWidth = showFullClipDetails ? 750 : 600;
		//int screenWidth = Mathf.Max(Screen.width, minWidth);
		int iconSize = lineHeight;

		GUILayout.BeginArea(new Rect(0f,
		                             (showHierarchy || showProperties) ? bottomSplitter.pos + bottomSplitter.thickness : 0f, 
		                             scaledScreenWidth, 
		                             (showHierarchy || showProperties) ? scaledScreenHeight - bottomSplitter.pos - bottomSplitter.thickness - lineHeight * 2 : scaledScreenHeight));

		// Header
		Rect headerRect = DrawHeader("AUDIO CLIPS", ref clipSearchString, scaledScreenWidth / 4, true);

		// Column labels
		Rect columnRect = EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Width(scaledScreenWidth), GUILayout.Height(headerHeight));
		headerStyle.alignment = TextAnchor.MiddleCenter;
		GUILayout.Label(GUIContent.none, GUILayout.Width(iconSize * 2), GUILayout.MaxWidth(iconSize * 2), GUILayout.MinWidth(iconSize * 2), GUILayout.ExpandWidth(false), GUILayout.Height(headerHeight));
		string[] categories = {
			"NAME",
			"LENGTH",
			"CHANNELS",
			"LOAD",
			"CMPRESN",
			// ADVANCED
			"QUALITY",
			"SAMPLNG",
			"BKGRND",
			"PRE LD",
			"MONO",
		};
		float[] widthScales = {
			1.75f, // NAME
			0.6f, // LENGTH
			0.65f, // CHANNELS
			0.6f, // LOAD
			0.6f, // CMPRESN
			// ADVANCED
			0.6f, // QUALITY
			0.6f, // SAMPLNG
			0.5f, // BKGRND
			0.45f, // PRE LD
			0.45f, // MONO
		};
		int[] widths = new int[categories.Length];
		int advancedStart = 5;
		float weightSum = 0;
		for(int catIndex = 0; catIndex < (showFullClipDetails ? categories.Length : advancedStart); ++catIndex)
		{
			weightSum += widthScales[catIndex];
		}

		int columnSum = 0;
		for(int catIndex = 0; catIndex < (showFullClipDetails ? categories.Length : advancedStart); ++catIndex)
		{
			int width = (int)(scaledScreenWidth * 0.98f * (widthScales[catIndex] / weightSum));
			GUI.Label(new Rect(columnRect.x + columnSum, columnRect.y, width, columnRect.height), categories[catIndex], headerStyle);
			columnSum += width;
			widths[catIndex] = width;
		}

		EditorGUILayout.EndHorizontal();
		EditorGUILayout.Space();

		// Audio Clips
		clipScrollPos = EditorGUILayout.BeginScrollView(clipScrollPos);

		string searchString = clipSearchString.ToLower();
		foreach(string folderName in clipItems.Keys)
		{
			AudioClipFolder folder = clipItems[folderName];

            if(string.IsNullOrEmpty(clipSearchString)  || folder.items.Find(x=>x.Name.ToLower().Contains(searchString))!=null)
            { 
			    Rect folderRect = EditorGUILayout.BeginHorizontal();
			    elementStyle.normal.textColor = UnselectedItemColor;
			    elementStyle.alignment = TextAnchor.MiddleLeft;
			    if(GUILayout.Button(folder.expanded ? expandedIcon : collapsedIcon, elementStyle, GUILayout.Width(iconSize), GUILayout.Height(iconSize)))
			    {
				    folder.expanded = !folder.expanded;
				    EditorPrefs.SetBool(expandedPrefPrefix + folderName, folder.expanded);
			    }
			    GUILayout.Label(folderIcon, elementStyle, GUILayout.Width(iconSize), GUILayout.Height(iconSize));
			    GUILayout.Label(folderName, elementStyle);
			    EditorGUILayout.EndHorizontal();

                if (folder.expanded)
                {
                    deadItems.Clear();
                    foreach (TreeItem item in folder.items)
                    {
                        AudioImporter importer = item.Importer;
                        if (importer == null || string.IsNullOrEmpty(item.Path))
                        {
                            deadItems.Add(item);
                        }
                        else if ((string.IsNullOrEmpty(clipSearchString) || item.Name.ToLower().Contains(searchString)))
                        {
                            item.ScrollRect = EditorGUILayout.BeginHorizontal();
                            item.WindowRect = item.ScrollRect;
                            item.WindowRect.y += bottomSplitter.pos;
                            item.WindowRect.y += headerRect.height;
                            item.WindowRect.y += columnRect.height;
                            item.WindowRect.y += folderRect.height;
                            item.WindowRect.y -= clipScrollPos.y;

                            bool selected = selectedClipItems.Contains(item);

                            if (selected && !lastSelectedHierarchy)
                            {
                                Rect selectionRect = item.ScrollRect;
                                selectionRect.y += 1;
                                selectionRect.height += 1;
                                GUI.Box(selectionRect, "", selectionBoxStyle);
                            }

                            elementStyle.normal.textColor = selected ? Color.white : UnselectedItemColor;

                            // Indent for folder
                            GUILayout.Label(GUIContent.none, GUILayout.Width(iconSize), GUILayout.MaxWidth(iconSize), GUILayout.MinWidth(iconSize), GUILayout.ExpandWidth(false));
                            // Audition button

                            if (GUILayout.Button(new GUIContent(playIcon, "Plays a preview of this clip."), iconButtonStyle, GUILayout.Width(iconSize), GUILayout.Height(iconSize)))
                            {
                                SECTR_AudioSystem.Audition(item.Clip);
                                selectedClipItem = item;
                            }

                            // Now for all the info
                            bool wasEnabled = GUI.enabled;
                            GUI.enabled &= SECTR_VC.IsEditable(item.Path);

                            int checkSize = 20;
                            int columnIndex = 0;
                            int columnWidth = 0;
                            float rowY = item.ScrollRect.y + 1;
                            columnSum = (int)item.ScrollRect.x;

                            // Editable properties
                            AudioCompressionFormat newCompressed = importer.defaultSampleSettings.compressionFormat;
                            AudioClipLoadType newStream = importer.defaultSampleSettings.loadType;
                            float newQuality = importer.defaultSampleSettings.quality;

                            AudioSampleRateSetting newSampleSetting = importer.defaultSampleSettings.sampleRateSetting;
                            uint newSampleRate = importer.defaultSampleSettings.sampleRateOverride;
                            SampleBehavior oldBehavior = SampleBehavior.PreserveRate;
                            switch (newSampleSetting)
                            {
                                case AudioSampleRateSetting.OptimizeSampleRate:
                                    oldBehavior = SampleBehavior.OptimizeRate;
                                    break;
                                case AudioSampleRateSetting.OverrideSampleRate:
                                    switch (newSampleRate)
                                    {
                                        case 8000:
                                            oldBehavior = SampleBehavior.Force8k;
                                            break;
                                        case 11025:
                                            oldBehavior = SampleBehavior.Force11k;
                                            break;
                                        case 22050:
                                            oldBehavior = SampleBehavior.Force22k;
                                            break;
                                        case 44100:
                                            oldBehavior = SampleBehavior.Force44k;
                                            break;
                                        case 48000:
                                            oldBehavior = SampleBehavior.Force48k;
                                            break;
                                        case 96000:
                                            oldBehavior = SampleBehavior.Force96k;
                                            break;
                                        case 192000:
                                            oldBehavior = SampleBehavior.Force192k;
                                            break;
                                    }
                                    break;
                            }
                            SampleBehavior newBehavior = oldBehavior;

                            bool newBackground = importer.loadInBackground;
#if UNITY_2022_2_OR_NEWER
							bool newPreload = GetPreloadAudioDataSetting(importer); 
#else
							bool newPreload = importer.preloadAudioData;
#endif
							bool newMono = importer.forceToMono;

                            // Name
                            columnWidth = widths[columnIndex++];
                            elementStyle.alignment = TextAnchor.MiddleLeft;
                            float shift = iconSize * 2.5f;
                            Rect nameRect = new Rect(columnSum + shift, rowY, columnWidth - shift, item.ScrollRect.height);
                            if (item.Rename)
                            {
                                string focusName = "RenamingItem";
                                GUI.SetNextControlName(focusName);
                                item.Name = GUI.TextField(nameRect, item.Name);
                                GUI.FocusControl(focusName);
                            }
                            else
                            {
                                GUI.Label(nameRect, item.Name, elementStyle);
                            }
                            elementStyle.alignment = TextAnchor.UpperCenter;
                            columnSum += columnWidth;

                            // Length
                            columnWidth = widths[columnIndex++];
                            float length = item.Clip.length;
                            string label = "s";
                            if (length > 60f)
                            {
                                length /= 60f;
                                label = "m";
                            }
                            EditorGUI.LabelField(new Rect(columnSum, rowY, columnWidth, item.ScrollRect.height), length.ToString("N2") + " " + label, elementStyle);
                            columnSum += columnWidth;

                            // Channels
                            string channels = item.Clip.channels + "ch";
                            channels += " @ " + (item.Clip.frequency / 1000f) + "k";
                            columnWidth = widths[columnIndex++];
                            EditorGUI.LabelField(new Rect(columnSum, rowY, columnWidth, item.ScrollRect.height), channels, elementStyle);
                            columnSum += columnWidth;

                            // Stream vs In Memory
                            columnWidth = widths[columnIndex++];
                            newStream = (AudioClipLoadType)EditorGUI.EnumPopup(new Rect(columnSum, rowY, columnWidth * 0.9f, item.ScrollRect.height), newStream);
                            columnSum += columnWidth;

                            // Compressed
                            columnWidth = widths[columnIndex++];
                            newCompressed = (AudioCompressionFormat)EditorGUI.EnumPopup(new Rect(columnSum, rowY, columnWidth * 0.9f, item.ScrollRect.height), newCompressed);
                            columnSum += columnWidth;


                            // Advanced Stuff
                            if (showFullClipDetails)
                            {
                                bool guiWasEnabled = GUI.enabled;
                                // Quality
                                bool compressed = (newCompressed == AudioCompressionFormat.Vorbis || newCompressed == AudioCompressionFormat.MP3);
                                int labelWidth = 40;
                                GUI.enabled &= compressed;
                                columnWidth = widths[columnIndex++];
                                if (newQuality >= 0)
                                {
                                    GUI.SetNextControlName("Clip Quality");
                                    newQuality = EditorGUI.FloatField(new Rect(columnSum - labelWidth / 2 + columnWidth / 2, rowY, labelWidth, item.ScrollRect.height), newQuality * 100f, busFieldStyle) / 100f;
                                    newQuality = Mathf.Clamp(newQuality, 0f, 1f);
                                }
                                else
                                {
                                    const float kDefaultQuality = 0.5f;
                                    GUI.SetNextControlName("Clip Quality");
                                    float userQuality = EditorGUI.FloatField(new Rect(columnSum - labelWidth / 2 + columnWidth / 2, rowY, labelWidth, item.ScrollRect.height), kDefaultQuality * 100f, busFieldStyle) / 100f;
                                    if (userQuality != kDefaultQuality)
                                    {
                                        newQuality = Mathf.Clamp(userQuality, 0f, 1f);
                                    }
                                }
                                GUI.enabled = true;
                                columnSum += columnWidth;

                                // Sample Settings
                                columnWidth = widths[columnIndex++];
                                GUI.enabled &= !compressed;
                                newBehavior = (SampleBehavior)EditorGUI.EnumPopup(new Rect(columnSum, rowY, columnWidth * 0.9f, item.ScrollRect.height), oldBehavior);
                                GUI.enabled = guiWasEnabled;
                                columnSum += columnWidth;

                                // Load in background
                                columnWidth = widths[columnIndex++];
                                newBackground = EditorGUI.Toggle(new Rect(columnSum - checkSize / 2 + columnWidth / 2, rowY, checkSize, item.ScrollRect.height), newBackground);
                                columnSum += columnWidth;

                                // Preload
                                columnWidth = widths[columnIndex++];
                                newPreload = EditorGUI.Toggle(new Rect(columnSum - checkSize / 2 + columnWidth / 2, rowY, checkSize, item.ScrollRect.height), newPreload);
                                columnSum += columnWidth;

                                // Force Mono
                                columnWidth = widths[columnIndex++];
                                newMono = EditorGUI.Toggle(new Rect(columnSum - checkSize / 2 + columnWidth / 2, rowY, checkSize, item.ScrollRect.height), newMono);
                                columnSum += columnWidth;
                            }

                            if ((newMono != importer.forceToMono) ||
                               (newStream != importer.defaultSampleSettings.loadType) ||
                               (newCompressed != importer.defaultSampleSettings.compressionFormat) ||
                               (newQuality != importer.defaultSampleSettings.quality) ||
                               (newBehavior != oldBehavior) ||
                               (newBackground != importer.loadInBackground) ||
#if UNITY_2022_2_OR_NEWER
							   (newPreload != GetPreloadAudioDataSetting(importer)) ||
#else
							   (newPreload != importer.preloadAudioData) ||
#endif
							   (newMono != importer.forceToMono) ||
                               changedAudioClip)
                            {
                                if (!selected)
                                {
                                    selectedClipItem = item;
                                    selectedClipItems.Clear();
                                    selectedClipItems.Add(item);
                                }

                                int numSelected = selectedClipItems.Count;
                                for (int clipIndex = 0; clipIndex < numSelected; ++clipIndex)
                                {
                                    importer = selectedClipItems[clipIndex].Importer;
                                    bool changedText = changedAudioClip || (importer.defaultSampleSettings.quality != newQuality || importer.defaultSampleSettings.sampleRateOverride != newSampleRate);

                                    if (newBehavior != oldBehavior)
                                    {
                                        switch (newBehavior)
                                        {
                                            case SampleBehavior.PreserveRate:
                                                newSampleSetting = AudioSampleRateSetting.PreserveSampleRate;
                                                break;
                                            case SampleBehavior.OptimizeRate:
                                                newSampleSetting = AudioSampleRateSetting.OptimizeSampleRate;
                                                break;
                                            case SampleBehavior.Force8k:
                                                newSampleSetting = AudioSampleRateSetting.OverrideSampleRate;
                                                newSampleRate = 8000;
                                                break;
                                            case SampleBehavior.Force11k:
                                                newSampleSetting = AudioSampleRateSetting.OverrideSampleRate;
                                                newSampleRate = 11025;
                                                break;
                                            case SampleBehavior.Force22k:
                                                newSampleSetting = AudioSampleRateSetting.OverrideSampleRate;
                                                newSampleRate = 22050;
                                                break;
                                            case SampleBehavior.Force44k:
                                                newSampleSetting = AudioSampleRateSetting.OverrideSampleRate;
                                                newSampleRate = 44100;
                                                break;
                                            case SampleBehavior.Force48k:
                                                newSampleSetting = AudioSampleRateSetting.OverrideSampleRate;
                                                newSampleRate = 48000;
                                                break;
                                            case SampleBehavior.Force96k:
                                                newSampleSetting = AudioSampleRateSetting.OverrideSampleRate;
                                                newSampleRate = 96000;
                                                break;
                                            case SampleBehavior.Force192k:
                                                newSampleSetting = AudioSampleRateSetting.OverrideSampleRate;
                                                newSampleRate = 192000;
                                                break;
                                        }
                                    }

                                    AudioImporterSampleSettings newSettings = new AudioImporterSampleSettings();
                                    newSettings.loadType = newStream;
                                    newSettings.compressionFormat = newCompressed;
                                    newSettings.quality = newQuality;
                                    newSettings.sampleRateSetting = newSampleSetting;
                                    newSettings.sampleRateOverride = newSampleRate;
                                    importer.defaultSampleSettings = newSettings;

                                    importer.loadInBackground = newBackground;
#if UNITY_2022_2_OR_NEWER
									SetPreloadAudioData(importer, newPreload);
#else
									importer.preloadAudioData = newPreload;
#endif
									importer.forceToMono = newMono;
                                    changedAudioClip = true;

                                    EditorUtility.SetDirty(importer);
                                    if (changedAudioClip && string.IsNullOrEmpty(GUI.GetNameOfFocusedControl()) &&
                                       (!changedText || GUIUtility.keyboardControl == 0))
                                    {
                                        AssetDatabase.WriteImportSettingsIfDirty(item.Path);
                                        AssetDatabase.Refresh();
                                        changedAudioClip = false;
                                    }
                                }
                            }

                            GUI.enabled = wasEnabled;
                            EditorGUILayout.EndHorizontal();

                            displayedClipItems.Add(item);
                        }
                    }
                    int numDeadItems = deadItems.Count;
                    for (int itemIndex = 0; itemIndex < numDeadItems; ++itemIndex)
                    {
                        folder.items.Remove(deadItems[itemIndex]);
                    }
                } //if
			} //for
		}
		
		EditorGUILayout.EndScrollView();
		if(Event.current.type == EventType.Repaint)
		{
			clipScrollRect = GUILayoutUtility.GetLastRect();
		}
		GUILayout.EndArea();
	}

#if UNITY_2022_2_OR_NEWER
	private void SetPreloadAudioData(AudioImporter importer, bool newPreload)
    {
		AudioImporterSampleSettings sampleSettings;
		if (importer.ContainsSampleSettingsOverride(Application.platform.ToString()))
		{
			sampleSettings = importer.GetOverrideSampleSettings(Application.platform.ToString());
			sampleSettings.preloadAudioData = newPreload;
			importer.SetOverrideSampleSettings(Application.platform.ToString(), sampleSettings);
		}
		else
		{
			sampleSettings = importer.defaultSampleSettings;
			sampleSettings.preloadAudioData = newPreload;
			importer.defaultSampleSettings = sampleSettings;
		}
	}

    private bool GetPreloadAudioDataSetting(AudioImporter importer)
    {
		if(importer.ContainsSampleSettingsOverride(Application.platform.ToString()))
		{ 
			return importer.GetOverrideSampleSettings(Application.platform.ToString()).preloadAudioData;
		}
		return importer.defaultSampleSettings.preloadAudioData;
	}
#endif

    private void _HandleEvents()
	{
		if(Event.current != null)
		{
			if(!string.IsNullOrEmpty(Event.current.commandName) && Event.current.commandName == "UndoRedoPerformed")
			{
				Repaint();
				return;
			}

			if((showHierarchy && showProperties && leftSplitter.HandleEvents(this)) || (showClipList && (showHierarchy || showProperties) && bottomSplitter.HandleEvents(this)))
			{
				Repaint();
				return;
			}

#if UNITY_EDITOR_OSX
			bool heldControl = (Event.current.modifiers & EventModifiers.Command) != 0;
#else
			bool heldControl = (Event.current.modifiers & EventModifiers.Control) != 0;
#endif
			bool heldShift = (Event.current.modifiers & EventModifiers.Shift) != 0;

			switch(Event.current.type)
			{
			case EventType.MouseDown:
				if(Event.current.button == 0)
				{
					if(Event.current.mousePosition.x < leftSplitter.pos && Event.current.mousePosition.y < bottomSplitter.pos)
					{
						foreach(TreeItem item in displayedHierarchyItems)
						{
							if(item.WindowRect.Contains(Event.current.mousePosition))
							{
								if(Event.current.clickCount > 1)
								{
									if(SECTR_VC.IsEditable(item.Path))
									{									
										_StartRenameItem(item, true);
									}
								}
								else
								{
									dragHierarchyItem = item;
								}
								break;
							}
						}
					}
					else if(Event.current.mousePosition.y > bottomSplitter.pos)
					{
						foreach(TreeItem item in displayedClipItems)
						{
							if(item.WindowRect.Contains(Event.current.mousePosition))
							{
								if(Event.current.clickCount > 1)
								{
									if(SECTR_VC.IsEditable(item.Path))
									{									
										_StartRenameItem(item, false);
									}
								}
								else
								{
									dragClipItem = item;
								}
								break;
							}
						}
					}
				}
				break;
			case EventType.MouseUp:
				dragHierarchyItem = null;
				dragClipItem = null;
				dragHoverItem = null;

				if(selectedHierarchyItem != null && selectedHierarchyItem.Rename && !selectedHierarchyItem.WindowRect.Contains(Event.current.mousePosition))
				{
					_CancelRename(selectedHierarchyItem);
				}
				else if(selectedClipItem != null && selectedClipItem.Rename && !selectedClipItem.WindowRect.Contains(Event.current.mousePosition))
				{
					_CancelRename(selectedClipItem);
				}
				else if(Event.current.mousePosition.x < leftSplitter.pos && Event.current.mousePosition.y < bottomSplitter.pos)
				{
					lastSelectedHierarchy = true;
					TreeItem newSelection = Event.current.button == 0 ? null : selectedHierarchyItem;
					foreach(TreeItem item in displayedHierarchyItems)
					{
						if(item.WindowRect.Contains(Event.current.mousePosition))
						{
							newSelection = item;
							break;
						}
					}

					if(newSelection != selectedHierarchyItem || heldControl || heldShift)
					{
						if(newSelection == null)
						{
							selectedHierarchyItem = null;
							selectedHierarchyItems.Clear();
						}
						else if(heldControl)
						{
							if(selectedHierarchyItems.Contains(newSelection))
							{
								selectedHierarchyItems.Remove(newSelection);
								if(selectedHierarchyItems.Count > 0)
								{
									selectedHierarchyItem = selectedHierarchyItems[0];
								}
								else
								{
									selectedHierarchyItem = null;
								}
							}
							else
							{
								selectedHierarchyItems.Add(newSelection);
								selectedHierarchyItem = newSelection;
							}
						}
						else if(heldShift && selectedHierarchyItem != null)
						{
							foreach(TreeItem item in displayedHierarchyItems)
							{
								if((item.WindowRect.y >= selectedHierarchyItem.WindowRect.y && item.WindowRect.y <= newSelection.WindowRect.y) ||
								   (item.WindowRect.y <= selectedHierarchyItem.WindowRect.y && item.WindowRect.y >= newSelection.WindowRect.y))
								{
									if(!selectedHierarchyItems.Contains(item))
									{
										selectedHierarchyItems.Add(item);
									}
								}
								else
								{
									selectedHierarchyItems.Remove(item);
								}
							}
							selectedHierarchyItem = newSelection;
						}
						else
						{
							selectedHierarchyItem = newSelection;
							selectedHierarchyItems.Clear();
							selectedHierarchyItems.Add(selectedHierarchyItem);
						}
						propertyEditor = null;
						GUI.FocusControl(null);
						Repaint();
					}
				}
				else if(Event.current.mousePosition.y > bottomSplitter.pos)
				{
					lastSelectedHierarchy = false;
					TreeItem newSelection = Event.current.button == 0 ? null : selectedClipItem;
					foreach(TreeItem item in displayedClipItems)
					{
						if(item.WindowRect.Contains(Event.current.mousePosition))
						{
							newSelection = item;
							break;
						}
					}

					if(newSelection != selectedClipItem || heldControl || heldShift)
					{
						if(newSelection == null)
						{
							selectedClipItem = null;
							selectedClipItems.Clear();
						}
						else if(heldControl)
						{
							if(selectedClipItems.Contains(newSelection))
							{
								selectedClipItems.Remove(newSelection);
								if(selectedClipItems.Count > 0)
								{
									selectedClipItem = selectedClipItems[0];
								}
								else
								{
									selectedClipItem = null;
								}
							}
							else
							{
								selectedClipItems.Add(newSelection);
								selectedClipItem = newSelection;
							}
						}
						else if(heldShift && selectedClipItem != null)
						{
							foreach(TreeItem item in displayedClipItems)
							{
								if((item.WindowRect.y >= selectedClipItem.WindowRect.y && item.WindowRect.y <= newSelection.WindowRect.y) ||
								   (item.WindowRect.y <= selectedClipItem.WindowRect.y && item.WindowRect.y >= newSelection.WindowRect.y))
								{
									if(!selectedClipItems.Contains(item))
									{
										selectedClipItems.Add(item);
									}
								}
								else
								{
									selectedClipItems.Remove(item);
								}
							}
							selectedClipItem = newSelection;
						}
						else
						{
							selectedClipItem = newSelection;
							selectedClipItems.Clear();
							selectedClipItems.Add(selectedClipItem);
						}

						Repaint();
					}
				}
				break;
			case EventType.MouseDrag:
				if(Event.current.mousePosition.y > bottomSplitter.pos && dragClipItem != null)
				{
					//if(!selectedClipItems.Contains(dragClipItem))
					//{
					//	selectedClipItem = dragClipItem;
					//	selectedClipItems.Clear();
					//	selectedClipItems.Add(selectedClipItem);
					//}
					DragAndDrop.PrepareStartDrag();
					int numSelected = selectedClipItems.Count;
					Object[] objects = new Object[numSelected];
					for(int selectedIndex = 0; selectedIndex < numSelected; ++selectedIndex)
					{
						objects[selectedIndex] = selectedClipItems[selectedIndex].Clip;
					}
					DragAndDrop.objectReferences = objects;
					DragAndDrop.StartDrag("Dragging " + dragClipItem.Name + " (AudioClip)");
					Event.current.Use();
				}
				else if(Event.current.mousePosition.x < leftSplitter.pos && dragHierarchyItem != null)
				{
					//if(!selectedHierarchyItems.Contains(dragHierarchyItem))
					//{
					//	selectedHierarchyItem = dragHierarchyItem;
					//	selectedHierarchyItems.Clear();
					//	selectedHierarchyItems.Add(selectedHierarchyItem);
					//}
					DragAndDrop.PrepareStartDrag();
					int numSelected = selectedHierarchyItems.Count;
					Object[] objects = new Object[numSelected];
                    if (objects.Length > 0)
                    {
                        for (int selectedIndex = 0; selectedIndex < numSelected; ++selectedIndex)
                        {
                            objects[selectedIndex] = selectedHierarchyItems[selectedIndex].AsObject;
                        }
                        DragAndDrop.objectReferences = objects;
                        DragAndDrop.StartDrag("Dragging " + dragHierarchyItem.Name + " (" + objects[0].GetType() + ")");
                        Event.current.Use();
                    }
				}
				break;
			case EventType.DragPerform:
			case EventType.DragUpdated:
				if(Event.current.mousePosition.x < leftSplitter.pos && Event.current.mousePosition.y < bottomSplitter.pos)
				{
					TreeItem hoverItem = null;
                    if (DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0)
                    {
                        Object dragObject = DragAndDrop.objectReferences[0];
                        if (dragObject && dragObject.GetType() == typeof(AudioClip))
                        {
                            foreach (TreeItem item in displayedHierarchyItems)
                            {
                                if (item.WindowRect.Contains(Event.current.mousePosition))
                                {
                                    if (Event.current.type == EventType.DragPerform)
                                    {
                                        if (item.Cue != null && SECTR_VC.IsEditable(item.Path))
                                        {
                                            SECTR_Undo.Record(item.Cue, "Add Clip");
                                            foreach (TreeItem selectedItem in selectedClipItems)
                                            {
                                                if (selectedItem.Importer != null)
                                                {
                                                    item.Cue.AddClip(selectedItem.Clip, false);
                                                }
                                            }
                                            selectedHierarchyItem = item;
                                            selectedHierarchyItems.Clear();
                                            selectedHierarchyItems.Add(selectedHierarchyItem);
                                            DragAndDrop.AcceptDrag();
                                            Repaint();
                                        }
                                        else if (item.Bus != null)
                                        {
                                            foreach (TreeItem selectedItem in selectedClipItems)
                                            {
                                                if (selectedItem.Importer != null)
                                                {
                                                    TreeItem newItem = _CreateTreeItem(item, false, selectedItem.Clip.name);
                                                    if (newItem != null)
                                                    {
                                                        SECTR_AudioCue cue = newItem.Cue;
                                                        cue.AddClip(selectedItem.Clip, false);
                                                        newItem.Rename = selectedClipItems.Count == 1;
                                                    }
                                                }
                                            }
                                            AssetDatabase.SaveAssets();
                                            AssetDatabase.Refresh();
                                            DragAndDrop.AcceptDrag();
                                            Repaint();
                                        }
                                    }
                                    else
                                    {
                                        hoverItem = item;
                                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                                    }
                                    break;
                                }
                            }
                        }
                        else if (dragObject && dragObject.GetType() == typeof(SECTR_AudioBus))
                        {
                            SECTR_AudioBus bus = ((SECTR_AudioBus)dragObject);
                            foreach (TreeItem item in displayedHierarchyItems)
                            {
                                if (item.WindowRect.Contains(Event.current.mousePosition))
                                {
                                    if (item.Bus != null && bus != item.Bus && bus.Parent != item.Bus && !bus.IsAncestorOf(item.Bus) &&
                                        SECTR_VC.IsEditable(item.Path))
                                    {
                                        if (Event.current.type == EventType.DragPerform)
                                        {
                                            foreach (TreeItem selectedItem in selectedHierarchyItems)
                                            {
                                                if (selectedItem.Bus != null && selectedItem.Bus.Parent != item.Bus && !selectedItem.Bus.IsAncestorOf(item.Bus) &&
                                                    SECTR_VC.IsEditable(selectedItem.Path))
                                                {
                                                    selectedItem.Bus.Parent = item.Bus;
                                                    EditorUtility.SetDirty(selectedItem.Bus);
                                                }
                                            }
                                            DragAndDrop.AcceptDrag();
                                            Repaint();
                                        }
                                        else
                                        {
                                            hoverItem = item;
                                            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                        else if (dragObject && dragObject.GetType() == typeof(SECTR_AudioCue))
                        {
                            SECTR_AudioCue cue = ((SECTR_AudioCue)dragObject);
                            foreach (TreeItem item in displayedHierarchyItems)
                            {
                                if (item.WindowRect.Contains(Event.current.mousePosition))
                                {
                                    if (item.Bus != null && item.Bus != cue.Bus &&
                                        SECTR_VC.IsEditable(item.Path))
                                    {
                                        if (Event.current.type == EventType.DragPerform)
                                        {
                                            foreach (TreeItem selectedItem in selectedHierarchyItems)
                                            {
                                                if (selectedItem.Cue && selectedItem.Cue.Bus != item.Bus &&
                                                    SECTR_VC.IsEditable(selectedItem.Path))
                                                {
                                                    selectedItem.Cue.Bus = item.Bus;
                                                    EditorUtility.SetDirty(selectedItem.Cue);
                                                }
                                            }
                                            DragAndDrop.AcceptDrag();
                                            Repaint();
                                        }
                                        else
                                        {
                                            hoverItem = item;
                                            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                                        }
                                    }
                                    break;
                                }
                            }
                        }

                        if (dragHoverItem != hoverItem)
                        {
                            dragHoverItem = hoverItem;
                            Repaint();
                        }
                    }
				}
				break;
			case EventType.KeyUp:
				bool renameHierarchy = lastSelectedHierarchy && selectedHierarchyItem != null && selectedHierarchyItem.Rename;
				bool renameClip = !lastSelectedHierarchy && selectedClipItem != null && selectedClipItem.Rename;
				if(renameHierarchy || renameClip)
				{
					if(Event.current.keyCode == KeyCode.Escape)
					{
						if(renameHierarchy)
						{
							_CancelRename(selectedHierarchyItem);
						}
						else if(renameClip)
						{
							_CancelRename(selectedClipItem);
						}
					}
					else if(Event.current.keyCode == KeyCode.Return)
					{
						string newPath = "";
						bool cue = selectedHierarchyItem != null && selectedHierarchyItem.Cue != null;
						bool bus = selectedHierarchyItem != null && selectedHierarchyItem.Bus != null;
						bool importer = selectedClipItem != null && selectedClipItem.Importer != null;

						if(renameHierarchy)
						{
							newPath = selectedHierarchyItem.Path.Replace(selectedHierarchyItem.DefaultName, selectedHierarchyItem.Name);
							if(newPath == selectedHierarchyItem.Path)
							{
								_CancelRename(selectedHierarchyItem);
								return;
							}
							AssetDatabase.RenameAsset(selectedHierarchyItem.Path, selectedHierarchyItem.Name);
							SECTR_VC.WaitForVC();
							_RemoveHierarchyItem(selectedHierarchyItem);
						}
						else if(renameClip)
						{
							newPath = selectedClipItem.Path.Replace(Path.GetFileNameWithoutExtension(selectedClipItem.Path), selectedClipItem.Name);
							if(newPath == selectedClipItem.Path)
							{
								_CancelRename(selectedClipItem);
								return;
							}
							AssetDatabase.RenameAsset(selectedClipItem.Path, selectedClipItem.Name);
							SECTR_VC.WaitForVC();
							_RemoveClipItem(selectedClipItem);
						}

						TreeItem renamedItem = null;
						if(renameHierarchy && cue)
						{
							SECTR_AudioBus newBus = (SECTR_AudioBus)AssetDatabase.LoadAssetAtPath(newPath, typeof(SECTR_AudioBus));
							if(newBus)
							{
								renamedItem = new TreeItem(this, newBus, newPath);
							}
						}
						else if(renameHierarchy && bus)
						{
							SECTR_AudioCue newCue = (SECTR_AudioCue)AssetDatabase.LoadAssetAtPath(newPath, typeof(SECTR_AudioCue));
							if(newCue)
							{
								renamedItem = new TreeItem(this, newCue, newPath);
							}
						}
						else if(renameClip && importer)
						{
							AudioImporter newImporter = (AudioImporter)AssetImporter.GetAtPath(newPath);
							if(newImporter)
							{
								renamedItem = new TreeItem(newImporter, newPath, Path.GetFileName(newPath));
							}
							AudioClipFolder folder = clipItems[Path.GetDirectoryName(newPath) + "/"];
							folder.items.Add(renamedItem);
							folder.items.Sort(delegate(TreeItem A, TreeItem B) 
							{
								return string.Compare(A.DefaultName, B.DefaultName);
							});
						}
						if(renamedItem != null)
						{
							if(renameHierarchy)
							{
								selectedHierarchyItem = renamedItem;
								selectedHierarchyItems.Clear();
								selectedHierarchyItems.Add(selectedHierarchyItem);
							}
							else if(renameClip)
							{
								selectedClipItem = renamedItem;
								selectedClipItems.Clear();
								selectedClipItems.Add(selectedHierarchyItem);
							}
						}
						else
						{
							Debug.LogWarning("Unable to rename asset. Name may already be in use.");
							_RefreshAssets();
						}
						GUI.FocusControl(null);
						Repaint();
					}
				}
				else
				{
					switch(Event.current.keyCode)
					{
					case KeyCode.Return:
						if(string.IsNullOrEmpty(GUI.GetNameOfFocusedControl()))
						{
							if(lastSelectedHierarchy && selectedHierarchyItem != null && SECTR_VC.IsEditable(selectedHierarchyItem.Path))
							{
								_StartRenameItem(selectedHierarchyItem, true);
							}
							else if(!lastSelectedHierarchy && selectedClipItem != null && SECTR_VC.IsEditable(selectedClipItem.Path))
							{
								_StartRenameItem(selectedClipItem, false);
								Repaint();
							}
						}
						else if(changedAudioClip)
						{
							GUI.FocusControl(null);
							Repaint();
						}
						break;
					case KeyCode.D:
						if(heldControl)
						{
							if(lastSelectedHierarchy && selectedHierarchyItem != null)
							{
								_DuplicateSelected(selectedHierarchyItem, true);
							}
							else if(!lastSelectedHierarchy && selectedClipItem != null)
							{
								_DuplicateSelected(selectedClipItem, false);
							}
						}
						break;
					case KeyCode.Delete:
					case KeyCode.Backspace:
						if(string.IsNullOrEmpty(GUI.GetNameOfFocusedControl()))
						{
							if(lastSelectedHierarchy && selectedHierarchyItem != null && SECTR_VC.IsEditable(selectedHierarchyItem.Path))
							{
								_DeleteSelected(selectedHierarchyItem, true);
							}
							else if(!lastSelectedHierarchy && selectedClipItem != null && SECTR_VC.IsEditable(selectedClipItem.Path))
							{
								_DeleteSelected(selectedClipItem, false);
							}
						}
						break;
					case KeyCode.Escape:
						SECTR_AudioSystem.StopAudition();
						break;
					case KeyCode.Space:
						if(lastSelectedHierarchy && selectedHierarchyItem != null && selectedHierarchyItem.Cue != null)
						{
							SECTR_AudioSystem.Audition(selectedHierarchyItem.Cue);
						}
						else if(!lastSelectedHierarchy && selectedClipItem != null)
						{
							SECTR_AudioSystem.Audition(selectedClipItem.Clip);
						}
						break;
					case KeyCode.UpArrow:
					case KeyCode.DownArrow:
					case KeyCode.LeftArrow:
					case KeyCode.RightArrow:
						if(lastSelectedHierarchy && selectedHierarchyItem != null)
						{
							int numDisplayed = displayedHierarchyItems.Count;
							for(int treeIndex = 0; treeIndex < numDisplayed; ++treeIndex)
							{
								if(displayedHierarchyItems[treeIndex] == selectedHierarchyItem)
								{
									if(Event.current.keyCode == KeyCode.UpArrow && treeIndex > 0)
									{
										selectedHierarchyItem = displayedHierarchyItems[treeIndex - 1];
										if(!Event.current.shift)
										{
											selectedHierarchyItems.Clear();
										}
										if(!selectedHierarchyItems.Contains(selectedHierarchyItem))
										{
											selectedHierarchyItems.Add(selectedHierarchyItem);
										}
									}
									else if(Event.current.keyCode == KeyCode.DownArrow && treeIndex < numDisplayed - 1)
									{
										selectedHierarchyItem = displayedHierarchyItems[treeIndex + 1];
										if(!Event.current.shift)
										{
											selectedHierarchyItems.Clear();
										}
										if(!selectedHierarchyItems.Contains(selectedHierarchyItem))
										{
											selectedHierarchyItems.Add(selectedHierarchyItem);
										}
									}
									else if(Event.current.keyCode == KeyCode.RightArrow && selectedHierarchyItem.Bus != null)
									{
										selectedHierarchyItem.Expanded = true;
									}
									else if(Event.current.keyCode == KeyCode.LeftArrow && selectedHierarchyItem.Bus != null)
									{
										selectedHierarchyItem.Expanded = false;
									}
									Repaint();
									break;
								}
							}
						}
						else if(!lastSelectedHierarchy && selectedClipItem != null)
						{
							int numDisplayed = displayedClipItems.Count;
							for(int treeIndex = 0; treeIndex < numDisplayed; ++treeIndex)
							{
								if(displayedClipItems[treeIndex] == selectedClipItem)
								{
									if(Event.current.keyCode == KeyCode.UpArrow && treeIndex > 0)
									{
										selectedClipItem = displayedClipItems[treeIndex - 1];
										if(!Event.current.shift)
										{
											selectedClipItems.Clear();
										}
										if(!selectedClipItems.Contains(selectedClipItem))
										{
											selectedClipItems.Add(selectedClipItem);
										}
										if(selectedClipItem.ScrollRect.y < clipScrollPos.y)
										{
											clipScrollPos.y = selectedClipItem.ScrollRect.y;
										}
									}
									else if(Event.current.keyCode == KeyCode.DownArrow && treeIndex < numDisplayed - 1)
									{
										selectedClipItem = displayedClipItems[treeIndex + 1];
										if(!Event.current.shift)
										{
											selectedClipItems.Clear();
										}
										if(!selectedClipItems.Contains(selectedClipItem))
										{
											selectedClipItems.Add(selectedClipItem);
										}
										if(selectedClipItem.ScrollRect.y > clipScrollPos.y + clipScrollRect.height)
										{
											clipScrollPos.y = selectedClipItem.ScrollRect.y;
										}
									}
									else if(Event.current.keyCode == KeyCode.RightArrow && selectedClipItem != null)
									{
										selectedClipItem.Expanded = true;
									}
									else if(Event.current.keyCode == KeyCode.LeftArrow && selectedClipItem != null)
									{
										selectedClipItem.Expanded = false;
									}
									Repaint();
									break;
								}
							}
						}

						break;
					}
				}
				break;
			case EventType.ContextClick:

				GenericMenu menu = new GenericMenu();
				
				menu.AddItem(new GUIContent("Show Hierarchy"), showHierarchy, delegate() 
				{
					showHierarchy = !showHierarchy;
					UnityEditor.EditorPrefs.SetBool(showPrefPrefix + "Hierarchy", showHierarchy);
				});
				menu.AddItem(new GUIContent("Show Properties"), showProperties, delegate() 
				{
					showProperties = !showProperties;
					UnityEditor.EditorPrefs.SetBool(showPrefPrefix + "Properties", showProperties);
				});
				menu.AddItem(new GUIContent("Show Audio Clips"), showClipList, delegate() 
				{
					showClipList = !showClipList;
					UnityEditor.EditorPrefs.SetBool(showPrefPrefix + "ClipList", showClipList);
				});

				if(Event.current.mousePosition.x < leftSplitter.pos || Event.current.mousePosition.y > bottomSplitter.pos)
				{
					menu.AddSeparator(null);
					bool hasVC = SECTR_VC.HasVC();
					if(Event.current.mousePosition.x < leftSplitter.pos && Event.current.mousePosition.y < bottomSplitter.pos)
					{
						TreeItem cloneItem = null;
						foreach(TreeItem item in displayedHierarchyItems)
						{
							if(item.WindowRect.Contains(Event.current.mousePosition))
							{
								cloneItem = item;
								bool editable = !hasVC || SECTR_VC.IsEditable(item.Path);

								if(hasVC)
								{
									if(!editable)
									{
										menu.AddItem(new GUIContent("Check Out"), false, delegate() 
										{
											_CheckoutSelected(item, true);
										});
									}
									else
									{
										menu.AddItem(new GUIContent("Revert"), false, delegate()
										{
											_RevertSelected(item, true);
										});
									}
								}
								
								menu.AddItem(new GUIContent("Show In Project"), false, delegate()
								{
									if(item.Cue != null) Selection.activeObject = item.Cue;
									if(item.Bus != null) Selection.activeObject = item.Bus;
									EditorUtility.FocusProjectWindow();
								});
								menu.AddSeparator("");

								menu.AddItem(new GUIContent("Duplicate"), false, delegate()
								{
									_DuplicateSelected(item, true);
								});

								if(editable)
								{
									menu.AddItem(new GUIContent("Rename"), false, delegate() 
									{
										_StartRenameItem(item, true);
									});

									menu.AddItem(new GUIContent("Delete"), false, delegate() 
									{
										_DeleteSelected(item, true);
									});
								}
								else
								{
									menu.AddSeparator("Rename");
									menu.AddSeparator("Delete");
								}

								menu.AddSeparator(null);
								break;
							}
						}

						menu.AddItem(new GUIContent("Create New Bus"), false, delegate() 
						{
							_CreateTreeItem(cloneItem, true, null);
							Repaint();
						});
						menu.AddItem(new GUIContent("Create New Cue"), false, delegate() 
						{
							_CreateTreeItem(cloneItem, false, null);
							Repaint();
						});
					}
					else if(Event.current.mousePosition.y > bottomSplitter.pos)
					{
						foreach(TreeItem item in displayedClipItems)
						{
							if(item.WindowRect.Contains(Event.current.mousePosition))
							{
								bool editable = !hasVC || SECTR_VC.IsEditable(item.Path);
								// Project Items
								if(SECTR_VC.HasVC())
								{
									if(!editable)
									{
										menu.AddItem(new GUIContent("Check Out"), false, delegate()
										{
											_CheckoutSelected(item, false);
										});
									}
									else
									{
										menu.AddItem(new GUIContent("Revert"), false, delegate()
										{
											_RevertSelected(item, false);
										});
									}
								}
								menu.AddItem(new GUIContent("Show In Project"), false, delegate()
								{
									Selection.activeObject = item.Clip;
									EditorUtility.FocusProjectWindow();
								});
								menu.AddSeparator("");

								menu.AddItem(new GUIContent("Duplicate"), false, delegate()
								{
									_DuplicateSelected(item, false);
								});
								
								if(editable)
								{
									menu.AddItem(new GUIContent("Rename"), false, delegate() 
									{
										_StartRenameItem(item, false);
									});
									menu.AddItem(new GUIContent("Delete"), false, delegate() 
									{
										_DeleteSelected(item, false);
									});
								}
								else
								{
									menu.AddSeparator("Rename");
									menu.AddSeparator("Delete");
								}

								menu.AddSeparator("");

								// Creation Items
								if(selectedHierarchyItem != null && selectedHierarchyItem.Cue != null && 
								   !selectedHierarchyItem.Cue.HasClip(item.Clip))
								{
									menu.AddItem(new GUIContent("Add Selected to Cue"), false, delegate()
									{
										SECTR_Undo.Record(selectedHierarchyItem.Cue, "Add Clips");
										foreach(TreeItem selectedItem in selectedClipItems)
										{
											selectedHierarchyItem.Cue.AddClip(selectedItem.Clip, false);
										}
										Repaint();
									});
								}
								else
								{
									menu.AddSeparator("Add Clip to Selected Cue");
								}
								menu.AddItem(new GUIContent("Create Cues from Selected"), false, delegate() 
								{
									foreach(TreeItem selectedItem in selectedClipItems)
									{
										TreeItem newItem = _CreateTreeItem(selectedHierarchyItem, false, selectedItem.Clip.name);
										if(newItem != null)
										{
											newItem.Rename = selectedClipItems.Count == 1;
											SECTR_AudioCue cue = newItem.Cue;
											cue.AddClip(selectedItem.Clip, false);
										}
									}
									Repaint();
								});
								menu.AddSeparator("");
								break;
							}
						}

						menu.AddItem(new GUIContent("Show Advanced Properties"), showFullClipDetails, delegate()
						{
							showFullClipDetails = !showFullClipDetails;
							UnityEditor.EditorPrefs.SetBool(fullDetailsPref, showFullClipDetails);
							Repaint();
						});

						menu.AddItem(new GUIContent("Close All Folders"), false, delegate()
						{
							foreach(string path in clipItems.Keys)
							{
								clipItems[path].expanded = false;
								UnityEditor.EditorPrefs.SetBool(expandedPrefPrefix + path, false);
							}
							Repaint();
						});
					}
				}

				// Items in all menus.
				menu.AddSeparator(null);
				menu.AddItem(new GUIContent("Bake All HDR Keys"), false, delegate() 
				{
					List<string> paths = new List<string>();
					List<string> extensions = new List<string>()
					{
						".asset",
					};
					bakeMaster = SECTR_ComputeRMS.BakeList(SECTR_Asset.GetAll<SECTR_AudioCue>(audioRootPath, extensions, ref paths, false));
				});
				menu.AddSeparator(null);
				menu.AddItem(new GUIContent("Refresh Assets"), false, delegate() 
				{
					_RefreshAssets();
				});
				menu.AddSeparator(null);
				menu.AddItem(new GUIContent("Change Audio Root"), false, delegate() 
				{
					_SelectAudioRoot();
					_RefreshAssets();
				});
				menu.ShowAsContext();
				break;
			}

		}
	}

	private TreeItem _CreateTreeItem(TreeItem selectedItem, bool createBus, string name)
	{
		SECTR_AudioBus parentBus = null;
		string dirPath = audioRootPath;
		string fileName = "";
		string typeName = createBus ? "Bus" : "Cue";
		if(string.IsNullOrEmpty(name))
		{
			name = "New" + typeName;
		}
		if(selectedItem != null)
		{
			if(selectedItem.Bus)
			{
				parentBus = selectedItem.Bus;
			}
			else if(selectedItem.Cue)
			{
				parentBus = selectedItem.Cue.Bus;
			}
			SECTR_Asset.SplitPath(selectedItem.Path, out dirPath, out fileName);
		}
		else 
		{
			foreach(TreeItem item in hierarchyItems.Values)
			{
				if((item.Bus && item.Bus.Parent == null) || (item.Cue && item.Cue.Bus == null))
				{
					SECTR_Asset.SplitPath(item.Path, out dirPath, out fileName);
					break;
				}
			}
			
			dirPath = EditorUtility.SaveFolderPanel("Chose " + typeName + " Location", dirPath, "");
			if(string.IsNullOrEmpty(dirPath))
			{
				return null;
			}
			dirPath = dirPath.Replace(Application.dataPath, "Assets");
		}
		
		string assetPath = "";
		TreeItem newItem;
		if(createBus)
		{
			SECTR_AudioBus bus = SECTR_Asset.Create<SECTR_AudioBus>(dirPath, name, ref assetPath);
			bus.Parent = parentBus;
			newItem = new TreeItem(this, bus, assetPath);
		}
		else
		{
			SECTR_AudioCue cue = SECTR_Asset.Create<SECTR_AudioCue>(dirPath, name, ref assetPath);
			cue.Bus = parentBus;
			newItem = new TreeItem(this, cue, assetPath);
		}

		newItem.Rename = true;
		selectedHierarchyItem = newItem;
		selectedHierarchyItems.Clear();
		selectedHierarchyItems.Add(selectedHierarchyItem);
		propertyEditor = null;
		return newItem;
	}

	private void _RemoveHierarchyItem(TreeItem item)
	{
		if(item.Bus)
		{
			EditorPrefs.DeleteKey(expandedPrefPrefix + item.Path);
		}
		if(item == selectedHierarchyItem)
		{
			selectedHierarchyItem = null;
			selectedHierarchyItems.Clear();
		}
		hierarchyItems.Remove(item.AsObject);
	}

	private void _RemoveClipItem(TreeItem item)
	{
		if(item == selectedClipItem)
		{
			selectedClipItem = null;
			selectedHierarchyItems.Clear();
		}
		string dirPath;
		string fileName;
		SECTR_Asset.SplitPath(item.Path, out dirPath, out fileName);
		AudioClipFolder clipFolder = clipItems[dirPath];
		clipFolder.items.Remove(item);
		if(clipFolder.items.Count == 0)
		{
			clipItems.Remove(dirPath);
		}
	}

	private void _SelectAudioRoot()
	{
		audioRootPath = EditorUtility.OpenFolderPanel("Choose AUDIO Root", audioRootPath, "");
		if(!string.IsNullOrEmpty(audioRootPath))
		{
			UnityEditor.EditorPrefs.SetString(rootPrefPrefix + SECTR_Asset.GetProjectName(), audioRootPath);
		}
	}

	private void _DeleteSelected(TreeItem item, bool hierarchy)
	{
		_UpdateSelected(item, hierarchy);

		string targetName = "the selected ";
		if(hierarchy && selectedHierarchyItems.Count == 1)
		{
			targetName = selectedHierarchyItems[0].Name;
		}
		else if(!hierarchy && selectedClipItems.Count == 1)
		{
			targetName = selectedClipItems[0].Name;
		}
		else
		{
			targetName += "clips";
		}

		if(EditorUtility.DisplayDialog("Are you sure?", "Deleting will destroy " + targetName + ". This cannot be Undone." , "Ok", "Cancel") )
		{
			List<TreeItem> oldSelection = new List<TreeItem>(hierarchy ? selectedHierarchyItems : selectedClipItems);
			foreach(TreeItem selectedItem in oldSelection)
			{
				if(selectedItem.Bus != null && (selectedItem.Bus.Children.Count > 0 || selectedItem.Bus.Cues.Count > 0))
				{
					EditorUtility.DisplayDialog("Cannot Delete", selectedItem.Name + " cannot be deleted while it has children.", "Ok"); 
				}
				else if(selectedItem.Cue || selectedItem.Bus)
				{
					_RemoveHierarchyItem(selectedItem);
					ScriptableObject.DestroyImmediate(selectedItem.AsObject, true);
					AssetDatabase.DeleteAsset(selectedItem.Path);
				}
				else if(selectedItem.Clip)
				{
					_RemoveClipItem(selectedItem);
					ScriptableObject.DestroyImmediate(selectedItem.Clip, true);
					AssetDatabase.DeleteAsset(selectedItem.Path);
				}
			}
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
			Resources.UnloadUnusedAssets();
			if(hierarchy)
			{
				selectedHierarchyItem = null;
				selectedHierarchyItems.Clear();
			}
			else
			{
				selectedClipItem = null;
				selectedClipItems.Clear();
			}
			Repaint();
		}
	}

	private void _CheckoutSelected(TreeItem item, bool hierarchy)
	{
		_UpdateSelected(item, hierarchy);

		foreach(TreeItem selectedItem in (hierarchy? selectedHierarchyItems : selectedClipItems))
		{
			SECTR_VC.CheckOut(selectedItem.Path);
		}
	}

	private void _RevertSelected(TreeItem item, bool hierarchy)
	{
		_UpdateSelected(item, hierarchy);

		string targetName = "the selected ";
		if(hierarchy && selectedHierarchyItems.Count == 1)
		{
			targetName = selectedHierarchyItems[0].Name;
		}
		else if(!hierarchy && selectedClipItems.Count == 1)
		{
			targetName = selectedClipItems[0].Name;
		}
		else
		{
			targetName += "clips";
		}

		if(EditorUtility.DisplayDialog("Are you sure?", "Reverting will discard all changes to " + targetName + ". This cannot be Undone." , "Ok", "Cancel") )
		{
			foreach(TreeItem selectedItem in (hierarchy? selectedHierarchyItems : selectedClipItems))
			{
				SECTR_VC.Revert(selectedItem.Path);
			}
			_RefreshAssets();
		}
	}

	private void _UpdateSelected(TreeItem item, bool hierarchy)
	{
		if(hierarchy && !selectedHierarchyItems.Contains(item))
		{
			selectedHierarchyItems.Clear();
			selectedHierarchyItems.Add(item);
			selectedHierarchyItem = item;
		}
		else if(!hierarchy && !selectedClipItems.Contains(item))
		{
			selectedClipItems.Clear();
			selectedClipItems.Add(item);
			selectedClipItem = item;
		}
	}

	private void _StartRenameItem(TreeItem item, bool hierarchy)
	{
		if(item != null)
		{
			if(selectedHierarchyItem != null && selectedHierarchyItem.Rename)
			{
				selectedHierarchyItem.Rename = false;
			}
			if(selectedClipItem != null && selectedClipItem.Rename)
			{
				selectedClipItem.Rename = false;
			}

			if(hierarchy)
			{
				selectedHierarchyItem = item;
				selectedHierarchyItems.Clear();
				selectedHierarchyItems.Add(selectedHierarchyItem);
			}
			else
			{
				selectedClipItem = item;
				selectedClipItems.Clear();
				selectedClipItems.Add(selectedClipItem);
				selectedClipItem.Name = Path.GetFileNameWithoutExtension(selectedClipItem.Path);
			}
			item.Rename = true;
			propertyEditor = null;
			Repaint();
		}
	}

	private void _CancelRename(TreeItem item)
	{
		item.Name = item.DefaultName;
		item.Rename = false;
		GUI.FocusControl(null);
		Repaint();
	}

	private void _DuplicateSelected(TreeItem item, bool hierarchy)
	{
		_UpdateSelected(item, hierarchy);

		List<TreeItem> selectedItems = hierarchy ? selectedHierarchyItems : selectedClipItems;
		List<TreeItem> newItems = new List<TreeItem>(selectedItems.Count);
		foreach(TreeItem selectedItem in selectedItems)
		{
			TreeItem newItem = null;
			string dirPath = "";
			string fileName = "";
			SECTR_Asset.SplitPath(selectedItem.Path, out dirPath, out fileName);

			string copyName = selectedItem.Name + " Copy";
			string newPath = dirPath + copyName + Path.GetExtension(fileName);
			if(AssetDatabase.CopyAsset(selectedItem.Path, newPath))
			{
				SECTR_VC.WaitForVC();
				if(selectedItem.Bus)
				{
					SECTR_AudioBus bus = SECTR_Asset.Load<SECTR_AudioBus>(newPath);
					newItem = new TreeItem(this, bus, newPath);
				}
				else if(selectedItem.Cue)
				{
					SECTR_AudioCue cue = SECTR_Asset.Load<SECTR_AudioCue>(newPath);
					newItem = new TreeItem(this, cue, newPath);
				}
				else if(selectedItem.Clip)
				{
					newItem = new TreeItem((AudioImporter)AssetImporter.GetAtPath(newPath), newPath, copyName);
					AudioClipFolder folder = clipItems[dirPath];
					folder.items.Add(newItem);
				}
				
				if(newItem != null)
				{
					newItems.Add(newItem);
				}
			}
		}
		if(hierarchy)
		{
			selectedHierarchyItems = newItems;
			selectedHierarchyItem = selectedItems.Count > 0 ? selectedItems[0] : null;
		}
		else
		{
			selectedClipItems = newItems;
			selectedClipItem = selectedItems.Count > 0 ? selectedItems[0] : null;
		}
		propertyEditor = null;
		Repaint();
	}
#endregion
}
