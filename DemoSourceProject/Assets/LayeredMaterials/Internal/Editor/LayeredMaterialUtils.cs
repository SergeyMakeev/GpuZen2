using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using System.IO;



namespace LM
{

    public static class Utils
    {

        [MenuItem("Assets/Create/Layered Material", priority = 301)]
        public static void CreateLayeredMaterialTemplateAsset()
        {
            var icon = EditorGUIUtility.FindTexture("Material Icon");
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, ScriptableObject.CreateInstance<DoCreateLayredMaterialTemplateAsset>(), "New Layered Material.asset", icon, null);
        }
    }


    class DoCreateLayredMaterialTemplateAsset : EndNameEditAction
    {
        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            MaterialTemplate materialTemplate = ScriptableObject.CreateInstance<MaterialTemplate>();
            materialTemplate.name = Path.GetFileName(pathName);
            AssetDatabase.CreateAsset(materialTemplate, pathName);
        }
    }



}