﻿// Copyright (c) 2014 Make Code Now! LLC
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public static class SECTR_VC
{
	public static bool HasVC()
	{
		return UnityEditor.VersionControl.Provider.enabled && UnityEditor.VersionControl.Provider.isActive;
	}
	
	public static void WaitForVC()
	{
		if(HasVC())
		{
			while(UnityEditor.VersionControl.Provider.activeTask != null)
			{
				UnityEditor.VersionControl.Provider.activeTask.Wait();
			}
		}
		AssetDatabase.Refresh();
		AssetDatabase.SaveAssets();

	}
	
	public static bool CheckOut(string path)
	{
		if(HasVC())
		{
			UnityEditor.VersionControl.Asset vcAsset = UnityEditor.VersionControl.Provider.GetAssetByPath(path);
			if(vcAsset != null)
			{
				UnityEditor.VersionControl.Task task = UnityEditor.VersionControl.Provider.Checkout(vcAsset, UnityEditor.VersionControl.CheckoutMode.Both);
				task.Wait();
			}
		}
		return IsEditable(path);
	}
	
	public static void Revert(string path)
	{
		if(HasVC())
		{
			UnityEditor.VersionControl.Asset vcAsset = UnityEditor.VersionControl.Provider.GetAssetByPath(path);
			if(vcAsset != null)
			{
				UnityEditor.VersionControl.Task task = UnityEditor.VersionControl.Provider.Revert(vcAsset, UnityEditor.VersionControl.RevertMode.Normal);
				task.Wait();
				AssetDatabase.Refresh();
			}
		}
	}
	
	public static bool IsEditable(string path)
	{
		if(HasVC())
		{
			UnityEditor.VersionControl.Asset vcAsset = UnityEditor.VersionControl.Provider.GetAssetByPath(path);
			return vcAsset != null ? UnityEditor.VersionControl.Provider.IsOpenForEdit(vcAsset) : true;
		}
		else
		{
			return true;
		}
	}
}
