namespace Anduril.Integrations;

internal static class MediumArticleBrowserConfiguration
{
    public static bool HasAttachEndpoint(MediumArticleToolOptions options) =>
        !string.IsNullOrWhiteSpace(options.BrowserRemoteDebuggingUrl);

    public static bool HasLaunchProfile(MediumArticleToolOptions options) =>
        !string.IsNullOrWhiteSpace(options.BrowserUserDataDirectory);

    public static bool IsConfigured(MediumArticleToolOptions options) =>
        HasAttachEndpoint(options) || HasLaunchProfile(options);

    public static string MissingConfigurationMessage =>
        "Configure Integrations:MediumArticle:BrowserRemoteDebuggingUrl to attach to an already-running Chrome/Edge session, or Integrations:MediumArticle:BrowserUserDataDirectory to launch a dedicated browser profile.";
}