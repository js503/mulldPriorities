using UnityEngine;
using Verse;

namespace Mulld.Priorities
{
    [StaticConstructorOnStartup]
    public static class UiAssets
    {
        public static readonly Texture2D Preview = ContentFinder<Texture2D>.Get("UI/Mulld/Preview");
    }
}
