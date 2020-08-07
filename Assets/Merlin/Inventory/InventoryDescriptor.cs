
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Merlin
{
    [AddComponentMenu("Merlin/Inventory Descriptor")]
    public class InventoryDescriptor : MonoBehaviour
    {
        [System.Serializable]
        public class InventorySlot
        {
            public string slotName;
            public Texture2D slotIcon;
            public GameObject[] slotToggleItems;
            public bool startEnabled;
        }

        public bool advancedMode = false;
        public AnimatorController basisAnimator;
        public VRCExpressionsMenu basisMenu;
        public VRCExpressionParameters basisStageParameters;
        public VRCAvatarDescriptor.AnimLayerType inventoryAnimLayer = VRCAvatarDescriptor.AnimLayerType.Gesture;
        public InventorySlot[] inventorySlots;
        public string descriptorGUID;

        private void Reset()
        {
            inventoryAnimLayer = VRCAvatarDescriptor.AnimLayerType.Gesture;
            basisAnimator = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/VRCSDK/Examples3/Animation/Controllers/vrc_AvatarV3HandsLayer.controller");
            descriptorGUID = GUID.Generate().ToString();
            hideFlags = HideFlags.DontSaveInBuild;
        }

        public void GenerateInventory()
        {
            string generatedDirPath = $"Assets/Merlin/Inventory/_generated/{descriptorGUID}";

            if (!Directory.Exists(generatedDirPath))
            {
                Directory.CreateDirectory(generatedDirPath);
            }

            // Generate the stage parameters for the inventory toggles
            VRCExpressionParameters inventoryStageParams;
            string stageParameterPath = $"{generatedDirPath}/customStageParams.asset";

            if (basisStageParameters != null)
            {
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(basisStageParameters), stageParameterPath);
                inventoryStageParams = AssetDatabase.LoadAssetAtPath<VRCExpressionParameters>(stageParameterPath);
            }
            else
            {
                inventoryStageParams = ScriptableObject.CreateInstance<VRCExpressionParameters>();
                AssetDatabase.CreateAsset(inventoryStageParams, $"{generatedDirPath}/customStageParams.asset");
            }

            List<VRCExpressionParameters.Parameter> originalParams = new List<VRCExpressionParameters.Parameter>();

            if (inventoryStageParams.parameters != null)
            {
                foreach (VRCExpressionParameters.Parameter param in inventoryStageParams.parameters)
                {
                    if (!string.IsNullOrEmpty(param.name))
                    {
                        originalParams.Add(new VRCExpressionParameters.Parameter() { name = param.name, valueType = param.valueType });
                    }
                }
            }
            
            if (inventorySlots.Length + originalParams.Count > 16)
            {
                Debug.LogError($"Cannot have more than {16 - originalParams.Count} inventory slots");
                return;
            }

            VRCExpressionParameters.Parameter[] basisParameters = inventoryStageParams.parameters;
            inventoryStageParams.parameters = new VRCExpressionParameters.Parameter[16];

            for (int i = 0; i < originalParams.Count; ++i) inventoryStageParams.parameters[i] = originalParams[i];

            for (int i = originalParams.Count; i < inventorySlots.Length + originalParams.Count; ++i)
                inventoryStageParams.parameters[i] = new VRCExpressionParameters.Parameter() { name = $"GenInventorySlot{i - originalParams.Count}", valueType = VRCExpressionParameters.ValueType.Int };

            for (int i = originalParams.Count + inventorySlots.Length; i < 16; ++i) // Clear out empty params
                inventoryStageParams.parameters[i] = new VRCExpressionParameters.Parameter() { name = "", valueType = VRCExpressionParameters.ValueType.Float };

            // Generate menu asset
            VRCExpressionsMenu menuAsset;
            string menuPath = $"{generatedDirPath}/expressionMenu.asset";
            if (basisMenu)
            {
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(basisMenu), menuPath);
                menuAsset = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(menuPath);
            }
            else
            {
                menuAsset = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                AssetDatabase.CreateAsset(menuAsset, menuPath);
            }
            
            for (int i = 0; i < inventorySlots.Length; ++i)
            {
                menuAsset.controls.Add(new VRCExpressionsMenu.Control()
                {
                    icon = inventorySlots[i].slotIcon,
                    name = inventorySlots[i].slotName,
                    parameter = new VRCExpressionsMenu.Control.Parameter() { name = $"GenInventorySlot{i}" },
                    type = VRCExpressionsMenu.Control.ControlType.Toggle,
                    value = 1,
                });
            }

            // Generate controller
            AnimatorController controller;
            string controllerPath = $"{generatedDirPath}/inventoryController.controller";

            if (basisAnimator)
            {
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(basisAnimator), controllerPath);

                controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            }
            else
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            }

            AnimationClip[] inventoryClips = new AnimationClip[inventorySlots.Length];
            
            // Generate layer mask
            AvatarMask maskEverything = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; ++i)
                maskEverything.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);

            maskEverything.name = "maskEverythingMask";
            AssetDatabase.AddObjectToAsset(maskEverything, controller);

            // Generate animation clips
            for (int i = 0; i < inventorySlots.Length; ++i)
            {
                InventorySlot slot = inventorySlots[i];

                // Set initial object state
                foreach (GameObject toggleObject in slot.slotToggleItems)
                    if (toggleObject)
                        toggleObject.SetActive(slot.startEnabled);

                string animationClipPath = $"{generatedDirPath}/Animations/_toggle{i}.anim";
                AnimationClip toggleClip = GenerateToggleClip(slot.slotToggleItems, !slot.startEnabled);

                //AssetDatabase.CreateAsset(toggleClip, animationClipPath);

                inventoryClips[i] = toggleClip;

                toggleClip.name = $"toggleAnim{i}";
                AssetDatabase.AddObjectToAsset(toggleClip, controller);
            }

            // Generate controller layers
            for (int i = 0; i < inventorySlots.Length; ++i)
            {
                string paramName = $"GenInventorySlot{i}";
                controller.AddParameter(paramName, AnimatorControllerParameterType.Int);

                string layerName = $"GenToggleLayer{i}";

                AnimatorControllerLayer toggleLayer = new AnimatorControllerLayer();
                toggleLayer.name = layerName;
                toggleLayer.defaultWeight = 1f;
                toggleLayer.stateMachine = new AnimatorStateMachine();
                toggleLayer.stateMachine.name = toggleLayer.name;
                toggleLayer.stateMachine.hideFlags = HideFlags.HideInHierarchy;
                toggleLayer.avatarMask = maskEverything;

                if (AssetDatabase.GetAssetPath(controller) != "")
                    AssetDatabase.AddObjectToAsset(toggleLayer.stateMachine, AssetDatabase.GetAssetPath(controller));

                AnimatorStateMachine stateMachine = toggleLayer.stateMachine;

                AnimatorState nullState = stateMachine.AddState("Null State", stateMachine.entryPosition + new Vector3(200f, 0f));
                AnimatorState toggleState = stateMachine.AddState("Toggle Triggered", stateMachine.entryPosition + new Vector3(500f, 0f));
                toggleState.motion = inventoryClips[i];
                
                AnimatorStateTransition toToggle = nullState.AddTransition(toggleState);
                toToggle.exitTime = 0f;
                toToggle.hasExitTime = false;
                toToggle.hasFixedDuration = true;
                toToggle.duration = 0f;

                AnimatorStateTransition toNull = toggleState.AddTransition(nullState);
                toNull.exitTime = 0f;
                toNull.hasExitTime = false;
                toNull.hasFixedDuration = true;
                toNull.duration = 0f;

                toToggle.AddCondition(AnimatorConditionMode.Greater, 0f, paramName);
                toNull.AddCondition(AnimatorConditionMode.Equals, 0f, paramName);

                controller.AddLayer(toggleLayer);
            }

            // Setup layers on the avatar descriptor
            VRCAvatarDescriptor descriptor = GetComponent<VRCAvatarDescriptor>();

            descriptor.expressionsMenu = menuAsset;
            descriptor.expressionParameters = inventoryStageParams;

            VRCAvatarDescriptor.CustomAnimLayer layer = new VRCAvatarDescriptor.CustomAnimLayer();
            layer.isDefault = false;
            layer.animatorController = controller;
            layer.type = inventoryAnimLayer;

            for (int i = 0; i < descriptor.baseAnimationLayers.Length; ++i)
            {
                if (descriptor.baseAnimationLayers[i].type == inventoryAnimLayer)
                {
                    descriptor.baseAnimationLayers[i] = layer;
                    break;
                }
            }

            AssetDatabase.SaveAssets();
        }

        private string GetGameObjectAnimPath(GameObject gameObject)
        {
            string path = gameObject.name;

            Transform currentTransform = gameObject.transform.parent;
            while (currentTransform != null && currentTransform.parent != null)
            {
                path = $"{currentTransform.gameObject.name}/{path}";
                currentTransform = currentTransform.parent;
            }

            return path;
        }

        private AnimationClip GenerateToggleClip(GameObject[] gameObjects, bool enableToggle)
        {
            AnimationClip clip = new AnimationClip();

            for (int i = 0; i < gameObjects.Length; ++i)
            {
                if (gameObjects[i])
                {
                    AnimationCurve enableCurve = new AnimationCurve(new Keyframe[] { new Keyframe(0f, enableToggle ? 1f : 0f) });
                    AnimationUtility.SetEditorCurve(clip, new EditorCurveBinding() { path = GetGameObjectAnimPath(gameObjects[i]), propertyName = "m_IsActive", type = typeof(GameObject) }, enableCurve);
                }
            }

            return clip;
        }
    }

    [CustomEditor(typeof(InventoryDescriptor))]
    public class InventoryDescriptorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            InventoryDescriptor descriptor = (InventoryDescriptor)target;
            if (descriptor.GetComponent<VRCAvatarDescriptor>() == null)
            {
                EditorGUILayout.HelpBox("Inventory Descriptor must be on the same object as the VRC Avatar Descriptor", MessageType.Error);
                return;
            }

            SerializedObject descriptorObject = new SerializedObject(descriptor);
            SerializedProperty advProperty = descriptorObject.FindProperty(nameof(InventoryDescriptor.advancedMode));
            SerializedProperty inventoryProperty = descriptorObject.FindProperty(nameof(InventoryDescriptor.inventorySlots));

            EditorGUI.BeginChangeCheck();

            
            EditorGUILayout.PropertyField(advProperty);
            if (descriptor.advancedMode)
            {
                SerializedProperty basisAnimator = descriptorObject.FindProperty(nameof(InventoryDescriptor.basisAnimator));
                SerializedProperty basisMenu = descriptorObject.FindProperty(nameof(InventoryDescriptor.basisMenu));
                SerializedProperty basisStageParameters = descriptorObject.FindProperty(nameof(InventoryDescriptor.basisStageParameters));
                SerializedProperty inventoryAnimLayer = descriptorObject.FindProperty(nameof(InventoryDescriptor.inventoryAnimLayer));

                EditorGUILayout.PropertyField(basisAnimator);
                EditorGUILayout.PropertyField(basisMenu);
                EditorGUILayout.PropertyField(basisStageParameters);
                EditorGUILayout.PropertyField(inventoryAnimLayer);

            }

            EditorGUILayout.PropertyField(inventoryProperty, true);

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.Space();
            EditorGUILayout.TextField("Inventory GUID", descriptor.descriptorGUID);
            EditorGUI.EndDisabledGroup();


            if (EditorGUI.EndChangeCheck())
                descriptorObject.ApplyModifiedProperties();

            EditorGUILayout.Space();

            if (GUILayout.Button("Generate Inventory"))
            {
                descriptor.GenerateInventory();
            }
        }
    }
}

#endif
