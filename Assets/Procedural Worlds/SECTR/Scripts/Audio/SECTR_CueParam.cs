// Copyright (c) 2014 Make Code Now! LLC
using UnityEngine;
using System;

[System.Serializable]
public class SECTR_CueParam
{
	public enum TargetType 
	{
		Volume,
		Pitch,
		Attribute,
	}

	[System.Serializable]
	public class AttributeData
	{
		private Type componentType;
		[SerializeField]
		private string componentTypeString;

		public Type ComponentType 
		{
			set
			{
				componentType = value;
				if(componentType != null)
				{
					componentTypeString = componentType.ToString();
				}
			}
			get
			{
				if(componentType == null)
				{
                    if (componentTypeString != null)
                    {
                        componentType = System.Type.GetType(componentTypeString);
                    }
                    else
                    {
                        return null;
                    }
				}
				return componentType;
			}
		}

		public string attributeName;
		public bool fieldAttribute;
	}

	public string name;
	public TargetType affects;
	public float defaultValue;
	public AnimationCurve curve;
	public AttributeData attributeData;
	public bool toggle;

	public SECTR_CueParam()
	{
		name = "distance";
		affects = TargetType.Volume;
		defaultValue = 0f;
		Keyframe[] defaultKeys = 
		{
			new Keyframe(0f, 1f),
			new Keyframe(1f, 1f),
		};
		curve = new AnimationCurve(defaultKeys);
		toggle = true;
	}
}
