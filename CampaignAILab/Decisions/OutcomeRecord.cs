using TaleWorlds.CampaignSystem;

namespace CampaignAILab.Decisions
{
    public class OutcomeRecord
    {
        public string DecisionId;
        public string OutcomeType;
        public CampaignTime ResolutionTime;
        public float DurationHours;

        public int TroopsLost;
        public int GoldChange;
        public int MoraleChange;

        public bool TargetCaptured;
        public bool PartyDestroyed;
    }
}
