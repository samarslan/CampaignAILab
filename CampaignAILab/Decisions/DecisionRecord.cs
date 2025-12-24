using TaleWorlds.CampaignSystem;

namespace CampaignAILab.Decisions
{
    public class DecisionRecord
    {
        public string DecisionId;
        public CampaignTime Timestamp;

        public string PartyId;
        public string FactionId;

        public string DecisionType;
        public string TargetId;

        public DecisionContextSnapshot Context;
    }
}
