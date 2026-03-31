namespace RiftStrap.Features.InGameUI
{

    public static class CoreScriptInjector
    {
        private const string SettingsHubPath = "ExtraContent/scripts/CoreScripts/Modules/Settings/SettingsHub.lua";
        private const string CustomPagePath = "ExtraContent/scripts/CoreScripts/Modules/Settings/Pages/RiftStrapPage.lua";

        private const string PatchMarker = "-- RIFTSTRAP_PATCHED";

        private const string PatchCode = @"
-- RIFTSTRAP_PATCHED
-- RiftStrap: Custom ESC menu page
pcall(function()
    local RiftStrapPage = require(RobloxGui.Modules.Settings.Pages.RiftStrapPage)
    RiftStrapPage:SetHub(this)
    this:AddPage(RiftStrapPage)
end)
";

        public static bool IsEnabled
        {
            get => App.Settings.Prop.EnableCustomInGameUI;
            set
            {
                App.Settings.Prop.EnableCustomInGameUI = value;
                App.Settings.Save();
            }
        }

        public static void Apply()
        {
            if (!IsEnabled)
            {
                Remove();
                return;
            }

            try
            {
                DeployCustomPage();
                PatchSettingsHub();
                App.Logger.WriteLine("CoreScriptInjector", "CoreScript mods applied successfully");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("CoreScriptInjector", $"Failed to apply CoreScript mods: {ex.Message}");
            }
        }

        public static void Remove()
        {
            try
            {

                var pagePath = Path.Combine(Paths.Modifications, CustomPagePath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(pagePath))
                    File.Delete(pagePath);

                var hubPath = Path.Combine(Paths.Modifications, SettingsHubPath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(hubPath))
                    File.Delete(hubPath);

                App.Logger.WriteLine("CoreScriptInjector", "CoreScript mods removed");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("CoreScriptInjector", $"Failed to remove CoreScript mods: {ex.Message}");
            }
        }

        private static void DeployCustomPage()
        {
            var destPath = Path.Combine(Paths.Modifications, CustomPagePath.Replace('/', Path.DirectorySeparatorChar));
            var destDir = Path.GetDirectoryName(destPath)!;
            Directory.CreateDirectory(destDir);

            var sourcePath = Path.Combine(
                Path.GetDirectoryName(Paths.Process)!,
                "Features", "InGameUI", "CoreScriptMods", "Settings", "Pages", "RiftStrapPage.lua");

            if (File.Exists(sourcePath))
            {
                File.Copy(sourcePath, destPath, true);
            }
            else
            {

                File.WriteAllText(destPath, GetRiftStrapPageLua());
            }

            App.Logger.WriteLine("CoreScriptInjector", $"Deployed RiftStrapPage.lua to {destPath}");
        }

        private static void PatchSettingsHub()
        {
            var modPath = Path.Combine(Paths.Modifications, SettingsHubPath.Replace('/', Path.DirectorySeparatorChar));
            var modDir = Path.GetDirectoryName(modPath)!;
            Directory.CreateDirectory(modDir);

            string hubContent;

            if (File.Exists(modPath))
            {
                hubContent = File.ReadAllText(modPath);
                if (hubContent.Contains(PatchMarker))
                {
                    App.Logger.WriteLine("CoreScriptInjector", "SettingsHub.lua already patched");
                    return;
                }
            }
            else
            {

                var originalPath = FindOriginalSettingsHub();
                if (originalPath == null || !File.Exists(originalPath))
                {
                    App.Logger.WriteLine("CoreScriptInjector", "Could not find original SettingsHub.lua");
                    return;
                }
                hubContent = File.ReadAllText(originalPath);
            }

            var injectionPoint = hubContent.LastIndexOf("this:AddPage(", StringComparison.Ordinal);
            if (injectionPoint < 0)
            {
                App.Logger.WriteLine("CoreScriptInjector", "Could not find injection point in SettingsHub.lua");
                return;
            }

            var lineEnd = hubContent.IndexOf('\n', injectionPoint);
            if (lineEnd < 0) lineEnd = hubContent.Length;

            hubContent = hubContent.Insert(lineEnd + 1, PatchCode);

            File.WriteAllText(modPath, hubContent);
            App.Logger.WriteLine("CoreScriptInjector", "Patched SettingsHub.lua with RiftStrap page registration");
        }

        private static string? FindOriginalSettingsHub()
        {
            try
            {

                var versionsDir = Paths.Versions;
                if (!Directory.Exists(versionsDir))
                    return null;

                foreach (var versionDir in Directory.GetDirectories(versionsDir))
                {
                    var hubPath = Path.Combine(versionDir, SettingsHubPath.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(hubPath))
                    {
                        App.Logger.WriteLine("CoreScriptInjector", $"Found SettingsHub.lua in {versionDir}");
                        return hubPath;
                    }
                }

                var playerData = new AppData.RobloxPlayerData();
                var robloxDir = Path.GetDirectoryName(playerData.ExecutablePath);
                if (robloxDir != null)
                {
                    var hubPath = Path.Combine(robloxDir, SettingsHubPath.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(hubPath))
                        return hubPath;
                }

                return null;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("CoreScriptInjector", $"FindOriginalSettingsHub error: {ex.Message}");
                return null;
            }
        }

        private static string GetRiftStrapPageLua()
        {
            return @"
-- RiftStrap Custom ESC Menu Page (inline fallback)
local CoreGui = game:GetService('CoreGui')
local RobloxGui = CoreGui:WaitForChild('RobloxGui')
local Players = game:GetService('Players')
local settingsPageFactory = require(RobloxGui.Modules.Settings.SettingsPageFactory)

local PageInstance = settingsPageFactory:CreateNewPage()
PageInstance.Page.Name = 'RiftStrapPage'
PageInstance.ShouldShowBottomBar = true
PageInstance.ShouldShowHubBar = true

local container = Instance.new('Frame')
container.Size = UDim2.new(1, 0, 1, 0)
container.BackgroundTransparency = 1
container.Parent = PageInstance.Page

local layout = Instance.new('UIListLayout')
layout.SortOrder = Enum.SortOrder.LayoutOrder
layout.Padding = UDim.new(0, 8)
layout.Parent = container

local padding = Instance.new('UIPadding')
padding.PaddingTop = UDim.new(0, 16)
padding.PaddingLeft = UDim.new(0, 20)
padding.PaddingRight = UDim.new(0, 20)
padding.Parent = container

local title = Instance.new('TextLabel')
title.Size = UDim2.new(1, 0, 0, 30)
title.BackgroundTransparency = 1
title.Text = 'RiftStrap'
title.TextColor3 = Color3.fromRGB(250, 250, 250)
title.TextSize = 22
title.Font = Enum.Font.GothamBold
title.TextXAlignment = Enum.TextXAlignment.Left
title.LayoutOrder = 1
title.Parent = container

local sub = Instance.new('TextLabel')
sub.Size = UDim2.new(1, 0, 0, 20)
sub.BackgroundTransparency = 1
sub.Text = 'Your Roblox, elevated'
sub.TextColor3 = Color3.fromRGB(80, 80, 80)
sub.TextSize = 13
sub.Font = Enum.Font.Gotham
sub.TextXAlignment = Enum.TextXAlignment.Left
sub.LayoutOrder = 2
sub.Parent = container

return PageInstance
";
        }
    }
}
