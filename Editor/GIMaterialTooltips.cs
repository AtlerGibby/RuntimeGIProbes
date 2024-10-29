using System.Collections;
using System.Collections.Generic;
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GIProbesRuntime
{
    public class GIMaterialTooltips : MaterialPropertyDrawer
    {
		private GUIContent guiContent;

		private MethodInfo internalMethod;
		private Type[] methodArgumentTypes;
		private object[] methodArguments;

		public GIMaterialTooltips(string tooltip)
		{
            string description = "";


            if(tooltip == "_FogColor")
                description = "The color of the fog.";
            if(tooltip == "_FogDensity")
                description = "How dense the fog is. Only does anything if using Exponential or Exponential Squared Fog.";
            if(tooltip == "_FogStart")
                description = "Where the fog begins. Only does anything if using Linear Fog.";
            if(tooltip == "_FogEnd")
                description = "Where the fog ends. Only does anything if using Linear Fog.";
            if(tooltip == "_GIColInf")
                description = "How much does global illumination color the scene.";
            if(tooltip == "_GIBrightness")
                description = "The brightness of the global illumination.";
            if(tooltip == "_GIContrast")
                description = "The contrast of the global illumination.";
            if(tooltip == "_GIStrength")
                description = "The overall strength of the global illumination effect.";

            if(tooltip == "_GIVolRad")
                description = "Globally adds to all bounding box falloff values.";
            if(tooltip == "_LCStrength")
                description = "The strength of the light checker texture on the direct lighting input to this shader. The overall effect of the shader.";
            if(tooltip == "_SSSMStrength")
                description = "The strength the Screen Space Shadow Map has on the direct lighting input to this shader.";
            if(tooltip == "_DebugGILC")
                description = "See direct lighting info.";


			this.guiContent = new GUIContent(string.Empty, description);

			methodArgumentTypes = new[] {typeof(Rect), typeof(MaterialProperty), typeof(GUIContent)};
			methodArguments = new object[3];
			
			internalMethod = typeof(MaterialEditor)
				.GetMethod("DefaultShaderPropertyInternal", BindingFlags.Instance | BindingFlags.NonPublic, 
				null, 
				methodArgumentTypes, 
				null);
		}

		public override void OnGUI(Rect position, MaterialProperty prop, String label, MaterialEditor editor)
		{
			guiContent.text = label;
				
			if (internalMethod != null)
			{
				methodArguments[0] = position;
				methodArguments[1] = prop;
				methodArguments[2] = guiContent;
				
				internalMethod.Invoke(editor, methodArguments);
			}
		}
    }
}

