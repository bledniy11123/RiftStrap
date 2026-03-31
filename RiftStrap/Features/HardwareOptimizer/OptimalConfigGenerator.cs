using RiftStrap.Features.HardwareOptimizer.Models;

namespace RiftStrap.Features.HardwareOptimizer
{

    public static class OptimalConfigGenerator
    {
        public static Dictionary<string, object> Generate(HardwareInfo hw)
        {
            return hw.Tier switch
            {
                HardwareTier.Low => GenerateLow(hw),
                HardwareTier.Mid => GenerateMid(hw),
                HardwareTier.High => GenerateHigh(hw),
                HardwareTier.Ultra => GenerateUltra(hw),
                _ => GenerateMid(hw),
            };
        }

        public static string GetDescription(HardwareTier tier)
        {
            return tier switch
            {
                HardwareTier.Low => "Optimized for smooth gameplay on low-end hardware. Reduced visual quality for better FPS.",
                HardwareTier.Mid => "Balanced quality and performance. Good visuals with stable frame rates.",
                HardwareTier.High => "High quality visuals with anti-aliasing. Optimized for powerful hardware.",
                HardwareTier.Ultra => "Maximum quality. Uncapped FPS, full anti-aliasing, highest render quality.",
                _ => "Balanced configuration."
            };
        }

        private static Dictionary<string, object> GenerateLow(HardwareInfo hw)
        {
            var flags = new Dictionary<string, object>
            {

                ["DFIntTaskSchedulerTargetFps"] = 60,
                ["FIntRenderShadowIntensity"] = 0,
                ["DFFlagDebugRenderForceTechnologyVoxel"] = true,
                ["FFlagGlobalWindRendering"] = false,

                ["FIntDebugTextureManagerSkipMips"] = 2,

                ["FFlagDebugSkyGray"] = false,
                ["DFIntMaxFrameBufferSize"] = 4,

                ["DFFlagDebugRenderForceTechnologyVoxel"] = true,
            };

            return flags;
        }

        private static Dictionary<string, object> GenerateMid(HardwareInfo hw)
        {
            var flags = new Dictionary<string, object>
            {

                ["DFIntTaskSchedulerTargetFps"] = hw.MonitorRefreshRate > 60 ? hw.MonitorRefreshRate : 60,

                ["FIntRenderShadowIntensity"] = 128,

                ["FIntDebugForceMSAASamples"] = 2,

                ["FFlagGlobalWindRendering"] = true,

                ["FIntDebugTextureManagerSkipMips"] = 0,
            };

            return flags;
        }

        private static Dictionary<string, object> GenerateHigh(HardwareInfo hw)
        {
            var flags = new Dictionary<string, object>
            {

                ["DFIntTaskSchedulerTargetFps"] = hw.MonitorRefreshRate,

                ["FIntRenderShadowIntensity"] = 255,

                ["FIntDebugForceMSAASamples"] = 4,

                ["FFlagGlobalWindRendering"] = true,
                ["FIntDebugTextureManagerSkipMips"] = 0,

                ["DFFlagDebugRenderForceTechnologyVoxel"] = false,
            };

            return flags;
        }

        private static Dictionary<string, object> GenerateUltra(HardwareInfo hw)
        {
            var flags = new Dictionary<string, object>
            {

                ["DFIntTaskSchedulerTargetFps"] = Math.Max(hw.MonitorRefreshRate, 240),

                ["FIntRenderShadowIntensity"] = 255,
                ["FIntDebugForceMSAASamples"] = 4,
                ["FFlagGlobalWindRendering"] = true,
                ["FIntDebugTextureManagerSkipMips"] = 0,
                ["DFFlagDebugRenderForceTechnologyVoxel"] = false,

                ["DFIntMaxFrameBufferSize"] = 16,
            };

            return flags;
        }
    }
}
