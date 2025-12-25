using TaleWorlds.CampaignSystem;

namespace CampaignAILab.Decisions
{
    public sealed class OutcomeRecord
    {
        public string DecisionId;
        public string OutcomeType;
        public CampaignTime ResolutionTime;
        public float DurationHours;

        public int TroopsLost;
        public int GoldChange;
        public float MoraleChange;

        public bool TargetCaptured;
        public bool PartyDestroyed;

        public string OverriddenByDecisionType;
        public string Notes;
    }
}
