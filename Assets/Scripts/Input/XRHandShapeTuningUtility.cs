using System.Text;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;

namespace ArcaneVR.Input
{
    public static class XRHandShapeTuningUtility
    {
        private const float ExternalBuffer = 0.1f;

        public static bool TryCalculateCompleteness(in XRHand hand, XRHandShape handShape, out float completeness)
        {
            completeness = 0f;

            if (!hand.isTracked || handShape == null)
                return false;

            var conditions = handShape.fingerShapeConditions;
            if (conditions == null || conditions.Count == 0)
                return false;

            var scoreSum = 0f;
            var validCount = 0;
            for (var i = 0; i < conditions.Count; i++)
            {
                var condition = conditions[i];
                var fingerShape = hand.CalculateFingerShape(condition.fingerID, XRFingerShapeTypes.All);
                if (!TryCalculateFingerScore(fingerShape, condition, out var fingerScore))
                    continue;

                scoreSum += fingerScore;
                validCount++;
            }

            if (validCount == 0)
                return false;

            completeness = scoreSum / validCount;
            return true;
        }

        public static string BuildCompactReport(in XRHand hand, XRHandShape handShape, int maxFingerReports = 3)
        {
            if (!hand.isTracked || handShape == null)
                return "score=n/a";

            if (!TryCalculateCompleteness(hand, handShape, out var completeness))
                return "score=n/a";

            var builder = new StringBuilder();
            builder.Append("score=");
            builder.Append(completeness.ToString("0.00"));

            var conditions = handShape.fingerShapeConditions;
            var appended = 0;
            for (var i = 0; i < conditions.Count && appended < maxFingerReports; i++)
            {
                var condition = conditions[i];
                var fingerShape = hand.CalculateFingerShape(condition.fingerID, XRFingerShapeTypes.All);
                if (!TryFindWorstTarget(fingerShape, condition, out var target, out var value, out var targetScore))
                    continue;

                builder.Append(' ');
                builder.Append(condition.fingerID);
                builder.Append(':');
                builder.Append(target.shapeType);
                builder.Append('=');
                builder.Append(value.ToString("0.00"));
                builder.Append("[");
                builder.Append(Mathf.Clamp01(target.desired - target.lowerTolerance).ToString("0.00"));
                builder.Append("-");
                builder.Append(Mathf.Clamp01(target.desired + target.upperTolerance).ToString("0.00"));
                builder.Append("]");
                builder.Append("s");
                builder.Append(targetScore.ToString("0.00"));
                appended++;
            }

            return builder.ToString();
        }

        private static bool TryCalculateFingerScore(in XRFingerShape fingerShape, in XRFingerShapeCondition condition, out float score)
        {
            score = 0f;
            if (condition.targets == null || condition.targets.Length == 0)
                return false;

            var scoreSum = 0f;
            var validCount = 0;
            for (var i = 0; i < condition.targets.Length; i++)
            {
                var target = condition.targets[i];
                if (!TryGetFingerShapeValue(target.shapeType, fingerShape, out var value))
                    continue;

                scoreSum += CalculateTargetScore(value, target);
                validCount++;
            }

            if (validCount == 0)
                return false;

            score = scoreSum / validCount;
            return true;
        }

        private static bool TryFindWorstTarget(
            in XRFingerShape fingerShape,
            in XRFingerShapeCondition condition,
            out XRFingerShapeCondition.Target worstTarget,
            out float worstValue,
            out float worstScore)
        {
            worstTarget = default;
            worstValue = 0f;
            worstScore = 1f;

            if (condition.targets == null || condition.targets.Length == 0)
                return false;

            var found = false;
            for (var i = 0; i < condition.targets.Length; i++)
            {
                var target = condition.targets[i];
                if (!TryGetFingerShapeValue(target.shapeType, fingerShape, out var value))
                    continue;

                var score = CalculateTargetScore(value, target);
                if (found && score >= worstScore)
                    continue;

                found = true;
                worstTarget = target;
                worstValue = value;
                worstScore = score;
            }

            return found;
        }

        private static bool TryGetFingerShapeValue(XRFingerShapeType shapeType, in XRFingerShape fingerShape, out float value)
        {
            value = 0f;
            return shapeType switch
            {
                XRFingerShapeType.FullCurl => fingerShape.TryGetFullCurl(out value),
                XRFingerShapeType.BaseCurl => fingerShape.TryGetBaseCurl(out value),
                XRFingerShapeType.TipCurl => fingerShape.TryGetTipCurl(out value),
                XRFingerShapeType.Pinch => fingerShape.TryGetPinch(out value),
                XRFingerShapeType.Spread => fingerShape.TryGetSpread(out value),
                _ => false
            };
        }

        private static float CalculateTargetScore(float value, in XRFingerShapeCondition.Target target)
        {
            var lower = Mathf.Clamp01(target.desired - target.lowerTolerance);
            var upper = Mathf.Clamp01(target.desired + target.upperTolerance);
            if (lower <= value && value <= upper)
                return 1f;

            var lowerBuffer = Mathf.Clamp01(lower - ExternalBuffer);
            if (lowerBuffer < value && value < lower)
                return Mathf.InverseLerp(lowerBuffer, lower, value);

            var upperBuffer = Mathf.Clamp01(upper + ExternalBuffer);
            if (upper < value && value < upperBuffer)
                return Mathf.InverseLerp(upperBuffer, upper, value);

            return 0f;
        }
    }
}
