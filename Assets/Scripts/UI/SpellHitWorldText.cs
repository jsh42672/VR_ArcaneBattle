using ArcaneVR.Spell;
using UnityEngine;

namespace ArcaneVR.UI
{
    /// <summary>
    /// Lightweight world-space hit label for VR debugging and presentations.
    /// Uses Unity TextMesh, not Canvas.
    /// </summary>
    [DisallowMultipleComponent]
    public class SpellHitWorldText : MonoBehaviour
    {
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 1.05f, 0f);
        [SerializeField] private int fontSize = 48;
        [SerializeField] private float characterSize = 0.035f;
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private Color fireColor = new Color(1f, 0.35f, 0.15f, 1f);
        [SerializeField] private Color iceColor = new Color(0.35f, 0.7f, 1f, 1f);
        [SerializeField] private Color thunderColor = new Color(1f, 0.9f, 0.1f, 1f);

        private TextMesh textMesh;
        private Transform textTransform;

        private void Awake()
        {
            EnsureTextMesh();
            SetIdleText();
        }

        private void LateUpdate()
        {
            if (textTransform == null)
                return;

            var cameraTransform = Camera.main != null ? Camera.main.transform : null;
            if (cameraTransform == null)
                return;

            var lookDirection = textTransform.position - cameraTransform.position;
            if (lookDirection.sqrMagnitude > 0.001f)
                textTransform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        }

        public void ShowHit(ElementType element, StatusEffect statusEffect, float damage)
        {
            EnsureTextMesh();
            textMesh.text = $"Hit: {element} | {statusEffect} | DMG: {damage:0.#}";
            textMesh.color = GetElementColor(element);
        }

        private void SetIdleText()
        {
            if (textMesh == null)
                return;

            textMesh.text = "Hit: -";
            textMesh.color = textColor;
        }

        private void EnsureTextMesh()
        {
            if (textMesh != null)
                return;

            var textObjectName = "HitInfo_WorldText";
            var existing = transform.Find(textObjectName);
            var textObject = existing != null ? existing.gameObject : new GameObject(textObjectName);
            textObject.transform.SetParent(transform, false);
            textObject.transform.localPosition = localOffset;
            textObject.transform.localScale = Vector3.one;

            textMesh = textObject.GetComponent<TextMesh>();
            if (textMesh == null)
                textMesh = textObject.AddComponent<TextMesh>();

            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.fontSize = fontSize;
            textMesh.characterSize = characterSize;
            textMesh.text = string.Empty;

            textTransform = textObject.transform;
        }

        private Color GetElementColor(ElementType element)
        {
            return element switch
            {
                ElementType.Fire => fireColor,
                ElementType.Ice => iceColor,
                ElementType.Thunder => thunderColor,
                _ => textColor
            };
        }
    }
}
