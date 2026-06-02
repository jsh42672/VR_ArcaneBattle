using System.Collections.Generic;
using UnityEngine;

namespace ArcaneVR.UI
{
    [CreateAssetMenu(fileName = "NewGrimoireData", menuName = "Grimoire/Grimoire Data")]
    public class GrimoireData : ScriptableObject
    {
        public List<GrimoirePageData> pages = new List<GrimoirePageData>();
    }
}
