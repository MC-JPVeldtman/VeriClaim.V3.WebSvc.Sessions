using VeriClaim.SessionState.Client;

namespace Sessions.Tests
{
    public class CookielessReferrerRecoveryTests
    {
        private const string Id = "abcdefghijklmnopqrstuvwx";

        [Theory]
        [InlineData("https://x.vericlaim.co.za/Diary.aspx?d=1", "https://x.vericlaim.co.za/(S(abcdefghijklmnopqrstuvwx))/Main.aspx", "/", "/(S(abcdefghijklmnopqrstuvwx))/Diary.aspx?d=1")]
        [InlineData("https://x.vericlaim.co.za/Services/Get.aspx", "https://x.vericlaim.co.za/(S(abcdefghijklmnopqrstuvwx))/", "/", "/(S(abcdefghijklmnopqrstuvwx))/Services/Get.aspx")]
        [InlineData("https://h/app/Page.aspx", "https://h/app/(S(abcdefghijklmnopqrstuvwx))/Main.aspx", "/app", "/app/(S(abcdefghijklmnopqrstuvwx))/Page.aspx")]
        public void Recovers_IdFromSameOriginReferrer(string request, string referrer, string appPath, string expected)
        {
            Assert.True(CookielessReferrerRecovery.TryRecover(new Uri(request), new Uri(referrer), appPath, out var redirectUrl));
            Assert.Equal(expected, redirectUrl);
        }

        [Theory]
        [InlineData("https://x.vericlaim.co.za/Diary.aspx", null)]
        [InlineData("https://x.vericlaim.co.za/Diary.aspx", "https://x.vericlaim.co.za/Main.aspx")]
        [InlineData("https://x.vericlaim.co.za/Diary.aspx", "https://evil.example/(S(" + Id + "))/Main.aspx")]
        [InlineData("https://x.vericlaim.co.za/Diary.aspx", "https://x2.vericlaim.co.za/(S(" + Id + "))/Main.aspx")]
        [InlineData("https://x.vericlaim.co.za/Diary.aspx", "https://x.vericlaim.co.za/Folder/(S(" + Id + "))/Main.aspx")]
        [InlineData("https://x.vericlaim.co.za/Diary.aspx", "https://x.vericlaim.co.za/(S(NOT_A_VALID_ID_AT_ALL_XX))/Main.aspx")]
        public void Ignores_MissingForeignOrMalformedReferrers(string request, string? referrer)
        {
            Assert.False(CookielessReferrerRecovery.TryRecover(new Uri(request), referrer == null ? null! : new Uri(referrer), "/", out _));
        }
    }
}
