Shader "Custom/AudioHUD_Lines" {
	Properties {
		_Color ("Main Color", Color) = (1,1,1,1)
	}
	SubShader {
		Color [_Color]
		Pass {}
	}
}
