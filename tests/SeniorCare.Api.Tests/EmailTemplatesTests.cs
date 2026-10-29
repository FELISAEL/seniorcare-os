using Microsoft.Extensions.Configuration;
using SeniorCare.Api.App.Services.Notifications;

namespace SeniorCare.Api.Tests.Services.Notifications;

public sealed class EmailTemplatesTests
{
    [Fact]
    public void DoseConfirmed_IncludesResidentAndMedication()
    {
        var mail = EmailTemplates.DoseConfirmed(
            "ana@example.com", "María López", "Losartán", "50 mg",
            DateTimeOffset.UtcNow, "el propio residente");

        Assert.Equal("SeniorCare · María López tomó su medicamento", mail.Subject);
        Assert.Contains("Losartán", mail.TextBody);
        Assert.Contains("el propio residente", mail.HtmlBody);
    }

    [Fact]
    public void AlertCreated_EmergencyUsesSosSubject()
    {
        var mail = EmailTemplates.AlertCreated(
            "ana@example.com", "María López", "emergency", "SOS", DateTimeOffset.UtcNow);

        Assert.Contains("ALERTA SOS", mail.Subject);
    }

    [Fact]
    public void Templates_EncodeHtml()
    {
        var mail = EmailTemplates.AlertCreated(
            "ana@example.com", "<b>X</b>", "assistance", "<script>", DateTimeOffset.UtcNow);

        Assert.DoesNotContain("<script>", mail.HtmlBody);
    }

    [Theory]
    [InlineData("abcd efgh ijkl mnop", "abcdefghijklmnop")]
    public void Options_StripSpacesFromAppPassword(string raw, string expected)
    {
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SMTP_ENABLED"] = "true",
                ["SMTP_USER"] = "a@gmail.com",
                ["SMTP_PASSWORD"] = raw
            }).Build();

        var options = EmailOptions.FromConfiguration(config);

        Assert.Equal(expected, options.Password);
        Assert.True(options.IsConfigured);
        Assert.Equal("a@gmail.com", options.From);
    }

    [Fact]
    public void VideoCallRequested_IncludesJoinLink()
    {
        var mail = EmailTemplates.VideoCallRequested(
            "ana@example.com", "María López", "https://meet.google.com/abc-defg-hij", DateTimeOffset.UtcNow);

        Assert.Contains("quiere hacer una videollamada", mail.Subject);
        Assert.Contains("https://meet.google.com/abc-defg-hij", mail.HtmlBody);
        Assert.Contains("https://meet.google.com/abc-defg-hij", mail.TextBody);
    }
}
