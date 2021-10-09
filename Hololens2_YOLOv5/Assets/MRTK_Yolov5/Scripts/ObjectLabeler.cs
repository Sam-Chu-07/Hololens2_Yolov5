using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.MixedReality.Toolkit.UI;
using Microsoft.MixedReality.Toolkit.SpatialAwareness;


using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
public class ObjectLabeler : MonoBehaviour
{
    private Text refObject;
    private GameObject textBoxPrefab;
    private List<GameObject> _createdObjects;

    public ObjectLabeler(GameObject textPrefab, Text referenceObject)
    {
        _createdObjects = new List<GameObject>();
        textBoxPrefab = textPrefab;
        refObject = referenceObject;
    }

    public void AddObj()
    {
        var rand = new System.Random();
        int x = rand.Next(-75, 76);
        int y = rand.Next(-42, 43);

        Debug.Log($"x = {x}, y = {y}");
        refObject.transform.localPosition = new Vector3(x, y, -5);
        Debug.Log("Pos = " + refObject.transform.position);
        var _tempBoundingBox = Instantiate(
                textBoxPrefab,
                refObject.transform.position,
                refObject.transform.rotation) as GameObject;

        _tempBoundingBox.GetComponent<TextMesh>().text = "Cat";
        _tempBoundingBox.GetComponent<TextMesh>().color = Color.cyan;
        _tempBoundingBox.GetComponent<TextMesh>().fontSize = 20;

        _createdObjects.Add(_tempBoundingBox);
        if (_createdObjects.Count >= 10)
            clearObject();
    }

    public void LabelObjects(JObject BoxJobject, Matrix4x4 cameraToWorldMatrix, Matrix4x4 projMatrix)
    {
        clearObject();

        foreach (var box in BoxJobject["Bbox"])
        {
            var x = box["x"].ToObject<float>();
            var y = box["y"].ToObject<float>();
            var width = box["width"].ToObject<float>();
            var height = box["height"].ToObject<float>();
            var confidence = box["confidence"].ToObject<float>();
            var classs = box["class"].ToString();

            // Only draw boxes over a certain size
            if (width < 50 || height < 50)
                continue;

            // Create a new 3D text object at position and
            // set the label string.Canvas is scaled to x = -0.5, 5
            // and y = -0.5, 0.5.
            refObject.transform.localPosition = new Vector3(x/10 - 64 , 36 - y/10, -5);
            var _tempBoundingBox = Instantiate(
                textBoxPrefab,
                refObject.transform.position,
                refObject.transform.rotation) as GameObject;

            //Vector3 worldPosition = pixelToWorld((int)x, (int)y, cameraToWorldMatrix, projMatrix);
            //_tempBoundingBox.transform.position = worldPosition;

            // Set the label of the bounding box.
            var label = $"{classs}";
            _tempBoundingBox.GetComponent<TextMesh>().text = label;
            _tempBoundingBox.GetComponent<TextMesh>().color = Color.cyan;
            _tempBoundingBox.GetComponent<TextMesh>().fontSize = 20;

            _createdObjects.Add(_tempBoundingBox);
        }
    }
    public static Vector3 UnProjectVector(Matrix4x4 proj, Vector3 to)
    {
        Vector3 from = new Vector3(0, 0, 0);
        var axsX = proj.GetRow(0);
        var axsY = proj.GetRow(1);
        var axsZ = proj.GetRow(2);
        from.z = to.z / axsZ.z;
        from.y = (to.y - (from.z * axsY.z)) / axsY.y;
        from.x = (to.x - (from.z * axsX.z)) / axsX.x;
        return from;
    }

    private Vector3 pixelToWorld(int x, int y, Matrix4x4 cameraToWorldMat, Matrix4x4 projMat)
    {
        var pixelPos = new Vector2(x, y);
        var imagePosZeroToOne = new Vector2(pixelPos.x / 1280, 1 - (pixelPos.y / 720));
        var imagePosProjected = (imagePosZeroToOne * 2) - new Vector2(1, 1);    // -1 to 1 space
        var cameraSpacePos = UnProjectVector(projMat, new Vector3(imagePosProjected.x, imagePosProjected.y, 1));
        var worldSpaceRayPoint1 = cameraToWorldMat.MultiplyPoint(Vector3.zero);     // camera location in world space
        var worldSpaceRayPoint2 = cameraToWorldMat.MultiplyPoint(cameraSpacePos);   // ray point in world space
        
        RaycastHit hit;
        if (!Physics.Raycast(worldSpaceRayPoint1, worldSpaceRayPoint2 - worldSpaceRayPoint1, out hit, 5, 1 << 31))
        {
            Debug.Log("error: Physics.Raycast failed");
            return Vector3.zero;
        }
        
        return hit.point;
    }

    public void clearObject()
    {
        foreach (var label in _createdObjects)
        {
            Destroy(label);
        }
        _createdObjects.Clear();
    }
}

