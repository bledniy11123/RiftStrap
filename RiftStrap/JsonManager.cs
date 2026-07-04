namespace RiftStrap
{
    public class JsonManager<T> where T : class, new()
    {
        protected T _prop = new();

        public virtual T Prop
        {
            get => _prop;
            set => _prop = value;
        }

        public string? LastFileHash { get; private set; }

        public bool Loaded { get; protected set; } = false;

        public virtual string ClassName { get; }

        public virtual string FileName => $"{ClassName}.json";

        public virtual string FileLocation => Path.Combine(Paths.Base, FileName);

        public bool IsSaved => File.Exists(FileLocation);

        public virtual string LOG_IDENT_CLASS => $"JsonManager<{ClassName}>";

        public JsonManager(string? className = null)
        {
            ClassName = string.IsNullOrEmpty(className) ? typeof(T).Name : className;
        }

        public virtual bool Load(bool alertFailure = true)
        {
            string LOG_IDENT = $"{LOG_IDENT_CLASS}::Load";

            App.Logger.WriteLine(LOG_IDENT, $"Loading from {FileLocation}...");

            try
            {
                if (File.Exists(FileLocation))
                {
                    string contents = File.ReadAllText(FileLocation);

                    T? settings = JsonSerializer.Deserialize<T>(contents);

                    if (settings is null)
                        throw new ArgumentNullException("Deserialization returned null");

                    _prop = settings;
                    Loaded = true;
                    LastFileHash = MD5Hash.FromString(contents);

                    App.Logger.WriteLine(LOG_IDENT, "Loaded successfully!");

                    return true;
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Could not find {FileLocation}.");
                    Loaded = true;

                    return false;
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to load!");
                App.Logger.WriteException(LOG_IDENT, ex);

                // Only treat true corruption (invalid JSON / null deserialization) as a reason to
                // reset the config to defaults. Transient IO/permission errors must never cause the
                // on-disk file to be overwritten with defaults, or live settings/FastFlags are lost.
                bool isCorruption = ex is JsonException || ex is ArgumentNullException;

                if (!isCorruption)
                {
                    // read failed transiently: preserve the file on disk, do not Save() over it
                    return false;
                }

                if (alertFailure)
                {
                    string message = "";

                    if (ClassName == nameof(Settings))
                        message = Strings.JsonManager_SettingsLoadFailed;
                    else if (ClassName == nameof(FastFlagManager))
                        message = Strings.JsonManager_FastFlagsLoadFailed;

                    if (!String.IsNullOrEmpty(message))
                        Frontend.ShowMessageBox($"{message}\n\n{ex.Message}", System.Windows.MessageBoxImage.Warning);
                }

                // back up the corrupt file unconditionally (regardless of alertFailure) before
                // overwriting it with defaults, so the original is always recoverable
                try
                {
                    File.Copy(FileLocation, FileLocation + ".bak", true);
                }
                catch (Exception copyEx)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to create backup file: {FileLocation}.bak");
                    App.Logger.WriteException(LOG_IDENT, copyEx);
                }

                Loaded = true;
                Save();

                return false;
            }
        }

        public virtual bool Save()
        {
            string LOG_IDENT = $"{LOG_IDENT_CLASS}::Save";

            App.Logger.WriteLine(LOG_IDENT, $"Saving to {FileLocation}...");

            Directory.CreateDirectory(Path.GetDirectoryName(FileLocation)!);

            try
            {
                string contents = JsonSerializer.Serialize(Prop, new JsonSerializerOptions { WriteIndented = true });

                // atomic write: a crash mid-write must not leave a truncated/corrupt settings file
                string tmp = FileLocation + ".tmp";
                File.WriteAllText(tmp, contents);
                File.Move(tmp, FileLocation, true);

                LastFileHash = MD5Hash.FromString(contents);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to save");
                App.Logger.WriteException(LOG_IDENT, ex);

                string errorMessage = string.Format(Resources.Strings.Bootstrapper_JsonManagerSaveFailed, ClassName, ex.Message);
                Frontend.ShowMessageBox(errorMessage, System.Windows.MessageBoxImage.Warning);

                return false;
            }

            App.Logger.WriteLine(LOG_IDENT, "Save complete!");

            return true;
        }

        public virtual void Delete()
        {
            string LOG_IDENT = $"{LOG_IDENT_CLASS}::Delete";

            try
            {
                if (File.Exists(FileLocation))
                {
                    File.Delete(FileLocation);

                    Loaded = false;
                    App.Logger.WriteLine(LOG_IDENT, "Delete complete!");
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT, "File does not exist on disk");
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to delete");
                App.Logger.WriteException(LOG_IDENT, ex);

            }
        }

        public bool HasFileOnDiskChanged()
        {

            // guard existence before hashing: a file removed after a hash was recorded must
            // yield a clean "changed" result rather than throwing FileNotFoundException
            if (!File.Exists(FileLocation))
                return !string.IsNullOrEmpty(LastFileHash);

            if (string.IsNullOrEmpty(LastFileHash))
                return true;

            return LastFileHash != MD5Hash.FromFile(FileLocation);
        }
    }

    public class LazyJsonManager<T> : JsonManager<T> where T : class, new()
    {
        public override T Prop
        {
            get
            {
                if (!Loaded)
                    Load();

                return _prop;
            }
            set
            {
                _prop = value;
                Loaded = true;
            }
        }

        public LazyJsonManager(string? className)
            : base(className)
        {
        }
    }
}
