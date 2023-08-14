// Copyright (c) 2014 Make Code Now! LLC

using UnityEngine;
using System;

public static class SECTR_Modules
{
	public static bool AUDIO = false;
	public static bool VIS = false;
	public static bool STREAM = false;
	public static bool DEV = false;
	public static string VERSION = string.Format("{0}.{1}", SECTR_Constants.MAJOR_VERSION, SECTR_Constants.MINOR_VERSION);

	static SECTR_Modules()
	{
		AUDIO = Type.GetType("SECTR_AudioSystem") != null;
		VIS = Type.GetType("SECTR_CullingCamera") != null;
		STREAM = Type.GetType("SECTR_Chunk") != null;
		DEV = Type.GetType("SECTR_Tests") != null;
	}

	public static bool HasPro()
	{
		// Unity 5 is Pro for all the ways that SECTR cares.
		return true;
	}

	public static bool HasComplete()
	{
		return AUDIO && VIS && STREAM;
	}
}