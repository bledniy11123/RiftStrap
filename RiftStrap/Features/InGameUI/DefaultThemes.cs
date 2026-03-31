using RiftStrap.Features.InGameUI.Models;

namespace RiftStrap.Features.InGameUI
{

    public static class DefaultThemes
    {
        public static readonly RiftTheme[] All =
        {
            new()
            {
                Id = "minimal-dark",
                Name = "Minimal Dark",
                Author = "RiftStrap",
                Version = "1.0.0",
                Description = "Clean dark aesthetic with thin monochrome icons and muted textures. Designed for focus.",
                Category = ThemeCategory.Full,
                Files = new Dictionary<string, string>
                {

                    ["content/textures/Cursors/KeyboardMouse/ArrowCursor.png"] = "cursors/ArrowCursor.png",
                    ["content/textures/Cursors/KeyboardMouse/ArrowFarCursor.png"] = "cursors/ArrowFarCursor.png",
                }
            },

            new()
            {
                Id = "minimal-light",
                Name = "Minimal Light",
                Author = "RiftStrap",
                Version = "1.0.0",
                Description = "Bright, clean, and minimal. White surfaces with soft gray accents.",
                Category = ThemeCategory.Full,
                Files = new Dictionary<string, string>
                {
                    ["content/textures/Cursors/KeyboardMouse/ArrowCursor.png"] = "cursors/ArrowCursor.png",
                    ["content/textures/Cursors/KeyboardMouse/ArrowFarCursor.png"] = "cursors/ArrowFarCursor.png",
                }
            },

            new()
            {
                Id = "neon",
                Name = "Neon",
                Author = "RiftStrap",
                Version = "1.0.0",
                Description = "Vibrant neon accents on a dark canvas. Glowing cursors and UI highlights.",
                Category = ThemeCategory.Cursors,
                Files = new Dictionary<string, string>
                {
                    ["content/textures/Cursors/KeyboardMouse/ArrowCursor.png"] = "cursors/ArrowCursor.png",
                    ["content/textures/Cursors/KeyboardMouse/ArrowFarCursor.png"] = "cursors/ArrowFarCursor.png",
                }
            },

            new()
            {
                Id = "retro",
                Name = "Retro 2013",
                Author = "RiftStrap",
                Version = "1.0.0",
                Description = "Classic Roblox look from 2013. Original cursors and sounds from the golden era.",
                Category = ThemeCategory.Full,
                Files = new Dictionary<string, string>
                {

                    ["content/textures/Cursors/KeyboardMouse/ArrowCursor.png"] = "cursors/ArrowCursor.png",
                    ["content/textures/Cursors/KeyboardMouse/ArrowFarCursor.png"] = "cursors/ArrowFarCursor.png",
                }
            },

            new()
            {
                Id = "glass",
                Name = "Glass",
                Author = "RiftStrap",
                Version = "1.0.0",
                Description = "Translucent, glassmorphism-inspired textures. Semi-transparent UI elements.",
                Category = ThemeCategory.Textures,
                Files = new Dictionary<string, string>
                {
                    ["content/textures/Cursors/KeyboardMouse/ArrowCursor.png"] = "cursors/ArrowCursor.png",
                    ["content/textures/Cursors/KeyboardMouse/ArrowFarCursor.png"] = "cursors/ArrowFarCursor.png",
                }
            },
        };

        public static void EnsureInstalled(ThemeEngine engine)
        {
            var installed = engine.GetInstalledThemes();
            var installedIds = installed.Select(t => t.Id).ToHashSet();

            foreach (var theme in All)
            {
                if (installedIds.Contains(theme.Id))
                    continue;

                var themeDir = Path.Combine(Paths.Base, "Themes", theme.Id);
                Directory.CreateDirectory(themeDir);

                var json = JsonSerializer.Serialize(theme, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(themeDir, "theme.json"), json);

                var cursorsDir = Path.Combine(themeDir, "cursors");
                Directory.CreateDirectory(cursorsDir);

                GenerateCursorImages(theme.Id, cursorsDir);

                App.Logger.WriteLine("DefaultThemes", $"Installed default theme: {theme.Name}");
            }
        }

        private static void GenerateCursorImages(string themeId, string outputDir)
        {
            var size = 64;

            using var arrowBmp = new System.Drawing.Bitmap(size, size);
            using var arrowFarBmp = new System.Drawing.Bitmap(size, size);

            using (var g = System.Drawing.Graphics.FromImage(arrowBmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(System.Drawing.Color.Transparent);

                var (fillColor, outlineColor) = themeId switch
                {
                    "minimal-dark" => (System.Drawing.Color.FromArgb(220, 220, 220), System.Drawing.Color.FromArgb(40, 40, 40)),
                    "minimal-light" => (System.Drawing.Color.White, System.Drawing.Color.FromArgb(80, 80, 80)),
                    "neon" => (System.Drawing.Color.FromArgb(0, 255, 200), System.Drawing.Color.FromArgb(0, 180, 140)),
                    "retro" => (System.Drawing.Color.White, System.Drawing.Color.Black),
                    "glass" => (System.Drawing.Color.FromArgb(180, 255, 255, 255), System.Drawing.Color.FromArgb(100, 255, 255, 255)),
                    _ => (System.Drawing.Color.White, System.Drawing.Color.Black),
                };

                var points = new System.Drawing.Point[]
                {
                    new(8, 4),
                    new(8, 44),
                    new(18, 34),
                    new(28, 50),
                    new(34, 46),
                    new(24, 30),
                    new(38, 30),
                };

                using var fill = new System.Drawing.SolidBrush(fillColor);
                using var outline = new System.Drawing.Pen(outlineColor, 2f);
                g.FillPolygon(fill, points);
                g.DrawPolygon(outline, points);
            }

            using (var g = System.Drawing.Graphics.FromImage(arrowFarBmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(System.Drawing.Color.Transparent);
                g.ScaleTransform(0.6f, 0.6f);
                g.TranslateTransform(12, 8);

                var fillColor = themeId switch
                {
                    "neon" => System.Drawing.Color.FromArgb(0, 255, 200),
                    "glass" => System.Drawing.Color.FromArgb(140, 255, 255, 255),
                    _ => System.Drawing.Color.FromArgb(200, 200, 200),
                };

                var points = new System.Drawing.Point[]
                {
                    new(8, 4), new(8, 44), new(18, 34),
                    new(28, 50), new(34, 46), new(24, 30), new(38, 30),
                };

                using var fill = new System.Drawing.SolidBrush(fillColor);
                g.FillPolygon(fill, points);
            }

            arrowBmp.Save(Path.Combine(outputDir, "ArrowCursor.png"), System.Drawing.Imaging.ImageFormat.Png);
            arrowFarBmp.Save(Path.Combine(outputDir, "ArrowFarCursor.png"), System.Drawing.Imaging.ImageFormat.Png);
        }
    }
}
