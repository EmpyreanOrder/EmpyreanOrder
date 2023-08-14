using UnityEngine;
using UnityEditor;
using System.Collections;

public class SECTR_Window : EditorWindow
{
	#region Private Details
	protected int headerHeight = 25;
	protected int lineHeight = (int)EditorGUIUtility.singleLineHeight;
	protected GUIStyle headerStyle = null;
	protected GUIStyle m_sectionStyle = null;
	protected GUIStyle elementStyle = null;
	protected GUIStyle selectionBoxStyle = null;
	protected GUIStyle iconButtonStyle = null;
	protected GUIStyle searchBoxStyle = null;
	protected GUIStyle searchCancelStyle = null;
	protected Texture2D selectionBG = null;
	#endregion

	#region Public Interface
	public static Color UnselectedItemColor
	{
		get { return EditorGUIUtility.isProSkin ? Color.gray : Color.black; }
	}
	#endregion


	protected virtual void OnGUI()
	{
		if (headerStyle == null)
		{
			headerStyle = new GUIStyle(EditorStyles.toolbar);
			headerStyle.fontStyle = FontStyle.Bold;
			headerStyle.alignment = TextAnchor.MiddleLeft;
		}

		if (m_sectionStyle == null)
		{
			m_sectionStyle = new GUIStyle(GUI.skin.label);
			m_sectionStyle.fixedHeight = 20f;
			m_sectionStyle.fontStyle = FontStyle.Bold;
			m_sectionStyle.alignment = TextAnchor.MiddleLeft;
		}

		if (elementStyle == null)
		{
			elementStyle = new GUIStyle(GUI.skin.label);
			elementStyle.margin = new RectOffset(0, 0, 5, 5);
			elementStyle.border = new RectOffset(0, 0, 0, 0);
			elementStyle.normal.textColor = UnselectedItemColor;
		}

		if (selectionBG == null)
		{
			selectionBG = new Texture2D(1, 1);
			selectionBG.SetPixel(0, 0, new Color(62f / 255f, 125f / 255f, 231f / 255f));
			selectionBG.Apply();
		}

		if (selectionBoxStyle == null)
		{
			selectionBoxStyle = new GUIStyle(GUI.skin.box);
			selectionBoxStyle.normal.background = selectionBG;
		}

		if (iconButtonStyle == null)
		{
			iconButtonStyle = new GUIStyle(GUI.skin.button);
			iconButtonStyle.padding = new RectOffset(2, 2, 2, 2);
		}

		if (searchBoxStyle == null)
		{
#if UNITY_2022_3_OR_NEWER
				searchBoxStyle = GUI.skin.FindStyle("ToolbarSearchTextField");
#else
			searchBoxStyle = GUI.skin.FindStyle("ToolbarSeachTextField");
#endif
		}

		if (searchCancelStyle == null)
		{
#if UNITY_2022_3_OR_NEWER
				searchCancelStyle = GUI.skin.FindStyle("ToolbarSearchCancelButton");
#else
			searchCancelStyle = GUI.skin.FindStyle("ToolbarSeachCancelButton");
#endif
		}
	}

	protected Rect DrawHeader(string title, bool center)
	{
		string nullSearch = null;
		return DrawHeader(new GUIContent(title), ref nullSearch, 0, center);
	}

	protected Rect DrawHeader(string title, ref string searchString, float searchWidth, bool center)
	{
		return DrawHeader(new GUIContent(title), ref searchString, searchWidth, center);
	}

	protected Rect DrawHeader(GUIContent title, bool center)
	{
		string nullSearch = null;
		return DrawHeader(new GUIContent(title), ref nullSearch, 0, center);
	}

	protected Rect DrawHeader(GUIContent title, ref string searchString, float searchWidth, bool center)
	{
		Rect headerRect = EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
		headerStyle.alignment = center ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
		if (center)
		{
			GUILayout.FlexibleSpace();
		}
		GUILayout.Label(title, headerStyle);
		if (searchString != null)
		{
			////GUI.SetNextControlName(title + "_Header");
			string controlName = "ValueFld" + GUIUtility.GetControlID(FocusType.Keyboard);
			GUI.SetNextControlName(controlName);
			searchString = EditorGUILayout.TextField(searchString, searchBoxStyle, GUILayout.Width(searchWidth));
			if (GUILayout.Button("", searchCancelStyle))
			{
				// Remove focus if cleared
				searchString = "";
				GUI.FocusControl(null);
			}
		}
		GUILayout.FlexibleSpace();
		EditorGUILayout.EndHorizontal();
		return headerRect;
	}

	protected void SectionHeader(GUIContent title, bool center)
	{
		if (center)
		{
			m_sectionStyle.alignment = TextAnchor.MiddleCenter;
		}

		Rect rect = EditorGUILayout.BeginHorizontal();
		{
			GUILayout.Label(title, m_sectionStyle);
		}
		EditorGUILayout.EndHorizontal();

		Handles.BeginGUI();
		Handles.color = SECTR_Constants.UI_SEPARATOR_LINE_COLOR;
		Handles.DrawLine(rect.min, new Vector3(rect.xMax, rect.yMin));
		Handles.EndGUI();

	}
}
