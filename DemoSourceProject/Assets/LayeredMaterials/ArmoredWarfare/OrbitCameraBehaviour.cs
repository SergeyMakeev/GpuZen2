using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OrbitCameraBehaviour : MonoBehaviour {

    float deltaTime = 0.0f;

    public GameObject target;
    public float zoomMin = 4.5f;
    public float zoomMax = 13.0f;
    public float zoomDefault = 8.0f;
    public float rotSpeed = 1.0f;
    public float zoomSpeed = 1.0f;

    Vector3 origin = Vector3.zero;

    float yaw = 10.0f;
    float pitch = 140.0f;
    float dist = 10.0f;

    bool isRotatorIsActive = false;

    public float initialYaw = 10.0f;
    public float initialPitch = 140.0f;

    bool isLowQuality = false;

    public float yawMin = 4.0f;
    public float yawMax = 87.0f;

    List<LM.LayeredMaterialBehaviour> materialInstanceList;
    List<LM.MaterialTemplate.LayerTemplate> dirtLayers;
    List<LM.MaterialTemplate.LayerTemplate> paintLayers;

    public Material[] dirtOverrides = new Material[2];
    public Material[] paintOverrides = new Material[2];

    public List<GameObject> tanks = new List<GameObject>();

    // Use this for initialization
    void Start () {

        yaw = initialYaw;
        pitch = initialPitch;

        isLowQuality = false;
        Shader.DisableKeyword("LOW_QUALITY");

        if (target != null)
        {
            origin = target.transform.position;
        }

        dist = zoomDefault;
        dist = Mathf.Clamp(dist, zoomMin, zoomMax);

        SwitchTank(0);

        materialInstanceList = GetAllLayeredMaterialBehaviours();

        dirtLayers = GetAllLayersByName("dirt");
        paintLayers = GetAllLayersByName("paint");
    }


    List<LM.LayeredMaterialBehaviour> GetAllLayeredMaterialBehaviours()
    {
        List<LM.LayeredMaterialBehaviour> list = new List<LM.LayeredMaterialBehaviour>();

        for (int i = 0; i < tanks.Count; i++)
        {
            GameObject tank = tanks[i];
            if (tank == null)
            {
                continue;
            }

            Component[] layeredMaterials = tank.GetComponentsInChildren<LM.LayeredMaterialBehaviour>(true);
            if (layeredMaterials == null)
            {
                continue;
            }

            foreach(LM.LayeredMaterialBehaviour lm in layeredMaterials)
            {
                if (lm != null)
                {
                    list.Add(lm);
                }

            }
        }

        return list;
    }

    List<LM.MaterialTemplate.LayerTemplate> GetAllLayersByName(string name)
    {
        string lName = name.ToLower();

        List<LM.MaterialTemplate.LayerTemplate> list = new List<LM.MaterialTemplate.LayerTemplate>();
        foreach (LM.LayeredMaterialBehaviour lm in materialInstanceList)
        {
            LM.MaterialTemplate lmTemplate = lm.template;
            if (lmTemplate == null)
            {
                continue;
            }

            foreach (LM.MaterialTemplate.LayerTemplate matLayer in lmTemplate.layers)
            {
                if (matLayer.material == null)
                {
                    continue;
                }

                if (!matLayer.material.name.ToLower().Contains(lName) && !matLayer.layerName.ToLower().Contains(lName))
                {
                    continue;
                }

                list.Add(matLayer);
            }
        }

        return list;
    }


    void UpdateAllLayeredMaterials()
    {
        foreach (LM.LayeredMaterialBehaviour lm in materialInstanceList)
        {
            lm.UpdateShaderParams();
        }

    }

    public void ChangeDirtAmount(float v)
    {
        foreach (LM.MaterialTemplate.LayerTemplate matLayer in dirtLayers)
        {

            matLayer.globalAlphaRuntime = v / 100.0f;
        }

        UpdateAllLayeredMaterials();
    }

    public void OverrideDirtMaterial(int index)
    {
        Material overrideMaterial = null;
        if (index > 0 && index <= dirtOverrides.Length)
        {
            overrideMaterial = dirtOverrides[index - 1];
        }

        foreach (LM.MaterialTemplate.LayerTemplate matLayer in dirtLayers)
        {
            matLayer.materialRuntimeOverride = overrideMaterial;
        }

        UpdateAllLayeredMaterials();

    }

    public void OverridePaintMaterial(int index)
    {
        Material overrideMaterial = null;
        if (index > 0 && index <= paintOverrides.Length)
        {
            overrideMaterial = paintOverrides[index - 1];
        }

        foreach (LM.MaterialTemplate.LayerTemplate matLayer in paintLayers)
        {
            matLayer.materialRuntimeOverride = overrideMaterial;
        }

        UpdateAllLayeredMaterials();

    }

    public void SwitchTank(int index)
    {
        for(int i = 0; i < tanks.Count; i++)
        {
            GameObject tank = tanks[i];
            if (tank == null)
            {
                continue;
            }

            bool isActive = (index == i);
            tank.SetActive(isActive);
        }
    }

    public void LowSpecToggle(bool val)
    {
        //Debug.Log("LowSpecToggle - " + val.ToString());

        if (val)
        {
            isLowQuality = true;
            Shader.EnableKeyword("LOW_QUALITY");
        } else
        {
            isLowQuality = false;
            Shader.DisableKeyword("LOW_QUALITY");
        }

    }

    // Update is called once per frame
    void Update ()
    {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;

        //Shader.EnableKeyword("LOW_QUALITY");
        //Shader.DisableKeyword("LOW_QUALITY");

        float dx = 0.0f;
        float dy = 0.0f;
        float dd = 0.0f;

        if (isRotatorIsActive)
        {
            dx = Input.GetAxis("Mouse X") * rotSpeed;
            dy = Input.GetAxis("Mouse Y") * -rotSpeed;
        }

        dd = Input.GetAxis("Mouse ScrollWheel") * zoomSpeed;


        dist += dd;
        pitch += dx;
        yaw += dy;

        yaw = Mathf.Clamp(yaw, yawMin, yawMax);

        Quaternion rotation = Quaternion.Euler(yaw, pitch, 0);

        //Debug.Log(string.Format("d:{0}, min:{1}, max:{2}", dist, zoomMin, zoomMax));

        dist = Mathf.Clamp(dist, zoomMin, zoomMax);


        Vector3 offset = new Vector3(0.0f, 0.0f, -dist);

        Vector3 position = rotation * offset + origin;

        transform.rotation = rotation;
        transform.position = position;

        if (Input.GetMouseButtonDown(1))
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            isRotatorIsActive = true;
        }

        if (Input.GetMouseButtonUp(1))
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            isRotatorIsActive = false;
        }



    }

    void OnGUI()
    {
        int w = Screen.width, h = Screen.height;

        GUIStyle style = new GUIStyle();

        Rect rect = new Rect(0, 0, w, h * 2 / 100);
        style.alignment = TextAnchor.UpperLeft;
        style.fontSize = 16;
        style.normal.textColor = new Color(0.2f, 0.8f, 0.2f, 1.0f);
        float msec = deltaTime * 1000.0f;
        string technique = isLowQuality ? "Single detail texture (fast)" : "Multiple detail textures (maximum quality)";
        string text = string.Format("{0:0.0} ms, Technique : ", msec) + technique;
        GUI.Label(rect, text, style);
    }

    public void OnQuitButtonPressed()
    {
        Application.Quit();
    }
}
