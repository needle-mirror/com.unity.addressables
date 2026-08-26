using NUnit.Framework;
using UnityEditor.AddressableAssets.BuildReportVisualizer;

namespace Tests.Editor.BuildReportVisualizer
{
    public class BuildReportUtilityTests
    {
        [TestCase(0d, "0s")]
        [TestCase(-1d, "0s")]
        [TestCase(-3600d, "0s")]
        public void GetDurationString_ReturnsZero_WhenSecondsIsNotPositive(double seconds, string expected)
        {
            Assert.AreEqual(expected, BuildReportUtility.GetDurationString(seconds));
        }

        [TestCase(0.001d, "1ms")]
        [TestCase(0.5d, "500ms")]
        [TestCase(0.9d, "900ms")]
        public void GetDurationString_UsesMilliseconds_BelowOneSecond(double seconds, string expected)
        {
            Assert.AreEqual(expected, BuildReportUtility.GetDurationString(seconds));
        }

        [TestCase(1d, "1s")]
        [TestCase(2d, "2s")]
        [TestCase(9d, "9s")]
        [TestCase(10d, "10s")]
        [TestCase(59d, "59s")]
        public void GetDurationString_UsesSeconds_BelowOneMinute(double seconds, string expected)
        {
            Assert.AreEqual(expected, BuildReportUtility.GetDurationString(seconds));
        }

        [TestCase(60d, "1m 0s")]
        [TestCase(90d, "1m 30s")]
        [TestCase(3599d, "59m 59s")]
        public void GetDurationString_UsesMinutesAndSeconds_BelowOneHour(double seconds, string expected)
        {
            Assert.AreEqual(expected, BuildReportUtility.GetDurationString(seconds));
        }

        [TestCase(3600d, "1h 0m 0s")]
        [TestCase(3661d, "1h 1m 1s")]
        [TestCase(86399d, "23h 59m 59s")]
        public void GetDurationString_UsesHoursMinutesAndSeconds_BelowOneDay(double seconds, string expected)
        {
            Assert.AreEqual(expected, BuildReportUtility.GetDurationString(seconds));
        }

        [TestCase(86400d, "1d 0h 0m")]
        [TestCase(90061d, "1d 1h 1m")]
        [TestCase(172800d, "2d 0h 0m")]
        public void GetDurationString_UsesDaysHoursAndMinutes_FromOneDay(double seconds, string expected)
        {
            Assert.AreEqual(expected, BuildReportUtility.GetDurationString(seconds));
        }
    }
}
