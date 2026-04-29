using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using XRInputDevice = UnityEngine.XR.InputDevice;

public class XRInputDiagnostics : MonoBehaviour
{
    const float PinchThresholdMeters = 0.035f;

    readonly List<XRInputDevice> m_Devices = new();
    readonly List<XRHandSubsystem> m_HandSubsystems = new();
    readonly StringBuilder m_Text = new();

    Transform m_XrOrigin;
    Camera m_MainCamera;
    GameObject m_HudRoot;
    TextMesh m_StatusText;
    Marker m_LeftControllerMarker;
    Marker m_RightControllerMarker;
    Marker m_LeftHandMarker;
    Marker m_RightHandMarker;
    XRHandSubsystem m_HandSubsystem;

    Material m_LeftControllerMaterial;
    Material m_RightControllerMaterial;
    Material m_LeftHandMaterial;
    Material m_RightHandMaterial;
    Material m_ActiveMaterial;
    bool m_DiagnosticsVisible;
    bool m_ToggleWasPressed;

    void Awake()
    {
        m_XrOrigin = FindXrOrigin();
        CreateMaterials();
        CreateHud();
        CreateMarkers();
    }

    void Update()
    {
        if (m_MainCamera == null)
            m_MainCamera = Camera.main;

        if (m_XrOrigin == null)
            m_XrOrigin = FindXrOrigin();

        RefreshHandSubsystem();
        HandleToggle();

        if (!m_DiagnosticsVisible)
        {
            SetMarkersVisible(false);
            return;
        }

        var leftController = UpdateController(
            InputDeviceCharacteristics.Left,
            m_LeftControllerMarker,
            m_LeftControllerMaterial,
            out var leftControllerSummary);

        var rightController = UpdateController(
            InputDeviceCharacteristics.Right,
            m_RightControllerMarker,
            m_RightControllerMaterial,
            out var rightControllerSummary);

        var leftHand = UpdateHand(true, m_LeftHandMarker, m_LeftHandMaterial, out var leftHandSummary);
        var rightHand = UpdateHand(false, m_RightHandMarker, m_RightHandMaterial, out var rightHandSummary);

        UpdateHud(leftControllerSummary, rightControllerSummary, leftHandSummary, rightHandSummary);

        if (!leftController)
            m_LeftControllerMarker.SetVisible(false);

        if (!rightController)
            m_RightControllerMarker.SetVisible(false);

        if (!leftHand)
            m_LeftHandMarker.SetVisible(false);

        if (!rightHand)
            m_RightHandMarker.SetVisible(false);
    }

    Transform FindXrOrigin()
    {
        var origin = GameObject.Find("XR Origin");
        if (origin != null)
            return origin.transform;

        origin = GameObject.Find("XROriginCameraRig");
        return origin != null ? origin.transform : null;
    }

    void CreateMaterials()
    {
        m_LeftControllerMaterial = CreateMaterial(new Color(0.1f, 0.45f, 1f, 1f));
        m_RightControllerMaterial = CreateMaterial(new Color(1f, 0.25f, 0.2f, 1f));
        m_LeftHandMaterial = CreateMaterial(new Color(0f, 0.9f, 1f, 1f));
        m_RightHandMaterial = CreateMaterial(new Color(1f, 0.2f, 0.95f, 1f));
        m_ActiveMaterial = CreateMaterial(new Color(1f, 0.9f, 0.1f, 1f));
    }

    Material CreateMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        var material = new Material(shader);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        else if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        return material;
    }

    void CreateHud()
    {
        m_MainCamera = Camera.main;
        var parent = m_MainCamera != null ? m_MainCamera.transform : transform;

        m_HudRoot = new GameObject("XR Diagnostics HUD");
        m_HudRoot.transform.SetParent(parent, false);
        m_HudRoot.transform.localPosition = new Vector3(-0.72f, -0.22f, 1.55f);
        m_HudRoot.transform.localRotation = Quaternion.identity;
        m_HudRoot.transform.localScale = Vector3.one;

        var textObject = new GameObject("Status Text");
        textObject.transform.SetParent(m_HudRoot.transform, false);
        textObject.transform.localPosition = Vector3.zero;
        textObject.transform.localRotation = Quaternion.identity;

        m_StatusText = textObject.AddComponent<TextMesh>();
        m_StatusText.anchor = TextAnchor.UpperLeft;
        m_StatusText.alignment = TextAlignment.Left;
        m_StatusText.fontSize = 44;
        m_StatusText.characterSize = 0.01f;
        m_StatusText.color = Color.white;
        m_StatusText.text = "XR diagnostics starting...";
        m_HudRoot.SetActive(m_DiagnosticsVisible);
    }

    void CreateMarkers()
    {
        m_LeftControllerMarker = new Marker("L Controller", PrimitiveType.Cube, 0.08f);
        m_RightControllerMarker = new Marker("R Controller", PrimitiveType.Cube, 0.08f);
        m_LeftHandMarker = new Marker("L Hand", PrimitiveType.Sphere, 0.055f);
        m_RightHandMarker = new Marker("R Hand", PrimitiveType.Sphere, 0.055f);
    }

    void HandleToggle()
    {
        var togglePressed = F1WasPressed();
        var leftController = GetController(InputDeviceCharacteristics.Left);
        if (leftController.isValid)
            togglePressed |= ReadButton(leftController, CommonUsages.secondaryButton);

        if (togglePressed && !m_ToggleWasPressed)
        {
            m_DiagnosticsVisible = !m_DiagnosticsVisible;
            if (m_HudRoot != null)
                m_HudRoot.SetActive(m_DiagnosticsVisible);

            if (!m_DiagnosticsVisible)
                SetMarkersVisible(false);
        }

        m_ToggleWasPressed = togglePressed;
    }

    bool F1WasPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        return keyboard != null && keyboard.f1Key.wasPressedThisFrame;
#else
        return false;
#endif
    }

    void SetMarkersVisible(bool visible)
    {
        m_LeftControllerMarker.SetVisible(visible);
        m_RightControllerMarker.SetVisible(visible);
        m_LeftHandMarker.SetVisible(visible);
        m_RightHandMarker.SetVisible(visible);
    }

    bool UpdateController(
        InputDeviceCharacteristics side,
        Marker marker,
        Material baseMaterial,
        out string summary)
    {
        var device = GetController(side);
        if (!device.isValid)
        {
            summary = "not found";
            return false;
        }

        var hasPosition = device.TryGetFeatureValue(CommonUsages.devicePosition, out var localPosition);
        var hasRotation = device.TryGetFeatureValue(CommonUsages.deviceRotation, out var localRotation);
        var isTracked = !device.TryGetFeatureValue(CommonUsages.isTracked, out var trackedValue) || trackedValue;

        var hasPose = hasPosition && hasRotation && isTracked;
        var trigger = ReadFloat(device, CommonUsages.trigger);
        var grip = ReadFloat(device, CommonUsages.grip);
        var pressed = trigger > 0.6f ||
            grip > 0.6f ||
            ReadButton(device, CommonUsages.primaryButton) ||
            ReadButton(device, CommonUsages.secondaryButton) ||
            ReadButton(device, CommonUsages.menuButton);

        if (hasPose)
        {
            var worldPose = ToWorldPose(localPosition, localRotation);
            marker.SetPose(worldPose.position, worldPose.rotation);
            marker.SetMaterial(pressed ? m_ActiveMaterial : baseMaterial);
            marker.SetScale(pressed ? 1.35f : 1f);
        }

        summary = hasPose
            ? $"tracked | trigger {trigger:0.00} | grip {grip:0.00} | buttons {(pressed ? "ON" : "off")}"
            : "device found, pose not tracked";

        return hasPose;
    }

    XRInputDevice GetController(InputDeviceCharacteristics side)
    {
        var characteristics = InputDeviceCharacteristics.HeldInHand |
            InputDeviceCharacteristics.Controller |
            side;

        InputDevices.GetDevicesWithCharacteristics(characteristics, m_Devices);
        if (m_Devices.Count > 0)
            return m_Devices[0];

        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller | side, m_Devices);
        return m_Devices.Count > 0 ? m_Devices[0] : default;
    }

    bool UpdateHand(bool left, Marker marker, Material baseMaterial, out string summary)
    {
        if (m_HandSubsystem == null || !m_HandSubsystem.running)
        {
            summary = "hand subsystem not running";
            return false;
        }

        var hand = left ? m_HandSubsystem.leftHand : m_HandSubsystem.rightHand;
        if (!hand.isTracked)
        {
            summary = "not tracked";
            return false;
        }

        var palmPoseAvailable = TryGetJointWorldPose(hand, XRHandJointID.Palm, out var palmPose);
        if (!palmPoseAvailable)
            palmPose = ToWorldPose(hand.rootPose.position, hand.rootPose.rotation);

        var pinching = TryGetPinchDistance(hand, out var pinchDistance) &&
            pinchDistance <= PinchThresholdMeters;

        marker.SetPose(palmPose.position, palmPose.rotation);
        marker.SetMaterial(pinching ? m_ActiveMaterial : baseMaterial);
        marker.SetScale(pinching ? 1.45f : 1f);

        summary = pinchDistance >= 0
            ? $"tracked | pinch {pinchDistance * 100f:0.0} cm | {(pinching ? "PINCH" : "open")}"
            : "tracked | pinch unavailable";

        return true;
    }

    void RefreshHandSubsystem()
    {
        if (m_HandSubsystem != null && m_HandSubsystem.running)
            return;

        SubsystemManager.GetSubsystems(m_HandSubsystems);
        m_HandSubsystem = null;

        foreach (var subsystem in m_HandSubsystems)
        {
            if (subsystem.running)
            {
                m_HandSubsystem = subsystem;
                return;
            }
        }

        if (m_HandSubsystems.Count > 0)
            m_HandSubsystem = m_HandSubsystems[0];
    }

    bool TryGetJointWorldPose(XRHand hand, XRHandJointID jointId, out Pose worldPose)
    {
        var joint = hand.GetJoint(jointId);
        if (!joint.TryGetPose(out var localPose))
        {
            worldPose = Pose.identity;
            return false;
        }

        worldPose = ToWorldPose(localPose.position, localPose.rotation);
        return true;
    }

    bool TryGetPinchDistance(XRHand hand, out float distance)
    {
        if (!TryGetJointWorldPose(hand, XRHandJointID.ThumbTip, out var thumbPose) ||
            !TryGetJointWorldPose(hand, XRHandJointID.IndexTip, out var indexPose))
        {
            distance = -1f;
            return false;
        }

        distance = Vector3.Distance(thumbPose.position, indexPose.position);
        return true;
    }

    Pose ToWorldPose(Vector3 localPosition, Quaternion localRotation)
    {
        var origin = m_XrOrigin != null ? m_XrOrigin : transform;
        return new Pose(
            origin.TransformPoint(localPosition),
            origin.rotation * localRotation);
    }

    float ReadFloat(XRInputDevice device, InputFeatureUsage<float> usage)
    {
        return device.TryGetFeatureValue(usage, out var value) ? value : 0f;
    }

    bool ReadButton(XRInputDevice device, InputFeatureUsage<bool> usage)
    {
        return device.TryGetFeatureValue(usage, out var value) && value;
    }

    void UpdateHud(
        string leftController,
        string rightController,
        string leftHand,
        string rightHand)
    {
        m_Text.Clear();
        m_Text.AppendLine("XR INPUT DIAGNOSTICS");
        m_Text.AppendLine("Controllers: move, trigger, grip, A/B/X/Y.");
        m_Text.AppendLine("Hands: put controllers down, then pinch thumb + index.");
        m_Text.AppendLine();
        m_Text.Append("XR Origin: ");
        m_Text.AppendLine(m_XrOrigin != null ? m_XrOrigin.name : "not found");
        m_Text.Append("Hand subsystem: ");
        m_Text.AppendLine(m_HandSubsystem != null && m_HandSubsystem.running ? "running" : "not running");
        m_Text.AppendLine();
        m_Text.Append("L Controller: ");
        m_Text.AppendLine(leftController);
        m_Text.Append("R Controller: ");
        m_Text.AppendLine(rightController);
        m_Text.Append("L Hand: ");
        m_Text.AppendLine(leftHand);
        m_Text.Append("R Hand: ");
        m_Text.AppendLine(rightHand);

        if (m_StatusText != null)
            m_StatusText.text = m_Text.ToString();
    }

    class Marker
    {
        readonly GameObject m_Object;
        readonly Renderer m_Renderer;
        readonly Vector3 m_BaseScale;

        public Marker(string name, PrimitiveType primitiveType, float scale)
        {
            m_Object = GameObject.CreatePrimitive(primitiveType);
            m_Object.name = name;
            m_BaseScale = Vector3.one * scale;
            m_Object.transform.localScale = m_BaseScale;
            m_Renderer = m_Object.GetComponent<Renderer>();
            SetVisible(false);
        }

        public void SetPose(Vector3 position, Quaternion rotation)
        {
            m_Object.SetActive(true);
            m_Object.transform.SetPositionAndRotation(position, rotation);
        }

        public void SetVisible(bool visible)
        {
            m_Object.SetActive(visible);
        }

        public void SetMaterial(Material material)
        {
            if (m_Renderer != null && material != null)
                m_Renderer.material = material;
        }

        public void SetScale(float multiplier)
        {
            m_Object.transform.localScale = m_BaseScale * multiplier;
        }
    }
}
