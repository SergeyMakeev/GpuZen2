using UnityEngine;
using System.Collections;


namespace LM
{

    [ExecuteInEditMode]
    public class LayeredMaterialBehaviour : MonoBehaviour
    {
        public LM.MaterialTemplate template = null;
        MaterialPropertyBlock propBlock = null;


        private static string GetGameObjectPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
        }

        public void UpdateShaderParams()
        {
            Renderer renderer = gameObject.GetComponent<Renderer>();
            Material unityMaterial = renderer.sharedMaterial;

            if (unityMaterial == null || unityMaterial.shader.name != "Custom/PrototypeLayeredShader")
            {
                //Debug.LogError("UpdateShaderParams failed " + gameObject.name);
                return;
            }


            //Debug.Log("Update '" + GetGameObjectPath(gameObject.transform) + "'");
            if (template != null)
            {
                template.UpdateComputeBuffer();
                template.ApplyParametersToUnityMaterial(unityMaterial, propBlock);
                renderer.SetPropertyBlock(propBlock);
            }
        }


        // Use this for initialization
        void Start()
        {
            propBlock = new MaterialPropertyBlock();

            //Debug.Log("Start '" + GetGameObjectPath(gameObject.transform) + "'");
            UpdateShaderParams();
        }

        // Update is called once per frame
        void Update()
        {
            
        }

        void OnWillRenderObject()
        {
#if UNITY_EDITOR
            if (Application.isPlaying == false)
            {
                UpdateShaderParams();

            }
#endif
        }


    }

}

