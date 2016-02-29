using System;
using System.Xml.Serialization;
using System.Collections.Generic;
using System.IO;

namespace GTANetwork
{
    public class GameSettings
    {
        public static Settings LoadGameSettings()
        {
            var filePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments,
                Environment.SpecialFolderOption.Create) + "Rockstar Games\\GTA V\\settings.xml";
            if (!File.Exists(filePath)) return null;

            using (var stream = File.OpenRead(filePath))
            {
                var ser = new XmlSerializer(typeof(Settings));
                var settings = (Settings) ser.Deserialize(stream);
                return settings;
            }
        }

        public static void SaveSettings(Settings sets)
        {
            var filePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments,
                Environment.SpecialFolderOption.Create) + "Rockstar Games\\GTA V\\settings.xml";
            using (var stream = new FileStream(filePath, File.Exists(filePath) ? FileMode.Truncate : FileMode.CreateNew)
                )
            {
                var ser = new XmlSerializer(typeof(Settings));
                ser.Serialize(stream, sets);
            }
        }


        [XmlRoot(ElementName = "version")]
        public class Version
        {
            [XmlAttribute(AttributeName = "value")]
            public int Value { get; set; }
        }

        [XmlRoot(ElementName = "Tessellation")]
        public class Tessellation
        {
            [XmlAttribute(AttributeName = "value")]
            public int Value { get; set; }
        }

        [XmlRoot(ElementName = "LodScale")]
        public class LodScale
        {
            [XmlAttribute(AttributeName = "value")]
            public double Value { get; set; }
        }

        [XmlRoot(ElementName = "PedLodBias")]
        public class PedLodBias
        {
            [XmlAttribute(AttributeName = "value")]
            public double Value { get; set; }
        }

        [XmlRoot(ElementName = "VehicleLodBias")]
        public class VehicleLodBias
        {
            [XmlAttribute(AttributeName = "value")]
            public double Value { get; set; }
        }

        [XmlRoot(ElementName = "ShadowQuality")]
        public class ShadowQuality
        {
            [XmlAttribute(AttributeName = "value")]
            public int Value { get; set; }
        }

        [XmlRoot(ElementName = "ReflectionQuality")]
        public class ReflectionQuality
        {
            [XmlAttribute(AttributeName = "value")]
            public int Value { get; set; }
        }

        [XmlRoot(ElementName = "ReflectionMSAA")]
        public class ReflectionMSAA
        {
            [XmlAttribute(AttributeName = "value")]
            public int Value { get; set; }
        }

        [XmlRoot(ElementName = "SSAO")]
        public class SSAO
        {
            [XmlAttribute(AttributeName = "value")]
            public int Value { get; set; }
        }

        [XmlRoot(ElementName = "AnisotropicFiltering")]
        public class AnisotropicFiltering
        {
            [XmlAttribute(AttributeName = "value")]
            public int Value { get; set; }
        }

        [XmlRoot(ElementName = "MSAA")]
        public class MSAA
        {
            [XmlAttribute(AttributeName = "value")]
            public int Value { get; set; }
        }

        [XmlRoot(ElementName = "MSAAFragments")]
        public class MSAAFragments
        {
            [XmlAttribute(AttributeName = "value")]
            public int Value { get; set; }
        }

        [XmlRoot(ElementName = "MSAAQuality")]
        public class MSAAQuality
        {
            [XmlAttribute(AttributeName = "value")]
            public int Value { get; set; }
        }

        [XmlRoot(ElementName = "SamplingMode")]
        public class SamplingMode
        {
            [XmlAttribute(AttributeName = "value")]
            public int Value { get; set; }
        }

        [XmlRoot(ElementName = "TextureQuality")]
        public class TextureQuality
        {
            [XmlAttribute(AttributeName = "value")]
            public int Value { get; set; }
        }

        [XmlRoot(ElementName = "ParticleQuality")]
        public class ParticleQuality
        {
            [XmlAttribute(AttributeName = "value")]
            public int Value { get; set; }
        }

        [XmlRoot(ElementName = "WaterQuality")]
        public class WaterQuality
        {
            [XmlAttribute(AttributeName = "value")]
            public int Value { get; set; }
        }

        [XmlRoot(ElementName = "GrassQuality")]
        public class GrassQuality
        {
            [XmlAttribute(AttributeName = "value")]
            public int Value { get; set; }
        }

        [XmlRoot(ElementName = "ShaderQuality")]
        public class ShaderQuality
        {
            [XmlAttribute(AttributeName = "value")]
            public int Value { get; set; }
        }

        [XmlRoot(ElementName = "Shadow_SoftShadows")]
        public class Shadow_SoftShadows
        {
            [XmlAttribute(AttributeName = "value")]
            public int Value { get; set; }
        }

        [XmlRoot(ElementName = "UltraShadows_Enabled")]
        public class UltraShadows_Enabled
        {
            [XmlAttribute(AttributeName = "value")]
            public bool Value { get; set; }
        }

        [XmlRoot(ElementName = "Shadow_ParticleShadows")]
        public class Shadow_ParticleShadows
        {
            [XmlAttribute(AttributeName = "value")]
            public bool Value { get; set; }
        }

        [XmlRoot(ElementName = "Shadow_Distance")]
        public class Shadow_Distance
        {
            [XmlAttribute(AttributeName = "value")]
            public double Value { get; set; }
        }

        [XmlRoot(ElementName = "Shadow_LongShadows")]
        public class Shadow_LongShadows
        {
            [XmlAttribute(AttributeName = "value")]
            public bool Value { get; set; }
        }

        [XmlRoot(ElementName = "Shadow_SplitZStart")]
        public class Shadow_SplitZStart
        {
            [XmlAttribute(AttributeName = "value")]
            public double Value { get; set; }
        }

        [XmlRoot(ElementName = "Shadow_SplitZEnd")]
        public class Shadow_SplitZEnd
        {
            [XmlAttribute(AttributeName = "value")]
            public double Value { get; set; }
        }

        [XmlRoot(ElementName = "Shadow_aircraftExpWeight")]
        public class Shadow_aircraftExpWeight
        {
            [XmlAttribute(AttributeName = "value")]
            public double Value { get; set; }
        }

        [XmlRoot(ElementName = "Shadow_DisableScreenSizeCheck")]
        public class Shadow_DisableScreenSizeCheck
        {
            [XmlAttribute(AttributeName = "value")]
            public bool Value { get; set; }
        }

        [XmlRoot(ElementName = "Reflection_MipBlur")]
        public class Reflection_MipBlur
        {
            [XmlAttribute(AttributeName = "value")]
            public bool Value { get; set; }
        }

        [XmlRoot(ElementName = "FXAA_Enabled")]
        public class FXAA_Enabled
        {
            [XmlAttribute(AttributeName = "value")]
            public bool Value { get; set; }
        }

        [XmlRoot(ElementName = "TXAA_Enabled")]
        public class TXAA_Enabled
        {
            [XmlAttribute(AttributeName = "value")]
            public bool Value { get; set; }
        }

        [XmlRoot(ElementName = "Lighting_FogVolumes")]
        public class Lighting_FogVolumes
        {
            [XmlAttribute(AttributeName = "value")]
            public bool Value { get; set; }
        }

        [XmlRoot(ElementName = "Shader_SSA")]
        public class Shader_SSA
        {
            [XmlAttribute(AttributeName = "value")]
            public bool Value { get; set; }
        }

        [XmlRoot(ElementName = "DX_Version")]
        public class DX_Version
        {
            [XmlAttribute(AttributeName = "value")]
            public int Value { get; set; }
        }

        [XmlRoot(ElementName = "CityDensity")]
        public class CityDensity
        {
            [XmlAttribute(AttributeName = "value")]
            public double Value { get; set; }
        }

        [XmlRoot(ElementName = "PedVarietyMultiplier")]
        public class PedVarietyMultiplier
        {
            [XmlAttribute(AttributeName = "value")]
            public double Value { get; set; }
        }

        [XmlRoot(ElementName = "VehicleVarietyMultiplier")]
        public class VehicleVarietyMultiplier
        {
            [XmlAttribute(AttributeName = "value")]
            public double Value { get; set; }
        }

        [XmlRoot(ElementName = "PostFX")]
        public class PostFX
        {
            [XmlAttribute(AttributeName = "value")]
            public int Value { get; set; }
        }

        [XmlRoot(ElementName = "DoF")]
        public class DoF
        {
            [XmlAttribute(AttributeName = "value")]
            public bool Value { get; set; }
        }

        [XmlRoot(ElementName = "HdStreamingInFlight")]
        public class HdStreamingInFlight
        {
            [XmlAttribute(AttributeName = "value")]
            public bool Value { get; set; }
        }

        [XmlRoot(ElementName = "MaxLodScale")]
        public class MaxLodScale
        {
            [XmlAttribute(AttributeName = "value")]
            public double Value { get; set; }
        }

        [XmlRoot(ElementName = "MotionBlurStrength")]
        public class MotionBlurStrength
        {
            [XmlAttribute(AttributeName = "value")]
            public double Value { get; set; }
        }

        [XmlRoot(ElementName = "graphics")]
        public class Graphics
        {
            [XmlElement(ElementName = "Tessellation")]
            public Tessellation Tessellation { get; set; }
            [XmlElement(ElementName = "LodScale")]
            public LodScale LodScale { get; set; }
            [XmlElement(ElementName = "PedLodBias")]
            public PedLodBias PedLodBias { get; set; }
            [XmlElement(ElementName = "VehicleLodBias")]
            public VehicleLodBias VehicleLodBias { get; set; }
            [XmlElement(ElementName = "ShadowQuality")]
            public ShadowQuality ShadowQuality { get; set; }
            [XmlElement(ElementName = "ReflectionQuality")]
            public ReflectionQuality ReflectionQuality { get; set; }
            [XmlElement(ElementName = "ReflectionMSAA")]
            public ReflectionMSAA ReflectionMSAA { get; set; }
            [XmlElement(ElementName = "SSAO")]
            public SSAO SSAO { get; set; }
            [XmlElement(ElementName = "AnisotropicFiltering")]
            public AnisotropicFiltering AnisotropicFiltering { get; set; }
            [XmlElement(ElementName = "MSAA")]
            public MSAA MSAA { get; set; }
            [XmlElement(ElementName = "MSAAFragments")]
            public MSAAFragments MSAAFragments { get; set; }
            [XmlElement(ElementName = "MSAAQuality")]
            public MSAAQuality MSAAQuality { get; set; }
            [XmlElement(ElementName = "SamplingMode")]
            public SamplingMode SamplingMode { get; set; }
            [XmlElement(ElementName = "TextureQuality")]
            public TextureQuality TextureQuality { get; set; }
            [XmlElement(ElementName = "ParticleQuality")]
            public ParticleQuality ParticleQuality { get; set; }
            [XmlElement(ElementName = "WaterQuality")]
            public WaterQuality WaterQuality { get; set; }
            [XmlElement(ElementName = "GrassQuality")]
            public GrassQuality GrassQuality { get; set; }
            [XmlElement(ElementName = "ShaderQuality")]
            public ShaderQuality ShaderQuality { get; set; }
            [XmlElement(ElementName = "Shadow_SoftShadows")]
            public Shadow_SoftShadows Shadow_SoftShadows { get; set; }
            [XmlElement(ElementName = "UltraShadows_Enabled")]
            public UltraShadows_Enabled UltraShadows_Enabled { get; set; }
            [XmlElement(ElementName = "Shadow_ParticleShadows")]
            public Shadow_ParticleShadows Shadow_ParticleShadows { get; set; }
            [XmlElement(ElementName = "Shadow_Distance")]
            public Shadow_Distance Shadow_Distance { get; set; }
            [XmlElement(ElementName = "Shadow_LongShadows")]
            public Shadow_LongShadows Shadow_LongShadows { get; set; }
            [XmlElement(ElementName = "Shadow_SplitZStart")]
            public Shadow_SplitZStart Shadow_SplitZStart { get; set; }
            [XmlElement(ElementName = "Shadow_SplitZEnd")]
            public Shadow_SplitZEnd Shadow_SplitZEnd { get; set; }
            [XmlElement(ElementName = "Shadow_aircraftExpWeight")]
            public Shadow_aircraftExpWeight Shadow_aircraftExpWeight { get; set; }
            [XmlElement(ElementName = "Shadow_DisableScreenSizeCheck")]
            public Shadow_DisableScreenSizeCheck Shadow_DisableScreenSizeCheck { get; set; }
            [XmlElement(ElementName = "Reflection_MipBlur")]
            public Reflection_MipBlur Reflection_MipBlur { get; set; }
            [XmlElement(ElementName = "FXAA_Enabled")]
            public FXAA_Enabled FXAA_Enabled { get; set; }
            [XmlElement(ElementName = "TXAA_Enabled")]
            public TXAA_Enabled TXAA_Enabled { get; set; }
            [XmlElement(ElementName = "Lighting_FogVolumes")]
            public Lighting_FogVolumes Lighting_FogVolumes { get; set; }
            [XmlElement(ElementName = "Shader_SSA")]
            public Shader_SSA Shader_SSA { get; set; }
            [XmlElement(ElementName = "DX_Version")]
            public DX_Version DX_Version { get; set; }
            [XmlElement(ElementName = "CityDensity")]
            public CityDensity CityDensity { get; set; }
            [XmlElement(ElementName = "PedVarietyMultiplier")]
            public PedVarietyMultiplier PedVarietyMultiplier { get; set; }
            [XmlElement(ElementName = "VehicleVarietyMultiplier")]
            public VehicleVarietyMultiplier VehicleVarietyMultiplier { get; set; }
            [XmlElement(ElementName = "PostFX")]
            public PostFX PostFX { get; set; }
            [XmlElement(ElementName = "DoF")]
            public DoF DoF { get; set; }
            [XmlElement(ElementName = "HdStreamingInFlight")]
            public HdStreamingInFlight HdStreamingInFlight { get; set; }
            [XmlElement(ElementName = "MaxLodScale")]
            public MaxLodScale MaxLodScale { get; set; }
            [XmlElement(ElementName = "MotionBlurStrength")]
            public MotionBlurStrength MotionBlurStrength { get; set; }
        }

        [XmlRoot(ElementName = "numBytesPerReplayBlock")]
        public class NumBytesPerReplayBlock
        {
            [XmlAttribute(AttributeName = "value")]
            public int Value { get; set; }
        }

        [XmlRoot(ElementName = "numReplayBlocks")]
        public class NumReplayBlocks
        {
            [XmlAttribute(AttributeName = "value")]
            public int Value { get; set; }
        }

        [XmlRoot(ElementName = "maxSizeOfStreamingReplay")]
        public class MaxSizeOfStreamingReplay
        {
            [XmlAttribute(AttributeName = "value")]
            public int Value { get; set; }
        }

        [XmlRoot(ElementName = "maxFileStoreSize")]
        public class MaxFileStoreSize
        {
            [XmlAttribute(AttributeName = "value")]
            public int Value { get; set; }
        }

        [XmlRoot(ElementName = "system")]
        public class System
        {
            [XmlElement(ElementName = "numBytesPerReplayBlock")]
            public NumBytesPerReplayBlock NumBytesPerReplayBlock { get; set; }
            [XmlElement(ElementName = "numReplayBlocks")]
            public NumReplayBlocks NumReplayBlocks { get; set; }
            [XmlElement(ElementName = "maxSizeOfStreamingReplay")]
            public MaxSizeOfStreamingReplay MaxSizeOfStreamingReplay { get; set; }
            [XmlElement(ElementName = "maxFileStoreSize")]
            public MaxFileStoreSize MaxFileStoreSize { get; set; }
        }

        [XmlRoot(ElementName = "Audio3d")]
        public class Audio3d
        {
            [XmlAttribute(AttributeName = "value")]
            public bool Value { get; set; }
        }

        [XmlRoot(ElementName = "audio")]
        public class Audio
        {
            [XmlElement(ElementName = "Audio3d")]
            public Audio3d Audio3d { get; set; }
        }

        [XmlRoot(ElementName = "AdapterIndex")]
        public class AdapterIndex
        {
            [XmlAttribute(AttributeName = "value")]
            public int Value { get; set; }
        }

        [XmlRoot(ElementName = "OutputIndex")]
        public class OutputIndex
        {
            [XmlAttribute(AttributeName = "value")]
            public int Value { get; set; }
        }

        [XmlRoot(ElementName = "ScreenWidth")]
        public class ScreenWidth
        {
            [XmlAttribute(AttributeName = "value")]
            public int Value { get; set; }
        }

        [XmlRoot(ElementName = "ScreenHeight")]
        public class ScreenHeight
        {
            [XmlAttribute(AttributeName = "value")]
            public int Value { get; set; }
        }

        [XmlRoot(ElementName = "RefreshRate")]
        public class RefreshRate
        {
            [XmlAttribute(AttributeName = "value")]
            public int Value { get; set; }
        }

        [XmlRoot(ElementName = "Windowed")]
        public class Windowed
        {
            [XmlAttribute(AttributeName = "value")]
            public int Value { get; set; }
        }

        [XmlRoot(ElementName = "VSync")]
        public class VSync
        {
            [XmlAttribute(AttributeName = "value")]
            public int Value { get; set; }
        }

        [XmlRoot(ElementName = "Stereo")]
        public class Stereo
        {
            [XmlAttribute(AttributeName = "value")]
            public int Value { get; set; }
        }

        [XmlRoot(ElementName = "Convergence")]
        public class Convergence
        {
            [XmlAttribute(AttributeName = "value")]
            public double Value { get; set; }
        }

        [XmlRoot(ElementName = "Separation")]
        public class Separation
        {
            [XmlAttribute(AttributeName = "value")]
            public double Value { get; set; }
        }

        [XmlRoot(ElementName = "PauseOnFocusLoss")]
        public class PauseOnFocusLoss
        {
            [XmlAttribute(AttributeName = "value")]
            public int Value { get; set; }
        }

        [XmlRoot(ElementName = "AspectRatio")]
        public class AspectRatio
        {
            [XmlAttribute(AttributeName = "value")]
            public int Value { get; set; }
        }

        [XmlRoot(ElementName = "video")]
        public class Video
        {
            [XmlElement(ElementName = "AdapterIndex")]
            public AdapterIndex AdapterIndex { get; set; }
            [XmlElement(ElementName = "OutputIndex")]
            public OutputIndex OutputIndex { get; set; }
            [XmlElement(ElementName = "ScreenWidth")]
            public ScreenWidth ScreenWidth { get; set; }
            [XmlElement(ElementName = "ScreenHeight")]
            public ScreenHeight ScreenHeight { get; set; }
            [XmlElement(ElementName = "RefreshRate")]
            public RefreshRate RefreshRate { get; set; }
            [XmlElement(ElementName = "Windowed")]
            public Windowed Windowed { get; set; }
            [XmlElement(ElementName = "VSync")]
            public VSync VSync { get; set; }
            [XmlElement(ElementName = "Stereo")]
            public Stereo Stereo { get; set; }
            [XmlElement(ElementName = "Convergence")]
            public Convergence Convergence { get; set; }
            [XmlElement(ElementName = "Separation")]
            public Separation Separation { get; set; }
            [XmlElement(ElementName = "PauseOnFocusLoss")]
            public PauseOnFocusLoss PauseOnFocusLoss { get; set; }
            [XmlElement(ElementName = "AspectRatio")]
            public AspectRatio AspectRatio { get; set; }
        }

        [XmlRoot(ElementName = "Settings")]
        public class Settings
        {
            [XmlElement(ElementName = "version")]
            public Version Version { get; set; }
            [XmlElement(ElementName = "configSource")]
            public string ConfigSource { get; set; }
            [XmlElement(ElementName = "graphics")]
            public Graphics Graphics { get; set; }
            [XmlElement(ElementName = "system")]
            public System System { get; set; }
            [XmlElement(ElementName = "audio")]
            public Audio Audio { get; set; }
            [XmlElement(ElementName = "video")]
            public Video Video { get; set; }
            [XmlElement(ElementName = "VideoCardDescription")]
            public string VideoCardDescription { get; set; }
        }
    }
}