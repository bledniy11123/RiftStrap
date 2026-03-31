--[[
    RiftStrap Custom ESC Menu Page
    Injected into Roblox CoreScripts via Modifications overlay.
    Shows RiftStrap branding + quick actions inside the ESC menu.
]]

local CoreGui = game:GetService("CoreGui")
local RobloxGui = CoreGui:WaitForChild("RobloxGui")
local Players = game:GetService("Players")

local settingsPageFactory = require(RobloxGui.Modules.Settings.SettingsPageFactory)
local Theme = require(RobloxGui.Modules.Settings.Theme)

local PageInstance = settingsPageFactory:CreateNewPage()

PageInstance.Page.Name = "RiftStrapPage"
PageInstance.ShouldShowBottomBar = true
PageInstance.ShouldShowHubBar = true

-- ═══════════════════════════════════════════
--  BUILD UI
-- ═══════════════════════════════════════════

local function createUI()
    local page = PageInstance.Page

    -- Container
    local container = Instance.new("Frame")
    container.Name = "RiftStrapContainer"
    container.Size = UDim2.new(1, 0, 1, 0)
    container.BackgroundTransparency = 1
    container.Parent = page

    local layout = Instance.new("UIListLayout")
    layout.SortOrder = Enum.SortOrder.LayoutOrder
    layout.Padding = UDim.new(0, 12)
    layout.Parent = container

    local padding = Instance.new("UIPadding")
    padding.PaddingTop = UDim.new(0, 16)
    padding.PaddingLeft = UDim.new(0, 20)
    padding.PaddingRight = UDim.new(0, 20)
    padding.Parent = container

    -- ── Branding ──
    local brandFrame = Instance.new("Frame")
    brandFrame.Name = "Branding"
    brandFrame.Size = UDim2.new(1, 0, 0, 60)
    brandFrame.BackgroundTransparency = 1
    brandFrame.LayoutOrder = 1
    brandFrame.Parent = container

    local title = Instance.new("TextLabel")
    title.Name = "Title"
    title.Size = UDim2.new(1, 0, 0, 28)
    title.Position = UDim2.new(0, 0, 0, 0)
    title.BackgroundTransparency = 1
    title.Text = "RiftStrap"
    title.TextColor3 = Color3.fromRGB(250, 250, 250)
    title.TextSize = 22
    title.Font = Enum.Font.GothamBold
    title.TextXAlignment = Enum.TextXAlignment.Left
    title.Parent = brandFrame

    local subtitle = Instance.new("TextLabel")
    subtitle.Name = "Subtitle"
    subtitle.Size = UDim2.new(1, 0, 0, 18)
    subtitle.Position = UDim2.new(0, 0, 0, 30)
    subtitle.BackgroundTransparency = 1
    subtitle.Text = "Your Roblox, elevated"
    subtitle.TextColor3 = Color3.fromRGB(100, 100, 100)
    subtitle.TextSize = 13
    subtitle.Font = Enum.Font.Gotham
    subtitle.TextXAlignment = Enum.TextXAlignment.Left
    subtitle.Parent = brandFrame

    -- ── Player Info Card ──
    local infoCard = Instance.new("Frame")
    infoCard.Name = "InfoCard"
    infoCard.Size = UDim2.new(1, 0, 0, 70)
    infoCard.BackgroundColor3 = Color3.fromRGB(25, 25, 28)
    infoCard.BorderSizePixel = 0
    infoCard.LayoutOrder = 2
    infoCard.Parent = container

    local infoCorner = Instance.new("UICorner")
    infoCorner.CornerRadius = UDim.new(0, 10)
    infoCorner.Parent = infoCard

    local infoStroke = Instance.new("UIStroke")
    infoStroke.Color = Color3.fromRGB(40, 40, 44)
    infoStroke.Thickness = 1
    infoStroke.Parent = infoCard

    local playerName = Instance.new("TextLabel")
    playerName.Name = "PlayerName"
    playerName.Size = UDim2.new(1, -24, 0, 22)
    playerName.Position = UDim2.new(0, 16, 0, 14)
    playerName.BackgroundTransparency = 1
    playerName.TextColor3 = Color3.fromRGB(220, 220, 220)
    playerName.TextSize = 16
    playerName.Font = Enum.Font.GothamSemibold
    playerName.TextXAlignment = Enum.TextXAlignment.Left
    playerName.Parent = infoCard

    local playerInfo = Instance.new("TextLabel")
    playerInfo.Name = "PlayerInfo"
    playerInfo.Size = UDim2.new(1, -24, 0, 16)
    playerInfo.Position = UDim2.new(0, 16, 0, 38)
    playerInfo.BackgroundTransparency = 1
    playerInfo.TextColor3 = Color3.fromRGB(80, 80, 80)
    playerInfo.TextSize = 12
    playerInfo.Font = Enum.Font.Gotham
    playerInfo.TextXAlignment = Enum.TextXAlignment.Left
    playerInfo.Parent = infoCard

    -- Fill player info
    local localPlayer = Players.LocalPlayer
    if localPlayer then
        playerName.Text = localPlayer.DisplayName
        playerInfo.Text = "@" .. localPlayer.Name .. " · ID: " .. tostring(localPlayer.UserId)
    end

    -- ── Action Buttons ──
    local function createActionButton(name, text, description, order, callback)
        local btn = Instance.new("TextButton")
        btn.Name = name
        btn.Size = UDim2.new(1, 0, 0, 54)
        btn.BackgroundColor3 = Color3.fromRGB(20, 20, 23)
        btn.BorderSizePixel = 0
        btn.AutoButtonColor = false
        btn.LayoutOrder = order
        btn.Text = ""
        btn.Parent = container

        local btnCorner = Instance.new("UICorner")
        btnCorner.CornerRadius = UDim.new(0, 10)
        btnCorner.Parent = btn

        local btnStroke = Instance.new("UIStroke")
        btnStroke.Color = Color3.fromRGB(35, 35, 38)
        btnStroke.Thickness = 1
        btnStroke.Parent = btn

        local btnTitle = Instance.new("TextLabel")
        btnTitle.Size = UDim2.new(1, -60, 0, 20)
        btnTitle.Position = UDim2.new(0, 16, 0, 10)
        btnTitle.BackgroundTransparency = 1
        btnTitle.Text = text
        btnTitle.TextColor3 = Color3.fromRGB(200, 200, 200)
        btnTitle.TextSize = 14
        btnTitle.Font = Enum.Font.GothamSemibold
        btnTitle.TextXAlignment = Enum.TextXAlignment.Left
        btnTitle.Parent = btn

        local btnDesc = Instance.new("TextLabel")
        btnDesc.Size = UDim2.new(1, -60, 0, 14)
        btnDesc.Position = UDim2.new(0, 16, 0, 30)
        btnDesc.BackgroundTransparency = 1
        btnDesc.Text = description
        btnDesc.TextColor3 = Color3.fromRGB(80, 80, 80)
        btnDesc.TextSize = 11
        btnDesc.Font = Enum.Font.Gotham
        btnDesc.TextXAlignment = Enum.TextXAlignment.Left
        btnDesc.Parent = btn

        local arrow = Instance.new("TextLabel")
        arrow.Size = UDim2.new(0, 20, 1, 0)
        arrow.Position = UDim2.new(1, -36, 0, 0)
        arrow.BackgroundTransparency = 1
        arrow.Text = "→"
        arrow.TextColor3 = Color3.fromRGB(50, 50, 50)
        arrow.TextSize = 16
        arrow.Font = Enum.Font.Gotham
        arrow.Parent = btn

        -- Hover effects
        btn.MouseEnter:Connect(function()
            game:GetService("TweenService"):Create(btn, TweenInfo.new(0.15), {
                BackgroundColor3 = Color3.fromRGB(28, 28, 32)
            }):Play()
            game:GetService("TweenService"):Create(btnStroke, TweenInfo.new(0.15), {
                Color = Color3.fromRGB(50, 50, 55)
            }):Play()
            game:GetService("TweenService"):Create(arrow, TweenInfo.new(0.15), {
                TextColor3 = Color3.fromRGB(150, 150, 150)
            }):Play()
        end)

        btn.MouseLeave:Connect(function()
            game:GetService("TweenService"):Create(btn, TweenInfo.new(0.2), {
                BackgroundColor3 = Color3.fromRGB(20, 20, 23)
            }):Play()
            game:GetService("TweenService"):Create(btnStroke, TweenInfo.new(0.2), {
                Color = Color3.fromRGB(35, 35, 38)
            }):Play()
            game:GetService("TweenService"):Create(arrow, TweenInfo.new(0.2), {
                TextColor3 = Color3.fromRGB(50, 50, 50)
            }):Play()
        end)

        if callback then
            btn.MouseButton1Click:Connect(callback)
        end

        return btn
    end

    -- Performance stats button
    createActionButton("PerfStats", "Performance Stats", "View FPS, ping, and memory usage", 3, function()
        game:GetService("StarterGui"):SetCore("DevConsoleVisible", true)
    end)

    -- Server info button
    createActionButton("ServerInfo", "Server Information", "View current server details", 4, function()
        -- Display server info
        local jobId = game.JobId
        local placeId = game.PlaceId
        game:GetService("StarterGui"):SetCore("SendNotification", {
            Title = "Server Info",
            Text = "Place: " .. tostring(placeId) .. "\nServer: " .. string.sub(jobId, 1, 8),
            Duration = 5
        })
    end)

    -- Rejoin button
    createActionButton("Rejoin", "Rejoin Server", "Reconnect to this server", 5, function()
        game:GetService("TeleportService"):TeleportToPlaceInstance(game.PlaceId, game.JobId)
    end)

    -- Copy Server ID button
    createActionButton("CopyServerId", "Copy Server ID", "Copy current server ID to clipboard", 6, function()
        if setclipboard then
            setclipboard(game.JobId)
        end
        game:GetService("StarterGui"):SetCore("SendNotification", {
            Title = "Copied",
            Text = "Server ID copied to clipboard",
            Duration = 3
        })
    end)

    -- ── Version footer ──
    local version = Instance.new("TextLabel")
    version.Name = "Version"
    version.Size = UDim2.new(1, 0, 0, 20)
    version.BackgroundTransparency = 1
    version.Text = "RiftStrap · Open Source"
    version.TextColor3 = Color3.fromRGB(40, 40, 40)
    version.TextSize = 10
    version.Font = Enum.Font.Gotham
    version.TextXAlignment = Enum.TextXAlignment.Center
    version.LayoutOrder = 10
    version.Parent = container
end

createUI()

return PageInstance
