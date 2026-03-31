using System.ComponentModel;

namespace RiftStrap.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RobloxChannel
    {
        [Description("production")]
        [EnumName(StaticName = "Live")]
        Production,

        [Description("zcanary")]
        [EnumName(StaticName = "ZCanary (Beta)")]
        ZCanary,

        [Description("zintegration")]
        [EnumName(StaticName = "ZIntegration (Internal)")]
        ZIntegration,

        [Description("zflag")]
        [EnumName(StaticName = "ZFlag")]
        ZFlag,

        [Description("znext")]
        [EnumName(StaticName = "ZNext (Future)")]
        ZNext
    }
}
