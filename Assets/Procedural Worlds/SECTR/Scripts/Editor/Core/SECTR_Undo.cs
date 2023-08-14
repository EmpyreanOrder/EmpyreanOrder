using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public static class SECTR_Undo 
{
	public static void Record(Object undoObject, string undoString)
	{
		Undo.RecordObject(undoObject, undoString);
	}

	public static void Created(Object undoObject, string undoString)
	{
		Undo.RegisterCreatedObjectUndo(undoObject, undoString);
	}

	public static void Destroy(GameObject undoObject, string undoString)
	{
		Undo.DestroyObjectImmediate(undoObject);
	}

	/// <summary>
	/// Assigns parent Transform to a child Transform with Undo and returns the child.
	/// </summary>
	/// <param name="parent">Parent Transform</param>
	/// <param name="child">Child Transform</param>
	/// <param name="undoString">String to appear in the Undo list.</param>
	public static Transform Parent(Transform parent, Transform child, string undoString)
	{
		Undo.SetTransformParent(child, parent, undoString);
		return child;
	}

	public static void Parent(GameObject parent, GameObject child, string undoString)
	{
		Undo.SetTransformParent(child.transform, parent.transform, undoString);
	}
}
