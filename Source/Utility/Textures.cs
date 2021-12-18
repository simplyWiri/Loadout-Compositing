using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Inventory {

    [StaticConstructorOnStartup]
    public static class Textures {

        public static readonly Texture ValvetTex = SolidColorMaterials.NewSolidColorTexture(GenColor.FromHex("cc1a00"));
        public static readonly Texture RWPrimaryTex = SolidColorMaterials.NewSolidColorTexture(GenColor.FromHex("6a512e"));

        public static readonly Texture2D ApparelTex = ContentFinder<Texture2D>.Get("Apparel");
        public static readonly Texture2D EditTex = ContentFinder<Texture2D>.Get("Edit");
        public static readonly Texture2D MedicalTex = ContentFinder<Texture2D>.Get("Medical");
        public static readonly Texture2D MeleeTex = ContentFinder<Texture2D>.Get("Melee");
        public static readonly Texture2D MiscItemsTex = ContentFinder<Texture2D>.Get("MiscItems");
        public static readonly Texture2D NextTex = ContentFinder<Texture2D>.Get("Next");
        public static readonly Texture2D PreviousTex = ContentFinder<Texture2D>.Get("Previous");
        public static readonly Texture2D RangedTex = ContentFinder<Texture2D>.Get("Ranged");

    }

}