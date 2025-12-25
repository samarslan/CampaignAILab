using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text;
using CampaignAILab.Decisions;

namespace CampaignAILab.Logging
{
    public static class AsyncLogger
    {
        private static readonly ConcurrentQueue<DecisionRecord> _decisionQueue
            = new ConcurrentQueue<DecisionRecord>();

        private static readonly ConcurrentQueue<OutcomeRecord> _outcomeQueue
            = new ConcurrentQueue<OutcomeRecord>();

        private static readonly object _flushLock = new object();
        private static bool _initialized;

        private static string _logRoot;
        private static string _decisionPath;
        private static string _outcomePath;

        /* =========================================================
         * PUBLIC API
         * ========================================================= */

        public static void EnqueueDecision(DecisionRecord decision)
        {
            if (decision == null)
                return;

            _decisionQueue.Enqueue(decision);
        }

        public static void EnqueueOutcome(OutcomeRecord outcome)
        {
            if (outcome == null)
                return;

            _outcomeQueue.Enqueue(outcome);
        }

        /// <summary>
        /// Must be called from DailyTick only.
        /// Performs bounded, append-only IO.
        /// </summary>
        public static void Flush()
        {
            lock (_flushLock)
            {
                EnsureInitialized();

                FlushDecisions();
                FlushOutcomes();
            }
        }

        /* =========================================================
         * INITIALIZATION
         * ========================================================= */

        private static void EnsureInitialized()
        {
            if (_initialized)
                return;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // Navigate up: Win64_Shipping_Client -> bin -> Bannerlord root
            string gameRoot = Path.GetFullPath(
                Path.Combine(baseDir, "..", ".."));

            _logRoot = Path.Combine(
                gameRoot,
                "Modules",
                "CampaignAILab",
                "Logs");

            Directory.CreateDirectory(_logRoot);

            _decisionPath = Path.Combine(_logRoot, "decisions.jsonl");
            _outcomePath = Path.Combine(_logRoot, "outcomes.jsonl");

            _initialized = true;
        }

        /* =========================================================
         * FLUSH IMPLEMENTATION
         * ========================================================= */

        private static void FlushDecisions()
        {
            if (_decisionQueue.IsEmpty)
                return;

            using (var stream = new FileStream(
                _decisionPath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                while (_decisionQueue.TryDequeue(out DecisionRecord record))
                {
                    writer.WriteLine(SerializeDecision(record));
                }
            }
        }

        private static void FlushOutcomes()
        {
            if (_outcomeQueue.IsEmpty)
                return;

            using (var stream = new FileStream(
                _outcomePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                while (_outcomeQueue.TryDequeue(out OutcomeRecord record))
                {
                    writer.WriteLine(SerializeOutcome(record));
                }
            }
        }

        /* =========================================================
         * SERIALIZATION
         * ========================================================= */

        private static string SerializeDecision(DecisionRecord d)
        {
            var sb = new StringBuilder(512);
            sb.Append('{');

            Append(sb, "decisionId", d.DecisionId);
            Append(sb, "timestamp", d.Timestamp.ToString());
            Append(sb, "partyId", d.PartyId);
            Append(sb, "factionId", d.FactionId);
            Append(sb, "decisionType", d.DecisionType);
            Append(sb, "targetId", d.TargetId);

            sb.Append("\"context\":");
            AppendContext(sb, d.Context);

            sb.Append('}');
            return sb.ToString();
        }

        private static string SerializeOutcome(OutcomeRecord o)
        {
            var sb = new StringBuilder(320);
            sb.Append('{');

            Append(sb, "decisionId", o.DecisionId);
            Append(sb, "outcomeType", o.OutcomeType);
            Append(sb, "resolutionTime", o.ResolutionTime.ToString());

            sb.Append("\"durationHours\":")
              .Append(o.DurationHours.ToString(CultureInfo.InvariantCulture))
              .Append(',');

            sb.Append("\"troopsLost\":").Append(o.TroopsLost).Append(',');
            sb.Append("\"goldChange\":").Append(o.GoldChange).Append(',');
            sb.Append("\"moraleChange\":").Append(o.MoraleChange).Append(',');

            sb.Append("\"targetCaptured\":")
              .Append(o.TargetCaptured.ToString().ToLowerInvariant()).Append(',');

            sb.Append("\"partyDestroyed\":")
              .Append(o.PartyDestroyed.ToString().ToLowerInvariant());

            // OPTIONAL FIELDS (append-only, schema-safe)

            if (!string.IsNullOrEmpty(o.OverriddenByDecisionType))
            {
                sb.Append(',');
                Append(sb, "overriddenByDecisionType", o.OverriddenByDecisionType);
                sb.Length--; // remove trailing comma from Append()
            }

            if (!string.IsNullOrEmpty(o.Notes))
            {
                sb.Append(',');
                Append(sb, "notes", o.Notes);
                sb.Length--; // remove trailing comma from Append()
            }

            sb.Append('}');
            return sb.ToString();
        }


        private static void AppendContext(StringBuilder sb, DecisionContextSnapshot c)
        {
            if (c == null)
            {
                sb.Append("null");
                return;
            }

            sb.Append('{');

            AppendNumber(sb, "troopCount", c.TroopCount);
            AppendNumber(sb, "tierSum", c.TierSum);

            AppendNumber(sb, "isAtWar", c.IsAtWar ? 1 : 0);
            AppendNumber(sb, "activeWarCount", c.ActiveWarCount);

            AppendNumber(sb, "aggression", c.Aggression);
            AppendNumber(sb, "caution", c.Caution);
            AppendNumber(sb, "honor", c.Honor);
            AppendNumber(sb, "generosity", c.Generosity);

            AppendNumber(sb, "gold", c.Gold);
            AppendNumber(sb, "food", c.Food);
            AppendNumber(sb, "morale", c.Morale, isLast: true);

            sb.Append('}');
        }

        /* =========================================================
         * JSON HELPERS
         * ========================================================= */

        private static void Append(StringBuilder sb, string key, string value)
        {
            sb.Append('"').Append(key).Append("\":");
            if (value == null)
                sb.Append("null");
            else
                sb.Append('"').Append(Escape(value)).Append('"');
            sb.Append(',');
        }

        private static void AppendNumber(StringBuilder sb, string key, float value, bool isLast = false)
        {
            sb.Append('"').Append(key).Append("\":")
              .Append(value.ToString(CultureInfo.InvariantCulture));
            if (!isLast)
                sb.Append(',');
        }

        private static void AppendNumber(StringBuilder sb, string key, int value, bool isLast = false)
        {
            sb.Append('"').Append(key).Append("\":").Append(value);
            if (!isLast)
                sb.Append(',');
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
