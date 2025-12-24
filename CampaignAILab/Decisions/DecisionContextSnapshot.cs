namespace CampaignAILab.Decisions
{
    public class DecisionContextSnapshot
    {
        public int TroopCount;
        public int TierSum;

        public int NearbyEnemyCount;
        public int NearbyAllyCount;

        public float NearbyEnemyStrength;
        public float NearbyAllyStrength;

        public float DistanceToTarget;
        public float DistanceToNearestFriendlySettlement;
        public float DistanceToNearestEnemySettlement;

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
