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

        // DistanceToTarget is legacy / unused in Phase-2
        public float DistanceToTarget;

        public float DistanceToNearestFriendlySettlement;
        public float DistanceToNearestEnemySettlement;


        // Instantaneous party speed at decision time
        public float PartySpeed;

        // True if party is already inside a settlement
        public bool IsAtSettlementAtDecision;

        /* =========================================================
         * MOVE TARGET CONTEXT (1.4)
         * Populated ONLY when TargetId != null
        * ========================================================= */

        // Straight-line distance to target at decision time
        // -1 if no target
        public float TargetDistanceStraightLine;
        // === Target Context (1.4) ===
        public byte TargetSettlementType; // 0=None, 1=Town, 2=Castle, 3=Village, 4=Hideout
        public string TargetFactionId;
        public bool TargetIsFriendly;

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

        /* =========================================================
        * MANDATORY METADATA (1.5)
        * ========================================================= */
        public string CampaignAILabAssemblyVersion;
        public string GameVersionString;

        // === Negative / null controls (1.4) ===
        // Schema-level controls for offline validation
        public int ContextSchemaVersion;
        public int ContextFieldCount;

        // Cosmetic/string control
        public int PartyIdStringLength;

        // Deterministic pseudo-random null control
        public int NullDeterministicHash;

    }
}
