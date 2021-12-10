using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Inventory
{
    [StaticConstructorOnStartup]
    public static class Textures
    {
        public static readonly Texture2D PlaceholderTex = ContentFinder<Texture2D>.Get("Placeholder");
        public static readonly Texture ValvetTex = SolidColorMaterials.NewSolidColorTexture(GenColor.FromHex("cc1a00"));
        public static readonly Texture RWPrimaryTex = SolidColorMaterials.NewSolidColorTexture(GenColor.FromHex("6a512e"));
    }
}
