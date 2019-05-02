using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace LM
{


    [CustomEditor(typeof(LM.MaterialTemplate))]
    public class SmartPrefabEditor : Editor
    {
        private PreviewRenderUtility previewRenderUtility;

        private Vector2 previewDir = new Vector2(0.0f, -20f);

        private int currentMeshIndex = 0;

        private static Mesh sphereMesh;
        private static Mesh cubeMesh;
        private static Mesh cylinderMesh;
        private static Mesh torusMesh;
        private static Mesh planeMesh;

        private static GUIContent sphereIcon;
        private static GUIContent cubeIcon;
        private static GUIContent cylinderIcon;
        private static GUIContent torusIcon;
        private static GUIContent planeIcon;

        private static Material previewMaterial;

        private static int sliderHash = "Slider".GetHashCode();
        private static GUIStyle buttonStyle;

        public class XmlMaterialLayer
        {
            public string name;
            public List<int> resourceSlots = new List<int>(5);
        }



        Mesh GetMesh()
        {
            switch(currentMeshIndex)
            {
                case 1:
                    return cubeMesh;
                case 2:
                    return cylinderMesh;
                case 3:
                    return torusMesh;
                case 4:
                    return planeMesh;
            }

            return sphereMesh;
        }

        GUIContent GetMeshIcon()
        {
            switch (currentMeshIndex)
            {
                case 1:
                    return cubeIcon;
                case 2:
                    return cylinderIcon;
                case 3:
                    return torusIcon;
                case 4:
                    return planeIcon;
            }

            return sphereIcon;
        }


        void OnSceneDrag(SceneView sceneView)
        {
            Event current = Event.current;
            if (current.type == EventType.Repaint)
            {
                return;
            }

            int materialIndex = -1;
            GameObject go = HandleUtility.PickGameObject(current.mousePosition, out materialIndex);
            if (go == null)
            {
                return;
            }

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }


            bool isDragDropEvent = false;
            switch (current.type)
            {
                case EventType.DragUpdated:
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    isDragDropEvent = true;
                    break;
                case EventType.DragPerform:
                    DragAndDrop.AcceptDrag();
                    isDragDropEvent = true;
                    break;
            }

            if (!isDragDropEvent)
            { 
                return;
            }

            Material unityMaterial = renderer.sharedMaterial;
            if (unityMaterial == null || unityMaterial.shader.name != "Custom/PrototypeLayeredShader")
            {
                unityMaterial = new Material(Shader.Find("Custom/PrototypeLayeredShader"));
            }


            Undo.RecordObject(renderer, "Assign Material");

            // add component if need
            LayeredMaterialBehaviour layeredComponent = renderer.gameObject.GetComponent<LayeredMaterialBehaviour>();
            if (layeredComponent == null)
            {
                layeredComponent = renderer.gameObject.AddComponent<LayeredMaterialBehaviour>();
            }

            // assign material template
            MaterialTemplate materialTemplate = target as MaterialTemplate;
            layeredComponent.template = materialTemplate;

            // assign unity materials
            Material[] sharedMaterials = renderer.sharedMaterials;

            if (!current.alt && (materialIndex >= 0 && materialIndex < renderer.sharedMaterials.Length))
            {
                sharedMaterials[materialIndex] = unityMaterial;
            }
            else
            {
                for (int index = 0; index < sharedMaterials.Length; ++index)
                {
                    sharedMaterials[index] = unityMaterial;
                }
            }

            renderer.sharedMaterials = sharedMaterials;
            current.Use();

            Debug.Log("SCENE DRAG!!");
        }

        void InitializePreviewIfNeed()
        {
            if (previewRenderUtility != null)
            {
                return;
            }

            previewRenderUtility = new PreviewRenderUtility();
            previewRenderUtility.camera.transform.position = new Vector3(0, 0, -6);
            previewRenderUtility.camera.transform.rotation = Quaternion.identity;

            GameObject gameObject = (GameObject)EditorGUIUtility.LoadRequired("Previews/PreviewMaterials.fbx");
            gameObject.SetActive(false);
            foreach (Transform transform in gameObject.transform)
            {
                MeshFilter component = transform.GetComponent<MeshFilter>();
                string name = transform.name;

                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                if (name == "sphere")
                {
                    sphereMesh = component.sharedMesh;
                }

                if (name == "cube")
                {
                    cubeMesh = component.sharedMesh;
                }

                if (name == "cylinder")
                {
                    cylinderMesh = component.sharedMesh;
                }

                if (name == "torus")
                {
                    torusMesh = component.sharedMesh;
                }
            }

            planeMesh = Resources.GetBuiltinResource(typeof(Mesh), "Quad.fbx") as Mesh;
            previewMaterial = new Material(Shader.Find("Custom/PrototypeLayeredShader"));

            sphereIcon = EditorGUIUtility.IconContent("PreMatSphere");
            cubeIcon = EditorGUIUtility.IconContent("PreMatCube");
            cylinderIcon = EditorGUIUtility.IconContent("PreMatCylinder");
            torusIcon = EditorGUIUtility.IconContent("PreMatTorus");
            planeIcon = EditorGUIUtility.IconContent("PreMatQuad");

            buttonStyle = (GUIStyle)"preButton";
        }

        void DrawPreview()
        {
            previewRenderUtility.camera.transform.position = -Vector3.forward * 5f;
            previewRenderUtility.camera.transform.rotation = Quaternion.identity;

            Quaternion objectRotation = Quaternion.Euler(previewDir.y, 0.0f, 0.0f) * Quaternion.Euler(0.0f, this.previewDir.x, 0.0f);

            /*
                        previewRenderUtility.m_Light[0].intensity = 1f;
                        previewRenderUtility.m_Light[0].transform.rotation = Quaternion.Euler(30f, 30f, 0.0f);
                        previewRenderUtility.m_Light[1].intensity = 0.0f;
            */

            Mesh mesh = GetMesh();
            if (mesh != null)
            {
                //targets
                MaterialTemplate materialTemplate = target as MaterialTemplate;
                materialTemplate.ForceUpdateParametersToUnityMaterial(previewMaterial);
                previewRenderUtility.DrawMesh(mesh, Vector3.zero, objectRotation, previewMaterial, 0, (MaterialPropertyBlock)null);
            }

            previewRenderUtility.camera.Render();
        }

        void OnDestroy()
        {
            if (previewRenderUtility != null)
            {
                previewRenderUtility.Cleanup();
                previewRenderUtility = null;
            }
        }

        public virtual void Awake()
        {
            previewDir = new Vector2(0.0f, 50f);
        }

        public override void OnInspectorGUI()
        {
            MaterialTemplate materialTemplate = target as MaterialTemplate;

            if (string.IsNullOrEmpty(materialTemplate.sourceDescriptionXml))
            {
                EditorGUILayout.HelpBox("Load description XML!", MessageType.Error);
            } else
            {
                EditorGUILayout.HelpBox(materialTemplate.sourceDescriptionXml, MessageType.Info);
            }

            if (GUILayout.Button("Load description XML", GUILayout.Height(20.0f)))
            {
                string sourceDescriptionXml = EditorUtility.OpenFilePanel("Select description XML", "", "xml");
                if (!string.IsNullOrEmpty(sourceDescriptionXml))
                {
                    Debug.Log("loading " + sourceDescriptionXml);
                    materialTemplate.sourceDescriptionXml = sourceDescriptionXml;
                    LoadDescriptionXML();
                }
            }

            if (GUILayout.Button("Reload description XML", GUILayout.Height(20.0f)))
            {
                LoadDescriptionXML();
            }

            EditorGUILayout.Space();

            const float offset = 160.0f;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Normal map", GUILayout.Width(offset));
            EditorGUI.BeginChangeCheck();
            Texture2D normalMap = EditorGUILayout.ObjectField(materialTemplate.normalMap, typeof(Texture2D), false) as Texture2D;
            if (EditorGUI.EndChangeCheck())
            {
                materialTemplate.normalMap = normalMap;
                UpdateMaterialParameters(materialTemplate);

            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Indirection map", GUILayout.Width(offset));
            EditorGUI.BeginChangeCheck();
            Texture2D indirectionMap = EditorGUILayout.ObjectField(materialTemplate.indirectionMap, typeof(Texture2D), false) as Texture2D;
            if (EditorGUI.EndChangeCheck())
            {
                materialTemplate.indirectionMap = indirectionMap;
                UpdateMaterialParameters(materialTemplate);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Weights map", GUILayout.Width(offset));
            EditorGUI.BeginChangeCheck();
            Texture2D weightsMap = EditorGUILayout.ObjectField(materialTemplate.weightsMap, typeof(Texture2D), false) as Texture2D;
            if (EditorGUI.EndChangeCheck())
            {
                materialTemplate.weightsMap = weightsMap;
                UpdateMaterialParameters(materialTemplate);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("AmbientMap map", GUILayout.Width(offset));
            EditorGUI.BeginChangeCheck();
            Texture2D ambientMap = EditorGUILayout.ObjectField(materialTemplate.ambientMap, typeof(Texture2D), false) as Texture2D;
            if (EditorGUI.EndChangeCheck())
            {
                materialTemplate.ambientMap = ambientMap;
                UpdateMaterialParameters(materialTemplate);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("AmbientMap multipler", GUILayout.Width(offset));
            EditorGUI.BeginChangeCheck();
            float ambientCorrection = EditorGUILayout.Slider(materialTemplate.ambientCorrection, 0.0f, 4.0f);
            if (EditorGUI.EndChangeCheck())
            {
                materialTemplate.ambientCorrection = ambientCorrection;
                UpdateMaterialParameters(materialTemplate);
            }

            EditorGUILayout.EndHorizontal();


            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Alpha map", GUILayout.Width(offset));
            EditorGUI.BeginChangeCheck();
            Texture2D alphaMap = EditorGUILayout.ObjectField(materialTemplate.alphaMap, typeof(Texture2D), false) as Texture2D;
            if (EditorGUI.EndChangeCheck())
            {
                materialTemplate.alphaMap = alphaMap;
                UpdateMaterialParameters(materialTemplate);
            }
            EditorGUILayout.EndHorizontal();



            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Is AlphaTest", GUILayout.Width(offset));
            EditorGUI.BeginChangeCheck();

            bool isAlphaTested = EditorGUILayout.Toggle(materialTemplate.isAlphaTested);

            if (EditorGUI.EndChangeCheck())
            {
                materialTemplate.isAlphaTested = isAlphaTested;
                UpdateMaterialParameters(materialTemplate);
            }
            EditorGUILayout.EndHorizontal();



            EditorGUILayout.Space();
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Details normals (texture array)", GUILayout.Width(offset));
            EditorGUI.BeginChangeCheck();
            Texture2DArray textureArrayNormals = EditorGUILayout.ObjectField(materialTemplate.textureArrayNormals, typeof(Texture2DArray), false) as Texture2DArray;
            if (EditorGUI.EndChangeCheck())
            {
                materialTemplate.textureArrayNormals = textureArrayNormals;
                UpdateMaterialParameters(materialTemplate);
            }
            EditorGUILayout.EndHorizontal();


            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Details surface (texture array)", GUILayout.Width(offset));
            EditorGUI.BeginChangeCheck();
            Texture2DArray textureArraySurface = EditorGUILayout.ObjectField(materialTemplate.textureArraySurface, typeof(Texture2DArray), false) as Texture2DArray;
            if (EditorGUI.EndChangeCheck())
            {
                materialTemplate.textureArraySurface = textureArraySurface;
                UpdateMaterialParameters(materialTemplate);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.TextArea("", GUI.skin.horizontalSlider);


            for (int i = 0; i < materialTemplate.layers.Count; i++)
            {
                MaterialTemplate.LayerTemplate layer = materialTemplate.layers[i];

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Source key (read only)", GUILayout.Width(offset));
                EditorGUILayout.SelectableLabel(layer.layerKey, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                EditorGUILayout.EndHorizontal();


                EditorGUILayout.BeginHorizontal();

                EditorGUI.BeginChangeCheck();
                string layerName = EditorGUILayout.TextField(layer.layerName, GUILayout.Width(offset));
                if (EditorGUI.EndChangeCheck())
                {
                    layer.layerName = layerName;
                }

                EditorGUI.BeginChangeCheck();
                Material material = EditorGUILayout.ObjectField(layer.material, typeof(Material), false) as Material;
                if (EditorGUI.EndChangeCheck())
                {
                    if (material.shader.name == "Custom/PrototypeSingleShader")
                    {
                        layer.material = material;
                        UpdateMaterialParameters(materialTemplate);
                    } else
                    {
                        EditorUtility.DisplayDialog("Error!", "You can only assign 'PrototypeSingleShader' to layer!\nYou drop '" + material.shader.name + "' shader.", "Yep");
                    }

                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Albedo color alpha", GUILayout.Width(offset));
                EditorGUI.BeginChangeCheck();
                float albedoAlpha = EditorGUILayout.Slider(layer.albedoAlpha, 0.0f, 15.0f);
                if (EditorGUI.EndChangeCheck())
                {
                    layer.albedoAlpha = albedoAlpha;
                    UpdateMaterialParameters(materialTemplate);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Surface properties alpha", GUILayout.Width(offset));
                EditorGUI.BeginChangeCheck();
                float surfaceAlpha = EditorGUILayout.Slider(layer.surfaceAlpha, 0.0f, 15.0f);
                if (EditorGUI.EndChangeCheck())
                {
                    layer.surfaceAlpha = surfaceAlpha;
                    UpdateMaterialParameters(materialTemplate);
                }
                EditorGUILayout.EndHorizontal();


                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Normals alpha", GUILayout.Width(offset));
                EditorGUI.BeginChangeCheck();
                float normalsAlpha = EditorGUILayout.Slider(layer.normalsAlpha, 0.0f, 15.0f);
                if (EditorGUI.EndChangeCheck())
                {
                    layer.normalsAlpha = normalsAlpha;
                    UpdateMaterialParameters(materialTemplate);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Global alpha", GUILayout.Width(offset));
                EditorGUI.BeginChangeCheck();
                float globalAlpha = EditorGUILayout.Slider(layer.globalAlpha, 0.0f, 5.0f);
                if (EditorGUI.EndChangeCheck())
                {
                    layer.globalAlpha = globalAlpha;
                    UpdateMaterialParameters(materialTemplate);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Global offset", GUILayout.Width(offset));
                EditorGUI.BeginChangeCheck();
                float globalOffset = EditorGUILayout.Slider(layer.globalOffset, 0.0f, 1.0f);
                if (EditorGUI.EndChangeCheck())
                {
                    layer.globalOffset = globalOffset;
                    UpdateMaterialParameters(materialTemplate);
                }
                EditorGUILayout.EndHorizontal();


                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Global normals scale", GUILayout.Width(offset));
                EditorGUI.BeginChangeCheck();
                float globalNormalScale = EditorGUILayout.Slider(layer.globalNormalScale, 0.0f, 1.0f);
                if (EditorGUI.EndChangeCheck())
                {
                    layer.globalNormalScale = globalNormalScale;
                    UpdateMaterialParameters(materialTemplate);
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Surface rotation", GUILayout.Width(offset));
                EditorGUI.BeginChangeCheck();
                float surfaceRotation = EditorGUILayout.Slider(layer.surfaceRotation, 0.0f, 180.0f);
                if (EditorGUI.EndChangeCheck())
                {
                    layer.surfaceRotation = surfaceRotation;
                    UpdateMaterialParameters(materialTemplate);
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Normals rotation", GUILayout.Width(offset));
                EditorGUI.BeginChangeCheck();
                float normalsRotation = EditorGUILayout.Slider(layer.normalsRotation, 0.0f, 180.0f);
                if (EditorGUI.EndChangeCheck())
                {
                    layer.normalsRotation = normalsRotation;
                    UpdateMaterialParameters(materialTemplate);
                }

                EditorGUILayout.EndHorizontal();


                EditorGUILayout.Space();
                EditorGUILayout.TextArea("", GUI.skin.horizontalSlider);
                //EditorGUILayout.Space();
            }

            //DrawDefaultInspector();
        }

        void UpdateMaterialParameters(MaterialTemplate materialTemplate)
        {
            materialTemplate.CreateComputeBuffer();

            AssetDatabase.Refresh();

            EditorUtility.SetDirty(target);

            //AssetDatabase.SaveAssets();

            SceneView.RepaintAll();
        }

        public override bool HasPreviewGUI()
        {
            return true;
        }

        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            if (Event.current.type == EventType.Repaint)
            {
                InitializePreviewIfNeed();
                previewRenderUtility.BeginPreview(r, background);
                DrawPreview();
                previewRenderUtility.EndAndDrawPreview(r);
            }

            previewDir = UpdateDragRotation(previewDir, r);
        }

        public static Vector2 UpdateDragRotation(Vector2 scrollPosition, Rect position)
        {
            int controlId = GUIUtility.GetControlID(sliderHash, FocusType.Passive);
            Event current = Event.current;

            switch (current.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (position.Contains(current.mousePosition) && (double)position.width > 50.0)
                    {
                        GUIUtility.hotControl = controlId;
                        current.Use();
                        EditorGUIUtility.SetWantsMouseJumping(1);
                        break;
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId)
                    {
                        GUIUtility.hotControl = 0;
                    }
                    EditorGUIUtility.SetWantsMouseJumping(0);
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId)
                    {
                        scrollPosition -= current.delta * (!current.shift ? 1f : 3f) / Mathf.Min(position.width, position.height) * 140f;
                        scrollPosition.y = Mathf.Clamp(scrollPosition.y, -90f, 90f);
                        current.Use();
                        GUI.changed = true;
                        break;
                    }
                    break;
            }
            return scrollPosition;
        }



        public override void OnPreviewSettings()
        {
            InitializePreviewIfNeed();

            if (GUILayout.Button(GetMeshIcon(), buttonStyle, new GUILayoutOption[0]))
            {
                currentMeshIndex++;
                if (currentMeshIndex > 4)
                {
                    currentMeshIndex = 0;
                }
                Debug.Log("Switch model");
            }

            GUILayout.Label("Layered material", GUI.skin.label, new GUILayoutOption[0]);
        }

        public override Texture2D RenderStaticPreview(string assetPath, Object[] subAssets, int width, int height)
        {
            InitializePreviewIfNeed();
            previewRenderUtility.BeginStaticPreview(new Rect(0.0f, 0.0f, (float)width, (float)height));
            DrawPreview();
            return previewRenderUtility.EndStaticPreview();
        }



        static List<XmlMaterialLayer> ParseDescriptionXML(string xmlPath)
        {
            if (!File.Exists(xmlPath))
            {
                return null;
            }

            string xmlContent = System.IO.File.ReadAllText(xmlPath);

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xmlContent);

            Dictionary<string, List<int>> uniqueLayers = new Dictionary<string, List<int>>();

            int layersPerPixel = 5;

            XmlNodeList rootList = xmlDoc.GetElementsByTagName("Palettes");
            Debug.Log("count = " + rootList.Count);
            if (rootList.Count > 0)
            {
                XmlNodeList palettes = rootList.Item(0).ChildNodes;
                Debug.Log("ch count = " + palettes.Count);

                for (int i = 0; i < palettes.Count; i++)
                {
                    if (palettes.Item(i).Name != "Palette")
                    {
                        continue;
                    }

                    XmlNode palette = palettes.Item(i);

                    Debug.Log(i.ToString() + ". " + palette.Name);
                    string paletteID = palette.Attributes["id"].InnerText;
                    Debug.Log(paletteID);

                    int paletteIndex = int.Parse(paletteID);

                    int localOffset = 0;
                    foreach (XmlNode layer in palette.ChildNodes)
                    {
                        string layerKey = layer.Attributes["name"].InnerText;

                        int slotIndex = paletteIndex * layersPerPixel;

                        if (layer.Name == "BaseLayer")
                        {
                            //base layer is always in last slot
                            slotIndex += (layersPerPixel - 1);
                        } else
                        {
                            slotIndex += localOffset;
                            localOffset++;
                        }


                        //find existing layer or add new one
                        List<int> indexList = null;
                        if (uniqueLayers.TryGetValue(layerKey, out indexList))
                        {
                            indexList.Add(slotIndex);
                        }
                        else
                        {
                            indexList = new List<int>();
                            indexList.Add(slotIndex);
                            uniqueLayers.Add(layerKey, indexList);
                        }
                    }
                } // palettes count

            }


            List<XmlMaterialLayer> results = new List<XmlMaterialLayer>();

            foreach (KeyValuePair<string, List<int>> layer in uniqueLayers)
            {
                XmlMaterialLayer xmlLayer = new XmlMaterialLayer();
                xmlLayer.name = layer.Key;
                xmlLayer.resourceSlots = layer.Value;
                results.Add(xmlLayer);
            }


            return results;
        }

        bool LoadDescriptionXML()
        {
            MaterialTemplate materialTemplate = target as MaterialTemplate;

            List<XmlMaterialLayer> xmlLayers = ParseDescriptionXML(materialTemplate.sourceDescriptionXml);
            if (xmlLayers == null || xmlLayers.Count == 0)
            {
                Debug.LogError("Can't load XML");
                return false;
            }

            //=====================================================================

            List<MaterialTemplate.LayerTemplate> oldLayers = new List<MaterialTemplate.LayerTemplate>(materialTemplate.layers);
            materialTemplate.layers.Clear();
            foreach (XmlMaterialLayer layer in xmlLayers)
            {
                MaterialTemplate.LayerTemplate layerTemplate = new MaterialTemplate.LayerTemplate();
                layerTemplate.layerKey = Path.GetFileNameWithoutExtension(layer.name);
                layerTemplate.layerName = Path.GetFileNameWithoutExtension(layer.name);
                layerTemplate.targetSlots = layer.resourceSlots.ToArray();
                layerTemplate.albedoAlpha = 1.0f;
                layerTemplate.surfaceAlpha = 1.0f;
                layerTemplate.normalsAlpha = 1.0f;
                layerTemplate.globalNormalScale = 1.0f;
                layerTemplate.material = null;

                //restore params from old layer if such layer exist
                foreach(MaterialTemplate.LayerTemplate oldLayer in oldLayers)
                {
                    if (oldLayer.layerKey == layerTemplate.layerKey)
                    {
                        layerTemplate.layerName = oldLayer.layerName;
                        layerTemplate.albedoAlpha = oldLayer.albedoAlpha;
                        layerTemplate.surfaceAlpha = oldLayer.surfaceAlpha;
                        layerTemplate.normalsAlpha = oldLayer.normalsAlpha;
                        layerTemplate.globalNormalScale = oldLayer.globalNormalScale;
                        layerTemplate.material = oldLayer.material;
                        break;
                    }
                }

                materialTemplate.layers.Add(layerTemplate);
            }

            //--------------------------------------------------

            materialTemplate.layers.Sort((i1, i2) =>
            {
                return i1.layerName.CompareTo(i2.layerName);
            });


            //--------------------------------------------------
            foreach(MaterialTemplate.LayerTemplate matLayer in materialTemplate.layers)
            {
                string slots = "";
                foreach(int slotIndex in matLayer.targetSlots)
                {
                    slots += " ";
                    slots += slotIndex.ToString();
                }

                Debug.Log(string.Format("{0} - {1}. Slots:{2}", matLayer.layerKey, matLayer.layerName, slots));
            }


            return true;
        }




    }

}