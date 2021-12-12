using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Microsoft.MixedReality.Toolkit.Utilities;
using UnityEngine;
using UnityEngine.UI;


namespace HoloToolkit.Yolov5.ObjectDetection
{
    [System.Serializable]
    public class ElementData
    {
        public string name;
        public string category; //素材顏色
        public string spectral_img;
        public int xpos;
        public int ypos;
        public string named_by;
        public string symbol;
        public float price;
        public string material;
        public string number;
        public string summary;
    }

    [System.Serializable]
    class ElementsData
    {
        public List<ElementData> elements;

        public static ElementsData FromJSON(string json)
        {
            return JsonUtility.FromJson<ElementsData>(json);
        }
    }

    [System.Serializable]
    public class GoodsData
    {
        public string Name;         // 商品名稱
        public string Symbol;       // 商品價格
        public int Price;           // 商品價格
        public string Manufacturer; // 製造商
        public string Ingredients;  // 商品成分
        public float Calorie;       // 熱量
        public int Color;           // 物件材質顏色
        public string Note;         // 備註
        public float X;
        public float Y;
    }

 

    public class PeriodicTableLoader : MonoBehaviour
    {
        // What object to parent the instantiated elements to
        public Transform Parent;

        // Generic element prefab to instantiate at each position in the table
        public GameObject ElementPrefab;

        // How much space to put between each element prefab
        public float ElementSeperationDistance;

        // Legend
        public Transform LegendTransform;

        public Text refObject;

        public Material MatRed;
        public Material MatOrange;
        public Material MatYellow;
        public Material MatGreen;
        public Material MatSpringgreen;
        public Material MatAqua;
        public Material MatBlue;
        public Material MatPurple;
        public Material MatPink;

        public bool Lock = false;
        private float preserveDistance = 0.05f;
        private Dictionary<int, Material> typeMaterials;
        private List<GameObject> _CreatedObjects;

        private void Start()
        {
            InitializeData();
        }

        public void InitializeData()
        {
            _CreatedObjects = new List<GameObject>();

            typeMaterials = new Dictionary<int, Material>()
            {
                { 1, MatRed },
                { 2, MatOrange },
                { 3, MatYellow },
                { 4, MatGreen },
                { 5, MatSpringgreen },
                { 6, MatAqua },
                { 7, MatBlue },
                { 8, MatPurple },
                { 9, MatPink },
            };
        }

        public void CreateElement(List<GoodsData> goodsInfo)
        {
            if (Lock)
                return;

            List<GameObject> preserveObj = new List<GameObject>();
            List<GameObject> addObj = new List<GameObject>();

            foreach (GoodsData data in goodsInfo)
            {
                refObject.transform.localPosition = new Vector3(data.X / 10 - 75.2f, 42.3f - data.Y / 10, -5);

                if(!isExist(refObject.transform.position, data.Name, preserveObj))
                {
                    GameObject newElement = Instantiate<GameObject>(ElementPrefab, Parent);
                    newElement.GetComponentInChildren<Element>().SetFromGoodsData(data, typeMaterials);
                    newElement.transform.localPosition = refObject.transform.position;
                    newElement.transform.localRotation = refObject.transform.rotation;
                    addObj.Add(newElement);
                }
            }
            UpdateElement(preserveObj, addObj);
        }

        public void ClearElement()
        {
            if (Lock)
                return;

            foreach (var obj in _CreatedObjects)
            {
                Destroy(obj);
            }
            _CreatedObjects.Clear();
        }

        private void UpdateElement(List<GameObject> preserveObj, List<GameObject> addObj)
        {
            var CreatedObj = _CreatedObjects.ToList();
            foreach (var obj in CreatedObj)
            {
                if(!preserveObj.Contains(obj))
                {
                    Destroy(obj);
                    _CreatedObjects.Remove(obj);
                }
                else
                    obj.transform.rotation = refObject.transform.rotation;
            }

            foreach (var obj in addObj)
                 _CreatedObjects.Add(obj);
        }

        private bool isExist(Vector3 goodsPos, string goodsName, List<GameObject> preserveObj)
        {
            foreach (var existObj in _CreatedObjects)
            {
                if (existObj.name == goodsName && Vector3.Distance(existObj.transform.position, goodsPos) < preserveDistance)
                {
                    preserveObj.Add(existObj);
                    return true;
                }
            }
            return false;
        }

        public void test()
        {
            Debug.Log("TestTestTest");
        }
    }
}