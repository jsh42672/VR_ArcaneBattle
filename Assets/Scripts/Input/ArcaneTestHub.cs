using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using XRInputDevice = UnityEngine.XR.InputDevice;

public class ArcaneTestHub : MonoBehaviour
{
    const float PinchThresholdMeters = 0.035f;

    readonly List<XRInputDevice> m_Devices = new List<XRInputDevice>();
    readonly List<XRHandSubsystem> m_HandSubsystems = new List<XRHandSubsystem>();
    readonly StringBuilder m_HudBuilder = new StringBuilder();

    Transform m_XrOrigin;
    Camera m_MainCamera;
    TextMesh m_HudText;
    ArcaneTestTarget m_Target;
    XRHandSubsystem m_HandSubsystem;
    Material m_ProjectileMaterial;
    Material m_TargetMaterial;
    Material m_TargetHitMaterial;

    float m_NextFireTime;
    float m_LastHitTime = -99f;
    int m_HitCount;
    bool m_RightTriggerWasPressed;
    bool m_RightPrimaryWasPressed;
    bool m_RightPinchWasPressed;

    void Awake()
    {
        m_XrOrigin = FindXrOrigin();
        m_MainCamera = Camera.main;
        CreateMaterials();
        CreateHud();
        CreateTarget();
    }

    void Update()
    {
        if (m_XrOrigin == null)
            m_XrOrigin = FindXrOrigin();

        if (m_MainCamera == null)
            m_MainCamera = Camera.main;

        RefreshHandSubsystem();
        HandleFireInput();
        UpdateHud();
    }

    Transform FindXrOrigin()
    {
        var origin = GameObject.Find("XR Origin");
        if (origin != null)
            return origin.transform;

        origin = GameObject.Find("XROriginCameraRig");
        return origin != null ? origin.transform : transform;
    }

    void CreateMaterials()
    {
        m_ProjectileMaterial = CreateMaterial(new Color(0.15f, 0.65f, 1f, 1f));
        m_TargetMaterial = CreateMaterial(new Color(0.38f, 0.36f, 0.42f, 1f));
        m_TargetHitMaterial = CreateMaterial(new Color(1f, 0.22f, 0.15f, 1f));
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
        var parent = m_MainCamera != null ? m_MainCamera.transform : transform;
        var hudRoot = new GameObject("Arcane Test HUD");
        hudRoot.transform.SetParent(parent, false);
        hudRoot.transform.localPosition = new Vector3(-0.42f, 0.24f, 1.45f);
        hudRoot.transform.localRotation = Quaternion.identity;

        var textObject = new GameObject("Status Text");
        textObject.transform.SetParent(hudRoot.transform, false);
        textObject.transform.localPosition = Vector3.zero;
        textObject.transform.localRotation = Quaternion.identity;

        m_HudText = textObject.AddComponent<TextMesh>();
        m_HudText.anchor = TextAnchor.UpperLeft;
        m_HudText.alignment = TextAlignment.Left;
        m_HudText.fontSize = 48;
        m_HudText.characterSize = 0.012f;
        m_HudText.color = Color.white;
    }

    void CreateTarget()
    {
        var targetRoot = new GameObject("Test Dummy Golem");
        targetRoot.transform.SetParent(null);
        targetRoot.transform.position = new Vector3(0f, 0.9f, 4f);
        targetRoot.transform.rotation = Quaternion.identity;

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Golem Body";
        body.transform.SetParent(targetRoot.transform, false);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = new Vector3(0.8f, 1.2f, 0.8f);

        var renderer = body.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material = m_TargetMaterial;

        var bodyCollider = body.GetComponent<Collider>();
        if (bodyCollider != null)
            bodyCollider.isTrigger = true;

        var rigidbody = targetRoot.AddComponent<Rigidbody>();
        rigidbody.useGravity = false;
        rigidbody.isKinematic = true;

        var labelObject = new GameObject("Target Label");
        labelObject.transform.SetParent(targetRoot.transform, false);
        labelObject.transform.localPosition = new Vector3(-0.55f, 1.25f, 0f);

        var label = labelObject.AddComponent<TextMesh>();
        label.anchor = TextAnchor.MiddleLeft;
        label.alignment = TextAlignment.Left;
        label.fontSize = 52;
        label.characterSize = 0.018f;
        label.color = Color.white;

        m_Target = targetRoot.AddComponent<ArcaneTestTarget>();
        m_Target.Initialize(renderer, label, m_TargetMaterial, m_TargetHitMaterial, OnTargetHit);
    }

    void HandleFireInput()
    {
        var fired = false;
        var rightController = GetController(InputDeviceCharacteristics.Right);

        if (rightController.isValid)
        {
            var triggerPressed = ReadFloat(rightController, CommonUsages.trigger) > 0.75f;
            var primaryPressed = ReadButton(rightController, CommonUsages.primaryButton);

            if ((triggerPressed && !m_RightTriggerWasPressed) ||
                (primaryPressed && !m_RightPrimaryWasPressed))
            {
                fired = TryFireFromController(rightController);
            }

            m_RightTriggerWasPressed = triggerPressed;
            m_RightPrimaryWasPressed = primaryPressed;
        }

        if (!fired)
        {
            var pinchPressed = TryGetRightHandPinch(out var pinchPose) &&
                Time.time >= m_NextFireTime;

            if (pinchPressed && !m_RightPinchWasPressed)
                fired = Fire(pinchPose.position, GetAimDirection(pinchPose.rotation), "Right pinch");

            m_RightPinchWasPressed = pinchPressed;
        }

        if (!fired && SpaceWasPressed())
        {
            var origin = m_MainCamera != null ? m_MainCamera.transform : transform;
            Fire(origin.position + origin.forward * 0.25f, origin.forward, "Keyboard");
        }
    }

    bool SpaceWasPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        return keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
#else
        return false;
#endif
    }

    bool TryFireFromController(XRInputDevice device)
    {
        if (Time.time < m_NextFireTime)
            return false;

        if (!device.TryGetFeatureValue(CommonUsages.devicePosition, out var localPosition) ||
            !device.TryGetFeatureValue(CommonUsages.deviceRotation, out var localRotation))
        {
            return false;
        }

        var pose = ToWorldPose(localPosition, localRotation);
        return Fire(pose.position, GetAimDirection(pose.rotation), "Right controller");
    }

    bool Fire(Vector3 position, Vector3 direction, string source)
    {
        if (Time.time < m_NextFireTime)
            return false;

        if (direction.sqrMagnitude < 0.01f)
            direction = m_MainCamera != null ? m_MainCamera.transform.forward : Vector3.forward;

        var projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectileObject.name = "Arcane Test Bolt";
        projectileObject.transform.position = position + direction.normalized * 0.18f;
        projectileObject.transform.localScale = Vector3.one * 0.14f;

        var renderer = projectileObject.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material = m_ProjectileMaterial;

        var collider = projectileObject.GetComponent<SphereCollider>();
        if (collider != null)
            collider.isTrigger = true;

        var rigidbody = projectileObject.AddComponent<Rigidbody>();
        rigidbody.useGravity = false;
        rigidbody.isKinematic = true;

        var projectile = projectileObject.AddComponent<ArcaneTestProjectile>();
        projectile.Launch(direction.normalized, 8f, 25f, source);

        m_NextFireTime = Time.time + 0.75f;
        return true;
    }

    Vector3 GetAimDirection(Quaternion sourceRotation)
    {
        var sourceForward = sourceRotation * Vector3.forward;
        var cameraForward = m_MainCamera != null ? m_MainCamera.transform.forward : sourceForward;
        return Vector3.Slerp(sourceForward, cameraForward, 0.25f).normalized;
    }

    bool TryGetRightHandPinch(out Pose pinchPose)
    {
        pinchPose = Pose.identity;

        if (m_HandSubsystem == null || !m_HandSubsystem.running)
            return false;

        var hand = m_HandSubsystem.rightHand;
        if (!hand.isTracked)
            return false;

        if (!TryGetJointWorldPose(hand, XRHandJointID.ThumbTip, out var thumbPose) ||
            !TryGetJointWorldPose(hand, XRHandJointID.IndexTip, out var indexPose))
        {
            return false;
        }

        var distance = Vector3.Distance(thumbPose.position, indexPose.position);
        if (distance > PinchThresholdMeters)
            return false;

        pinchPose = new Pose((thumbPose.position + indexPose.position) * 0.5f, indexPose.rotation);
        return true;
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

    Pose ToWorldPose(Vector3 localPosition, Quaternion localRotation)
    {
        var origin = m_XrOrigin != null ? m_XrOrigin : transform;
        return new Pose(origin.TransformPoint(localPosition), origin.rotation * localRotation);
    }

    XRInputDevice GetController(InputDeviceCharacteristics side)
    {
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Controller | side,
            m_Devices);

        if (m_Devices.Count > 0)
            return m_Devices[0];

        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller | side, m_Devices);
        return m_Devices.Count > 0 ? m_Devices[0] : default;
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

    float ReadFloat(XRInputDevice device, InputFeatureUsage<float> usage)
    {
        return device.TryGetFeatureValue(usage, out var value) ? value : 0f;
    }

    bool ReadButton(XRInputDevice device, InputFeatureUsage<bool> usage)
    {
        return device.TryGetFeatureValue(usage, out var value) && value;
    }

    void OnTargetHit(float damage, string source)
    {
        m_HitCount++;
        m_LastHitTime = Time.time;
    }

    void UpdateHud()
    {
        if (m_HudText == null)
            return;

        var cooldown = Mathf.Max(0f, m_NextFireTime - Time.time);
        var targetHealth = m_Target != null ? m_Target.Health : 0f;

        m_HudBuilder.Clear();
        m_HudBuilder.AppendLine("ARCANE TEST HUB");
        m_HudBuilder.AppendLine("Right trigger/A: bolt");
        m_HudBuilder.AppendLine("Right hand pinch: bolt");
        m_HudBuilder.AppendLine("Y/F1: diagnostics");
        m_HudBuilder.AppendLine();
        m_HudBuilder.Append("Cooldown: ");
        m_HudBuilder.AppendLine(cooldown <= 0f ? "READY" : cooldown.ToString("0.0"));
        m_HudBuilder.Append("Dummy HP: ");
        m_HudBuilder.AppendLine(targetHealth.ToString("0"));
        m_HudBuilder.Append("Hits: ");
        m_HudBuilder.AppendLine(m_HitCount.ToString());
        m_HudBuilder.Append("Last hit: ");
        m_HudBuilder.AppendLine(Time.time - m_LastHitTime < 1.2f ? "YES" : "-");

        m_HudText.text = m_HudBuilder.ToString();
    }
}

public class ArcaneTestProjectile : MonoBehaviour
{
    Vector3 m_Direction;
    float m_Speed;
    float m_Damage;
    float m_SpawnTime;
    string m_Source;

    public void Launch(Vector3 direction, float speed, float damage, string source)
    {
        m_Direction = direction.normalized;
        m_Speed = speed;
        m_Damage = damage;
        m_Source = source;
        m_SpawnTime = Time.time;
    }

    void Update()
    {
        transform.position += m_Direction * (m_Speed * Time.deltaTime);

        if (Time.time - m_SpawnTime > 4f)
            Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        var target = other.GetComponentInParent<ArcaneTestTarget>();
        if (target == null)
            return;

        target.ApplyDamage(m_Damage, m_Source);
        Destroy(gameObject);
    }
}

public class ArcaneTestTarget : MonoBehaviour
{
    Renderer m_Renderer;
    TextMesh m_Label;
    Material m_NormalMaterial;
    Material m_HitMaterial;
    System.Action<float, string> m_OnHit;
    float m_Health = 100f;
    float m_LastHitTime = -99f;

    public float Health => m_Health;

    public void Initialize(
        Renderer targetRenderer,
        TextMesh label,
        Material normalMaterial,
        Material hitMaterial,
        System.Action<float, string> onHit)
    {
        m_Renderer = targetRenderer;
        m_Label = label;
        m_NormalMaterial = normalMaterial;
        m_HitMaterial = hitMaterial;
        m_OnHit = onHit;
        UpdateLabel();
    }

    public void ApplyDamage(float damage, string source)
    {
        m_Health = Mathf.Max(0f, m_Health - damage);
        m_LastHitTime = Time.time;
        m_OnHit?.Invoke(damage, source);

        if (m_Renderer != null)
            m_Renderer.material = m_HitMaterial;

        if (m_Health <= 0f)
            Invoke(nameof(ResetTarget), 1f);

        UpdateLabel();
    }

    void Update()
    {
        if (m_Renderer != null && Time.time - m_LastHitTime > 0.18f)
            m_Renderer.material = m_NormalMaterial;

        if (m_Label != null && Camera.main != null)
            m_Label.transform.rotation = Quaternion.LookRotation(m_Label.transform.position - Camera.main.transform.position);
    }

    void ResetTarget()
    {
        m_Health = 100f;
        UpdateLabel();
    }

    void UpdateLabel()
    {
        if (m_Label != null)
            m_Label.text = $"Dummy Golem\nHP {m_Health:0}/100";
    }
}
