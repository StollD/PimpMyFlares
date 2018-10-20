using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Kopernicus;
using Kopernicus.Components;
using Kopernicus.Configuration;
using UnityEngine;

namespace PimpMyFlares
{
    [RequireConfigType(ConfigType.Node)]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [ParserTargetExternal("Light", "Flare", "Kopernicus")]
    public class FlareLoader : BaseLoader, IParserEventSubscriber
    {
        private static Dictionary<String, Byte[]> _encodedCache = new Dictionary<String, Byte[]>();

        /// <summary>
        /// Defines how the texture gets interpreted by Unity
        /// </summary>
        public enum TextureLayout
        {
            OneLargeFourSmall,
            OneLargeTwoMediumEightSmall,
            OneTexture,
            TwoxTwoGrid,
            ThreexThreeGrid,
            FourxFourGrid
        }

        [RequireConfigType(ConfigType.Node)]
        public class FlareElement
        {
            [ParserTarget("imageIndex")] 
            public NumericParser<Int32> imageIndex;

            [ParserTarget("position")] 
            public NumericParser<Single> position;
            
            [ParserTarget("size")] 
            public NumericParser<Single> size;
            
            [ParserTarget("color")] 
            public ColorParser color;
            
            [ParserTarget("useLightColor")] 
            public NumericParser<Boolean> useLightColor;
            
            [ParserTarget("rotate")] 
            public NumericParser<Boolean> rotate;
            
            [ParserTarget("zoom")] 
            public NumericParser<Boolean> zoom;
            
            [ParserTarget("fade")] 
            public NumericParser<Boolean> fade;
        }

        [ParserTarget("texture")]
        public Texture2DParser texture;

        [ParserTarget("cache")]
        public String cache;

        [ParserTarget("layout")]
        public EnumParser<TextureLayout> layout;

        [ParserTarget("useFog")]
        public NumericParser<Boolean> useFog;
        
        [ParserTargetCollection("Elements", NameSignificance = NameSignificance.None)]
        public List<FlareElement> elements = new List<FlareElement>();


        void IParserEventSubscriber.Apply(ConfigNode node) { }

        void IParserEventSubscriber.PostApply(ConfigNode node)
        {
            // Encode the texture as .png
            Byte[] textureData = null;
            if (!_encodedCache.ContainsKey(texture.ValueToString()))
            {
                textureData = Utility.CreateReadable(texture.Value).EncodeToPNG();
                _encodedCache.Add(texture.ValueToString(), textureData);
            }
            else
            {
                textureData = _encodedCache[texture.ValueToString()];
            }

            // Build a .flare object
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("%YAML 1.1");
            builder.AppendLine("%TAG !u! tag:unity3d.com,2011:");
            builder.AppendLine("--- !u!121 &12100000");
            builder.AppendLine("Flare:");
            builder.AppendLine("  m_ObjectHideFlags: 0");
            builder.AppendLine("  m_PrefabParentObject: {fileID: 0}");
            builder.AppendLine("  m_PrefabInternal: {fileID: 0}");
            builder.AppendLine("  m_Name: Flare");
            builder.AppendLine("  m_FlareTexture: {fileID: 2800000, guid: 0b430725379d3428d974f7b47d7de9f0, type: 3}");
            builder.AppendLine("  m_TextureLayout: " + (Int32) layout.Value);
            builder.AppendLine("  m_Elements:");
            foreach (FlareElement element in elements)
            {
                builder.AppendLine("  - m_ImageIndex: " + element.imageIndex.Value);
                builder.AppendLine("    m_Position: " + element.position.Value);
                builder.AppendLine("    m_Size: " + element.size.Value);
                builder.AppendLine("    m_Color: {r: " + element.color.Value.r + ", g: " + element.color.Value.g +
                                   ", b: " +
                                   element.color.Value.b + ", a: " + element.color.Value.a + "}");
                builder.AppendLine("    m_UseLightColor: " + (element.useLightColor ? 1 : 0));
                builder.AppendLine("    m_Rotate: " + (element.rotate ? 1 : 0));
                builder.AppendLine("    m_Zoom: " + (element.zoom ? 1 : 0));
                builder.AppendLine("    m_Fade: " + (element.fade ? 1 : 0));
            }

            builder.AppendLine("  m_UseFog: " + (useFog ? 1 : 0));
            Byte[] flare = Encoding.UTF8.GetBytes(builder.ToStringAndRelease());
            
            // Hash the data to see if the flare was already downloaded
            SHA1 hash = SHA1.Create();
            String textureHash = BitConverter.ToString(hash.ComputeHash(textureData));
            String configHash = BitConverter.ToString(hash.ComputeHash(flare));

            String dir = KSPUtil.ApplicationRootPath + "GameData/" + Body.ScaledSpaceCacheDirectory + "/PimpMyFlares";
            String file = dir + "/" + (textureHash + configHash).Replace("-", "").ToLower() + ".unity3d";
            if (!String.IsNullOrEmpty(cache))
            {
                dir = Path.GetDirectoryName(cache);
                file = cache;
            }
            Directory.CreateDirectory(dir);
            if (File.Exists(file))
            {
                LoadFlareAsset(AssetBundle.LoadFromMemory(File.ReadAllBytes(file)));
                return;
            }
            
            // Start a www request
            WWWForm request = new WWWForm();
            request.AddField("unity", Application.unityVersion);
            request.AddBinaryData("Flare.png", textureData, "Flare.png");
            request.AddBinaryData("Flare.flare", flare, "Flare.flare");
            WWW www = new WWW("https://tmsp.io/unity/flare", request);
            while (!www.isDone) { Thread.Sleep(100); }
            LoadFlareAsset(www.assetBundle);
            File.WriteAllBytes(file, www.bytes);
        }

        private void LoadFlareAsset(AssetBundle bundle)
        {
            // Get the flare asset
            Flare flare = UnityEngine.Object.Instantiate(bundle.LoadAsset<Flare>("Flare"));
            UnityEngine.Object.DontDestroyOnLoad(flare);
            
            // Assign the flare
            generatedBody.scaledVersion.GetComponentInChildren<LightShifter>().sunFlare = flare;
        }
    }
}