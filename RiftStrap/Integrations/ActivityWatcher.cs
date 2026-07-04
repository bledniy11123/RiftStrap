namespace RiftStrap.Integrations
{
    public class ActivityWatcher : IDisposable
    {
        private const string GameMessageEntry                = "[FLog::Output] [RiftStrapRPC]";
        private const string GameJoiningEntry                = "[FLog::Output] ! Joining game";

        private const string GameTeleportingEntry            = "[FLog::GameJoinUtil] GameJoinUtil::initiateTeleportToPlace";
        private const string GameJoiningPrivateServerEntry   = "[FLog::GameJoinUtil] GameJoinUtil::joinGamePostPrivateServer";
        private const string GameJoiningReservedServerEntry  = "[FLog::GameJoinUtil] GameJoinUtil::initiateTeleportToReservedServer";
        private const string GameJoiningUniverseEntry        = "[FLog::GameJoinLoadTime] Report game_join_loadtime:";
        private const string GameJoiningUDMUXEntry           = "[FLog::Network] UDMUX Address = ";
        private const string GameJoinedEntry                 = "[FLog::Network] serverId:";
        private const string GameDisconnectedEntry           = "[FLog::Network] Time to disconnect replication data:";
        private const string GameLeavingEntry                = "[FLog::SingleSurfaceApp] leaveUGCGameInternal";

        private const string GameJoiningEntryPattern         = @"! Joining game '([0-9a-f\-]{36})' place ([0-9]+) at ([0-9\.]+)";
        private const string GameJoiningPrivateServerPattern = @"""accessCode"":""([0-9a-f\-]{36})""";
        private const string GameJoiningUniversePattern      = @"universeid:([0-9]+).*userid:([0-9]+)";
        private const string GameJoiningUDMUXPattern         = @"UDMUX Address = ([0-9\.]+), Port = [0-9]+ \| RCC Server Address = ([0-9\.]+), Port = [0-9]+";
        private const string GameJoinedEntryPattern          = @"serverId: ([0-9\.]+)\|[0-9]+";
        private const string GameMessageEntryPattern         = @"\[RiftStrapRPC\] (.*)";

        private int _logEntriesRead = 0;
        private bool _teleportMarker = false;
        private bool _reservedTeleportMarker = false;

        public event EventHandler<string>? OnLogEntry;
        public event EventHandler? OnGameJoin;
        public event EventHandler? OnGameLeave;
        public event EventHandler? OnLogOpen;
        public event EventHandler? OnAppClose;
        public event EventHandler<Message>? OnRPCMessage;

        private DateTime LastRPCRequest;

        public string LogLocation = null!;

        public bool InGame = false;

        public ActivityData Data { get; private set; } = new();

        private readonly object _historyLock = new();
        private readonly List<ActivityData> _history = new();

        // Returns a snapshot copied under the history lock so consumers never
        // enumerate the live list while the watcher mutates it on its own thread.
        public List<ActivityData> History
        {
            get
            {
                lock (_historyLock)
                    return new List<ActivityData>(_history);
            }
        }

        public bool IsDisposed = false;

        public ActivityWatcher(string? logFile = null)
        {
            if (!String.IsNullOrEmpty(logFile))
                LogLocation = logFile;
        }

        public async Task Start()
        {
            const string LOG_IDENT = "ActivityWatcher::Start";

            FileInfo logFileInfo;

            if (String.IsNullOrEmpty(LogLocation))
            {
                string logDirectory = Path.Combine(Paths.LocalAppData, "Roblox\\logs");

                if (!Directory.Exists(logDirectory))
                    return;

                App.Logger.WriteLine(LOG_IDENT, "Opening Roblox log file...");

                while (true)
                {
                    var newest = new DirectoryInfo(logDirectory)
                        .GetFiles()
                        .Where(x => x.Name.Contains("Player", StringComparison.OrdinalIgnoreCase) && x.CreationTime <= DateTime.Now)
                        .OrderByDescending(x => x.CreationTime)
                        .FirstOrDefault();

                    if (newest is null)   // no Player log yet -> keep waiting instead of throwing
                    {
                        if (IsDisposed) return;
                        await Task.Delay(1000);
                        continue;
                    }

                    logFileInfo = newest;

                    if (logFileInfo.CreationTime.AddSeconds(15) > DateTime.Now)
                        break;

                    App.Logger.WriteLine(LOG_IDENT, $"Could not find recent enough log file, waiting... (newest is {logFileInfo.Name})");
                    await Task.Delay(1000);
                }

                LogLocation = logFileInfo.FullName;
            }
            else
            {
                logFileInfo = new FileInfo(LogLocation);
            }

            RaiseEvent(OnLogOpen);

            FileStream logFileStream;
            try
            {
                logFileStream = logFileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to open log file '{LogLocation}': {ex.Message}");
                return;
            }

            App.Logger.WriteLine(LOG_IDENT, $"Opened {LogLocation}");

            using var streamReader = new StreamReader(logFileStream);

            while (!IsDisposed)
            {
                try
                {
                    string? log = await streamReader.ReadLineAsync();

                    if (log is null)
                        await Task.Delay(1000);
                    else
                        ReadLogEntry(log);
                }
                catch (Exception ex)
                {
                    // Never let a single bad line or throwing subscriber tear down the watcher.
                    App.Logger.WriteLine(LOG_IDENT, "Unhandled exception while processing log entry, continuing...");
                    App.Logger.WriteException(LOG_IDENT, ex);
                    await Task.Delay(1000);
                }
            }
        }

        private void ReadLogEntry(string entry)
        {
            const string LOG_IDENT = "ActivityWatcher::ReadLogEntry";

            RaiseEvent(OnLogEntry, entry);

            _logEntriesRead += 1;

            if (_logEntriesRead <= 1000 && _logEntriesRead % 50 == 0)
                App.Logger.WriteLine(LOG_IDENT, $"Read {_logEntriesRead} log entries");
            else if (_logEntriesRead % 100 == 0)
                App.Logger.WriteLine(LOG_IDENT, $"Read {_logEntriesRead} log entries");

            int logMessageIdx = entry.IndexOf(' ');
            if (logMessageIdx == -1)
            {

                return;
            }

            string logMessage = entry[(logMessageIdx + 1)..];

            if (logMessage.StartsWith(GameLeavingEntry))
            {
                App.Logger.WriteLine(LOG_IDENT, "User is back into the desktop app");

                RaiseEvent(OnAppClose);

                if (Data.PlaceId != 0 && !InGame)
                {
                    App.Logger.WriteLine(LOG_IDENT, "User appears to be leaving from a cancelled/errored join");
                    Data = new();
                }

                return;
            }

            if (!InGame && Data.PlaceId == 0)
            {

                if (logMessage.StartsWith(GameJoiningPrivateServerEntry))
                {

                    Data.ServerType = ServerType.Private;

                    var match = Regex.Match(logMessage, GameJoiningPrivateServerPattern);

                    if (match.Groups.Count != 2)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Failed to assert format for game join private server entry");
                        App.Logger.WriteLine(LOG_IDENT, logMessage);
                        return;
                    }

                    Data.AccessCode = match.Groups[1].Value;
                }
                else if (logMessage.StartsWith(GameJoiningEntry))
                {
                    Match match = Regex.Match(logMessage, GameJoiningEntryPattern);

                    if (match.Groups.Count != 4)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to assert format for game join entry");
                        App.Logger.WriteLine(LOG_IDENT, logMessage);
                        return;
                    }

                    if (!long.TryParse(match.Groups[2].Value, out long placeId))
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Failed to parse place id for game join entry");
                        App.Logger.WriteLine(LOG_IDENT, logMessage);
                        return;
                    }

                    InGame = false;
                    Data.PlaceId = placeId;
                    Data.JobId = match.Groups[1].Value;
                    Data.MachineAddress = match.Groups[3].Value;

                    if (App.Settings.Prop.ShowServerDetails && Data.MachineAddressValid)
                        _ = Data.QueryServerLocation();

                    if (_teleportMarker)
                    {
                        Data.IsTeleport = true;
                        _teleportMarker = false;
                    }

                    if (_reservedTeleportMarker)
                    {
                        Data.ServerType = ServerType.Reserved;
                        _reservedTeleportMarker = false;
                    }

                    App.Logger.WriteLine(LOG_IDENT, $"Joining Game ({Data})");
                }
            }
            else if (!InGame && Data.PlaceId != 0)
            {

                if (logMessage.StartsWith(GameJoiningUniverseEntry))
                {
                    var match = Regex.Match(logMessage, GameJoiningUniversePattern);

                    if (match.Groups.Count != 3)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Failed to assert format for game join universe entry");
                        App.Logger.WriteLine(LOG_IDENT, logMessage);
                        return;
                    }

                    if (!Int64.TryParse(match.Groups[1].Value, out long universeId) || !Int64.TryParse(match.Groups[2].Value, out long userId))
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Failed to parse universe/user id for game join universe entry");
                        App.Logger.WriteLine(LOG_IDENT, logMessage);
                        return;
                    }

                    Data.UniverseId = universeId;
                    Data.UserId = userId;

                    ActivityData? lastActivity;
                    lock (_historyLock)
                        lastActivity = _history.FirstOrDefault();

                    if (lastActivity is not null)
                    {
                        if (Data.UniverseId == lastActivity.UniverseId && Data.IsTeleport)
                            Data.RootActivity = lastActivity.RootActivity ?? lastActivity;
                    }
                }
                else if (logMessage.StartsWith(GameJoiningUDMUXEntry))
                {
                    var match = Regex.Match(logMessage, GameJoiningUDMUXPattern);

                    if (match.Groups.Count != 3 || match.Groups[2].Value != Data.MachineAddress)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Failed to assert format for game join UDMUX entry");
                        App.Logger.WriteLine(LOG_IDENT, logMessage);
                        return;
                    }

                    Data.MachineAddress = match.Groups[1].Value;

                    if (App.Settings.Prop.ShowServerDetails && Data.MachineAddressValid)
                        _ = Data.QueryServerLocation();

                    App.Logger.WriteLine(LOG_IDENT, $"Server is UDMUX protected ({Data})");
                }
                else if (logMessage.StartsWith(GameJoinedEntry))
                {
                    Match match = Regex.Match(logMessage, GameJoinedEntryPattern);

                    if (match.Groups.Count != 2 || match.Groups[1].Value != Data.MachineAddress)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to assert format for game joined entry");
                        App.Logger.WriteLine(LOG_IDENT, logMessage);
                        return;
                    }

                    App.Logger.WriteLine(LOG_IDENT, $"Joined Game ({Data})");

                    InGame = true;
                    Data.TimeJoined = DateTime.Now;

                    RaiseEvent(OnGameJoin);
                }
            }
            else if (InGame && Data.PlaceId != 0)
            {

                if (logMessage.StartsWith(GameDisconnectedEntry))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Disconnected from Game ({Data})");

                    Data.TimeLeft = DateTime.Now;
                    lock (_historyLock)
                        _history.Insert(0, Data);

                    InGame = false;
                    Data = new();

                    RaiseEvent(OnGameLeave);
                }
                else if (logMessage.StartsWith(GameTeleportingEntry))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Initiating teleport to server ({Data})");
                    _teleportMarker = true;
                }
                else if (logMessage.StartsWith(GameJoiningReservedServerEntry))
                {
                    _teleportMarker = true;
                    _reservedTeleportMarker = true;
                }
                else if (logMessage.StartsWith(GameMessageEntry))
                {
                    var match = Regex.Match(logMessage, GameMessageEntryPattern);

                    if (match.Groups.Count != 2)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to assert format for RPC message entry");
                        App.Logger.WriteLine(LOG_IDENT, logMessage);
                        return;
                    }

                    string messagePlain = match.Groups[1].Value;
                    Message? message;

                    App.Logger.WriteLine(LOG_IDENT, $"Received message: '{messagePlain}'");

                    if ((DateTime.Now - LastRPCRequest).TotalSeconds <= 1)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Dropping message as ratelimit has been hit");
                        return;
                    }

                    try
                    {
                        message = JsonSerializer.Deserialize<Message>(messagePlain);
                    }
                    catch (Exception)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (JSON deserialization threw an exception)");
                        return;
                    }

                    if (message is null)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (JSON deserialization returned null)");
                        return;
                    }

                    if (string.IsNullOrEmpty(message.Command))
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (Command is empty)");
                        return;
                    }

                    if (message.Command == "SetLaunchData")
                    {
                        string? data;

                        try
                        {
                            data = message.Data.Deserialize<string>();
                        }
                        catch (Exception)
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (JSON deserialization threw an exception)");
                            return;
                        }

                        if (data is null)
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (JSON deserialization returned null)");
                            return;
                        }

                        if (data.Length > 200)
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Data cannot be longer than 200 characters");
                            return;
                        }

                        Data.RPCLaunchData = data;
                    }

                    RaiseEvent(OnRPCMessage, message);

                    LastRPCRequest = DateTime.Now;
                }
            }
        }

        // Invoke each subscriber in isolation so one throwing handler cannot prevent
        // the others from running or tear down the watcher loop.
        private void RaiseEvent(EventHandler? handler)
        {
            const string LOG_IDENT = "ActivityWatcher::RaiseEvent";

            if (handler is null)
                return;

            foreach (var subscriber in handler.GetInvocationList())
            {
                try
                {
                    ((EventHandler)subscriber)(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            }
        }

        private void RaiseEvent<T>(EventHandler<T>? handler, T args)
        {
            const string LOG_IDENT = "ActivityWatcher::RaiseEvent";

            if (handler is null)
                return;

            foreach (var subscriber in handler.GetInvocationList())
            {
                try
                {
                    ((EventHandler<T>)subscriber)(this, args);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            }
        }

        public void Dispose()
        {
            IsDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
