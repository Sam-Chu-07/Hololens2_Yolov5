//
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
//
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HoloToolkit.Yolov5.ObjectDetection
{
    public class Element : MonoBehaviour
    {
        public static Element ActiveElement;

        public TextMesh ElementName;
        public Text DataProductName;
        public Text DataPrice;
        public Text DataManufacture;
        public Text DataIngredient;
        public Text DataCalorie;

        public Renderer BoxRenderer;
        public MeshRenderer[] PanelSides;
        public MeshRenderer PanelFront;
        public MeshRenderer PanelBack;
        public MeshRenderer[] InfoPanels;


        [HideInInspector]
        public GoodsData data;

        private BoxCollider boxCollider;
        private Material dimMaterial;
        private Material clearMaterial;
        private PresentToPlayer present;

        public void SetActiveElement()
        {
            Element element = gameObject.GetComponent<Element>();
            ActiveElement = element;
        }

        public void ResetActiveElement()
        {
            ActiveElement = null;
            Invoke("UnLock", 1);
        }

        void UnLock()
        {
            GameObject.Find("ScriptLoader").GetComponent<PeriodicTableLoader>().Lock = false; // 重啟辨識
        }

        public void Start()
        {
            // Turn off our animator until it's needed
            GetComponent<Animator>().enabled = false;
            BoxRenderer.enabled = true;
            present = GetComponent<PresentToPlayer>();
        }

        public void Open()
        {
            if (present.Presenting)
                return;

            GameObject.Find("ScriptLoader").GetComponent<PeriodicTableLoader>().Lock = true; // 暫停辨識
            StartCoroutine(UpdateActive());
        }

        public void Dim()
        {
            if (ActiveElement == this)
                return;

            for (int i = 0; i < PanelSides.Length; i++)
            {
                PanelSides[i].sharedMaterial = dimMaterial;
            }
            PanelBack.sharedMaterial = dimMaterial;
            PanelFront.sharedMaterial = dimMaterial;
            BoxRenderer.sharedMaterial = dimMaterial;
        }

        public IEnumerator UpdateActive()
        {
            present.Present();

            while (!present.InPosition)
            {
                // Wait for the item to be in presentation distance before animating
                yield return null;
            }

            // Start the animation
            Animator animator = gameObject.GetComponent<Animator>();
            animator.enabled = true;
            animator.SetBool("Opened", true);

            while (Element.ActiveElement == this)
            {
                // Wait for the player to send it back
                yield return null;
            }

            animator.SetBool("Opened", false);

            yield return new WaitForSeconds(0.66f); // TODO get rid of magic number        

            // Return the item to its original position
            present.Return();
            Dim();
        }
  

        public void SetFromGoodsData(GoodsData data, Dictionary<int, Material> typeMaterials)
        {
            this.data = data;
            ElementName.text = data.Symbol;
            ElementName.text = ElementName.text.Replace("@", "\n");
            DataIngredient.text = "\n原料:\n" + data.Ingredients;
            DataProductName.text = data.Name;
            DataCalorie.text = data.Calorie.ToString() + " 大卡";
            DataManufacture.text = data.Manufacturer;
            DataPrice.text = "NT$ " + data.Price.ToString();

            // Set up our materials
            System.Random Ran = new System.Random();//Ran.Next(1, 10)
            typeMaterials.TryGetValue(9, out dimMaterial);
            
            Dim();

            foreach (Renderer infoPanel in InfoPanels)
            {
                // Copy the color of the element over to the information panels so they match
                infoPanel.material.color = dimMaterial.color;
            }

            BoxRenderer.enabled = false;

            // Set our name so the container can alphabetize
            transform.parent.name = data.Name;
        }
    }
}
