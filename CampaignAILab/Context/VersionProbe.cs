using TaleWorlds.ModuleManager;

namespace CampaignAILab.Util
{
    internal static class VersionProbe
    {
        public static string GetNativeGameVersion()
        {
            var native = ModuleHelper.GetModuleInfo("Native");
            if (native?.Version != null)
            {
                return "Native@" + native.Version.ToString();
            }

            return "Native@Unknown";
        }

        public static string GetCampaignAILabVersion()
        {
            var self = ModuleHelper.GetModuleInfo("CampaignAILab");
            if (self?.Version != null)
            {
                return self.Version.ToString();
            }

            return "Unknown";
        }
    }
}
