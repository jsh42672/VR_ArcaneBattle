using UnityEngine;

namespace ArcaneVR.UI
{
    [CreateAssetMenu(fileName = "NewGrimoirePage", menuName = "Grimoire/Page Data")]
    public class GrimoirePageData : ScriptableObject
    {
        public string pageTitle;
        public Sprite magicSymbol;
        [TextArea(3, 10)]
        public string description;
        public Color themeColor = Color.white;
    }
}
