namespace CampaignAILab.Decisions
{
    public class DecisionContextSnapshot
    {
        public int TroopCount;
        public int TierSum;

        public string PartyType;
        public bool IsMainParty;

        // Structural control – currently unavailable from engine API
        // Semantics:
        //  -1 = party age not observable in this build1.1
        public int PartyAgeDays;

        public int NearbyEnemyCount;
        public int NearbyAllyCount;

        public float NearbyEnemyStrength;
        public float NearbyAllyStrength;

        public float DistanceToTarget;
        public float DistanceToNearestFriendlySettlement;
        public float DistanceToNearestEnemySettlement;

        // Absolute campaign day (bias/drift control)
        public int CampaignDay;

        // Coarse season index (0..3)
        public int CampaignSeason;

        // 0=Morning, 1=Day, 2=Night
        public byte TimeOfDayBucket;

        public bool IsAtWar;
        public int ActiveWarCount;

        public float Aggression;
        public float Caution;
        public float Honor;
        public float Generosity;

        public int Gold;
        public int Food;
        public int Morale;
    }
}
