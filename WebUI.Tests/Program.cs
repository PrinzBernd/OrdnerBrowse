using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using WebUI.Web.Services;
using WebUI.Infrastructure;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Diagnostics;

var tests = new (string Name, Func<Task> Execute)[]
{
    ("Deaktivierter Guard benötigt kein Secret", TestDisabledAsync),
    ("Deaktivierter Guard lässt die Folgepipeline unverändert passieren", TestDisabledMiddlewareAsync),
    ("Aktivierter Guard ist nicht an RuntimeProfile, Umgebung oder OIDC gekoppelt", TestGuardIndependentFromRuntimeContextAsync),
    ("Automatische ASP.NET-Core-Forwarded-Headers-Aktivierung wird abgelehnt", TestAutomaticForwardedHeadersAsync),
    ("Eindeutiges Docker-Default-Gateway wird aus Linux-Routingtabelle gelesen", TestForwardedHeadersSingleGatewayAsync),
    ("Fehlendes Docker-Default-Gateway wird abgelehnt", TestForwardedHeadersMissingGatewayAsync),
    ("Mehrere Docker-Default-Gateways werden abgelehnt", TestForwardedHeadersMultipleGatewaysAsync),
    ("Proxy Guard steht in der Pipeline vor Forwarded Headers", TestProxyGuardRunsBeforeForwardedHeadersAsync),
    ("Forwarded Headers vertrauen genau dem ermittelten KnownProxy", TestForwardedHeadersKnownProxyConfigurationAsync),
    ("Direkter Secretwert wird abgelehnt", TestDirectSecretAsync),
    ("Relativer Secretpfad wird abgelehnt", TestRelativeSecretPathAsync),
    ("Fehlende Secretdatei wird abgelehnt", TestMissingSecretFileAsync),
    ("Leere Secretdatei wird abgelehnt", TestEmptySecretFileAsync),
    ("Falsche Secretlänge wird abgelehnt", TestWrongSecretLengthAsync),
    ("Großbuchstaben im Secret werden abgelehnt", TestUppercaseSecretAsync),
    ("Ungültiges Zeichen im Secret wird abgelehnt", TestInvalidSecretCharacterAsync),
    ("Zusätzlicher Zeilenumbruch wird abgelehnt", TestExtraNewlineAsync),
    ("Gültige Secretdatei ohne Zeilenumbruch wird akzeptiert", TestValidSecretWithoutNewlineAsync),
    ("Gültige Secretdatei mit LF wird akzeptiert", TestValidSecretWithLfAsync),
    ("Gültige Secretdatei mit CRLF wird akzeptiert", TestValidSecretWithCrLfAsync),
    ("Fehlender Header liefert leeres 403", TestMissingHeaderAsync),
    ("Leerer Header liefert leeres 403", TestEmptyHeaderAsync),
    ("Falscher Header liefert leeres 403", TestWrongHeaderAsync),
    ("Teiltreffer liefert leeres 403", TestPartialHeaderAsync),
    ("Großbuchstaben im Header liefern leeres 403", TestUppercaseHeaderAsync),
    ("Mehrere Headerwerte liefern leeres 403", TestMultipleHeadersAsync),
    ("Kommaliste liefert leeres 403", TestCommaListAsync),
    ("Gültiger Header setzt Pipeline fort", TestValidHeaderAsync),
    ("Gültiger Header wird vor Folgelogik entfernt", TestHeaderRemovedAsync),
    ("Paperless-Basisadresse als Direktwert wird akzeptiert", TestPaperlessBaseUrlDirectValueAsync),
    ("Paperless-Basisadresse aus Datei mit Zeilenumbruch wird akzeptiert", TestPaperlessBaseUrlFileValueAsync),
    ("Abschließender Schrägstrich der Paperless-Basisadresse wird entfernt", TestPaperlessBaseUrlTrailingSlashAsync),
    ("Direktwert und Dateipfad der Paperless-Basisadresse werden gemeinsam abgelehnt", TestPaperlessBaseUrlConflictingSourcesAsync),
    ("Relativer Dateipfad der Paperless-Basisadresse wird abgelehnt", TestPaperlessBaseUrlRelativePathAsync),
    ("Fehlende oder mehrzeilige Datei der Paperless-Basisadresse wird abgelehnt", TestPaperlessBaseUrlInvalidFileAsync),
    ("Ungültige Paperless-Basisadresse aus Datei wird abgelehnt", TestPaperlessBaseUrlInvalidUrlAsync),
    ("Phase A1 PaperlessApiClient bleibt vollständig fachlich read-only", TestPaperlessApiClientReadOnlySurfaceAsync),
    ("Phase A1 Paperless-Leserouten und Requestparameter bleiben kontrolliert", TestPaperlessReadEndpointContractAsync),
    ("Phase A1 Thumbnail-Proxy bleibt autorisiert und nicht cachebar", TestThumbnailProxyAuthorizationAndCacheAsync),
    ("Phase A1 externer Paperless-Link und Basisadresse bleiben sicher", TestPaperlessExternalLinkSecurityAsync),
    ("Phase A2-TEST geschützte Tokenablage bleibt verschlüsselt und atomar", TestProtectedTokenStoreEncryptedAtomicPersistenceAsync),
    ("Phase A2-TEST technische Benutzerkennungen bleiben strikt getrennt", TestTechnicalUserKeyIsolationAsync),
    ("Phase A2-TEST Tokenänderung verlangt zwei identische Eingaben", TestPaperlessConnectionDoubleEntryAsync),
    ("Phase A2-TEST lokaler Keychain-Lesevertrag bleibt secret-sicher", TestLocalKeychainReadContractAsync),
    ("Phase A2-TEST OIDC-Fehlerdiagnose bleibt allowlist- und encoding-begrenzt", TestAuthenticationHtmlPagesSafeDiagnosticsAsync),
    ("Phase A2-TEST Data-Protection-Persistenz bleibt pfadgebunden", TestDataProtectionPersistencePathValidationAsync),
    ("Phase A2-TEST Data-Protection-Zertifikatsschutz bleibt dateibasiert", TestDataProtectionCertificateSecretContractAsync),
    ("Phase A2-TEST lokaler Mehrbenutzertest bleibt vollständig Loopback-begrenzt", TestLocalTestModeLoopbackSecurityAsync),
    ("Phase A2-OIDC OIDC-Einstellungen erzwingen sichere HTTPS-Adressen und File-only-Secret", TestOidcSettingsSecurityContractAsync),
    ("Phase A2-OIDC Sitzungscookie bleibt __Host-, HttpOnly-, Secure- und SameSite-gebunden", TestOidcSessionCookieSecurityAsync),
    ("Phase A2-OIDC Auth-Status bleibt minimal und autorisierungspflichtig", TestOidcAuthStatusContractAsync),
    ("Phase A2-OIDC Callback setzt pseudonyme Benutzer-/Sitzungsclaims und gibt Challenge-Lease frei", TestOidcCallbackClaimsAndLeaseReleaseAsync),
    ("Phase A3-NAV Navigationscache speichert/lädt Formatversion 3 und verwirft inkompatible Versionen", TestNavigationCacheSaveLoadAndVersionAsync),
    ("Phase A3-NAV Navigationscache trennt Cachekeys und prüft SourceStates vollständig", TestNavigationCachePartitionAndMatchesAsync),
    ("Phase A3-NAV ApplyChanges ersetzt und ergänzt Dokumente konsistent", TestNavigationCacheApplyChangesAsync),
    ("Phase A3-NAV beschädigter Navigationscache fällt kontrolliert auf null zurück", TestNavigationCacheCorruptFallbackAsync),
    ("Phase A3-NAV zentraler Sync prüft sofort und dedupliziert manuelle Full-Refresh-Aufrufe", TestCentralNavigationSyncIncrementalAndManualAsync),
    ("Phase A3-NAV Full Refresh meldet Status/Fortschritt/Completion und respektiert Sitzungsverfügbarkeit", TestCentralNavigationSyncFullRefreshAsync),
    ("Phase A3-RUNTIME Fehlerklassifizierung trennt HTTP, Timeout, kontrollierten Abbruch und technische Ereignis-ID", TestErrorClassificationContractAsync),
    ("Phase A3-RUNTIME Performance-Diagnostik schreibt nur technische Messdaten und sanitisiert Steuerzeichen", TestPerformanceDiagnosticsMeasurementContractAsync),
    ("Phase A3-RUNTIME Anwendung und Preview-Endpunkte bleiben autorisierungspflichtig und Antiforgery bleibt aktiv", TestApplicationAndPreviewAuthorizationContractAsync),
    ("Phase A3-RUNTIME Healthz bleibt anonym, HSTS produktiv und HTTPS-Weiterleitung konfigurierbar", TestHealthzAndHttpsHstsContractAsync),
    ("Phase A3-RUNTIME Proxy-Guard-Healthcheck bleibt früher Sondermodus ohne normalen Serverstart", TestProxyGuardHealthcheckModeContractAsync),
    ("Phase A3-RUNTIME OIDC-Diagnostik-Hooks bleiben an sichere technische Ereignisse gebunden", TestOidcDiagnosticEventHooksContractAsync),
    ("Phase A3-CONTAINER Dockerfile bleibt Multi-Stage und architekturgebunden", TestDockerfileMultistageArchitectureContractAsync),
    ("Phase A3-CONTAINER Compose erzwingt Image/Port/tmpfs/init-Vertrag", TestComposeImagePortTmpfsInitContractAsync),
    ("Phase A3-CONTAINER Compose hält Healthcheck/Ressourcen/Logrotation fest", TestComposeHealthResourceLoggingContractAsync),
    ("Phase A3-CONTAINER Compose bleibt auf restart unless-stopped", TestComposeRestartContractAsync),
    ("Q-01 relative Pagination-URL bleibt zulässig", TestPaginationRelativeUrlAsync),
    ("Q-01 absolute Same-Origin-Pagination-URL bleibt zulässig", TestPaginationAbsoluteSameOriginUrlAsync),
    ("Q-01 Hostvergleich ist nicht groß-/kleinschreibungssensitiv", TestPaginationHostCaseAsync),
    ("Q-01 expliziter Standardport bleibt zulässig", TestPaginationExplicitDefaultPortAsync),
    ("Q-01 fremder Host wird abgelehnt", TestPaginationForeignHostAsync),
    ("Q-01 präfixgleicher fremder Host wird abgelehnt", TestPaginationPrefixHostAsync),
    ("Q-01 anderer Port wird abgelehnt", TestPaginationForeignPortAsync),
    ("Q-01 anderes Protokoll wird abgelehnt", TestPaginationForeignSchemeAsync),
    ("Q-01 protokollrelative fremde URL wird abgelehnt", TestPaginationProtocolRelativeForeignHostAsync),
    ("Q-01 URL mit Benutzerinformationen wird abgelehnt", TestPaginationUserInfoAsync),
    ("Q-01 ungültige Pagination-URL wird kontrolliert abgelehnt", TestPaginationInvalidUrlAsync),
    ("Q-03 Logout-Endpunkte verwenden ausschließlich POST", TestLogoutEndpointsUsePostAsync),
    ("Q-03 Logout-Endpunkte validieren Antiforgery vor Zustandsänderung", TestLogoutEndpointsValidateAntiforgeryAsync),
    ("Q-03 OIDC-Logout beendet ausschließlich die lokale WebUI-Cookie-Sitzung", TestOidcLogoutKeepsProviderSessionAsync),
    ("Q-03 lokaler Logout beendet nur die Testsession und leitet zur lokalen Anmeldung", TestLocalLogoutBehaviorAsync),
    ("Q-03 Abmelden wird als geschütztes POST-Formular gerendert", TestLogoutFormsUsePostAndAntiforgeryAsync),
    ("Q-02 PaperlessApiClient erzeugt keinen eigenen HttpClient oder Handler", TestQ02PaperlessApiClientUsesInjectedHttpClientAsync),
    ("Q-02 PaperlessClientFactory verwendet IHttpClientFactory", TestQ02PaperlessClientFactoryUsesHttpClientFactoryAsync),
    ("Q-02 Tokenvalidierung und lokaler Login verwenden den verwalteten Paperless-Client", TestQ02AllPaperlessCreationPathsUseHttpClientFactoryAsync),
    ("AP01 lokale Startseite bietet API-Token ändern nach Testanmeldung", TestAp01LocalStartOffersTokenChangeAsync),
    ("AP01 gemeinsame Tokenänderungsseite verwendet gemeinsamen Token-Store", TestAp01SharedTokenChangeStoreAsync),
    ("AP01 lokaler Schlüsselbund ersetzt Token und SHA-256-Fingerprint mit Rückfall", TestAp01LocalKeychainReplaceAndRollbackAsync),
    ("AP01 Schlüsselbundschreiben übergibt kein Secret an security-Prozessargumente", TestAp01LocalKeychainNoSecretProcessArgumentsAsync),
    ("AP01 lokale Authentifizierungsendpunkte sind aus Program.cs ausgelagert", TestAp01LocalEndpointsExtractedAsync),
    ("AP02 Challenge Guard lässt ersten Browserkontext ohne aktive Korrelation passieren", TestAp02ChallengeGuardAllowsFirstRequestAsync),
    ("AP02 Challenge Guard erkennt bestehende OIDC-Korrelation", TestAp02ChallengeGuardDetectsActiveCorrelationAsync),
    ("AP02 Challenge Guard ignoriert unbeteiligte Cookies", TestAp02ChallengeGuardIgnoresUnrelatedCookiesAsync),
    ("AP02 Login-Endpunkt blockiert Parallel-Challenge vor zweitem Challenge-Aufruf", TestAp02LoginEndpointBlocksParallelChallengeAsync),
    ("AP02 Startseite sperrt Mehrfachklick beim Anmeldestart", TestAp02StartLoginUiGuardAsync),
    ("AP02 Challenge Guard wertet keine Cookie-Inhalte aus", TestAp02ChallengeGuardDoesNotReadCookieValuesAsync),
    ("AP02.3 UID/GID-Prüfskript ist vorhanden und POSIX-sh-basiert", TestAp023ValidatorPresentAsync),
    ("AP02.3 positive UID/GID werden akzeptiert", TestAp023ValidatorAcceptsPositiveIdsAsync),
    ("AP02.3 fehlende UID wird abgelehnt", TestAp023ValidatorRejectsMissingUidAsync),
    ("AP02.3 fehlende GID wird abgelehnt", TestAp023ValidatorRejectsMissingGidAsync),
    ("AP02.3 Null-ID wird abgelehnt", TestAp023ValidatorRejectsZeroAsync),
    ("AP02.3 negative und vorzeichenbehaftete IDs werden abgelehnt", TestAp023ValidatorRejectsSignedValuesAsync),
    ("AP02.3 Leerzeichen und nicht numerische IDs werden abgelehnt", TestAp023ValidatorRejectsWhitespaceAndTextAsync),
    ("AP02.3 führende Nullen werden abgelehnt", TestAp023ValidatorRejectsLeadingZeroAsync),
    ("AP02.3 Compose verlangt APP_UID und APP_GID und setzt user explizit", TestAp023ComposeUserBindingAsync),
    ("AP02.3 Secret-Mount bleibt read-only", TestAp023SecretsRemainReadOnlyAsync),
    ("AP02.3 Container-Root bleibt read-only", TestAp023RootFilesystemRemainsReadOnlyAsync),
    ("AP02.3 no-new-privileges und cap_drop ALL bleiben erhalten", TestAp023ContainerSecurityOptionsRemainAsync),
    ("AP02.3 genau fünf persistente RW-Bind-Pfade bleiben erhalten", TestAp023PersistentWritableBindsRemainAsync),
    ("AP02.3 Dockerfile behält nicht privilegierten Default USER 1654", TestAp023DockerfileNonRootDefaultAsync),
    ("AP03 OIDC-Guard erlaubt bei zwei parallelen Acquire-Versuchen exakt einen Gewinner", TestAp03OidcAtomicAcquireAsync),
    ("AP03 OIDC-Guard isoliert verschiedene Browserkontexte", TestAp03OidcDifferentBrowserContextsAsync),
    ("AP03 OIDC-Guard Release gibt denselben Browserkontext wieder frei", TestAp03OidcReleaseAsync),
    ("AP03 OIDC-Guard ersetzt ausschließlich abgelaufene Leases", TestAp03OidcExpiryAsync),
    ("AP03 OIDC-Guard-Freigabe ist an TokenValidated und RemoteFailure angebunden", TestAp03OidcReleaseHooksAsync),
    ("AP03 OIDC-Guard-Schlüssel wird nicht diagnostisch protokolliert", TestAp03OidcGuardKeyNotLoggedAsync),
    ("AP03 OIDC-Browserkontextcookie mit __Host-Präfix ist immer Secure", TestAp03OidcBrowserContextCookieIsAlwaysSecureAsync),
    ("AP06 Docker Desktop Proxy-Hop wird über gateway.docker.internal ermittelt", TestAp06DockerDesktopProxyResolutionSourceAsync),
    ("AP06 erster OIDC-Login verwendet denselben vorbereiteten Browserkontext ohne 409-Zwischenseite", TestAp06OidcFirstLoginContinuesWithoutContextPageAsync),
    ("AP03 Start-Wrapper ist POSIX-sh-basiert und vorhanden", TestAp03StartWrapperPresentAsync),
    ("AP03 Start-Wrapper erzwingt Validator vor docker compose", TestAp03StartWrapperOrdersValidationBeforeDockerAsync),
    ("AP03 ungültige UID verhindert technisch jeden Docker-Aufruf", TestAp03StartWrapperBlocksDockerOnInvalidUidAsync),
    ("AP03 gültige UID/GID erreicht den kontrollierten Docker-Aufruf", TestAp03StartWrapperAllowsDockerAfterValidationAsync),
    ("AP03 Start-Wrapper verlangt genau einen sicheren Projektname", TestAp03StartWrapperRequiresProjectNameAsync),
    ("AP03 README dokumentiert ausschließlich den Wrapper als Standardstartweg", TestAp03ReadmeUsesStartWrapperAsync),
    ("Q-02 Paperless-Handler deaktiviert Cookies", TestQ02PaperlessHandlerDisablesCookiesAsync),
    ("Q-02 persönliche Authorization-Header bleiben pro logischem Client getrennt", TestQ02AuthorizationHeadersRemainSeparatedAsync),
    ("Q-02 Proxy-Guard-Sonderclient bleibt explizit lokal begrenzt", TestQ02ProxyGuardSpecialClientRemainsScopedAsync),
    ("OIDC-Diagnosedateiprotokollierung ist ohne Pfad deaktiviert", TestOidcDiagnosticsFileLoggingDisabledAsync),
    ("OIDC-Diagnosedateiprotokollierung lehnt ungültige Konfiguration ab", TestOidcDiagnosticsFileLoggingInvalidConfigurationAsync),
    ("OIDC-Diagnosedateiprotokollierung schreibt ausschließlich die sichere Kategorie", TestOidcDiagnosticsFileLoggingCategoryAsync),
    ("OIDC-Diagnosedateiprotokollierung entfernt Bestände älter als sieben Tage", TestOidcDiagnosticsFileLoggingRetentionAsync),
    ("OIDC-Diagnosedateiprotokollierung enthält die WebUI-Version", TestOidcDiagnosticsContainsVersionAsync),
    ("Performance Diagnostics lehnt ungültige Konfiguration ab", TestPerformanceDiagnosticsInvalidConfigurationAsync),
    ("Performance Diagnostics schreibt Tagesdatei und Version", TestPerformanceDiagnosticsDailyFileAsync),
    ("Performance Diagnostics entfernt nur eigene alte Dateien", TestPerformanceDiagnosticsRetentionAsync),
    ("Containerbetrieb-Compose verwendet gemeinsame Diagnosepfade", TestContainerbetriebComposeDiagnosticsPathsAsync),
    ("Containerbetrieb-Compose enthält keine alten Profil-/DiskStation-Pfade", TestContainerbetriebComposeLegacyPathsAbsentAsync),
    ("Mehrseiten-Schnellansicht verwendet Paperless-Preview-Endpunkt", TestDocumentPreviewApiPathAsync),
    ("Mehrseiten-Schnellansicht verwendet geschützten WebUI-Endpunkt", TestDocumentPreviewWebEndpointAsync),
    ("Normale Dokumentvorschau bleibt beim Thumbnail", TestDocumentThumbnailRemainsForNormalPreviewAsync),
    ("Schnellansicht schaltet anhand eigener Dialogbreite auf Einspaltenmodus", TestQuickLookDialogWidthControlsLayoutAsync),
    ("Einspalten-Schnellansicht unterstützt horizontal verschiebbare Trennlinie", TestQuickLookRowDividerAsync),
    ("Schmale Browserbreite erzwingt keinen Quick-Look-Vollbildmodus", TestQuickLookNarrowBrowserKeepsResizableDialogAsync),
    ("Responsive Umschaltgrenze liegt unter der unveränderten Standardstartbreite", TestQuickLookBreakpointBelowDefaultStartWidthAsync),
    ("Schnellansicht verwendet kontrollierten lokalen PDF.js-Renderer", TestQuickLookPdfRendererAsync),
    ("PDF.js-Renderer nutzt den geschützten read-only Dokumentendpunkt", TestQuickLookPdfReadOnlyEndpointAsync),
    ("PDF.js-Renderer reagiert auf Größenänderungen", TestQuickLookPdfResizeObserverAsync),
    ("PDF.js-Renderer berechnet Fit-to-Width unabhängig von der Vorschauhöhe", TestQuickLookPdfFitCalculationAsync),
    ("PDF.js-Renderer rendert alle Dokumentseiten", TestQuickLookPdfAllPagesAsync),
    ("PDF.js-Renderer berücksichtigt HiDPI-Ausgabe", TestQuickLookPdfHiDpiAsync),
    ("PDF.js-Renderer erhält die relative Scrollposition", TestQuickLookPdfScrollPositionAsync),
    ("Lokale PDF.js-Dateien und Lizenznachweis sind vorhanden", TestLocalPdfJsAssetsAsync),
    ("Versionsschema lautet v09.91.1 und .NET 0.9.91", TestApplicationDisplayVersionAsync),
    ("Display-Präfixe sind für vier Explorer-Bereiche getrennt konfigurierbar", TestDisplayPrefixesFourAreasAsync),
    ("Display-Präfixe entfernen exakt nur den längsten passenden Anfang", TestDisplayPrefixesLongestExactPrefixAsync),
    ("Display-Präfix-Muster unterstützen definierte Datums- und Zeichenregeln", TestDisplayPrefixPatternsAsync),
    ("Display-Präfixe ignorieren ungültige Einzelregeln kontrolliert", TestDisplayPrefixesInvalidRulesAsync),
    ("Display-Präfixe behandeln fehlende, leere und initial fehlerhafte Dateien optional", TestDisplayPrefixesOptionalFileAsync),
    ("Display-Präfixe behalten bei fehlerhaftem Reload die letzte gültige Konfiguration", TestDisplayPrefixesReloadFallbackAsync),
    ("Home verwendet Display-Präfixe in Bereichen und Dokumenttiteln ohne alte Festwerte", TestDisplayPrefixesHomeIntegrationAsync),
    ("OrdnerBrowse-Branding ist zentral definiert", TestOrdnerBrowseBrandingAsync),
    ("Dokumenten-Explorer verwendet zentralen OrdnerBrowse-Seitentitel", TestOrdnerBrowseHomeTitleAsync),
    ("Startseite verwendet sichtbares OrdnerBrowse-Branding", TestOrdnerBrowseStartBrandingAsync),
    ("Authentifizierungsseiten verwenden sichtbares OrdnerBrowse-Branding", TestOrdnerBrowseAuthenticationBrandingAsync),
    ("Dokumentansichten lesen Notizen read-only und Vorschau bleibt scrollbar", TestQuickLookPdfNoPaperlessWriteAsync),
    ("Quick Look hat 300 px Mindestbreite", TestQuickLookMinimumWidth300Async),
    ("Quick Look schaltet bei 520 px zwischen Spalten- und Zeilenlayout", TestQuickLookBreakpoint520Async),
    ("Quick Look öffnet bündig auf dem Dokumenten-Explorer", TestQuickLookInitialExplorerAnchorAsync),
    ("PDF.js-Renderer rendert bei reiner Höhenänderung nicht neu", TestQuickLookPdfWidthDrivenResizeAsync),
    ("OIDC direkter ClientSecretwert wird abgelehnt", TestOidcDirectClientSecretRejectedAsync),
    ("Production-Konfiguration enthält keine festen nicht geheimen FilePath-Defaults", TestProductionConfigurationNoNonSecretFilePathDefaultsAsync)
};

var failed = 0;

foreach (var test in tests)
{
    try
    {
        await test.Execute();
        Console.WriteLine($"[x] {test.Name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.WriteLine($"[f] {test.Name}: {exception.GetType().Name}");
    }
}

Console.WriteLine(
    failed == 0
        ? "Status: ERFOLGREICH"
        : $"Status: FEHLGESCHLAGEN ({failed})");

return failed == 0 ? 0 : 1;

static Task TestDisabledAsync()
{
    using var settings = ProxyGuardSettings.Load(
        Configuration());
    Assert(!settings.Enabled, "Guard muss deaktiviert sein.");
    return Task.CompletedTask;
}

static async Task TestDisabledMiddlewareAsync()
{
    using var settings = ProxyGuardSettings.Load(
        Configuration());
    var called = false;
    var middleware = new ProxyGuardMiddleware(
        _ =>
        {
            called = true;
            return Task.CompletedTask;
        },
        settings);
    var context = NewContext();

    await middleware.InvokeAsync(context);

    Assert(called, "Deaktivierter Guard blockierte die Folgepipeline.");
}

static Task TestGuardIndependentFromRuntimeContextAsync()
{
    using var fixture = SecretFixture.CreateRandom();
    using var settings = ProxyGuardSettings.Load(
        Configuration(
            new Dictionary<string, string?>
            {
                ["WebUi:RuntimeProfile"] = "beliebiger-wert",
                ["WebUi:ProxyGuard:Enabled"] = "true",
                ["WebUi:ProxyGuard:SecretFilePath"] = fixture.Path,
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                ["Authentication:Oidc:Enabled"] = "false"
            }));

    Assert(settings.Enabled,
        "Aktivierter Guard wurde unerwartet an Profil, Umgebung oder OIDC gekoppelt.");
    return Task.CompletedTask;
}

static Task TestAutomaticForwardedHeadersAsync()
{
    var threw = false;

    try
    {
        ForwardedHeadersProxyTrust.ValidateConfiguration(
            Configuration(
                new Dictionary<string, string?>
                {
                    ["ASPNETCORE_FORWARDEDHEADERS_ENABLED"] = "true"
                }));
    }
    catch (InvalidOperationException)
    {
        threw = true;
    }

    Assert(threw,
        "ASPNETCORE_FORWARDEDHEADERS_ENABLED=true wurde nicht sicher abgelehnt.");
    return Task.CompletedTask;
}

static Task TestForwardedHeadersSingleGatewayAsync()
{
    var gateway = ForwardedHeadersProxyTrust.ParseRequiredDefaultGateway(
        new[]
        {
            "Iface\tDestination\tGateway\tFlags\tRefCnt\tUse\tMetric\tMask\tMTU\tWindow\tIRTT",
            "eth0\t00000000\t01001BAC\t0003\t0\t0\t0\t00000000\t0\t0\t0",
            "eth0\t00001BAC\t00000000\t0001\t0\t0\t0\t0000FFFF\t0\t0\t0"
        });

    Assert(
        gateway.Equals(IPAddress.Parse("172.27.0.1")),
        "Docker-Default-Gateway wurde nicht korrekt aus /proc/net/route dekodiert.");
    return Task.CompletedTask;
}

static Task TestForwardedHeadersMissingGatewayAsync()
{
    var threw = false;

    try
    {
        _ = ForwardedHeadersProxyTrust.ParseRequiredDefaultGateway(
            new[]
            {
                "Iface\tDestination\tGateway\tFlags\tRefCnt\tUse\tMetric\tMask\tMTU\tWindow\tIRTT",
                "eth0\t00001BAC\t00000000\t0001\t0\t0\t0\t0000FFFF\t0\t0\t0"
            });
    }
    catch (InvalidOperationException)
    {
        threw = true;
    }

    Assert(threw,
        "Fehlendes Docker-Default-Gateway wurde nicht sicher abgelehnt.");
    return Task.CompletedTask;
}

static Task TestForwardedHeadersMultipleGatewaysAsync()
{
    var threw = false;

    try
    {
        _ = ForwardedHeadersProxyTrust.ParseRequiredDefaultGateway(
            new[]
            {
                "Iface\tDestination\tGateway\tFlags\tRefCnt\tUse\tMetric\tMask\tMTU\tWindow\tIRTT",
                "eth0\t00000000\t01001BAC\t0003\t0\t0\t0\t00000000\t0\t0\t0",
                "eth1\t00000000\t010012AC\t0003\t0\t0\t0\t00000000\t0\t0\t0"
            });
    }
    catch (InvalidOperationException)
    {
        threw = true;
    }

    Assert(threw,
        "Mehrere Docker-Default-Gateways wurden nicht sicher abgelehnt.");
    return Task.CompletedTask;
}

static Task TestProxyGuardRunsBeforeForwardedHeadersAsync()
{
    var source = ReadProjectSource("WebUI.Web/Program.cs");
    var guardPosition = source.IndexOf(
        "app.UseMiddleware<ProxyGuardMiddleware>();",
        StringComparison.Ordinal);
    var forwardedPosition = source.IndexOf(
        "app.UseForwardedHeaders();",
        StringComparison.Ordinal);

    Assert(
        guardPosition >= 0 &&
        forwardedPosition > guardPosition,
        "Proxy Guard steht nicht eindeutig vor der Forwarded-Headers-Verarbeitung.");
    return Task.CompletedTask;
}

static Task TestForwardedHeadersKnownProxyConfigurationAsync()
{
    var source = ReadProjectSource("WebUI.Web/Program.cs");

    Assert(
        source.Contains(
            "ForwardedHeaders.XForwardedFor |\n                ForwardedHeaders.XForwardedProto",
            StringComparison.Ordinal),
        "Forwarded Headers sind nicht eindeutig auf X-Forwarded-For und X-Forwarded-Proto begrenzt.");
    Assert(
        source.Contains(
            "options.KnownProxies.Add(\n                knownProxy);",
            StringComparison.Ordinal),
        "Das ermittelte Docker-Gateway wird nicht als einzelner KnownProxy eingetragen.");
    Assert(
        !source.Contains(
            "ForwardedHeaders.XForwardedHost",
            StringComparison.Ordinal),
        "X-Forwarded-Host wurde unerwartet in die Verarbeitung aufgenommen.");
    return Task.CompletedTask;
}

static Task TestDirectSecretAsync()
{
    var values = EnabledValues();
    values["WebUi:ProxyGuard:Secret"] = "unzulässig";
    return ExpectInvalidWithSecretAsync(
        values);
}

static Task TestRelativeSecretPathAsync()
{
    var values = EnabledValues("relative/proxy_guard_secret");
    return ExpectInvalidAsync(
        values);
}

static Task TestMissingSecretFileAsync()
{
    var values = EnabledValues(
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
    return ExpectInvalidAsync(
        values);
}

static Task TestEmptySecretFileAsync() =>
    ExpectInvalidSecretBytesAsync(Array.Empty<byte>());

static Task TestWrongSecretLengthAsync() =>
    ExpectInvalidSecretTextAsync(new string('a', 63));

static Task TestUppercaseSecretAsync() =>
    ExpectInvalidSecretTextAsync(new string('A', 64));

static Task TestInvalidSecretCharacterAsync() =>
    ExpectInvalidSecretTextAsync(new string('a', 63) + "g");

static Task TestExtraNewlineAsync() =>
    ExpectInvalidSecretBytesAsync(
        System.Text.Encoding.ASCII.GetBytes(new string('a', 64) + "\n\n"));

static Task TestValidSecretWithoutNewlineAsync() =>
    TestValidSecretBytesAsync(
        System.Text.Encoding.ASCII.GetBytes(new string('a', 64)));

static Task TestValidSecretWithLfAsync() =>
    TestValidSecretBytesAsync(
        System.Text.Encoding.ASCII.GetBytes(new string('a', 64) + "\n"));

static Task TestValidSecretWithCrLfAsync() =>
    TestValidSecretBytesAsync(
        System.Text.Encoding.ASCII.GetBytes(new string('a', 64) + "\r\n"));

static Task TestMissingHeaderAsync() =>
    RunRejectedRequestAsync(null);

static Task TestEmptyHeaderAsync() =>
    RunRejectedRequestAsync(string.Empty);

static Task TestWrongHeaderAsync() =>
    RunRejectedRequestAsync(new string('b', 64));

static Task TestPartialHeaderAsync() =>
    RunRejectedRequestAsync(new string('a', 32));

static Task TestUppercaseHeaderAsync() =>
    RunRejectedRequestAsync(new string('A', 64));

static Task TestMultipleHeadersAsync() =>
    RunRejectedRequestAsync(
        new StringValues(
            new[]
            {
                new string('a', 64),
                new string('a', 64)
            }));

static Task TestCommaListAsync() =>
    RunRejectedRequestAsync(
        $"{new string('a', 64)},{new string('a', 64)}");

static async Task TestValidHeaderAsync()
{
    using var fixture = SecretFixture.CreateRandom();
    using var settings = LoadEnabled(fixture.Path);
    var called = false;
    var middleware = new ProxyGuardMiddleware(
        _ =>
        {
            called = true;
            return Task.CompletedTask;
        },
        settings);
    var context = NewContext();
    context.Request.Headers[ProxyGuardSettings.HeaderName] = fixture.Value;

    await middleware.InvokeAsync(context);

    Assert(called, "Folgepipeline wurde nicht aufgerufen.");
    Assert(context.Response.StatusCode != StatusCodes.Status403Forbidden,
        "Gültiger Header wurde abgewiesen.");
}

static async Task TestHeaderRemovedAsync()
{
    using var fixture = SecretFixture.CreateRandom();
    using var settings = LoadEnabled(fixture.Path);
    var headerVisibleAfterGuard = true;
    var middleware = new ProxyGuardMiddleware(
        context =>
        {
            headerVisibleAfterGuard = context.Request.Headers.ContainsKey(
                ProxyGuardSettings.HeaderName);
            return Task.CompletedTask;
        },
        settings);
    var context = NewContext();
    context.Request.Headers[ProxyGuardSettings.HeaderName] = fixture.Value;

    await middleware.InvokeAsync(context);

    Assert(!headerVisibleAfterGuard,
        "Guard-Header blieb für die Folgelogik sichtbar.");
}

static Task TestPaperlessBaseUrlDirectValueAsync()
{
    var baseUrl = ReadPaperlessBaseUrl(
        new Dictionary<string, string?>
        {
            ["Paperless:BaseUrl"] = "https://paperless.example.test"
        });

    Assert(
        string.Equals(
            baseUrl,
            "https://paperless.example.test",
            StringComparison.Ordinal),
        "Direkte Paperless-Basisadresse wurde nicht unverändert übernommen.");
    return Task.CompletedTask;
}

static Task TestPaperlessBaseUrlFileValueAsync()
{
    using var fixture = TextFileFixture.Create(
        "https://paperless.example.test\n");
    var baseUrl = ReadPaperlessBaseUrl(
        new Dictionary<string, string?>
        {
            ["Paperless:BaseUrlFilePath"] = fixture.Path
        });

    Assert(
        string.Equals(
            baseUrl,
            "https://paperless.example.test",
            StringComparison.Ordinal),
        "Paperless-Basisadresse aus Datei wurde nicht korrekt gelesen.");
    return Task.CompletedTask;
}

static Task TestPaperlessBaseUrlTrailingSlashAsync()
{
    var baseUrl = ReadPaperlessBaseUrl(
        new Dictionary<string, string?>
        {
            ["Paperless:BaseUrl"] = "https://paperless.example.test/"
        });

    Assert(
        string.Equals(
            baseUrl,
            "https://paperless.example.test",
            StringComparison.Ordinal),
        "Abschließender Schrägstrich wurde nicht entfernt.");
    return Task.CompletedTask;
}

static Task TestPaperlessBaseUrlConflictingSourcesAsync()
{
    using var fixture = TextFileFixture.Create(
        "https://paperless.example.test\n");
    return ExpectPaperlessBaseUrlInvalidAsync(
        new Dictionary<string, string?>
        {
            ["Paperless:BaseUrl"] = "https://paperless.example.test",
            ["Paperless:BaseUrlFilePath"] = fixture.Path
        });
}

static Task TestPaperlessBaseUrlRelativePathAsync() =>
    ExpectPaperlessBaseUrlInvalidAsync(
        new Dictionary<string, string?>
        {
            ["Paperless:BaseUrlFilePath"] =
                "relative/paperless_base_url"
        });

static Task TestPaperlessBaseUrlInvalidFileAsync()
{
    var missingPath = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(),
        $"paperless-base-url-missing-{Guid.NewGuid():N}");
    ExpectPaperlessBaseUrlInvalid(
        new Dictionary<string, string?>
        {
            ["Paperless:BaseUrlFilePath"] = missingPath
        });

    using var fixture = TextFileFixture.Create(
        "https://paperless.example.test\nhttps://second.example.test\n");
    ExpectPaperlessBaseUrlInvalid(
        new Dictionary<string, string?>
        {
            ["Paperless:BaseUrlFilePath"] = fixture.Path
        });

    return Task.CompletedTask;
}

static Task TestPaperlessBaseUrlInvalidUrlAsync()
{
    using var fixture = TextFileFixture.Create(
        "ftp://paperless.example.test\n");
    return ExpectPaperlessBaseUrlInvalidAsync(
        new Dictionary<string, string?>
        {
            ["Paperless:BaseUrlFilePath"] = fixture.Path
        });
}

static async Task TestPaperlessApiClientReadOnlySurfaceAsync()
{
    var expectedPublicMethods = new[]
    {
        "GetAllDocumentsAsync",
        "GetCorrespondentsAsync",
        "GetCustomFieldsAsync",
        "GetDocumentAsync",
        "GetDocumentPreviewAsync",
        "GetDocumentThumbnailAsync",
        "GetDocumentTypesAsync",
        "GetDocumentsAsync",
        "GetNavigationDocumentsAsync",
        "GetNavigationDocumentsModifiedSinceAsync",
        "GetNavigationSourceStateAsync",
        "GetStoragePathsAsync",
        "GetSystemStatusAsync",
        "GetTagsAsync",
        "GetTrashDocumentIdsAsync",
        "GetVisibleNavigationDocumentCountAsync",
        "HasNavigationDocumentChangesAsync",
        "ValidateCurrentTokenAsync"
    };

    var actualPublicMethods = typeof(PaperlessApiClient)
        .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
        .Where(method => !method.IsSpecialName)
        .Select(method => method.Name)
        .OrderBy(name => name, StringComparer.Ordinal)
        .ToArray();

    Assert(
        actualPublicMethods.SequenceEqual(expectedPublicMethods),
        "Die öffentliche PaperlessApiClient-Oberfläche hat sich geändert und muss ausdrücklich als read-only klassifiziert werden.");

    using var handler = new PaperlessReadCaptureHandler();
    using var httpClient = new HttpClient(
        handler,
        disposeHandler: false);
    var client = new PaperlessApiClient(
        httpClient,
        "https://paperless.example.test",
        "synthetic-test-token");

    await ExercisePaperlessReadSurfaceAsync(client);

    Assert(
        handler.Requests.Count == expectedPublicMethods.Length,
        "Nicht jede öffentliche PaperlessApiClient-Leseoperation wurde durch den Capture-Handler beobachtet.");
    Assert(
        handler.Requests.All(request =>
            string.Equals(
                request.Method,
                HttpMethod.Get.Method,
                StringComparison.Ordinal)),
        "Mindestens eine PaperlessApiClient-Operation verwendet keine HTTP-GET-Methode.");
    Assert(
        handler.Requests.All(request => !request.HasContent),
        "Mindestens eine PaperlessApiClient-Leseoperation sendet unerwartet einen Request-Body.");
    Assert(
        handler.Requests.All(request =>
            string.Equals(
                request.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                request.Host,
                "paperless.example.test",
                StringComparison.OrdinalIgnoreCase) &&
            request.Port == 443 &&
            string.IsNullOrEmpty(request.UserInfo)),
        "Mindestens eine PaperlessApiClient-Anfrage verlässt die synthetische Paperless-Origin-Grenze.");
}

static async Task TestPaperlessReadEndpointContractAsync()
{
    using var handler = new PaperlessReadCaptureHandler();
    using var httpClient = new HttpClient(
        handler,
        disposeHandler: false);
    var client = new PaperlessApiClient(
        httpClient,
        "https://paperless.example.test",
        "synthetic-test-token");

    await ExercisePaperlessReadSurfaceAsync(client);

    AssertCapturedRequest(
        handler,
        "/api/status/?format=json");
    AssertCapturedRequest(
        handler,
        "/api/documents/?page_size=1&fields=id");
    AssertCapturedRequest(
        handler,
        "/api/correspondents/?page_size=2000&ordering=name");
    AssertCapturedRequest(
        handler,
        "/api/document_types/?page_size=2000&ordering=name");
    AssertCapturedRequest(
        handler,
        "/api/storage_paths/?page_size=2000&ordering=name");
    AssertCapturedRequest(
        handler,
        "/api/tags/?page_size=2000&ordering=name");
    AssertCapturedRequest(
        handler,
        "/api/custom_fields/?page_size=2000&ordering=name");
    AssertCapturedRequestContaining(
        handler,
        "/api/documents/?modified__gte=",
        "&page_size=1&ordering=-modified&fields=id,modified");
    AssertCapturedRequest(
        handler,
        "/api/trash/?page=1&page_size=1000&fields=id,deleted_at");
    AssertCapturedRequest(
        handler,
        "/api/documents/?page=1&page_size=1&ordering=-modified&fields=id,modified");
    AssertCapturedRequest(
        handler,
        "/api/documents/?page=1&page_size=1000&ordering=id&fields=id,storage_path,correspondent,document_type,modified");
    AssertCapturedRequestContaining(
        handler,
        "/api/documents/?modified__gte=",
        "&page=1&page_size=1000&ordering=modified&fields=id,storage_path,correspondent,document_type,modified");
    AssertCapturedRequestContaining(
        handler,
        "/api/documents/?page=2&page_size=25&ordering=created",
        "&storage_path__id__in=1,3",
        "&correspondent__isnull=true",
        "&document_type__id=9",
        "&title__icontains=alpha%20beta");
    AssertCapturedRequest(
        handler,
        "/api/documents/7/thumb/");
    AssertCapturedRequest(
        handler,
        "/api/documents/7/preview/");
    AssertCapturedRequestContaining(
        handler,
        "/api/documents/7/?fields=",
        "id,title,correspondent,document_type,storage_path",
        "archive_serial_number,notes");
}

static Task TestThumbnailProxyAuthorizationAndCacheAsync()
{
    var programSource = ReadProjectSource(
        "WebUI.Web/Program.cs");
    var homeSource = ReadProjectSource(
        "WebUI.Web/Components/Pages/Home.razor");

    var thumbnailBlock = ExtractSourceBlock(
        programSource,
        "var previewEndpoint = app.MapGet(",
        "var documentPreviewEndpoint = app.MapGet(");

    Assert(
        thumbnailBlock.Contains(
            "\"/preview/documents/{documentId:int}/thumbnail\"",
            StringComparison.Ordinal),
        "Der geschützte Thumbnail-WebUI-Endpunkt fehlt oder wurde umbenannt.");
    Assert(
        thumbnailBlock.Contains(
            "if (documentId <= 0)",
            StringComparison.Ordinal),
        "Der Thumbnail-Endpunkt validiert die Dokument-ID nicht mehr.");
    Assert(
        thumbnailBlock.Contains(
            "clientFactory.CreateAsync(cancellationToken)",
            StringComparison.Ordinal) &&
        thumbnailBlock.Contains(
            "client.GetDocumentThumbnailAsync(",
            StringComparison.Ordinal),
        "Der Thumbnail-Endpunkt verwendet nicht mehr den persönlichen serverseitigen Paperless-Client.");
    Assert(
        thumbnailBlock.Contains(
            "\"private, no-store, max-age=0\"",
            StringComparison.Ordinal),
        "Der Thumbnail-Endpunkt besitzt nicht mehr die erwartete no-store-Cache-Grenze.");
    Assert(
        thumbnailBlock.Contains(
            "Results.File(",
            StringComparison.Ordinal) &&
        thumbnailBlock.Contains(
            "thumbnail.Content",
            StringComparison.Ordinal) &&
        thumbnailBlock.Contains(
            "thumbnail.ContentType",
            StringComparison.Ordinal),
        "Der Thumbnail-Endpunkt liefert nicht mehr ausschließlich Binärinhalt und Content-Type.");

    var authorizationBlock = ExtractSourceBlock(
        programSource,
        "if (oidcSettings.Enabled || localMultiUserEnabled)\n{\n    previewEndpoint.RequireAuthorization();",
        "var razorComponents = app.MapRazorComponents<App>()");

    Assert(
        authorizationBlock.Contains(
            "previewEndpoint.RequireAuthorization();",
            StringComparison.Ordinal),
        "Der Thumbnail-Endpunkt ist im Authentifizierungsbetrieb nicht mehr geschützt.");
    Assert(
        homeSource.Contains(
            "private static string GetThumbnailUrl(int documentId) =>\n        $\"/preview/documents/{documentId}/thumbnail\";",
            StringComparison.Ordinal),
        "Die normale Dokumentvorschau verwendet nicht mehr ausschließlich den internen geschützten Thumbnail-Endpunkt.");

    return Task.CompletedTask;
}

static Task TestPaperlessExternalLinkSecurityAsync()
{
    var homeSource = ReadProjectSource(
        "WebUI.Web/Components/Pages/Home.razor");

    var linkBlock = ExtractSourceBlock(
        homeSource,
        "<a class=\"paperless-edit-link\"",
        "</a>");

    Assert(
        linkBlock.Contains(
            "href=\"@GetPaperlessDocumentUrl(document.Id)\"",
            StringComparison.Ordinal),
        "Der externe Paperless-Link wird nicht mehr ausschließlich über den kontrollierten URL-Builder erzeugt.");
    Assert(
        linkBlock.Contains(
            "target=\"_blank\"",
            StringComparison.Ordinal),
        "Der externe Paperless-Link öffnet nicht mehr in einem getrennten Browser-Tab.");
    Assert(
        linkBlock.Contains(
            "rel=\"noopener noreferrer\"",
            StringComparison.Ordinal),
        "Der externe Paperless-Link besitzt nicht mehr die sichere noopener/noreferrer-Kopplung.");

    var urlBuilderBlock = ExtractSourceBlock(
        homeSource,
        "private string GetPaperlessDocumentUrl(int documentId)",
        "private string BuildCompletedNavigationStatus(");

    Assert(
        urlBuilderBlock.Contains(
            "return $\"{_paperlessBaseUrl}/documents/{documentId}/details\";",
            StringComparison.Ordinal),
        "Der externe Paperless-Link verwendet nicht mehr den erwarteten Dokumentdetailpfad.");
    Assert(
        !linkBlock.Contains(
            "token",
            StringComparison.OrdinalIgnoreCase) &&
        !urlBuilderBlock.Contains(
            "token",
            StringComparison.OrdinalIgnoreCase),
        "Der externe Paperless-Link enthält einen Tokenbezug.");

    var invalidUserInfoBaseUrl =
        "https://synthetic-user@paperless.example.test";

    ExpectPaperlessBaseUrlInvalid(
        new Dictionary<string, string?>
        {
            ["Paperless:BaseUrl"] = invalidUserInfoBaseUrl
        });
    ExpectPaperlessFactoryBaseUrlInvalid(
        new Dictionary<string, string?>
        {
            ["Paperless:BaseUrl"] = invalidUserInfoBaseUrl
        });

    return Task.CompletedTask;
}

static async Task ExercisePaperlessReadSurfaceAsync(
    PaperlessApiClient client)
{
    await client.ValidateCurrentTokenAsync();
    _ = await client.GetSystemStatusAsync();
    _ = await client.GetCorrespondentsAsync();
    _ = await client.GetDocumentTypesAsync();
    _ = await client.GetStoragePathsAsync();
    _ = await client.GetTagsAsync();
    _ = await client.GetCustomFieldsAsync();
    _ = await client.GetVisibleNavigationDocumentCountAsync();
    _ = await client.HasNavigationDocumentChangesAsync(
        new DateTimeOffset(
            2026,
            1,
            2,
            3,
            4,
            5,
            TimeSpan.Zero),
        knownDocumentsAtLatestTimestamp: 0);
    _ = await client.GetTrashDocumentIdsAsync();
    _ = await client.GetNavigationSourceStateAsync();
    _ = await client.GetNavigationDocumentsAsync();
    _ = await client.GetNavigationDocumentsModifiedSinceAsync(
        new DateTimeOffset(
            2026,
            1,
            2,
            3,
            4,
            5,
            TimeSpan.Zero));
    _ = await client.GetDocumentsAsync(
        page: 2,
        pageSize: 25,
        storagePathIds: new[] { 3, 1, 3 },
        correspondentIsNull: true,
        documentTypeId: 9,
        sortAscending: true,
        titleContains: "alpha beta");
    _ = await client.GetAllDocumentsAsync();
    _ = await client.GetDocumentThumbnailAsync(7);
    _ = await client.GetDocumentPreviewAsync(7);
    _ = await client.GetDocumentAsync(7);
}

static void AssertCapturedRequest(
    PaperlessReadCaptureHandler handler,
    string expectedPathAndQuery)
{
    Assert(
        handler.Requests.Any(request =>
            string.Equals(
                request.PathAndQuery,
                expectedPathAndQuery,
                StringComparison.Ordinal)),
        $"Erwarteter Paperless-GET-Vertrag fehlt: {expectedPathAndQuery}");
}

static void AssertCapturedRequestContaining(
    PaperlessReadCaptureHandler handler,
    string requiredStart,
    params string[] requiredParts)
{
    Assert(
        handler.Requests.Any(request =>
            request.PathAndQuery.StartsWith(
                requiredStart,
                StringComparison.Ordinal) &&
            requiredParts.All(part =>
                request.PathAndQuery.Contains(
                    part,
                    StringComparison.Ordinal))),
        $"Erwarteter Paperless-GET-Vertrag fehlt oder besitzt unerwartete Parameter: {requiredStart}");
}

static void ExpectPaperlessFactoryBaseUrlInvalid(
    Dictionary<string, string?> values)
{
    var threw = false;

    try
    {
        _ = ReadPaperlessFactoryBaseUrl(values);
    }
    catch (InvalidOperationException)
    {
        threw = true;
    }

    Assert(
        threw,
        "PaperlessClientFactory akzeptierte unerwartet eine Basisadresse mit eingebetteten Benutzerinformationen.");
}

static string ReadPaperlessFactoryBaseUrl(
    Dictionary<string, string?> values)
{
    var method = typeof(PaperlessClientFactory).GetMethod(
        "ReadAndValidateBaseUrl",
        BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException(
            "Die interne PaperlessClientFactory-Basisadressprüfung wurde nicht gefunden.");

    var readBaseUrl = method.CreateDelegate<
        Func<IConfiguration, string>>();

    return readBaseUrl(
        Configuration(values));
}

static async Task TestProtectedTokenStoreEncryptedAtomicPersistenceAsync()
{
    using var tokenDirectory = TemporaryDirectoryFixture.Create(
        "phase-a2-token-store");
    using var dataProtectionDirectory = TemporaryDirectoryFixture.Create(
        "phase-a2-data-protection");

    var technicalUserKey = new string('a', 64);
    var sessionId = Guid.NewGuid().ToString("N");
    const string token = "synthetic-phase-a2-token-value";

    var context = NewAuthenticatedOidcContext(
        technicalUserKey,
        sessionId);
    var accessor = new HttpContextAccessor
    {
        HttpContext = context
    };
    var provider = DataProtectionProvider.Create(
        dataProtectionDirectory.Path);
    var configuration = Configuration(
        new Dictionary<string, string?>
        {
            ["Paperless:BaseUrl"] =
                "https://paperless.example.test",
            ["Paperless:UserTokenDirectory"] =
                tokenDirectory.Path
        });

    var store = new ProtectedUserPaperlessTokenStore(
        accessor,
        new SyntheticPaperlessValidationHttpClientFactory(),
        provider,
        configuration);

    var prepared = await store.PrepareAsync(token);

    Assert(
        !string.Equals(
            prepared.ProtectedToken,
            token,
            StringComparison.Ordinal),
        "Das vorbereitete persönliche Paperless-Token liegt unerwartet im Klartext vor.");
    Assert(
        !prepared.ProtectedToken.Contains(
            token,
            StringComparison.Ordinal),
        "Der geschützte Tokenwert enthält unerwartet den Klartexttoken.");

    await store.SavePreparedAsync(prepared);

    var expectedPath = Path.Combine(
        tokenDirectory.Path,
        $"{technicalUserKey}.protected");

    Assert(
        File.Exists(expectedPath),
        "Die erwartete geschützte persönliche Tokendatei wurde nicht atomar angelegt.");
    Assert(
        Directory.GetFiles(
            tokenDirectory.Path,
            "*.tmp",
            SearchOption.TopDirectoryOnly).Length == 0,
        "Nach erfolgreicher Tokeneinrichtung blieb unerwartet eine temporäre Tokendatei zurück.");

    var storedValue = (
        await File.ReadAllTextAsync(expectedPath)).Trim();

    Assert(
        !storedValue.Contains(
            token,
            StringComparison.Ordinal),
        "Die gespeicherte persönliche Tokendatei enthält unerwartet den Klartexttoken.");

    var tokenProvider =
        new ProtectedUserPaperlessTokenProvider(
            accessor,
            provider,
            configuration);
    var tokenContext =
        await tokenProvider.GetTokenContextAsync();

    Assert(
        string.Equals(
            tokenContext.Token,
            token,
            StringComparison.Ordinal),
        "Das geschützte persönliche Token konnte nicht korrekt entschlüsselt werden.");
    Assert(
        string.Equals(
            tokenContext.CachePartition,
            technicalUserKey,
            StringComparison.Ordinal),
        "Die technische Benutzerkennung ging bei der geschützten Tokenablage verloren.");
    Assert(
        string.Equals(
            tokenContext.SessionId,
            sessionId,
            StringComparison.Ordinal),
        "Die technische Sitzungskennung ging beim Lesen der persönlichen Tokenzuordnung verloren.");

    if (!OperatingSystem.IsWindows())
    {
        var mode = File.GetUnixFileMode(expectedPath);
        Assert(
            mode ==
                (UnixFileMode.UserRead |
                 UnixFileMode.UserWrite),
            "Die persönliche Tokendatei besitzt nicht ausschließlich Benutzer-Lese-/Schreibrechte.");
    }
}

static async Task TestTechnicalUserKeyIsolationAsync()
{
    using var tokenDirectory = TemporaryDirectoryFixture.Create(
        "phase-a2-token-isolation");
    using var dataProtectionDirectory = TemporaryDirectoryFixture.Create(
        "phase-a2-token-isolation-data-protection");

    var userA = new string('a', 64);
    var userB = new string('b', 64);
    var sessionA = Guid.NewGuid().ToString("N");
    var sessionB = Guid.NewGuid().ToString("N");

    var accessor = new HttpContextAccessor();
    var provider = DataProtectionProvider.Create(
        dataProtectionDirectory.Path);
    var configuration = Configuration(
        new Dictionary<string, string?>
        {
            ["Paperless:BaseUrl"] =
                "https://paperless.example.test",
            ["Paperless:UserTokenDirectory"] =
                tokenDirectory.Path
        });
    var store = new ProtectedUserPaperlessTokenStore(
        accessor,
        new SyntheticPaperlessValidationHttpClientFactory(),
        provider,
        configuration);
    var tokenProvider =
        new ProtectedUserPaperlessTokenProvider(
            accessor,
            provider,
            configuration);

    accessor.HttpContext = NewAuthenticatedOidcContext(
        userA,
        sessionA);
    var preparedA = await store.PrepareAsync(
        "synthetic-token-a");
    await store.SavePreparedAsync(preparedA);

    accessor.HttpContext = NewAuthenticatedOidcContext(
        userB,
        sessionB);
    var preparedB = await store.PrepareAsync(
        "synthetic-token-b");
    await store.SavePreparedAsync(preparedB);

    Assert(
        File.Exists(Path.Combine(
            tokenDirectory.Path,
            $"{userA}.protected")) &&
        File.Exists(Path.Combine(
            tokenDirectory.Path,
            $"{userB}.protected")),
        "Die persönlichen Tokenzuordnungen wurden nicht in getrennten technischen Benutzerdateien gespeichert.");

    accessor.HttpContext = NewAuthenticatedOidcContext(
        userA,
        sessionA);
    var contextA =
        await tokenProvider.GetTokenContextAsync();

    accessor.HttpContext = NewAuthenticatedOidcContext(
        userB,
        sessionB);
    var contextB =
        await tokenProvider.GetTokenContextAsync();

    Assert(
        contextA.Token == "synthetic-token-a" &&
        contextA.CachePartition == userA,
        "Benutzer A erhielt nicht ausschließlich seine eigene persönliche Tokenzuordnung.");
    Assert(
        contextB.Token == "synthetic-token-b" &&
        contextB.CachePartition == userB,
        "Benutzer B erhielt nicht ausschließlich seine eigene persönliche Tokenzuordnung.");

    accessor.HttpContext = NewAuthenticatedOidcContext(
        "../invalid",
        sessionA);

    await ExpectInvalidOperationAsync(
        () => tokenProvider.GetTokenContextAsync(),
        "Eine ungültige technische Benutzerkennung wurde nicht abgelehnt.");

    accessor.HttpContext = NewAuthenticatedOidcContext(
        userA,
        "invalid-session");

    await ExpectInvalidOperationAsync(
        () => tokenProvider.GetTokenContextAsync(),
        "Eine ungültige technische Sitzungskennung wurde nicht abgelehnt.");

    accessor.HttpContext = NewContext();

    await ExpectInvalidOperationAsync(
        () => tokenProvider.GetTokenContextAsync(),
        "Ein nicht angemeldeter Zugriff auf persönliche Token wurde nicht abgelehnt.");
}

static Task TestPaperlessConnectionDoubleEntryAsync()
{
    var source = ReadProjectSource(
        "WebUI.Web/Components/Pages/PaperlessConnection.razor");

    var prepareBlock = ExtractSourceBlock(
        source,
        "private async Task PrepareAsync()",
        "private async Task SaveAsync()");
    var saveBlock = ExtractSourceBlock(
        source,
        "private async Task SaveAsync()",
        "private void LogFailure(");

    Assert(
        source.Contains(
            "id=\"paperless-token\"",
            StringComparison.Ordinal) &&
        source.Contains(
            "id=\"paperless-token-confirmation\"",
            StringComparison.Ordinal) &&
        source.Contains(
            "type=\"password\"",
            StringComparison.Ordinal),
        "Die Tokenänderungsseite besitzt nicht mehr zwei geschützte Eingabefelder.");

    Assert(
        prepareBlock.Contains(
            "StringComparison.Ordinal",
            StringComparison.Ordinal) &&
        prepareBlock.Contains(
            "ClearInput();",
            StringComparison.Ordinal) &&
        prepareBlock.Contains(
            "Die beiden Eingaben stimmen nicht überein.",
            StringComparison.Ordinal),
        "Die doppelte Tokeneingabe wird nicht mehr ordinal verglichen und bei Abweichung verworfen.");

    var mismatchPosition = prepareBlock.IndexOf(
        "Die beiden Eingaben stimmen nicht überein.",
        StringComparison.Ordinal);
    var preparePosition = prepareBlock.IndexOf(
        "TokenStore.PrepareAsync",
        StringComparison.Ordinal);

    Assert(
        mismatchPosition >= 0 &&
        preparePosition > mismatchPosition,
        "Die Paperless-Tokenprüfung kann vor Abschluss der Doppel-Eingabeprüfung erreicht werden.");

    Assert(
        saveBlock.Contains(
            "if (_preparedToken is null)",
            StringComparison.Ordinal) &&
        saveBlock.Contains(
            "TokenStore.SavePreparedAsync(_preparedToken)",
            StringComparison.Ordinal),
        "Vorbereiten und verbindliches Speichern des Tokens sind nicht mehr zwei getrennte Schritte.");

    Assert(
        source.Contains(
            "Es wird nicht in Browser-Speichern,\n            URLs, Protokollen oder im Navigationscache abgelegt.",
            StringComparison.Ordinal),
        "Der dokumentierte Secret-Schutzvertrag der Tokenänderungsseite fehlt.");

    return Task.CompletedTask;
}

static Task TestLocalKeychainReadContractAsync()
{
    var settingsSource = ReadProjectSource(
        "WebUI.Web/Services/LocalKeychainPaperlessSettings.cs");
    var providerSource = ReadProjectSource(
        "WebUI.Web/Services/LocalTestUserKeychainTokenProvider.cs");

    Assert(
        settingsSource.Contains(
            "\"/usr/bin/security\"",
            StringComparison.Ordinal) &&
        settingsSource.Contains(
            "\"find-generic-password\"",
            StringComparison.Ordinal),
        "Der lokale Keychain-Leseweg verwendet nicht mehr den dokumentierten macOS-security-Lesebefehl.");

    Assert(
        settingsSource.Contains(
            "TokenFingerprintService",
            StringComparison.Ordinal) &&
        settingsSource.Contains(
            "CryptographicOperations.FixedTimeEquals",
            StringComparison.Ordinal),
        "Die lokale Token-Zuordnung wird nicht mehr über getrennten Fingerprint und zeitkonstanten Vergleich abgesichert.");

    Assert(
        settingsSource.Contains(
            "ArgumentList.Add(service)",
            StringComparison.Ordinal) &&
        settingsSource.Contains(
            "ArgumentList.Add(account)",
            StringComparison.Ordinal),
        "Keychain-Service und -Account werden nicht mehr getrennt als Prozessargumente übergeben.");

    Assert(
        providerSource.Contains(
            "LocalTestUserSettings.AliasClaimType",
            StringComparison.Ordinal) &&
        providerSource.Contains(
            "LocalTestUserSettings.TechnicalUserKeyClaimType",
            StringComparison.Ordinal) &&
        providerSource.Contains(
            "LocalTestUserSettings.SessionIdClaimType",
            StringComparison.Ordinal) &&
        providerSource.Contains(
            "credential.TechnicalUserKey",
            StringComparison.Ordinal),
        "Der lokale Tokenprovider prüft Alias, technische Benutzerkennung und Sitzung nicht mehr vollständig gegen die Keychain-Zuordnung.");

    return Task.CompletedTask;
}

static Task TestAuthenticationHtmlPagesSafeDiagnosticsAsync()
{
    var validQuery = NewOidcDiagnosticQuery(
        ("event", "OIDC-123456789ABC"),
        ("category", "Tokenprüfung"),
        ("exception", "OpenIdConnectProtocolException"),
        ("code", "1"),
        ("state", "1"),
        ("remoteError", "0"),
        ("correlation", "2"),
        ("nonce", "1"),
        ("currentUser", "0"),
        ("localCookie", "KeinErgebnis"),
        ("localCookieException", "AuthenticationFailureException"),
        ("https", "1"),
        ("forwardedProto", "1"),
        ("forwardedHost", "1"),
        ("responseStarted", "0"),
        ("message", "<script>synthetic-free-error</script>"),
        ("token", "synthetic-token-must-never-appear"),
        ("authorizationCode", "synthetic-code-must-never-appear"),
        ("stateValue", "synthetic-state-must-never-appear"),
        ("cookie", "synthetic-cookie-must-never-appear"));

    var validHtml = CreateOidcErrorPageForTest(validQuery);

    Assert(
        validHtml.Contains(
            "<h2>Technische Diagnose</h2>",
            StringComparison.Ordinal),
        "Eine vollständig gültige OIDC-Diagnose wurde nicht angezeigt.");
    Assert(
        validHtml.Contains(
            "OIDC-123456789ABC",
            StringComparison.Ordinal) &&
        validHtml.Contains(
            "OpenIdConnectProtocolException",
            StringComparison.Ordinal) &&
        validHtml.Contains(
            "Autorisierungscode vorhanden</dt><dd>Ja",
            StringComparison.Ordinal) &&
        validHtml.Contains(
            "Korrelationscookies</dt><dd>2",
            StringComparison.Ordinal),
        "Die gültige OIDC-Diagnose enthält nicht die erwarteten allowlist-geprüften technischen Angaben.");
    Assert(
        !validHtml.Contains(
            "synthetic-free-error",
            StringComparison.Ordinal) &&
        !validHtml.Contains(
            "synthetic-token-must-never-appear",
            StringComparison.Ordinal) &&
        !validHtml.Contains(
            "synthetic-code-must-never-appear",
            StringComparison.Ordinal) &&
        !validHtml.Contains(
            "synthetic-state-must-never-appear",
            StringComparison.Ordinal) &&
        !validHtml.Contains(
            "synthetic-cookie-must-never-appear",
            StringComparison.Ordinal),
        "Die OIDC-Fehlerseite gab freie Fehler-, Token-, Code-, State- oder Cookieinhalte aus.");

    var encodedCategory = System.Net.WebUtility.HtmlEncode(
        "Tokenprüfung");
    Assert(
        validHtml.Contains(
            encodedCategory,
            StringComparison.Ordinal),
        "Die allowlist-geprüfte OIDC-Kategorie wurde nicht HTML-encoded ausgegeben.");

    foreach (var invalidQuery in new[]
    {
        NewOidcDiagnosticQuery(
            ("event", "OIDC-123456789ABC"),
            ("category", "NichtErlaubt"),
            ("exception", "OpenIdConnectProtocolException")),
        NewOidcDiagnosticQuery(
            ("event", "OIDC-123456789ABC"),
            ("category", "Tokenprüfung"),
            ("exception", "Unsafe<script>")),
        NewOidcDiagnosticQuery(
            ("event", "OIDC-123456789ABC"),
            ("category", "Tokenprüfung"),
            ("exception", "OpenIdConnectProtocolException"),
            ("correlation", "21")),
        NewOidcDiagnosticQuery(
            ("event", "OIDC-123456789ABC"),
            ("category", "Tokenprüfung"),
            ("exception", "OpenIdConnectProtocolException"),
            ("code", "true"))
    })
    {
        var invalidHtml =
            CreateOidcErrorPageForTest(invalidQuery);

        Assert(
            !invalidHtml.Contains(
                "<h2>Technische Diagnose</h2>",
                StringComparison.Ordinal),
            "Manipulierte oder nicht allowlist-konforme OIDC-Diagnosewerte wurden unerwartet angezeigt.");
    }

    var multiValueQuery = NewOidcDiagnosticQuery(
        ("event", "OIDC-123456789ABC"),
        ("category", "Tokenprüfung"),
        ("exception", "OpenIdConnectProtocolException"));
    var multiValueValues =
        multiValueQuery.ToDictionary(
            item => item.Key,
            item => item.Value,
            StringComparer.OrdinalIgnoreCase);
    multiValueValues["code"] =
        new StringValues(new[] { "1", "0" });

    var multiValueHtml =
        CreateOidcErrorPageForTest(
            new QueryCollection(multiValueValues));

    Assert(
        !multiValueHtml.Contains(
            "<h2>Technische Diagnose</h2>",
            StringComparison.Ordinal),
        "Mehrdeutige OIDC-Diagnoseflags wurden unerwartet akzeptiert.");

    return Task.CompletedTask;
}

static IQueryCollection NewOidcDiagnosticQuery(
    params (string Key, string Value)[] overrides)
{
    var values = new Dictionary<string, StringValues>(
        StringComparer.OrdinalIgnoreCase)
    {
        ["event"] = "OIDC-123456789ABC",
        ["category"] = "Tokenprüfung",
        ["exception"] = "OpenIdConnectProtocolException",
        ["code"] = "0",
        ["state"] = "0",
        ["remoteError"] = "0",
        ["correlation"] = "0",
        ["nonce"] = "0",
        ["currentUser"] = "0",
        ["localCookie"] = "KeinErgebnis",
        ["localCookieException"] = "AuthenticationFailureException",
        ["https"] = "1",
        ["forwardedProto"] = "0",
        ["forwardedHost"] = "0",
        ["responseStarted"] = "0"
    };

    foreach (var (key, value) in overrides)
    {
        values[key] = value;
    }

    return new QueryCollection(values);
}

static string CreateOidcErrorPageForTest(
    IQueryCollection query)
{
    var pageType =
        typeof(PaperlessClientFactory).Assembly.GetType(
            "WebUI.Web.Services.AuthenticationHtmlPages",
            throwOnError: true)
        ?? throw new InvalidOperationException(
            "AuthenticationHtmlPages wurde nicht gefunden.");

    var method = pageType.GetMethod(
        "CreateOidcErrorPage",
        BindingFlags.Static | BindingFlags.Public)
        ?? throw new InvalidOperationException(
            "AuthenticationHtmlPages.CreateOidcErrorPage wurde nicht gefunden.");

    try
    {
        return method.Invoke(
                null,
                new object[] { query }) as string
            ?? throw new InvalidOperationException(
                "CreateOidcErrorPage lieferte keinen HTML-Text.");
    }
    catch (TargetInvocationException exception)
        when (exception.InnerException is not null)
    {
        throw exception.InnerException;
    }
}

static Task TestDataProtectionPersistencePathValidationAsync()
{
    var source = ReadProjectSource(
        "WebUI.Web/Program.cs");

    var block = ExtractSourceBlock(
        source,
        "var dataProtectionKeysDirectory =",
        "builder.Services\n    .AddHttpClient(PaperlessClientFactory.HttpClientName)");

    Assert(
        block.Contains(
            "WebUi:DataProtectionKeysDirectory",
            StringComparison.Ordinal) &&
        block.Contains(
            "!Path.IsPathFullyQualified(normalizedDirectory)",
            StringComparison.Ordinal) &&
        block.Contains(
            "Path.GetFullPath(normalizedDirectory)",
            StringComparison.Ordinal) &&
        block.Contains(
            "Directory.CreateDirectory(fullDirectory)",
            StringComparison.Ordinal),
        "Der persistente Data-Protection-Schlüsselpfad wird nicht mehr als absoluter serverseitiger Pfad validiert.");

    Assert(
        block.Contains(
            ".AddDataProtection()",
            StringComparison.Ordinal) &&
        block.Contains(
            ".PersistKeysToFileSystem(",
            StringComparison.Ordinal) &&
        block.Contains(
            ".SetApplicationName(\n            \"webui\")",
            StringComparison.Ordinal),
        "Data-Protection-Schlüssel werden nicht mehr persistent und an den dokumentierten ApplicationName gebunden.");

    return Task.CompletedTask;
}

static Task TestDataProtectionCertificateSecretContractAsync()
{
    var source = ReadProjectSource(
        "WebUI.Web/Program.cs");

    var block = ExtractSourceBlock(
        source,
        "var certificatePath =",
        "builder.Services\n    .AddHttpClient(PaperlessClientFactory.HttpClientName)");

    Assert(
        block.Contains(
            "WebUi:DataProtectionCertificatePath",
            StringComparison.Ordinal) &&
        block.Contains(
            "!Path.IsPathFullyQualified(normalizedCertificatePath)",
            StringComparison.Ordinal),
        "Der Data-Protection-Zertifikatspfad wird nicht mehr als absoluter Pfad erzwungen.");

    Assert(
        block.Contains(
            "WebUi:DataProtectionCertificatePasswordFilePath",
            StringComparison.Ordinal) &&
        block.Contains(
            "!Path.IsPathFullyQualified(normalizedPasswordPath)",
            StringComparison.Ordinal) &&
        block.Contains(
            "File.ReadAllText(normalizedPasswordPath).TrimEnd()",
            StringComparison.Ordinal),
        "Das optionale Zertifikatskennwort wird nicht mehr ausschließlich aus einem absoluten Dateipfad gelesen.");

    Assert(
        block.Contains(
            "X509KeyStorageFlags.EphemeralKeySet",
            StringComparison.Ordinal) &&
        block.Contains(
            "if (!certificate.HasPrivateKey)",
            StringComparison.Ordinal) &&
        block.Contains(
            "ProtectKeysWithCertificate(certificate)",
            StringComparison.Ordinal),
        "Der Data-Protection-Zertifikatsschutz besitzt nicht mehr die erwartete Private-Key-/Ephemeral-KeySet-Grenze.");

    Assert(
        !source.Contains(
            "WebUi:DataProtectionCertificatePassword\"]",
            StringComparison.Ordinal),
        "Ein direktes Konfigurationsfeld für das Data-Protection-Zertifikatskennwort wurde unerwartet eingeführt.");

    return Task.CompletedTask;
}

static Task TestLocalTestModeLoopbackSecurityAsync()
{
    Assert(
        LocalTestModeSecurity.GetRequiredLoopbackUrl(
            Configuration(
                new Dictionary<string, string?>
                {
                    ["LocalMultiUser:LoopbackUrl"] =
                        "http://127.0.0.1:57123"
                })) == "http://127.0.0.1:57123",
        "IPv4-Loopbackadresse wurde unerwartet abgelehnt.");

    Assert(
        LocalTestModeSecurity.GetRequiredLoopbackUrl(
            Configuration(
                new Dictionary<string, string?>
                {
                    ["LocalMultiUser:LoopbackUrl"] =
                        "http://localhost:57123"
                })) == "http://localhost:57123",
        "localhost-Loopbackadresse wurde unerwartet abgelehnt.");

    Assert(
        LocalTestModeSecurity.GetRequiredLoopbackUrl(
            Configuration(
                new Dictionary<string, string?>
                {
                    ["LocalMultiUser:LoopbackUrl"] =
                        "http://[::1]:57123"
                })) == "http://[::1]:57123",
        "IPv6-Loopbackadresse wurde unerwartet abgelehnt.");

    foreach (var rejected in new[]
    {
        "http://0.0.0.0:57123",
        "http://192.0.2.10:57123",
        "http://example.test:57123",
        "http://synthetic-user@127.0.0.1:57123",
        "http://127.0.0.1:57123/?x=1",
        "http://127.0.0.1:57123/#fragment",
        "http://127.0.0.1:57123/sub",
        "http://127.0.0.1:57123;http://localhost:57123"
    })
    {
        ExpectLocalLoopbackUrlInvalid(rejected);
    }

    var accepted = NewContext();
    accepted.Connection.RemoteIpAddress =
        IPAddress.Loopback;
    accepted.Connection.LocalIpAddress =
        IPAddress.Loopback;
    accepted.Request.Host =
        new HostString("localhost", 57123);

    Assert(
        LocalTestModeSecurity.IsLoopbackRequest(accepted),
        "Vollständig lokale Loopback-Anfrage wurde unerwartet abgelehnt.");

    var externalRemote = NewContext();
    externalRemote.Connection.RemoteIpAddress =
        IPAddress.Parse("192.0.2.10");
    externalRemote.Connection.LocalIpAddress =
        IPAddress.Loopback;
    externalRemote.Request.Host =
        new HostString("localhost", 57123);

    Assert(
        !LocalTestModeSecurity.IsLoopbackRequest(externalRemote),
        "Nicht-lokale Remote-IP wurde vom lokalen Mehrbenutzertest akzeptiert.");

    var externalLocal = NewContext();
    externalLocal.Connection.RemoteIpAddress =
        IPAddress.Loopback;
    externalLocal.Connection.LocalIpAddress =
        IPAddress.Parse("192.0.2.20");
    externalLocal.Request.Host =
        new HostString("localhost", 57123);

    Assert(
        !LocalTestModeSecurity.IsLoopbackRequest(externalLocal),
        "Nicht-lokale Serveradresse wurde vom lokalen Mehrbenutzertest akzeptiert.");

    var externalHost = NewContext();
    externalHost.Connection.RemoteIpAddress =
        IPAddress.Loopback;
    externalHost.Connection.LocalIpAddress =
        IPAddress.Loopback;
    externalHost.Request.Host =
        new HostString("example.test", 57123);

    Assert(
        !LocalTestModeSecurity.IsLoopbackRequest(externalHost),
        "Nicht-lokaler Hostheader wurde vom lokalen Mehrbenutzertest akzeptiert.");

    return Task.CompletedTask;
}

static DefaultHttpContext NewAuthenticatedOidcContext(
    string technicalUserKey,
    string sessionId)
{
    var context = NewContext();
    context.User = new System.Security.Claims.ClaimsPrincipal(
        new System.Security.Claims.ClaimsIdentity(
            new[]
            {
                new System.Security.Claims.Claim(
                    OidcAuthenticationSettings.TechnicalUserKeyClaimType,
                    technicalUserKey),
                new System.Security.Claims.Claim(
                    OidcAuthenticationSettings.SessionIdClaimType,
                    sessionId)
            },
            authenticationType: "synthetic-phase-a2"));
    return context;
}

static async Task ExpectInvalidOperationAsync(
    Func<Task> action,
    string failureMessage)
{
    var threw = false;

    try
    {
        await action();
    }
    catch (InvalidOperationException)
    {
        threw = true;
    }

    Assert(
        threw,
        failureMessage);
}

static void ExpectLocalLoopbackUrlInvalid(
    string value)
{
    var threw = false;

    try
    {
        _ = LocalTestModeSecurity.GetRequiredLoopbackUrl(
            Configuration(
                new Dictionary<string, string?>
                {
                    ["LocalMultiUser:LoopbackUrl"] = value
                }));
    }
    catch (InvalidOperationException)
    {
        threw = true;
    }

    Assert(
        threw,
        $"Unerlaubte lokale Testadresse wurde akzeptiert: {value}");
}

static Task TestOidcSettingsSecurityContractAsync()
{
    using var fixture = TemporaryDirectoryFixture.Create(
        "phase-a2-oidc-settings");
    var secretPath = Path.Combine(
        fixture.Path,
        "oidc-client-secret");
    File.WriteAllText(
        secretPath,
        "synthetic-oidc-client-secret");

    OidcAuthenticationSettings Load(
        string authority,
        string metadataAddress) =>
        OidcAuthenticationSettings.Load(
            Configuration(
                new Dictionary<string, string?>
                {
                    ["Authentication:Oidc:Enabled"] = "true",
                    ["Authentication:Oidc:Authority"] = authority,
                    ["Authentication:Oidc:MetadataAddress"] =
                        metadataAddress,
                    ["Authentication:Oidc:ClientId"] =
                        "synthetic-client",
                    ["Authentication:Oidc:ClientSecretFilePath"] =
                        secretPath
                }));

    var valid = Load(
        "https://oidc.example.test/",
        "https://oidc.example.test/.well-known/openid-configuration");

    Assert(
        valid.Enabled &&
        valid.Authority == "https://oidc.example.test" &&
        valid.MetadataAddress ==
            "https://oidc.example.test/.well-known/openid-configuration" &&
        valid.ClientId == "synthetic-client" &&
        valid.ClientSecret == "synthetic-oidc-client-secret",
        "Gültige OIDC-Einstellungen werden nicht mit dem erwarteten HTTPS-/File-only-Secret-Vertrag geladen.");

    foreach (var rejectedAuthority in new[]
    {
        "http://oidc.example.test",
        "https://synthetic-user@oidc.example.test",
        "https://oidc.example.test/?x=1",
        "https://oidc.example.test/#fragment"
    })
    {
        ExpectOidcSettingsInvalid(
            () => Load(
                rejectedAuthority,
                "https://oidc.example.test/.well-known/openid-configuration"),
            "Unzulässige OIDC-Authority wurde akzeptiert.");
    }

    foreach (var rejectedMetadata in new[]
    {
        "http://oidc.example.test/.well-known/openid-configuration",
        "https://synthetic-user@oidc.example.test/.well-known/openid-configuration",
        "https://oidc.example.test/.well-known/openid-configuration?x=1",
        "https://oidc.example.test/.well-known/openid-configuration#fragment"
    })
    {
        ExpectOidcSettingsInvalid(
            () => Load(
                "https://oidc.example.test",
                rejectedMetadata),
            "Unzulässige OIDC-Metadatenadresse wurde akzeptiert.");
    }

    ExpectOidcSettingsInvalid(
        () => OidcAuthenticationSettings.Load(
            Configuration(
                new Dictionary<string, string?>
                {
                    ["Authentication:Oidc:Enabled"] = "true",
                    ["Authentication:Oidc:Authority"] =
                        "https://oidc.example.test",
                    ["Authentication:Oidc:MetadataAddress"] =
                        "https://oidc.example.test/.well-known/openid-configuration",
                    ["Authentication:Oidc:ClientId"] =
                        "synthetic-client",
                    ["Authentication:Oidc:ClientSecret"] =
                        "synthetic-direct-secret",
                    ["Authentication:Oidc:ClientSecretFilePath"] =
                        secretPath
                })),
        "Direkter OIDC-Client-Secretwert wurde akzeptiert.");

    return Task.CompletedTask;
}

static Task TestOidcSessionCookieSecurityAsync()
{
    var source = ReadProjectSource(
        "WebUI.Web/Program.cs");

    var cookieBlock = ExtractSourceBlock(
        source,
        ".AddCookie(options =>",
        ".AddScheme<");

    Assert(
        cookieBlock.Contains(
            "options.Cookie.Name =\n                \"__Host-webui\";",
            StringComparison.Ordinal) &&
        cookieBlock.Contains(
            "options.Cookie.HttpOnly = true;",
            StringComparison.Ordinal) &&
        cookieBlock.Contains(
            "CookieSecurePolicy.Always",
            StringComparison.Ordinal) &&
        cookieBlock.Contains(
            "SameSiteMode.Lax",
            StringComparison.Ordinal) &&
        cookieBlock.Contains(
            "options.Cookie.Path = \"/\";",
            StringComparison.Ordinal),
        "Das OIDC-Sitzungscookie erfüllt nicht mehr den dokumentierten __Host-/HttpOnly-/Secure-/SameSite-/Path-Vertrag.");

    Assert(
        cookieBlock.Contains(
            "TimeSpan.FromHours(8)",
            StringComparison.Ordinal) &&
        cookieBlock.Contains(
            "options.SlidingExpiration = true;",
            StringComparison.Ordinal),
        "Ablaufzeit oder Sliding-Expiration des OIDC-Sitzungscookies wurden unerwartet verändert.");

    return Task.CompletedTask;
}

static Task TestOidcAuthStatusContractAsync()
{
    var source = ReadProjectSource(
        "WebUI.Web/Program.cs");

    var block = ExtractSourceBlock(
        source,
        "app.MapGet(\n        \"/auth/status\",",
        "app.MapPost(\n        \"/auth/logout\",");

    Assert(
        block.Contains(
            "authenticated =\n                    user.Identity?.IsAuthenticated == true",
            StringComparison.Ordinal) &&
        block.Contains(
            "technicalUserKeyPresent =",
            StringComparison.Ordinal) &&
        block.Contains(
            "OidcAuthenticationSettings.TechnicalUserKeyClaimType",
            StringComparison.Ordinal),
        "Der OIDC-Auth-Status liefert nicht mehr ausschließlich Authentifizierungsstatus und Vorhandensein der technischen Benutzerkennung.");

    Assert(
        block.Contains(
            ".RequireAuthorization();",
            StringComparison.Ordinal) &&
        !block.Contains(
            "AllowAnonymous",
            StringComparison.Ordinal) &&
        !block.Contains(
            "ClientSecret",
            StringComparison.Ordinal) &&
        !block.Contains(
            "SessionIdClaimType",
            StringComparison.Ordinal),
        "Der OIDC-Auth-Status ist nicht mehr autorisierungspflichtig oder gibt zusätzliche sensible Sitzungs-/Secretinformationen preis.");

    return Task.CompletedTask;
}

static Task TestOidcCallbackClaimsAndLeaseReleaseAsync()
{
    var source = ReadProjectSource(
        "WebUI.Web/Program.cs");

    var tokenValidatedBlock = ExtractSourceBlock(
        source,
        "options.Events.OnTokenValidated = context =>",
        "options.Events.OnRemoteFailure = async context =>");
    var remoteFailureBlock = ExtractSourceBlock(
        source,
        "options.Events.OnRemoteFailure = async context =>",
        "});\n\n    builder.Services.AddAuthorization();");

    Assert(
        tokenValidatedBlock.Contains(
            "challengeGuard.Release(",
            StringComparison.Ordinal) &&
        tokenValidatedBlock.Contains(
            "Grund: TokenValidated",
            StringComparison.Ordinal) &&
        remoteFailureBlock.Contains(
            "challengeGuard.Release(",
            StringComparison.Ordinal) &&
        remoteFailureBlock.Contains(
            "Grund: RemoteFailure",
            StringComparison.Ordinal),
        "Die OIDC-Challenge-Lease wird nach erfolgreichem Callback oder RemoteFailure nicht mehr sicher freigegeben.");

    Assert(
        tokenValidatedBlock.Contains(
            "context.SecurityToken?.Issuer",
            StringComparison.Ordinal) &&
        tokenValidatedBlock.Contains(
            "context.Principal.FindFirst(\"sub\")?.Value",
            StringComparison.Ordinal) &&
        tokenValidatedBlock.Contains(
            "CreateTechnicalUserKey(issuer, subject)",
            StringComparison.Ordinal) &&
        tokenValidatedBlock.Contains(
            "OidcAuthenticationSettings.TechnicalUserKeyClaimType",
            StringComparison.Ordinal),
        "Die technische OIDC-Benutzerkennung wird nicht mehr ausschließlich aus Issuer und Subject abgeleitet und als technischer Claim gesetzt.");

    Assert(
        tokenValidatedBlock.Contains(
            "OidcAuthenticationSettings.SessionIdClaimType",
            StringComparison.Ordinal) &&
        tokenValidatedBlock.Contains(
            "Guid.NewGuid().ToString(\"N\")",
            StringComparison.Ordinal),
        "Der erfolgreiche OIDC-Callback erzeugt keine neue serverseitige Sitzungskennung im N-Format.");

    Assert(
        tokenValidatedBlock.IndexOf(
            "challengeGuard.Release(",
            StringComparison.Ordinal) <
        tokenValidatedBlock.IndexOf(
            "CreateTechnicalUserKey(issuer, subject)",
            StringComparison.Ordinal),
        "Die Challenge-Lease wird beim erfolgreichen Callback nicht vor der weiteren Claim-Verarbeitung freigegeben.");

    return Task.CompletedTask;
}

static void ExpectOidcSettingsInvalid(
    Action action,
    string failureMessage)
{
    var threw = false;

    try
    {
        action();
    }
    catch (InvalidOperationException)
    {
        threw = true;
    }

    Assert(
        threw,
        failureMessage);
}

static async Task TestNavigationCacheSaveLoadAndVersionAsync()
{
    using var fixture = TemporaryDirectoryFixture.Create(
        "phase-a3-nav-save-load");

    var configuration = Configuration(
        new Dictionary<string, string?>
        {
            ["WebUi:NavigationCacheDirectory"] = fixture.Path
        });

    var service = new NavigationCacheService(configuration);
    const string cacheKey = "1111111111111111";

    var modifiedUtc = new DateTimeOffset(
        2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    var snapshot = NavigationCacheSnapshot.Create(
        new[]
        {
            new NavigationDocumentDto
            {
                Id = 1,
                StoragePathId = 10,
                CorrespondentId = 20,
                DocumentTypeId = 30,
                ModifiedUtc = modifiedUtc
            },
            new NavigationDocumentDto
            {
                Id = 2,
                StoragePathId = null,
                CorrespondentId = null,
                DocumentTypeId = 31,
                ModifiedUtc = modifiedUtc.AddMinutes(1)
            }
        });

    Assert(
        snapshot.FormatVersion == 3,
        "Neu erzeugte Navigationscache-Snapshots verwenden nicht Formatversion 3.");

    await service.SaveAsync(
        cacheKey,
        snapshot);

    var persistedPath = Path.Combine(
        fixture.Path,
        $"navigation-{cacheKey}.json");

    Assert(
        File.Exists(persistedPath),
        "Der Navigationscache wurde nicht unter dem erwarteten partitionierten Dateinamen gespeichert.");

    var freshService =
        new NavigationCacheService(configuration);
    var loaded =
        await freshService.LoadAsync(cacheKey);

    Assert(
        loaded is not null &&
        loaded.FormatVersion == 3 &&
        loaded.Documents.Count == 2 &&
        loaded.Documents.Select(document => document.Id).SequenceEqual(new[] { 1, 2 }),
        "Ein gültiger Formatversion-3-Navigationscache wird nicht vollständig aus der Datei geladen.");

    var serialized =
        await File.ReadAllTextAsync(persistedPath);

    var incompatible = serialized.Replace(
        "\"formatVersion\":3",
        "\"formatVersion\":2",
        StringComparison.Ordinal);

    Assert(
        !ReferenceEquals(serialized, incompatible) &&
        incompatible.Contains(
            "\"formatVersion\":2",
            StringComparison.Ordinal),
        "Die synthetische inkompatible Cacheversion konnte nicht erzeugt werden.");

    await File.WriteAllTextAsync(
        persistedPath,
        incompatible);

    var incompatibleService =
        new NavigationCacheService(configuration);

    Assert(
        await incompatibleService.LoadAsync(cacheKey) is null,
        "Eine inkompatible Navigationscache-Formatversion wird nicht kontrolliert verworfen.");
}

static async Task TestNavigationCachePartitionAndMatchesAsync()
{
    using var fixture = TemporaryDirectoryFixture.Create(
        "phase-a3-nav-partition");

    var configuration = Configuration(
        new Dictionary<string, string?>
        {
            ["WebUi:NavigationCacheDirectory"] = fixture.Path
        });

    var service =
        new NavigationCacheService(configuration);

    const string cacheKeyA = "aaaaaaaaaaaaaaaa";
    const string cacheKeyB = "bbbbbbbbbbbbbbbb";

    var latest = new DateTimeOffset(
        2026, 8, 30, 13, 0, 0, TimeSpan.Zero);

    var snapshot = NavigationCacheSnapshot.Create(
        new[]
        {
            new NavigationDocumentDto
            {
                Id = 11,
                ModifiedUtc = latest.AddMinutes(-1)
            },
            new NavigationDocumentDto
            {
                Id = 12,
                ModifiedUtc = latest
            }
        });

    await service.SaveAsync(
        cacheKeyA,
        snapshot);
    await service.SaveAsync(
        cacheKeyB,
        snapshot);

    Assert(
        File.Exists(Path.Combine(
            fixture.Path,
            $"navigation-{cacheKeyA}.json")) &&
        File.Exists(Path.Combine(
            fixture.Path,
            $"navigation-{cacheKeyB}.json")),
        "Unterschiedliche Cachekeys werden nicht in getrennte Navigationscache-Dateien partitioniert.");

    Assert(
        snapshot.Matches(
            new Dictionary<int, NavigationAreaStateDto>
            {
                [-1] = new(-1, 2, latest)
            }),
        "Matches() lehnt identische Dokumentzahl/LatestModifiedUtc unerwartet ab.");

    Assert(
        !snapshot.Matches(
            new Dictionary<int, NavigationAreaStateDto>
            {
                [-1] = new(-1, 3, latest)
            }),
        "Matches() erkennt eine geänderte Dokumentzahl nicht.");

    Assert(
        !snapshot.Matches(
            new Dictionary<int, NavigationAreaStateDto>
            {
                [-1] = new(-1, 2, latest.AddTicks(1))
            }),
        "Matches() erkennt ein geändertes LatestModifiedUtc nicht.");

    Assert(
        !snapshot.Matches(
            new Dictionary<int, NavigationAreaStateDto>()),
        "Matches() erkennt eine abweichende SourceStates-Menge nicht.");
}

static Task TestNavigationCacheApplyChangesAsync()
{
    var baseTimestamp = new DateTimeOffset(
        2026, 8, 30, 14, 0, 0, TimeSpan.Zero);

    var original = NavigationCacheSnapshot.Create(
        new[]
        {
            new NavigationDocumentDto
            {
                Id = 1,
                StoragePathId = 10,
                CorrespondentId = 20,
                DocumentTypeId = 30,
                ModifiedUtc = baseTimestamp
            },
            new NavigationDocumentDto
            {
                Id = 2,
                StoragePathId = 11,
                CorrespondentId = 21,
                DocumentTypeId = 31,
                ModifiedUtc = baseTimestamp.AddMinutes(1)
            }
        });

    var replacementTimestamp =
        baseTimestamp.AddMinutes(5);
    var addedTimestamp =
        baseTimestamp.AddMinutes(6);

    var changed = original.ApplyChanges(
        new[]
        {
            new NavigationDocumentDto
            {
                Id = 2,
                StoragePathId = 12,
                CorrespondentId = 22,
                DocumentTypeId = 32,
                ModifiedUtc = replacementTimestamp
            },
            new NavigationDocumentDto
            {
                Id = 3,
                StoragePathId = null,
                CorrespondentId = null,
                DocumentTypeId = null,
                ModifiedUtc = addedTimestamp
            }
        });

    Assert(
        changed.Documents.Select(document => document.Id)
            .SequenceEqual(new[] { 1, 2, 3 }),
        "ApplyChanges() hält die Dokumente nicht eindeutig und nach ID sortiert.");

    var replaced = changed.Documents.Single(
        document => document.Id == 2);

    Assert(
        replaced.StoragePathId == 12 &&
        replaced.CorrespondentId == 22 &&
        replaced.DocumentTypeId == 32 &&
        replaced.ModifiedUtc == replacementTimestamp,
        "ApplyChanges() ersetzt ein bereits vorhandenes Dokument nicht vollständig.");

    Assert(
        changed.SourceStates.TryGetValue(
            -1,
            out var state) &&
        state.DocumentCount == 3 &&
        state.LatestModifiedUtc == addedTimestamp,
        "ApplyChanges() berechnet SourceStates nach Ersetzen/Ergänzen nicht neu.");

    return Task.CompletedTask;
}

static async Task TestNavigationCacheCorruptFallbackAsync()
{
    using var fixture = TemporaryDirectoryFixture.Create(
        "phase-a3-nav-corrupt");

    var configuration = Configuration(
        new Dictionary<string, string?>
        {
            ["WebUi:NavigationCacheDirectory"] = fixture.Path
        });

    var service =
        new NavigationCacheService(configuration);
    const string cacheKey = "cccccccccccccccc";

    var path = Path.Combine(
        fixture.Path,
        $"navigation-{cacheKey}.json");

    await File.WriteAllTextAsync(
        path,
        "{ this is not valid json");

    var loaded =
        await service.LoadAsync(cacheKey);

    Assert(
        loaded is null,
        "Ein beschädigter Navigationscache verursacht keinen kontrollierten null-Fallback.");
}

static async Task TestCentralNavigationSyncIncrementalAndManualAsync()
{
    using var fixture = TemporaryDirectoryFixture.Create(
        "phase-a3-central-sync");
    using var loggerFactory =
        LoggerFactory.Create(_ => { });

    var cache = new NavigationCacheService(
        Configuration(
            new Dictionary<string, string?>
            {
                ["WebUi:NavigationCacheDirectory"] =
                    fixture.Path
            }));

    const string cacheKey = "dddddddddddddddd";
    var snapshot = NavigationCacheSnapshot.Create(
        new[]
        {
            new NavigationDocumentDto
            {
                Id = 101,
                ModifiedUtc = new DateTimeOffset(
                    2026, 8, 30, 15, 0, 0, TimeSpan.Zero)
            }
        });

    await cache.SaveAsync(
        cacheKey,
        snapshot);

    await using var sync =
        new CentralNavigationSyncService(
            cache,
            loggerFactory.CreateLogger<CentralNavigationSyncService>());

    var unavailableCheckCalls = 0;
    var availableCheckCalls = 0;
    var incrementalCompleted =
        new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
    var fullCompleted =
        new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

    using var unavailableRegistration =
        sync.Register(
            cacheKey,
            () => false,
            (cached, progress) =>
            {
                Interlocked.Increment(
                    ref unavailableCheckCalls);
                return Task.FromResult(
                    new NavigationRefreshResult(
                        cached,
                        false,
                        DateTimeOffset.UtcNow));
            },
            _ => Task.CompletedTask,
            (_, _) => Task.CompletedTask,
            _ => Task.CompletedTask);

    using var availableRegistration =
        sync.Register(
            cacheKey,
            () => true,
            (cached, progress) =>
            {
                Interlocked.Increment(
                    ref availableCheckCalls);
                return Task.FromResult(
                    new NavigationRefreshResult(
                        cached,
                        false,
                        DateTimeOffset.UtcNow));
            },
            _ => Task.CompletedTask,
            (_, isFullRefresh) =>
            {
                if (isFullRefresh)
                    fullCompleted.TrySetResult(true);
                else
                    incrementalCompleted.TrySetResult(true);

                return Task.CompletedTask;
            },
            _ => Task.CompletedTask);

    sync.RequestImmediateCheck(cacheKey);

    await incrementalCompleted.Task.WaitAsync(
        TimeSpan.FromSeconds(3));

    Assert(
        unavailableCheckCalls == 0 &&
        availableCheckCalls == 1,
        "Die unmittelbare zentrale Navigationsprüfung verwendet nicht ausschließlich einen verfügbaren Subscriber.");

    var fullRefreshEntered =
        new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
    var releaseFullRefresh =
        new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
    var fullRefreshCalls = 0;
    var duplicateRefreshCalls = 0;

    var firstRefresh = sync.RequestFullRefreshAsync(
        cacheKey,
        () => true,
        async progress =>
        {
            Interlocked.Increment(
                ref fullRefreshCalls);
            fullRefreshEntered.TrySetResult(true);

            await releaseFullRefresh.Task;

            return new NavigationRefreshResult(
                snapshot,
                true,
                DateTimeOffset.UtcNow);
        });

    await fullRefreshEntered.Task.WaitAsync(
        TimeSpan.FromSeconds(3));

    var duplicateRefresh = sync.RequestFullRefreshAsync(
        cacheKey,
        () => true,
        progress =>
        {
            Interlocked.Increment(
                ref duplicateRefreshCalls);
            return Task.FromResult(
                new NavigationRefreshResult(
                    snapshot,
                    true,
                    DateTimeOffset.UtcNow));
        });

    Assert(
        ReferenceEquals(
            firstRefresh,
            duplicateRefresh),
        "Parallele manuelle Full-Refresh-Aufrufe werden nicht auf dieselbe laufende Operation dedupliziert.");

    releaseFullRefresh.TrySetResult(true);

    await firstRefresh.WaitAsync(
        TimeSpan.FromSeconds(3));
    await fullCompleted.Task.WaitAsync(
        TimeSpan.FromSeconds(3));

    Assert(
        fullRefreshCalls == 1 &&
        duplicateRefreshCalls == 0,
        "Die deduplizierte manuelle Full-Refresh-Operation wurde mehrfach ausgeführt.");
}

static async Task TestCentralNavigationSyncFullRefreshAsync()
{
    using var fixture = TemporaryDirectoryFixture.Create(
        "phase-a3-full-refresh");
    using var loggerFactory =
        LoggerFactory.Create(_ => { });

    var cache = new NavigationCacheService(
        Configuration(
            new Dictionary<string, string?>
            {
                ["WebUi:NavigationCacheDirectory"] =
                    fixture.Path
            }));

    const string cacheKey = "eeeeeeeeeeeeeeee";
    var snapshot = NavigationCacheSnapshot.Create(
        new[]
        {
            new NavigationDocumentDto
            {
                Id = 201,
                ModifiedUtc = new DateTimeOffset(
                    2026, 8, 30, 16, 0, 0, TimeSpan.Zero)
            }
        });

    await using var sync =
        new CentralNavigationSyncService(
            cache,
            loggerFactory.CreateLogger<CentralNavigationSyncService>());

    var statuses =
        new List<CentralSyncStatus>();
    var completedFlags =
        new List<bool>();

    using var registration =
        sync.Register(
            cacheKey,
            () => true,
            (cached, progress) =>
                Task.FromResult(
                    new NavigationRefreshResult(
                        cached,
                        false,
                        DateTimeOffset.UtcNow)),
            status =>
            {
                lock (statuses)
                {
                    statuses.Add(status);
                }

                return Task.CompletedTask;
            },
            (_, isFullRefresh) =>
            {
                lock (completedFlags)
                {
                    completedFlags.Add(isFullRefresh);
                }

                return Task.CompletedTask;
            },
            _ => Task.CompletedTask);

    var refreshCalls = 0;

    await sync.RequestFullRefreshAsync(
        cacheKey,
        () => true,
        progress =>
        {
            Interlocked.Increment(
                ref refreshCalls);

            progress.Report(
                new CentralSyncProgress(
                    50,
                    1,
                    2,
                    "synthetischer Fortschritt"));

            return Task.FromResult(
                new NavigationRefreshResult(
                    snapshot,
                    true,
                    DateTimeOffset.UtcNow));
        }).WaitAsync(
            TimeSpan.FromSeconds(3));

    CentralSyncStatus[] statusSnapshot;
    bool[] completionSnapshot;

    lock (statuses)
    {
        statusSnapshot = statuses.ToArray();
    }

    lock (completedFlags)
    {
        completionSnapshot =
            completedFlags.ToArray();
    }

    Assert(
        refreshCalls == 1,
        "Der verfügbare Full-Refresh-Rückruf wurde nicht exakt einmal ausgeführt.");

    Assert(
        statusSnapshot.Any(status =>
            status.IsRunning &&
            status.IsFullRefresh) &&
        statusSnapshot.Any(status =>
            status.IsRunning &&
            status.IsFullRefresh &&
            status.Percent == 50 &&
            status.LoadedDocuments == 1 &&
            status.TotalDocuments == 2) &&
        statusSnapshot.LastOrDefault() is { IsRunning: false },
        "Full Refresh meldet Status, Fortschritt oder Abschlussstatus nicht vollständig.");

    Assert(
        completionSnapshot.Count(value => value) == 1,
        "Full Refresh meldet Completion nicht exakt einmal mit isFullRefresh=true.");

    var unavailableCalls = 0;

    await sync.RequestFullRefreshAsync(
        cacheKey,
        () => false,
        progress =>
        {
            Interlocked.Increment(
                ref unavailableCalls);
            return Task.FromResult(
                new NavigationRefreshResult(
                    snapshot,
                    true,
                    DateTimeOffset.UtcNow));
        }).WaitAsync(
            TimeSpan.FromSeconds(3));

    Assert(
        unavailableCalls == 0,
        "Ein Full Refresh wird trotz nicht verfügbarer anfordernder Sitzung ausgeführt.");
}

static Task TestErrorClassificationContractAsync()
{
    var unauthorized = PaperlessErrorClassifier.Classify(
        new HttpRequestException(
            "synthetic",
            inner: null,
            statusCode: System.Net.HttpStatusCode.Unauthorized));

    Assert(
        unauthorized.StatusCode == 401 &&
        unauthorized.Category == "Zugang nicht akzeptiert" &&
        unauthorized.UserMessage.Contains("[#401]", StringComparison.Ordinal) &&
        !unauthorized.IsTimeout &&
        !unauthorized.IsControlledCancellation,
        "HTTP-401 wird nicht mit dem erwarteten sicheren Fehlervertrag klassifiziert.");

    var gatewayTimeout = PaperlessErrorClassifier.Classify(
        new HttpRequestException(
            "synthetic",
            inner: null,
            statusCode: System.Net.HttpStatusCode.GatewayTimeout));

    Assert(
        gatewayTimeout.StatusCode == 504 &&
        gatewayTimeout.Category == "Zeitüberschreitung" &&
        gatewayTimeout.IsTimeout &&
        !gatewayTimeout.IsControlledCancellation,
        "HTTP-504 wird nicht als Zeitüberschreitung klassifiziert.");

    var timeout = PaperlessErrorClassifier.Classify(
        new OperationCanceledException());

    Assert(
        timeout.StatusCode is null &&
        timeout.Category == "Zeitüberschreitung" &&
        timeout.IsTimeout &&
        !timeout.IsControlledCancellation,
        "Unkontrollierter OperationCanceledException wird nicht als Zeitüberschreitung klassifiziert.");

    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();

    var controlled = PaperlessErrorClassifier.Classify(
        new OperationCanceledException(),
        cancellation.Token);

    Assert(
        controlled.Category == "Kontrollierter Abbruch" &&
        controlled.UserMessage.Length == 0 &&
        !controlled.IsTimeout &&
        controlled.IsControlledCancellation,
        "Explizit abgebrochene Operation wird nicht als kontrollierter Abbruch klassifiziert.");

    foreach (var error in new[]
    {
        unauthorized,
        gatewayTimeout,
        timeout,
        controlled
    })
    {
        Assert(
            error.EventId.StartsWith(
                "PLS-",
                StringComparison.Ordinal) &&
            error.EventId.Length == 16 &&
            error.EventId[4..].All(Uri.IsHexDigit),
            "Die technische Ereignis-ID folgt nicht mehr dem pseudonymen PLS-XXXXXXXXXXXX-Vertrag.");
    }

    return Task.CompletedTask;
}

static async Task TestPerformanceDiagnosticsMeasurementContractAsync()
{
    using var fixture = TemporaryDirectoryFixture.Create(
        "phase-a3-runtime-performance");

    PerformanceDiagnosticsLog.Configure(
        fixture.Path,
        7,
        "synthetic-version");

    var runId =
        PerformanceDiagnosticsLog.CreateRunId();

    Assert(
        runId.StartsWith(
            "PERF-",
            StringComparison.Ordinal) &&
        !runId.Contains(
            "secret",
            StringComparison.OrdinalIgnoreCase),
        "Performance Diagnostics erzeugt keine technisch pseudonyme Messlauf-ID.");

    PerformanceDiagnosticsLog.Write(
        runId,
        "RUNTIME\tCLASS",
        "MEASURE\nOPERATION",
        "OK",
        12.345,
        "detail-a\r\ndetail-b");

    PerformanceDiagnosticsLog.WriteFailure(
        runId,
        "RUNTIME",
        "FAILURE",
        3.5,
        new HttpRequestException(
            "synthetic-sensitive-message",
            inner: null,
            statusCode: System.Net.HttpStatusCode.ServiceUnavailable),
        "safe-detail");

    string? logPath = null;

    for (var attempt = 0;
        attempt < 40;
        attempt++)
    {
        logPath = Directory
            .EnumerateFiles(
                fixture.Path,
                "performance-diagnostics-*.log",
                SearchOption.TopDirectoryOnly)
            .SingleOrDefault();

        if (logPath is not null &&
            File.ReadAllText(logPath).Contains(
                "MEASURE OPERATION",
                StringComparison.Ordinal) &&
            File.ReadAllText(logPath).Contains(
                "FAILURE",
                StringComparison.Ordinal))
        {
            break;
        }

        await Task.Delay(50);
    }

    Assert(
        logPath is not null,
        "Performance Diagnostics hat keine Tagesdatei geschrieben.");

    var content =
        await File.ReadAllTextAsync(logPath!);

    Assert(
        content.Contains(
            "ZeitUtc\tVersion\tMesslauf\tKlasse\tOperation\tErgebnis\tDauerMs\tDetails",
            StringComparison.Ordinal) &&
        content.Contains(
            "synthetic-version",
            StringComparison.Ordinal) &&
        content.Contains(
            runId,
            StringComparison.Ordinal),
        "Performance Diagnostics enthält nicht die erwarteten technischen Kopf-/Versions-/Messlaufdaten.");

    Assert(
        content.Contains(
            "RUNTIME CLASS",
            StringComparison.Ordinal) &&
        content.Contains(
            "MEASURE OPERATION",
            StringComparison.Ordinal) &&
        content.Contains(
            "detail-a  detail-b",
            StringComparison.Ordinal),
        "Performance Diagnostics sanitisiert Tabulatoren oder Zeilenumbrüche nicht kontrolliert.");

    Assert(
        content.Contains(
            "timeout_candidate=False;exception=HttpRequestException;http_status=503;safe-detail",
            StringComparison.Ordinal) &&
        !content.Contains(
            "synthetic-sensitive-message",
            StringComparison.Ordinal),
        "WriteFailure schreibt die Exception-Nachricht statt ausschließlich technischer Fehlermerkmale.");
}

static Task TestApplicationAndPreviewAuthorizationContractAsync()
{
    var source = ReadProjectSource(
        "WebUI.Web/Program.cs");

    Assert(
        source.Contains(
            "app.UseAntiforgery();",
            StringComparison.Ordinal),
        "Die Antiforgery-Middleware fehlt aus der Anwendungspipeline.");

    Assert(
        source.Contains(
            "previewEndpoint.RequireAuthorization();",
            StringComparison.Ordinal) &&
        source.Contains(
            "documentPreviewEndpoint.RequireAuthorization();",
            StringComparison.Ordinal) &&
        source.Contains(
            "razorComponents.RequireAuthorization();",
            StringComparison.Ordinal),
        "Preview-Endpunkte oder die Razor-Anwendung sind nicht mehr autorisierungspflichtig.");

    Assert(
        !source.Contains(
            "previewEndpoint.AllowAnonymous();",
            StringComparison.Ordinal) &&
        !source.Contains(
            "documentPreviewEndpoint.AllowAnonymous();",
            StringComparison.Ordinal) &&
        !source.Contains(
            "razorComponents.AllowAnonymous();",
            StringComparison.Ordinal),
        "Ein geschützter Preview-/Anwendungsendpunkt wurde anonym freigegeben.");

    return Task.CompletedTask;
}

static Task TestHealthzAndHttpsHstsContractAsync()
{
    var source = ReadProjectSource(
        "WebUI.Web/Program.cs");

    var middlewareBlock = ExtractSourceBlock(
        source,
        "if (!app.Environment.IsDevelopment())",
        "app.UseAntiforgery();");

    Assert(
        middlewareBlock.Contains(
            "app.UseHsts();",
            StringComparison.Ordinal),
        "HSTS wird außerhalb Development nicht mehr aktiviert.");

    Assert(
        middlewareBlock.Contains(
            "\"WebUi:UseHttpsRedirection\"",
            StringComparison.Ordinal) &&
        middlewareBlock.Contains(
            "app.Environment.IsDevelopment()",
            StringComparison.Ordinal) &&
        middlewareBlock.Contains(
            "app.UseHttpsRedirection();",
            StringComparison.Ordinal),
        "HTTPS-Weiterleitung ist nicht mehr konfigurierbar mit dem dokumentierten Development-Default.");

    var healthBlock = ExtractSourceBlock(
        source,
        "app.MapGet(\n    \"/healthz\",",
        "if (oidcSettings.Enabled)");

    Assert(
        healthBlock.Contains(
            "status = \"healthy\"",
            StringComparison.Ordinal) &&
        healthBlock.Contains(
            ".AllowAnonymous();",
            StringComparison.Ordinal) &&
        !healthBlock.Contains(
            ".RequireAuthorization();",
            StringComparison.Ordinal),
        "/healthz ist nicht mehr ein minimaler anonymer Health-Endpunkt.");

    return Task.CompletedTask;
}

static Task TestProxyGuardHealthcheckModeContractAsync()
{
    var source = ReadProjectSource(
        "WebUI.Web/Program.cs");

    var specialModeBlock = ExtractSourceBlock(
        source,
        "if (args.Length == 1 &&",
        "const string OidcDiagnosticOriginProperty =");

    Assert(
        specialModeBlock.Contains(
            "\"--proxy-guard-healthcheck\"",
            StringComparison.Ordinal) &&
        specialModeBlock.Contains(
            "await ProxyGuardHealthCheckRunner.RunAsync();",
            StringComparison.Ordinal) &&
        specialModeBlock.Contains(
            "return;",
            StringComparison.Ordinal),
        "Der Proxy-Guard-Healthcheck ist nicht mehr als früher Sondermodus implementiert.");

    var builderPosition = source.IndexOf(
        "WebApplication.CreateBuilder(args)",
        StringComparison.Ordinal);
    var healthModePosition = source.IndexOf(
        "\"--proxy-guard-healthcheck\"",
        StringComparison.Ordinal);
    var returnPosition = specialModeBlock.IndexOf(
        "return;",
        StringComparison.Ordinal);

    Assert(
        healthModePosition >= 0 &&
        builderPosition > healthModePosition &&
        returnPosition >= 0,
        "Der Proxy-Guard-Healthcheck liegt nicht mehr vor dem normalen WebApplication-Startpfad.");

    return Task.CompletedTask;
}

static Task TestOidcDiagnosticEventHooksContractAsync()
{
    var source = ReadProjectSource(
        "WebUI.Web/Program.cs");

    var oidcEventsBlock = ExtractSourceBlock(
        source,
        "options.Events.OnRedirectToIdentityProvider = context =>",
        "options.Events.OnTokenValidated = context =>");

    foreach (var required in new[]
    {
        "OIDC-DIAG Challenge;",
        "OIDC-DIAG Callback;",
        "OIDC-DIAG TokenRequest;",
        "OIDC-DIAG TokenResponse;"
    })
    {
        Assert(
            oidcEventsBlock.Contains(
                required,
                StringComparison.Ordinal),
            $"OIDC-Diagnostik-Hook fehlt: {required}");
    }

    Assert(
        oidcEventsBlock.Contains(
            "CreateDiagnosticDigest(stateVerifier!)",
            StringComparison.Ordinal) &&
        oidcEventsBlock.Contains(
            "ToDiagnosticDigestComparison(",
            StringComparison.Ordinal) &&
        !oidcEventsBlock.Contains(
            "logger.LogInformation(\n                    stateVerifier",
            StringComparison.Ordinal),
        "Die OIDC-Diagnostik verarbeitet den PKCE-Verifier nicht ausschließlich als technische Digest-/Vergleichsinformation.");

    var remoteFailureBlock = ExtractSourceBlock(
        source,
        "options.Events.OnRemoteFailure = async context =>",
        "});\n\n    builder.Services.AddAuthorization();");

    Assert(
        remoteFailureBlock.Contains(
            "OIDC-DIAG RemoteFailure;",
            StringComparison.Ordinal) &&
        remoteFailureBlock.Contains(
            "GetOidcExceptionType(",
            StringComparison.Ordinal) &&
        remoteFailureBlock.Contains(
            "context.Failure",
            StringComparison.Ordinal) &&
        remoteFailureBlock.Contains(
            "Ausnahmetyp: {ExceptionType}",
            StringComparison.Ordinal) &&
        !remoteFailureBlock.Contains(
            "exception.Message",
            StringComparison.Ordinal),
        "RemoteFailure-Diagnostik protokolliert nicht mehr ausschließlich technische Fehlertypdaten.");

    return Task.CompletedTask;
}

static Task TestDockerfileMultistageArchitectureContractAsync()
{
    var source = ReadProjectSource(
        "Containerbetrieb/Dockerfile");

    Assert(
        source.Contains(
            "FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build",
            StringComparison.Ordinal) &&
        source.Contains(
            "ARG TARGETARCH",
            StringComparison.Ordinal),
        "Das Dockerfile besitzt nicht mehr den dokumentierten BUILDPLATFORM/TARGETARCH-Buildvertrag.");

    Assert(
        source.Contains(
            "dotnet restore \"WebUI.Web/WebUI.Web.csproj\" -a \"$TARGETARCH\"",
            StringComparison.Ordinal) &&
        source.Contains(
            "-a \"$TARGETARCH\"",
            StringComparison.Ordinal) &&
        source.Contains(
            "/p:UseAppHost=false",
            StringComparison.Ordinal),
        "Restore/Publish sind nicht mehr explizit an die Zielarchitektur gebunden.");

    Assert(
        source.Contains(
            "FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime",
            StringComparison.Ordinal) &&
        source.Contains(
            "COPY --from=build /app/publish .",
            StringComparison.Ordinal) &&
        source.Contains(
            "ENTRYPOINT [\"dotnet\", \"WebUI.Web.dll\"]",
            StringComparison.Ordinal),
        "Das Dockerfile verliert den getrennten Runtime-Stage-Vertrag.");

    return Task.CompletedTask;
}

static Task TestComposeImagePortTmpfsInitContractAsync()
{
    var source = ReadProjectSource(
        "Containerbetrieb/compose.yaml.example");

    Assert(
        source.Contains(
            "image: \"${WEBUI_IMAGE:?WEBUI_IMAGE muss gesetzt sein}\"",
            StringComparison.Ordinal) &&
        source.Contains(
            "pull_policy: never",
            StringComparison.Ordinal),
        "Compose erzwingt Imagevorgabe und lokalen Imagevertrag nicht mehr.");

    Assert(
        source.Contains(
            "\"${WEBUI_BIND_ADDRESS:?WEBUI_BIND_ADDRESS muss gesetzt sein}:${WEBUI_PORT:?WEBUI_PORT muss gesetzt sein}:8080\"",
            StringComparison.Ordinal),
        "Compose bindet Hostadresse/Hostport nicht mehr explizit auf Containerport 8080.");

    Assert(
        source.Contains(
            "init: true",
            StringComparison.Ordinal) &&
        source.Contains(
            "- /tmp:size=64m,mode=1777",
            StringComparison.Ordinal),
        "Compose verliert init=true oder den flüchtigen /tmp-tmpfs-Vertrag.");

    return Task.CompletedTask;
}

static Task TestComposeHealthResourceLoggingContractAsync()
{
    var source = ReadProjectSource(
        "Containerbetrieb/compose.yaml.example");

    var healthBlock = ExtractSourceBlock(
        source,
        "    healthcheck:",
        "    mem_limit:");

    Assert(
        healthBlock.Contains(
            "WebUi__ProxyGuard__Enabled",
            StringComparison.Ordinal) &&
        healthBlock.Contains(
            "dotnet WebUI.Web.dll --proxy-guard-healthcheck;",
            StringComparison.Ordinal) &&
        healthBlock.Contains(
            "curl --fail --silent --show-error http://127.0.0.1:8080/healthz",
            StringComparison.Ordinal),
        "Der Compose-Healthcheck schaltet nicht mehr zwischen Proxy-Guard-Sondermodus und /healthz um.");

    Assert(
        source.Contains(
            "mem_limit: 1g",
            StringComparison.Ordinal) &&
        source.Contains(
            "mem_reservation: 256m",
            StringComparison.Ordinal),
        "Compose enthält die dokumentierten Speichergrenzen nicht mehr.");

    var loggingBlock = ExtractSourceBlock(
        source,
        "    logging:",
        "      options:");

    Assert(
        loggingBlock.Contains(
            "driver: json-file",
            StringComparison.Ordinal) &&
        source.Contains(
            "max-size: \"10m\"",
            StringComparison.Ordinal) &&
        source.Contains(
            "max-file: \"3\"",
            StringComparison.Ordinal),
        "Compose enthält keine begrenzte json-file-Logrotation mehr.");

    return Task.CompletedTask;
}

static Task TestComposeRestartContractAsync()
{
    var source = ReadProjectSource(
        "Containerbetrieb/compose.yaml.example");

    Assert(
        source.Contains(
            "restart: unless-stopped",
            StringComparison.Ordinal),
        "Compose verwendet nicht mehr den dokumentierten Wiederanlaufvertrag restart: unless-stopped.");

    Assert(
        !source.Contains(
            "restart: always",
            StringComparison.Ordinal) &&
        !source.Contains(
            "restart: on-failure",
            StringComparison.Ordinal),
        "Compose enthält einen konkurrierenden Wiederanlaufvertrag.");

    return Task.CompletedTask;
}

static Task TestOidcDirectClientSecretRejectedAsync()
{
    var threw = false;

    try
    {
        _ = OidcAuthenticationSettings.Load(
            Configuration(
                new Dictionary<string, string?>
                {
                    ["Authentication:Oidc:Authority"] =
                        "https://oidc.example.test",
                    ["Authentication:Oidc:MetadataAddress"] =
                        "https://oidc.example.test/.well-known/openid-configuration",
                    ["Authentication:Oidc:ClientId"] =
                        "test-client",
                    ["Authentication:Oidc:ClientSecret"] =
                        "DARF_NICHT_VERWENDET_WERDEN",
                    ["Authentication:Oidc:ClientSecretFilePath"] =
                        "/run/secrets/oidc_client_secret"
                }));
    }
    catch (InvalidOperationException)
    {
        threw = true;
    }

    Assert(
        threw,
        "Direkter Authentication:Oidc:ClientSecret-Wert wurde nicht abgelehnt.");

    return Task.CompletedTask;
}

static Task TestProductionConfigurationNoNonSecretFilePathDefaultsAsync()
{
    var content = ReadProjectSource(
        "WebUI.Web/appsettings.Production.json");

    using var document =
        System.Text.Json.JsonDocument.Parse(content);

    var root = document.RootElement;
    var oidc = root
        .GetProperty("Authentication")
        .GetProperty("Oidc");

    Assert(
        !oidc.TryGetProperty("AuthorityFilePath", out _),
        "AuthorityFilePath darf kein Production-Default mehr sein.");
    Assert(
        !oidc.TryGetProperty("MetadataAddressFilePath", out _),
        "MetadataAddressFilePath darf kein Production-Default sein.");
    Assert(
        !oidc.TryGetProperty("ClientIdFilePath", out _),
        "ClientIdFilePath darf kein Production-Default mehr sein.");
    Assert(
        oidc.TryGetProperty("ClientSecretFilePath", out var clientSecretFilePath)
        && !string.IsNullOrWhiteSpace(clientSecretFilePath.GetString()),
        "ClientSecretFilePath muss als File-only-Secret erhalten bleiben.");

    var paperless = root.GetProperty("Paperless");
    Assert(
        !paperless.TryGetProperty("BaseUrlFilePath", out _),
        "BaseUrlFilePath darf kein Production-Default mehr sein.");

    return Task.CompletedTask;
}

static Task TestPaginationRelativeUrlAsync()
{
    var pageUri = ResolvePaginationUri(
        new Uri("https://paperless.example.test"),
        "/api/documents/?page=2");

    Assert(
        string.Equals(
            pageUri.AbsoluteUri,
            "https://paperless.example.test/api/documents/?page=2",
            StringComparison.Ordinal),
        "Relative Pagination-URL wurde nicht korrekt aufgelöst.");
    return Task.CompletedTask;
}

static Task TestPaginationAbsoluteSameOriginUrlAsync()
{
    var pageUri = ResolvePaginationUri(
        new Uri("https://paperless.example.test"),
        "https://paperless.example.test/api/documents/?page=2");

    Assert(
        string.Equals(
            pageUri.AbsoluteUri,
            "https://paperless.example.test/api/documents/?page=2",
            StringComparison.Ordinal),
        "Absolute Same-Origin-Pagination-URL wurde verändert oder abgelehnt.");
    return Task.CompletedTask;
}

static Task TestPaginationHostCaseAsync()
{
    var pageUri = ResolvePaginationUri(
        new Uri("https://paperless.example.test"),
        "https://PAPERLESS.EXAMPLE.TEST/api/documents/?page=2");

    Assert(
        string.Equals(
            pageUri.IdnHost,
            "paperless.example.test",
            StringComparison.OrdinalIgnoreCase),
        "Gleicher Host mit abweichender Groß-/Kleinschreibung wurde nicht akzeptiert.");
    return Task.CompletedTask;
}

static Task TestPaginationExplicitDefaultPortAsync()
{
    var pageUri = ResolvePaginationUri(
        new Uri("https://paperless.example.test"),
        "https://paperless.example.test:443/api/documents/?page=2");

    Assert(
        pageUri.Port == 443,
        "Expliziter HTTPS-Standardport wurde nicht als gleicher effektiver Port akzeptiert.");
    return Task.CompletedTask;
}

static Task TestPaginationForeignHostAsync() =>
    ExpectPaginationUriInvalidAsync(
        "https://foreign.example.test/api/documents/?page=2");

static Task TestPaginationPrefixHostAsync() =>
    ExpectPaginationUriInvalidAsync(
        "https://paperless.example.test.evil.invalid/api/documents/?page=2");

static Task TestPaginationForeignPortAsync() =>
    ExpectPaginationUriInvalidAsync(
        "https://paperless.example.test:8443/api/documents/?page=2");

static Task TestPaginationForeignSchemeAsync() =>
    ExpectPaginationUriInvalidAsync(
        "http://paperless.example.test/api/documents/?page=2");

static Task TestPaginationProtocolRelativeForeignHostAsync() =>
    ExpectPaginationUriInvalidAsync(
        "//foreign.example.test/api/documents/?page=2");

static Task TestPaginationUserInfoAsync() =>
    ExpectPaginationUriInvalidAsync(
        "https://user:password@paperless.example.test/api/documents/?page=2");

static Task TestPaginationInvalidUrlAsync() =>
    ExpectPaginationUriInvalidAsync(
        "https://[");

static Task ExpectPaginationUriInvalidAsync(string nextPath)
{
    var threw = false;

    try
    {
        _ = ResolvePaginationUri(
            new Uri("https://paperless.example.test"),
            nextPath);
    }
    catch (InvalidOperationException)
    {
        threw = true;
    }

    Assert(
        threw,
        "Unzulässige Pagination-URL wurde nicht abgelehnt.");
    return Task.CompletedTask;
}

static Uri ResolvePaginationUri(
    Uri baseAddress,
    string nextPath)
{
    var method = typeof(
            WebUI.Infrastructure.PaperlessApiClient)
        .GetMethod(
            "ResolvePaginationUri",
            BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException(
            "Die interne Q-01-Pagination-Adressprüfung wurde nicht gefunden.");

    var resolvePaginationUri = method.CreateDelegate<
        Func<Uri, string, Uri>>();

    return resolvePaginationUri(
        baseAddress,
        nextPath);
}

static Task ExpectPaperlessBaseUrlInvalidAsync(
    Dictionary<string, string?> values)
{
    ExpectPaperlessBaseUrlInvalid(values);
    return Task.CompletedTask;
}

static void ExpectPaperlessBaseUrlInvalid(
    Dictionary<string, string?> values)
{
    var threw = false;

    try
    {
        _ = ReadPaperlessBaseUrl(values);
    }
    catch (InvalidOperationException)
    {
        threw = true;
    }

    Assert(threw, "Erwarteter Konfigurationsabbruch blieb aus.");
}

static string ReadPaperlessBaseUrl(
    Dictionary<string, string?> values)
{
    using var tokenDirectory = TemporaryDirectoryFixture.Create(
        "paperless-token-test");
    using var dataProtectionDirectory = TemporaryDirectoryFixture.Create(
        "paperless-data-protection-test");
    var effectiveValues = new Dictionary<string, string?>(
        values,
        StringComparer.OrdinalIgnoreCase)
    {
        ["Paperless:UserTokenDirectory"] = tokenDirectory.Path
    };
    var store = new ProtectedUserPaperlessTokenStore(
        new HttpContextAccessor(),
        new NoNetworkHttpClientFactory(),
        DataProtectionProvider.Create(dataProtectionDirectory.Path),
        Configuration(effectiveValues));
    var method = typeof(ProtectedUserPaperlessTokenStore).GetMethod(
        "ReadAndValidateBaseUrl",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException(
            "Die interne Paperless-Basisadressprüfung wurde nicht gefunden.");

    var readBaseUrl = method.CreateDelegate<
        Func<ProtectedUserPaperlessTokenStore, string>>();
    return readBaseUrl(store);
}

static async Task RunRejectedRequestAsync(StringValues? headerValues)
{
    using var fixture = SecretFixture.CreateRandom();
    using var settings = LoadEnabled(fixture.Path);
    var called = false;
    var middleware = new ProxyGuardMiddleware(
        _ =>
        {
            called = true;
            return Task.CompletedTask;
        },
        settings);
    var context = NewContext();

    if (headerValues.HasValue)
    {
        context.Request.Headers[ProxyGuardSettings.HeaderName] =
            headerValues.Value;
    }

    await middleware.InvokeAsync(context);

    Assert(!called, "Ungültige Anfrage erreichte die Folgepipeline.");
    Assert(context.Response.StatusCode == StatusCodes.Status403Forbidden,
        "Erwarteter HTTP-Status 403 fehlt.");
    Assert(context.Response.ContentLength == 0,
        "403-Antwort besitzt keinen leeren Antwortkörper.");
    Assert(context.Response.ContentType is null,
        "403-Antwort besitzt einen unerwarteten Inhaltstyp.");
    Assert(context.Response.Body.Length == 0,
        "403-Antwort enthält unerwartete Daten.");
    Assert(
        string.Equals(
            context.Response.Headers["Cache-Control"].ToString(),
            "no-store",
            StringComparison.Ordinal),
        "403-Antwort besitzt nicht Cache-Control: no-store.");
}

static Task TestValidSecretBytesAsync(byte[] bytes)
{
    using var fixture = SecretFixture.Create(bytes);
    using var settings = LoadEnabled(fixture.Path);
    Assert(settings.Enabled, "Guard muss aktiviert sein.");
    return Task.CompletedTask;
}

static Task ExpectInvalidSecretTextAsync(string content) =>
    ExpectInvalidSecretBytesAsync(
        System.Text.Encoding.ASCII.GetBytes(content));

static Task TestLogoutEndpointsUsePostAsync()
{
    var programSource = ReadProjectSource("WebUI.Web/Program.cs");

    Assert(
        programSource.Contains("app.MapPost(\n        \"/auth/logout\"", StringComparison.Ordinal),
        "OIDC-Logout ist nicht als POST-Endpunkt definiert.");
    var localEndpointSource = ReadProjectSource(
        "WebUI.Web/Services/LocalDevelopmentAuthenticationEndpoints.cs");
    Assert(
        localEndpointSource.Contains("app.MapPost(\n            \"/local-auth/logout\"", StringComparison.Ordinal),
        "Lokaler Logout ist nicht als POST-Endpunkt definiert.");
    Assert(
        !programSource.Contains("app.MapGet(\n        \"/auth/logout\"", StringComparison.Ordinal),
        "OIDC-Logout ist weiterhin als GET-Endpunkt definiert.");
    Assert(
        !localEndpointSource.Contains("app.MapGet(\n            \"/local-auth/logout\"", StringComparison.Ordinal),
        "Lokaler Logout ist weiterhin als GET-Endpunkt definiert.");
    return Task.CompletedTask;
}

static Task TestLogoutEndpointsValidateAntiforgeryAsync()
{
    var programSource = ReadProjectSource("WebUI.Web/Program.cs");
    var oidcBlock = ExtractSourceBlock(
        programSource,
        "app.MapPost(\n        \"/auth/logout\"",
        "app.MapGet(\n        \"/auth/signed-out\"");
    var localEndpointSource = ReadProjectSource(
        "WebUI.Web/Services/LocalDevelopmentAuthenticationEndpoints.cs");
    var localBlock = ExtractSourceBlock(
        localEndpointSource,
        "app.MapPost(\n            \"/local-auth/logout\"",
        "    private static string CreateLocalLoginPage(");

    AssertAntiforgeryBeforeStateChange(oidcBlock, "OIDC-Logout");
    AssertAntiforgeryBeforeStateChange(localBlock, "Lokaler Logout");
    return Task.CompletedTask;
}

static Task TestOidcLogoutKeepsProviderSessionAsync()
{
    var programSource = ReadProjectSource("WebUI.Web/Program.cs");
    var oidcBlock = ExtractSourceBlock(
        programSource,
        "app.MapPost(\n        \"/auth/logout\"",
        "app.MapGet(\n        \"/auth/signed-out\"");

    Assert(
        oidcBlock.Contains("CookieAuthenticationDefaults.AuthenticationScheme", StringComparison.Ordinal),
        "OIDC-Logout beendet nicht mehr ausdrücklich die lokale WebUI-Cookie-Sitzung.");
    Assert(
        !oidcBlock.Contains("OpenIdConnectDefaults.AuthenticationScheme", StringComparison.Ordinal),
        "OIDC-Logout würde entgegen der Fachvorgabe auch den OIDC-Provider abmelden.");
    Assert(
        oidcBlock.Contains("RedirectUri = \"/auth/signed-out\"", StringComparison.Ordinal),
        "OIDC-Logout besitzt nicht mehr die vorgesehene lokale Abmeldeseite als Ziel.");
    return Task.CompletedTask;
}

static Task TestLocalLogoutBehaviorAsync()
{
    var localEndpointSource = ReadProjectSource(
        "WebUI.Web/Services/LocalDevelopmentAuthenticationEndpoints.cs");
    var localBlock = ExtractSourceBlock(
        localEndpointSource,
        "app.MapPost(\n            \"/local-auth/logout\"",
        "    private static string CreateLocalLoginPage(");

    Assert(
        localBlock.Contains("LocalTestUserSettings.AuthenticationScheme", StringComparison.Ordinal),
        "Lokaler Logout beendet nicht mehr ausdrücklich die lokale Testsession.");
    Assert(
        localBlock.Contains("Results.Redirect(\"/local-login\")", StringComparison.Ordinal),
        "Lokaler Logout leitet nicht mehr zur lokalen Anmeldung zurück.");
    Assert(
        !localBlock.Contains("PersonalToken", StringComparison.OrdinalIgnoreCase) &&
        !localBlock.Contains("Keychain", StringComparison.OrdinalIgnoreCase),
        "Lokaler Logout enthält unerwartete Token-/Schlüsselbund-Logik.");
    return Task.CompletedTask;
}

static Task TestLogoutFormsUsePostAndAntiforgeryAsync()
{
    var layoutSource = ReadProjectSource(
        "WebUI.Web/Components/Layout/MainLayout.razor");

    Assert(
        CountOccurrences(layoutSource, "method=\"post\"") >= 2,
        "Nicht beide Logout-Oberflächen verwenden POST-Formulare.");
    Assert(
        layoutSource.Contains("action=\"/auth/logout\"", StringComparison.Ordinal) &&
        layoutSource.Contains("action=\"/local-auth/logout\"", StringComparison.Ordinal),
        "Mindestens ein Logout-Formular besitzt nicht das vorgesehene Ziel.");
    Assert(
        CountOccurrences(layoutSource, "<AntiforgeryToken />") >= 2,
        "Nicht beide Logout-Formulare enthalten einen AntiforgeryToken.");
    Assert(
        !layoutSource.Contains("href=\"/auth/logout\"", StringComparison.Ordinal) &&
        !layoutSource.Contains("href=\"/local-auth/logout\"", StringComparison.Ordinal),
        "Mindestens ein alter GET-Logout-Link ist weiterhin vorhanden.");
    return Task.CompletedTask;
}

static void AssertAntiforgeryBeforeStateChange(
    string sourceBlock,
    string description)
{
    var validationIndex = sourceBlock.IndexOf(
        "antiforgery.IsRequestValidAsync(httpContext)",
        StringComparison.Ordinal);
    var revokeIndex = sourceBlock.IndexOf(
        "userSessions.Revoke(",
        StringComparison.Ordinal);
    var signOutIndex = sourceBlock.IndexOf(
        "SignOut",
        StringComparison.Ordinal);

    Assert(validationIndex >= 0, $"{description}: Antiforgery-Prüfung fehlt.");
    Assert(revokeIndex > validationIndex, $"{description}: Session wird vor der Antiforgery-Prüfung widerrufen.");
    Assert(signOutIndex > validationIndex, $"{description}: Sign-out erfolgt vor der Antiforgery-Prüfung.");
}

static string ExtractSourceBlock(
    string source,
    string startMarker,
    string endMarker)
{
    var start = source.IndexOf(startMarker, StringComparison.Ordinal);
    var end = source.IndexOf(endMarker, start >= 0 ? start : 0, StringComparison.Ordinal);

    Assert(start >= 0, $"Quellblock-Start fehlt: {startMarker}");
    Assert(end > start, $"Quellblock-Ende fehlt: {endMarker}");
    return source[start..end];
}

static string ReadProjectSource(string relativePath)
{
    var current = new DirectoryInfo(Directory.GetCurrentDirectory());

    while (current is not null)
    {
        var candidate = System.IO.Path.Combine(current.FullName, relativePath);
        if (File.Exists(candidate))
        {
            return File.ReadAllText(candidate);
        }

        current = current.Parent;
    }

    throw new InvalidOperationException(
        $"Projektquelldatei für Regressionstest nicht gefunden: {relativePath}");
}

static int CountOccurrences(string source, string value)
{
    var count = 0;
    var index = 0;

    while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
    {
        count++;
        index += value.Length;
    }

    return count;
}

static Task TestAp01LocalStartOffersTokenChangeAsync()
{
    var startSource = ReadProjectSource(
        "WebUI.Web/Components/Pages/Start.razor");

    Assert(
        startSource.Contains("<h2>Lokaler Entwicklungsbetrieb</h2>", StringComparison.Ordinal),
        "Die lokale Startseite kennzeichnet den Entwicklungsbetrieb nicht mehr.");
    Assert(
        startSource.Contains("API-Token ändern", StringComparison.Ordinal) &&
        startSource.Contains("href=\"/auth/paperless-connection\"", StringComparison.Ordinal),
        "Die lokale Startseite bietet den vorgesehenen Tokenänderungsweg nicht an.");
    Assert(
        startSource.Contains("macOS-Schlüsselbund", StringComparison.Ordinal),
        "Die lokale Startseite beschreibt den tatsächlichen geschützten Token-Speicher nicht.");

    var localLoginSource = ReadProjectSource(
        "WebUI.Web/Services/LocalDevelopmentAuthenticationEndpoints.cs");
    Assert(
        !localLoginSource.Contains("API-Token ändern", StringComparison.Ordinal),
        "Die reine lokale Testbenutzerauswahl enthält unerwartet Tokenverwaltung.");

    return Task.CompletedTask;
}

static Task TestAp01SharedTokenChangeStoreAsync()
{
    var pageSource = ReadProjectSource(
        "WebUI.Web/Components/Pages/PaperlessConnection.razor");
    var interfaceSource = ReadProjectSource(
        "WebUI.Web/Services/PaperlessConnectionTokenStore.cs");
    var protectedStoreSource = ReadProjectSource(
        "WebUI.Web/Services/ProtectedUserPaperlessTokenStore.cs");
    var localStoreSource = ReadProjectSource(
        "WebUI.Web/Services/LocalKeychainPaperlessSettings.cs");

    Assert(
        pageSource.Contains("@inject IPaperlessConnectionTokenStore TokenStore", StringComparison.Ordinal),
        "Die Tokenänderungsseite verwendet nicht den gemeinsamen Token-Store.");
    Assert(
        !pageSource.Contains("nur im OIDC-Betrieb verfügbar", StringComparison.Ordinal),
        "Die Tokenänderungsseite sperrt den lokalen Entwicklungsbetrieb weiterhin aus.");
    Assert(
        interfaceSource.Contains("Task<PreparedPaperlessToken> PrepareAsync", StringComparison.Ordinal) &&
        interfaceSource.Contains("Task SavePreparedAsync", StringComparison.Ordinal),
        "Der gemeinsame Token-Store besitzt nicht den zweistufigen Prüf-/Speicherablauf.");
    Assert(
        protectedStoreSource.Contains(": IPaperlessConnectionTokenStore", StringComparison.Ordinal) &&
        localStoreSource.Contains(": IPaperlessConnectionTokenStore", StringComparison.Ordinal),
        "Nicht beide Betriebsarten implementieren denselben Tokenänderungsvertrag.");

    return Task.CompletedTask;
}

static Task TestAp01LocalKeychainReplaceAndRollbackAsync()
{
    var source = ReadProjectSource(
        "WebUI.Web/Services/LocalKeychainPaperlessSettings.cs");

    Assert(
        source.Contains("TokenFingerprintService", StringComparison.Ordinal) &&
        source.Contains("CreateSha256Fingerprint(normalizedToken)", StringComparison.Ordinal),
        "Die SHA-256-Fingerprint-Absicherung wird beim lokalen Tokenwechsel nicht fortgeführt.");
    Assert(
        source.Contains("WriteExistingSecret(\n                TokenService", StringComparison.Ordinal) &&
        source.Contains("WriteExistingSecret(\n                TokenFingerprintService", StringComparison.Ordinal),
        "Token und SHA-256-Fingerprint werden nicht beide kontrolliert ersetzt.");
    Assert(
        source.Contains("previousToken", StringComparison.Ordinal) &&
        source.Contains("previousFingerprint", StringComparison.Ordinal) &&
        source.Contains("CancellationToken.None", StringComparison.Ordinal),
        "Der lokale Tokenwechsel besitzt keinen erkennbaren Rückfall auf den vorherigen Schlüsselbundzustand.");
    Assert(
        !source.Contains("WriteExistingSecret(\n                PaperlessUserIdService", StringComparison.Ordinal),
        "Die bestehende Paperless-Benutzer-ID-Zuordnung würde beim Tokenwechsel verändert.");

    return Task.CompletedTask;
}

static Task TestAp01LocalKeychainNoSecretProcessArgumentsAsync()
{
    var source = ReadProjectSource(
        "WebUI.Web/Services/LocalKeychainPaperlessSettings.cs");

    Assert(
        source.Contains("SecKeychainItemModifyAttributesAndData", StringComparison.Ordinal),
        "Das Schlüsselbundschreiben verwendet nicht die direkte macOS-Keychain-Schnittstelle.");
    Assert(
        !source.Contains("add-generic-password", StringComparison.Ordinal),
        "Das lokale Schlüsselbundschreiben könnte Secretwerte über security-Prozessargumente offenlegen.");
    Assert(
        source.Contains("CryptographicOperations.ZeroMemory(valueBytes)", StringComparison.Ordinal),
        "Der temporäre Bytepuffer des zu schreibenden Secrets wird nicht explizit geleert.");

    return Task.CompletedTask;
}

static Task TestAp01LocalEndpointsExtractedAsync()
{
    var programSource = ReadProjectSource("WebUI.Web/Program.cs");
    var endpointSource = ReadProjectSource(
        "WebUI.Web/Services/LocalDevelopmentAuthenticationEndpoints.cs");

    Assert(
        programSource.Contains("app.MapLocalDevelopmentAuthenticationEndpoints();", StringComparison.Ordinal),
        "Program.cs registriert den ausgelagerten lokalen Endpoint-Baustein nicht.");
    Assert(
        !programSource.Contains("CreateLocalLoginPage(", StringComparison.Ordinal) &&
        !programSource.Contains("\"/local-auth/login/{alias}\"", StringComparison.Ordinal) &&
        !programSource.Contains("\"/local-auth/logout\"", StringComparison.Ordinal),
        "Lokale Authentifizierungsroutinen liegen weiterhin direkt in Program.cs.");
    Assert(
        endpointSource.Contains("\"/local-login\"", StringComparison.Ordinal) &&
        endpointSource.Contains("\"/local-auth/login/{alias}\"", StringComparison.Ordinal) &&
        endpointSource.Contains("\"/local-auth/logout\"", StringComparison.Ordinal),
        "Der ausgelagerte Endpoint-Baustein enthält nicht alle bisherigen lokalen Authentifizierungswege.");

    return Task.CompletedTask;
}

static Task TestQ02PaperlessApiClientUsesInjectedHttpClientAsync()
{
    var source = ReadProjectSource("WebUI.Infrastructure/PaperlessApiClient.cs");

    Assert(
        source.Contains("HttpClient httpClient", StringComparison.Ordinal),
        "PaperlessApiClient erhält keinen HttpClient von außen.");
    Assert(
        !source.Contains("new HttpClient", StringComparison.Ordinal),
        "PaperlessApiClient erzeugt weiterhin selbst einen HttpClient.");
    Assert(
        !source.Contains("new SocketsHttpHandler", StringComparison.Ordinal),
        "PaperlessApiClient erzeugt weiterhin selbst einen SocketsHttpHandler.");

    return Task.CompletedTask;
}

static Task TestQ02PaperlessClientFactoryUsesHttpClientFactoryAsync()
{
    var source = ReadProjectSource("WebUI.Web/Services/PaperlessClientFactory.cs");

    Assert(
        source.Contains("IHttpClientFactory httpClientFactory", StringComparison.Ordinal),
        "PaperlessClientFactory erhält IHttpClientFactory nicht per Dependency Injection.");
    Assert(
        source.Contains("CreateClient(HttpClientName)", StringComparison.Ordinal),
        "PaperlessClientFactory verwendet den benannten Paperless-HttpClient nicht.");

    return Task.CompletedTask;
}

static Task TestQ02AllPaperlessCreationPathsUseHttpClientFactoryAsync()
{
    var tokenStoreSource = ReadProjectSource(
        "WebUI.Web/Services/ProtectedUserPaperlessTokenStore.cs");
    var localEndpointSource = ReadProjectSource(
        "WebUI.Web/Services/LocalDevelopmentAuthenticationEndpoints.cs");

    Assert(
        tokenStoreSource.Contains("IHttpClientFactory httpClientFactory", StringComparison.Ordinal) &&
        tokenStoreSource.Contains("CreateClient(PaperlessClientFactory.HttpClientName)", StringComparison.Ordinal),
        "Die persönliche Tokenvalidierung verwendet den verwalteten Paperless-HttpClient nicht.");

    var localLoginBlock = ExtractSourceBlock(
        localEndpointSource,
        "app.MapGet(\n            \"/local-auth/login/{alias}\"",
        "app.MapPost(\n            \"/local-auth/logout\"");

    Assert(
        localLoginBlock.Contains("IHttpClientFactory httpClientFactory", StringComparison.Ordinal) &&
        localLoginBlock.Contains("CreateClient(PaperlessClientFactory.HttpClientName)", StringComparison.Ordinal),
        "Der lokale Login verwendet den verwalteten Paperless-HttpClient nicht.");

    return Task.CompletedTask;
}

static Task TestAp02ChallengeGuardAllowsFirstRequestAsync()
{
    var context =
        new DefaultHttpContext();
    var guard =
        new OidcChallengeGuard();

    var state =
        guard.Inspect(
            context.Request);

    Assert(
        !state.HasActiveChallenge &&
        state.CorrelationCookieCount == 0,
        "Ein Browserkontext ohne OIDC-Korrelationscookie wurde fälschlich blockiert.");

    return Task.CompletedTask;
}

static Task TestAp02ChallengeGuardDetectsActiveCorrelationAsync()
{
    var context =
        new DefaultHttpContext();
    context.Request.Headers.Cookie =
        ".AspNetCore.Correlation.synthetic=opaque; unrelated=value";

    var state =
        new OidcChallengeGuard()
            .Inspect(
                context.Request);

    Assert(
        state.HasActiveChallenge &&
        state.CorrelationCookieCount == 1,
        "Eine vorhandene OIDC-Korrelation wurde nicht als aktive Challenge erkannt.");

    return Task.CompletedTask;
}

static Task TestAp02ChallengeGuardIgnoresUnrelatedCookiesAsync()
{
    var context =
        new DefaultHttpContext();
    context.Request.Headers.Cookie =
        "unrelated=value; another=opaque";

    var state =
        new OidcChallengeGuard()
            .Inspect(
                context.Request);

    Assert(
        !state.HasActiveChallenge &&
        state.CorrelationCookieCount == 0,
        "Unbeteiligte Cookies lösen fälschlich die OIDC-Parallel-Challenge-Sperre aus.");

    return Task.CompletedTask;
}

static Task TestAp02LoginEndpointBlocksParallelChallengeAsync()
{
    var programSource =
        ReadProjectSource(
            "WebUI.Web/Program.cs");
    var handlerSource =
        ReadProjectSource(
            "WebUI.Web/Services/OidcChallengeAuthenticationHandler.cs");

    Assert(
        programSource.Contains(
            "options.DefaultChallengeScheme =\n                OidcChallengeAuthenticationHandler.SchemeName;",
            StringComparison.Ordinal) &&
        programSource.Contains(
            "app.MapGet(\n        \"/auth/login\",\n        () => Results.Challenge(",
            StringComparison.Ordinal) &&
        handlerSource.Contains(
            "guard.TryAcquire(",
            StringComparison.Ordinal) &&
        handlerSource.Contains(
            "OidcChallengeAcquireResult.Blocked",
            StringComparison.Ordinal) &&
        handlerSource.IndexOf(
            "OidcChallengeAcquireResult.Blocked",
            StringComparison.Ordinal) <
        handlerSource.IndexOf(
            "Context.ChallengeAsync(",
            StringComparison.Ordinal),
        "Der Default-Challenge-Pfad wird nicht eindeutig vor dem OIDC-Challenge-Aufruf atomar gesperrt.");

    return Task.CompletedTask;
}

static Task TestAp02StartLoginUiGuardAsync()
{
    var startSource =
        ReadProjectSource(
            "WebUI.Web/Components/Pages/Start.razor");

    Assert(
        startSource.Contains(
            "disabled=\"@_isLoginStarting\"",
            StringComparison.Ordinal) &&
        startSource.Contains(
            "@onclick=\"StartLoginAsync\"",
            StringComparison.Ordinal) &&
        startSource.Contains(
            "Anmeldung wird gestartet …",
            StringComparison.Ordinal) &&
        startSource.Contains(
            "if (_isLoginStarting)",
            StringComparison.Ordinal) &&
        startSource.Contains(
            "Navigation.NavigateTo(",
            StringComparison.Ordinal) &&
        startSource.Contains(
            "\"/auth/login\"",
            StringComparison.Ordinal) &&
        startSource.Contains(
            "forceLoad: true",
            StringComparison.Ordinal),
        "Die Startseite sperrt den Anmeldebutton beim ersten Klick nicht eindeutig.");

    return Task.CompletedTask;
}

static Task TestAp02ChallengeGuardDoesNotReadCookieValuesAsync()
{
    var guardSource =
        ReadProjectSource(
            "WebUI.Web/Services/OidcChallengeGuard.cs");
    var handlerSource =
        ReadProjectSource(
            "WebUI.Web/Services/OidcChallengeAuthenticationHandler.cs");

    Assert(
        guardSource.Contains(
            "request.Cookies.Keys",
            StringComparison.Ordinal) &&
        !guardSource.Contains(
            "request.Cookies[",
            StringComparison.Ordinal) &&
        !handlerSource.Contains(
            "BrowserContextCookieName}",
            StringComparison.Ordinal) &&
        handlerSource.Contains(
            "Aktive Korrelationscookies: {CorrelationCookieCount}",
            StringComparison.Ordinal) &&
        !handlerSource.Contains(
            "ChallengeBlockedParallel; Cookie",
            StringComparison.Ordinal),
        "Der AP02-Challenge-Guard greift auf Cookie-Inhalte zu oder würde Cookie-Inhalte diagnostisch protokollieren.");

    return Task.CompletedTask;
}

static Task TestAp023ValidatorPresentAsync()
{
    var source =
        ReadProjectSource(
            "Containerbetrieb/validate-runtime-config.sh");

    Assert(
        source.StartsWith(
            "#!/bin/sh\nset -eu\n",
            StringComparison.Ordinal) &&
        source.Contains(
            "validate_positive_decimal_id",
            StringComparison.Ordinal) &&
        !source.Contains(
            "docker ",
            StringComparison.Ordinal) &&
        !source.Contains(
            "secrets",
            StringComparison.OrdinalIgnoreCase),
        "Das AP02.3-Prüfskript fehlt, ist nicht POSIX-sh-basiert oder greift über seinen Prüfauftrag hinaus.");

    return Task.CompletedTask;
}

static async Task TestAp023ValidatorAcceptsPositiveIdsAsync()
{
    await ExpectRuntimeIdValidationAsync(
        "1654",
        "1654",
        expectSuccess: true);
    await ExpectRuntimeIdValidationAsync(
        "1",
        "65534",
        expectSuccess: true);
}

static Task TestAp023ValidatorRejectsMissingUidAsync()
{
    return ExpectRuntimeIdValidationAsync(
        null,
        "1654",
        expectSuccess: false);
}

static Task TestAp023ValidatorRejectsMissingGidAsync()
{
    return ExpectRuntimeIdValidationAsync(
        "1654",
        null,
        expectSuccess: false);
}

static async Task TestAp023ValidatorRejectsZeroAsync()
{
    await ExpectRuntimeIdValidationAsync(
        "0",
        "1654",
        expectSuccess: false);
    await ExpectRuntimeIdValidationAsync(
        "1654",
        "0",
        expectSuccess: false);
}

static async Task TestAp023ValidatorRejectsSignedValuesAsync()
{
    await ExpectRuntimeIdValidationAsync(
        "-1",
        "1654",
        expectSuccess: false);
    await ExpectRuntimeIdValidationAsync(
        "+1654",
        "1654",
        expectSuccess: false);
}

static async Task TestAp023ValidatorRejectsWhitespaceAndTextAsync()
{
    await ExpectRuntimeIdValidationAsync(
        " 1654",
        "1654",
        expectSuccess: false);
    await ExpectRuntimeIdValidationAsync(
        "1654 ",
        "1654",
        expectSuccess: false);
    await ExpectRuntimeIdValidationAsync(
        "abc",
        "1654",
        expectSuccess: false);
}

static Task TestAp023ValidatorRejectsLeadingZeroAsync()
{
    return ExpectRuntimeIdValidationAsync(
        "01654",
        "1654",
        expectSuccess: false);
}

static Task TestAp023ComposeUserBindingAsync()
{
    var compose =
        ReadProjectSource(
            "Containerbetrieb/compose.yaml.example");

    Assert(
        compose.Contains(
            "user: \"${APP_UID:?APP_UID muss gesetzt sein}:${APP_GID:?APP_GID muss gesetzt sein}\"",
            StringComparison.Ordinal),
        "Compose verlangt APP_UID/APP_GID nicht mehr explizit oder bindet den Laufzeitbenutzer nicht eindeutig.");

    return Task.CompletedTask;
}

static Task TestAp023SecretsRemainReadOnlyAsync()
{
    var compose =
        ReadProjectSource(
            "Containerbetrieb/compose.yaml.example");

    var secretMount =
        ExtractSourceBlock(
            compose,
            "      - type: bind\n        source: ${WEBUI_RUNTIME_ROOT:-.}/secrets",
            "      - type: bind\n        source: ${WEBUI_RUNTIME_ROOT:-.}/navigation-cache");

    Assert(
        secretMount.Contains(
            "target: /run/secrets",
            StringComparison.Ordinal) &&
        secretMount.Contains(
            "read_only: true",
            StringComparison.Ordinal),
        "Der Secret-Mount ist nicht mehr eindeutig read-only.");

    return Task.CompletedTask;
}

static Task TestAp023RootFilesystemRemainsReadOnlyAsync()
{
    var compose =
        ReadProjectSource(
            "Containerbetrieb/compose.yaml.example");

    Assert(
        compose.Contains(
            "    read_only: true",
            StringComparison.Ordinal),
        "Das Container-Root-Dateisystem ist nicht mehr read-only.");

    return Task.CompletedTask;
}

static Task TestAp023ContainerSecurityOptionsRemainAsync()
{
    var compose =
        ReadProjectSource(
            "Containerbetrieb/compose.yaml.example");

    Assert(
        compose.Contains(
            "      - no-new-privileges:true",
            StringComparison.Ordinal) &&
        compose.Contains(
            "    cap_drop:\n      - ALL",
            StringComparison.Ordinal),
        "no-new-privileges oder cap_drop ALL fehlen.");

    return Task.CompletedTask;
}

static Task TestAp023PersistentWritableBindsRemainAsync()
{
    var compose =
        ReadProjectSource(
            "Containerbetrieb/compose.yaml.example");

    var expectedSources = new[]
    {
        "${WEBUI_RUNTIME_ROOT:-.}/navigation-cache",
        "${WEBUI_RUNTIME_ROOT:-.}/diagnostic/oidc",
        "${WEBUI_RUNTIME_ROOT:-.}/diagnostic/performance",
        "${WEBUI_RUNTIME_ROOT:-.}/user-tokens",
        "${WEBUI_RUNTIME_ROOT:-.}/data-protection-keys"
    };

    foreach (var source in expectedSources)
    {
        Assert(
            compose.Contains(
                $"source: {source}",
                StringComparison.Ordinal),
            $"Erwarteter persistenter RW-Bind fehlt: {source}");
    }

    Assert(
        compose.Contains(
            "source: ${WEBUI_RUNTIME_ROOT:-.}/config\n"
            + "        target: /data/config\n"
            + "        read_only: true",
            StringComparison.Ordinal),
        "Display-Präfix-Konfiguration bleibt als read-only Bind-Mount eingebunden.");

    var bindCount =
        compose.Split(
            "      - type: bind",
            StringSplitOptions.None).Length - 1;

    Assert(
        bindCount == 7,
        "Compose enthält nicht mehr exakt sieben Bind-Mounts (zwei RO plus fünf persistente RW-Pfade).");

    return Task.CompletedTask;
}

static Task TestAp023DockerfileNonRootDefaultAsync()
{
    var dockerfile =
        ReadProjectSource(
            "Containerbetrieb/Dockerfile");

    Assert(
        dockerfile.Contains(
            "\nUSER 1654\n",
            StringComparison.Ordinal) &&
        !dockerfile.Contains(
            "\nUSER 0\n",
            StringComparison.Ordinal) &&
        !dockerfile.Contains(
            "\nUSER root\n",
            StringComparison.OrdinalIgnoreCase),
        "Der Dockerfile-Default ist nicht mehr eindeutig nicht privilegiert.");

    return Task.CompletedTask;
}

static async Task ExpectRuntimeIdValidationAsync(
    string? uid,
    string? gid,
    bool expectSuccess)
{
    var sourceRoot =
        FindSourceRoot();
    var scriptPath =
        Path.Combine(
            sourceRoot,
            "Containerbetrieb",
            "validate-runtime-config.sh");

    var startInfo =
        new ProcessStartInfo(
            "/bin/sh",
            scriptPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

    startInfo.Environment.Remove("APP_UID");
    startInfo.Environment.Remove("APP_GID");

    if (uid is not null)
    {
        startInfo.Environment["APP_UID"] = uid;
    }

    if (gid is not null)
    {
        startInfo.Environment["APP_GID"] = gid;
    }

    using var process =
        Process.Start(startInfo) ??
        throw new InvalidOperationException(
            "AP02.3-Prüfskript konnte nicht gestartet werden.");

    await process.WaitForExitAsync();

    Assert(
        expectSuccess
            ? process.ExitCode == 0
            : process.ExitCode != 0,
        "AP02.3 UID/GID-Prüfung lieferte für den Testfall nicht das erwartete Ergebnis.");
}

static async Task TestAp03OidcAtomicAcquireAsync()
{
    var guard =
        new OidcChallengeGuard();
    var now =
        DateTimeOffset.UtcNow;
    var browserKey =
        Guid.NewGuid().ToString("N");

    var attempts =
        Enumerable.Range(
                0,
                32)
            .Select(
                _ =>
                    Task.Run(
                        () =>
                            guard.TryAcquire(
                                browserKey,
                                now)))
            .ToArray();

    var results =
        await Task.WhenAll(
            attempts);

    Assert(
        results.Count(
            result =>
                result ==
                OidcChallengeAcquireResult.Acquired) == 1 &&
        results.Count(
            result =>
                result ==
                OidcChallengeAcquireResult.Blocked) == 31,
        "Parallele Acquire-Versuche desselben Browserkontexts wurden nicht atomar auf genau einen Gewinner begrenzt.");
}

static Task TestAp03OidcDifferentBrowserContextsAsync()
{
    var guard =
        new OidcChallengeGuard();
    var now =
        DateTimeOffset.UtcNow;

    Assert(
        guard.TryAcquire(
            Guid.NewGuid().ToString("N"),
            now) ==
            OidcChallengeAcquireResult.Acquired &&
        guard.TryAcquire(
            Guid.NewGuid().ToString("N"),
            now) ==
            OidcChallengeAcquireResult.Acquired,
        "Verschiedene Browserkontexte beeinflussen sich im OIDC-Guard gegenseitig.");

    return Task.CompletedTask;
}

static Task TestAp03OidcReleaseAsync()
{
    var guard =
        new OidcChallengeGuard();
    var now =
        DateTimeOffset.UtcNow;
    var browserKey =
        Guid.NewGuid().ToString("N");

    Assert(
        guard.TryAcquire(
            browserKey,
            now) ==
            OidcChallengeAcquireResult.Acquired &&
        guard.Release(
            browserKey) &&
        guard.TryAcquire(
            browserKey,
            now) ==
            OidcChallengeAcquireResult.Acquired,
        "Release gibt denselben Browserkontext nicht unmittelbar wieder frei.");

    return Task.CompletedTask;
}

static Task TestAp03OidcExpiryAsync()
{
    var guard =
        new OidcChallengeGuard();
    var now =
        DateTimeOffset.UtcNow;
    var browserKey =
        Guid.NewGuid().ToString("N");

    Assert(
        guard.TryAcquire(
            browserKey,
            now) ==
            OidcChallengeAcquireResult.Acquired &&
        guard.TryAcquire(
            browserKey,
            now.Add(
                OidcChallengeGuard.LeaseLifetime)
                .AddMilliseconds(1)) ==
            OidcChallengeAcquireResult.ExpiredReplaced,
        "Eine abgelaufene OIDC-Challenge-Lease wird nicht kontrolliert ersetzt.");

    return Task.CompletedTask;
}

static Task TestAp03OidcReleaseHooksAsync()
{
    var programSource =
        ReadProjectSource(
            "WebUI.Web/Program.cs");

    var tokenValidatedBlock =
        ExtractSourceBlock(
            programSource,
            "options.Events.OnTokenValidated = context =>",
            "options.Events.OnRemoteFailure = async context =>");

    var remoteFailureBlock =
        ExtractSourceBlock(
            programSource,
            "options.Events.OnRemoteFailure = async context =>",
            "});\n\n    builder.Services.AddAuthorization();");

    Assert(
        tokenValidatedBlock.Contains(
            "challengeGuard.Release(",
            StringComparison.Ordinal) &&
        tokenValidatedBlock.Contains(
            "Grund: TokenValidated",
            StringComparison.Ordinal) &&
        remoteFailureBlock.Contains(
            "challengeGuard.Release(",
            StringComparison.Ordinal) &&
        remoteFailureBlock.Contains(
            "Grund: RemoteFailure",
            StringComparison.Ordinal),
        "Die OIDC-Challenge-Lease wird bei Erfolg oder kontrolliertem RemoteFailure nicht eindeutig freigegeben.");

    return Task.CompletedTask;
}

static Task TestAp03OidcGuardKeyNotLoggedAsync()
{
    var guardSource =
        ReadProjectSource(
            "WebUI.Web/Services/OidcChallengeGuard.cs");
    var handlerSource =
        ReadProjectSource(
            "WebUI.Web/Services/OidcChallengeAuthenticationHandler.cs");
    var programSource =
        ReadProjectSource(
            "WebUI.Web/Program.cs");

    Assert(
        guardSource.Contains(
            "BrowserContextCookieName",
            StringComparison.Ordinal) &&
        !handlerSource.Contains(
            "{BrowserContextKey}",
            StringComparison.Ordinal) &&
        !programSource.Contains(
            "{BrowserContextKey}",
            StringComparison.Ordinal),
        "Der pseudonyme OIDC-Guard-Schlüssel wird diagnostisch ausgegeben.");

    var searchIndex = 0;
    while (true)
    {
        var logIndex =
            handlerSource.IndexOf(
                "diagnosticLogger.Log",
                searchIndex,
                StringComparison.Ordinal);

        if (logIndex < 0)
        {
            break;
        }

        var callEnd =
            handlerSource.IndexOf(
                ");",
                logIndex,
                StringComparison.Ordinal);

        Assert(
            callEnd >= 0,
            "Ein OIDC-Diagnoseaufruf konnte nicht eindeutig abgegrenzt werden.");

        var diagnosticCall =
            handlerSource[
                logIndex..
                (callEnd + 2)];

        Assert(
            !diagnosticCall.Contains(
                "browserContextKey",
                StringComparison.OrdinalIgnoreCase),
            "Der pseudonyme OIDC-Guard-Schlüssel wird als Argument eines Diagnoseaufrufs verwendet.");

        searchIndex =
            callEnd + 2;
    }

    return Task.CompletedTask;
}


static Task TestAp06DockerDesktopProxyResolutionSourceAsync()
{
    var source =
        ReadProjectSource(
            "WebUI.Web/Services/ForwardedHeadersProxyTrust.cs");

    Assert(
        source.Contains(
            "gateway.docker.internal",
            StringComparison.Ordinal) &&
        source.Contains(
            "Dns.GetHostAddresses(",
            StringComparison.Ordinal) &&
        source.Contains(
            "catch (SocketException)",
            StringComparison.Ordinal) &&
        source.Contains(
            "return ParseRequiredDefaultGateway(routeLines);",
            StringComparison.Ordinal),
        "Die Docker-Desktop-Proxy-Ermittlung oder der sichere Rückfall auf die bestehende Linux-/Synology-Ermittlung fehlt.");

    return Task.CompletedTask;
}

static Task TestAp06OidcFirstLoginContinuesWithoutContextPageAsync()
{
    var context =
        new DefaultHttpContext();
    var guard =
        new OidcChallengeGuard();

    var firstKey =
        guard.EnsureBrowserContextCookie(
            context.Request,
            context.Response);
    var secondKey =
        guard.EnsureBrowserContextCookie(
            context.Request,
            context.Response);

    Assert(
        firstKey == secondKey &&
        Guid.TryParseExact(
            firstKey,
            "N",
            out _) &&
        context.Items.TryGetValue(
            OidcChallengeGuard.BrowserContextItemKey,
            out var storedValue) &&
        string.Equals(
            storedValue as string,
            firstKey,
            StringComparison.Ordinal),
        "Der im ersten Request vorbereitete Browserkontext wird nicht stabil im selben Request weiterverwendet.");

    var handlerSource =
        ReadProjectSource(
            "WebUI.Web/Services/OidcChallengeAuthenticationHandler.cs");

    Assert(
        !handlerSource.Contains(
            "Anmeldekontext vorbereitet",
            StringComparison.Ordinal) &&
        !handlerSource.Contains(
            "ChallengeContextEstablished",
            StringComparison.Ordinal) &&
        handlerSource.Contains(
            "browserContextKey =\n                guard.EnsureBrowserContextCookie(",
            StringComparison.Ordinal),
        "Der erste OIDC-Login enthält weiterhin die 409-Zwischenseite oder setzt den vorbereiteten Browserkontext nicht direkt fort.");

    return Task.CompletedTask;
}

static Task TestAp03OidcBrowserContextCookieIsAlwaysSecureAsync()
{
    var context =
        new DefaultHttpContext();
    context.Request.Scheme = "http";

    var guard =
        new OidcChallengeGuard();

    guard.EnsureBrowserContextCookie(
        context.Request,
        context.Response);

    var setCookieHeader =
        context.Response.Headers.SetCookie.ToString();

    Assert(
        OidcChallengeGuard.BrowserContextCookieName.StartsWith(
            "__Host-",
            StringComparison.Ordinal) &&
        setCookieHeader.Contains(
            $"{OidcChallengeGuard.BrowserContextCookieName}=",
            StringComparison.Ordinal) &&
        setCookieHeader.Contains(
            "; secure",
            StringComparison.OrdinalIgnoreCase),
        "Der OIDC-Browserkontextcookie mit __Host-Präfix wird nicht unabhängig vom erkannten Request-Schema als Secure gesetzt.");

    return Task.CompletedTask;
}

static Task TestAp03StartWrapperPresentAsync()
{
    var source =
        ReadProjectSource(
            "Containerbetrieb/start-container.sh");

    Assert(
        source.StartsWith(
            "#!/bin/sh\nset -eu\n",
            StringComparison.Ordinal) &&
        source.Contains(
            "validate-runtime-config.sh",
            StringComparison.Ordinal) &&
        source.Contains(
            "\"$DOCKER_BIN\" compose",
            StringComparison.Ordinal),
        "Der AP03-Start-Wrapper fehlt oder ist nicht als begrenzter POSIX-sh-Startweg aufgebaut.");

    return Task.CompletedTask;
}

static Task TestAp03StartWrapperOrdersValidationBeforeDockerAsync()
{
    var source =
        ReadProjectSource(
            "Containerbetrieb/start-container.sh");

    var validatorIndex =
        source.IndexOf(
            "/bin/sh \"$VALIDATOR\"",
            StringComparison.Ordinal);
    var dockerIndex =
        source.IndexOf(
            "\"$DOCKER_BIN\" compose",
            StringComparison.Ordinal);

    Assert(
        validatorIndex >= 0 &&
        dockerIndex > validatorIndex,
        "Der Start-Wrapper ruft Docker nicht eindeutig erst nach erfolgreicher UID/GID-Validierung auf.");

    return Task.CompletedTask;
}

static async Task TestAp03StartWrapperBlocksDockerOnInvalidUidAsync()
{
    var result =
        await RunStartWrapperWithFakeDockerAsync(
            "0",
            "1654");

    Assert(
        result.ExitCode != 0 &&
        result.DockerCalls == 0,
        "Ungültige UID erreicht trotz fehlgeschlagener Validierung einen Docker-Aufruf.");
}

static async Task TestAp03StartWrapperAllowsDockerAfterValidationAsync()
{
    var result =
        await RunStartWrapperWithFakeDockerAsync(
            "1654",
            "1654");

    Assert(
        result.ExitCode == 0 &&
        result.DockerCalls == 2,
        "Gültige UID/GID erreicht nicht genau die vorgesehenen Compose-Aufrufe für Start und Status.");
}

static async Task TestAp03StartWrapperRequiresProjectNameAsync()
{
    var sourceRoot =
        FindSourceRoot();
    var wrapper =
        Path.Combine(
            sourceRoot,
            "Containerbetrieb",
            "start-container.sh");

    var startInfo =
        new ProcessStartInfo(
            "/bin/sh",
            wrapper)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

    using var process =
        Process.Start(startInfo) ??
        throw new InvalidOperationException(
            "AP03-Start-Wrapper konnte nicht gestartet werden.");

    await process.WaitForExitAsync();

    Assert(
        process.ExitCode != 0,
        "Der Start-Wrapper akzeptiert einen Start ohne expliziten Compose-Projektnamen.");
}

static Task TestAp03ReadmeUsesStartWrapperAsync()
{
    var readme =
        ReadProjectSource(
            "Containerbetrieb/README_CONTAINERBETRIEB.md");

    var startSection =
        ExtractSourceBlock(
            readme,
            "## 17. Start und Statusprüfung",
            "## 18. Funktionsprüfung");

    Assert(
        startSection.Contains(
            "./start-container.sh webui-",
            StringComparison.Ordinal) &&
        startSection.Contains(
            "nicht der freigegebene Standardstartweg",
            StringComparison.Ordinal),
        "Die Betriebsdokumentation erzwingt den Start-Wrapper nicht eindeutig als Standardstartweg.");

    return Task.CompletedTask;
}

static async Task<(int ExitCode, int DockerCalls)>
    RunStartWrapperWithFakeDockerAsync(
        string uid,
        string gid)
{
    var sourceRoot =
        FindSourceRoot();
    var sourceContainer =
        Path.Combine(
            sourceRoot,
            "Containerbetrieb");
    var tempRoot =
        Path.Combine(
            Path.GetTempPath(),
            "webui-ap03-start-" +
            Guid.NewGuid().ToString("N"));
    var fakeBin =
        Path.Combine(
            tempRoot,
            "bin");
    var dockerLog =
        Path.Combine(
            tempRoot,
            "docker-calls.txt");

    Directory.CreateDirectory(
        fakeBin);

    try
    {
        File.Copy(
            Path.Combine(
                sourceContainer,
                "start-container.sh"),
            Path.Combine(
                tempRoot,
                "start-container.sh"));
        File.Copy(
            Path.Combine(
                sourceContainer,
                "validate-runtime-config.sh"),
            Path.Combine(
                tempRoot,
                "validate-runtime-config.sh"));
        File.WriteAllText(
            Path.Combine(
                tempRoot,
                ".env"),
            $"APP_UID={uid}\nAPP_GID={gid}\n");
        File.WriteAllText(
            Path.Combine(
                tempRoot,
                "compose.yaml"),
            "services:\n  webui:\n    image: synthetic:test\n");

        var fakeDocker =
            Path.Combine(
                fakeBin,
                "docker");
        File.WriteAllText(
            fakeDocker,
            """
            #!/bin/sh
            printf '%s\n' "$*" >> "$AP03_FAKE_DOCKER_LOG"
            exit 0
            """);

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                fakeDocker,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.UserExecute);
        }

        var startInfo =
            new ProcessStartInfo(
                "/bin/sh")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

        startInfo.ArgumentList.Add(
            Path.Combine(
                tempRoot,
                "start-container.sh"));

        startInfo.ArgumentList.Add(
            "webui-test-r1");
        startInfo.Environment["PATH"] =
            fakeBin +
            Path.PathSeparator +
            (Environment.GetEnvironmentVariable("PATH") ?? string.Empty);
        startInfo.Environment["AP03_FAKE_DOCKER_LOG"] =
            dockerLog;

        using var process =
            Process.Start(startInfo) ??
            throw new InvalidOperationException(
                "AP03-Start-Wrapper konnte nicht gestartet werden.");

        var stdoutTask =
            process.StandardOutput.ReadToEndAsync();
        var stderrTask =
            process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();
        _ = await stdoutTask;
        _ = await stderrTask;

        var dockerCalls =
            File.Exists(
                dockerLog)
                ? File.ReadAllLines(
                    dockerLog).Length
                : 0;

        return (
            process.ExitCode,
            dockerCalls);
    }
    finally
    {
        if (Directory.Exists(
                tempRoot))
        {
            Directory.Delete(
                tempRoot,
                recursive: true);
        }
    }
}

static Task TestQ02PaperlessHandlerDisablesCookiesAsync()
{
    var programSource = ReadProjectSource("WebUI.Web/Program.cs");
    var registrationBlock = ExtractSourceBlock(
        programSource,
        "builder.Services\n    .AddHttpClient(PaperlessClientFactory.HttpClientName)",
        "if (oidcSettings.Enabled)");

    Assert(
        registrationBlock.Contains("new SocketsHttpHandler", StringComparison.Ordinal),
        "Der benannte Paperless-Client besitzt keinen expliziten Primärhandler.");
    Assert(
        registrationBlock.Contains("UseCookies = false", StringComparison.Ordinal),
        "Cookies sind für den gepoolten Paperless-Handler nicht ausdrücklich deaktiviert.");

    return Task.CompletedTask;
}

static async Task TestQ02AuthorizationHeadersRemainSeparatedAsync()
{
    const string tokenA = "synthetic-token-a";
    const string tokenB = "synthetic-token-b";

    using var handlerA = new AuthorizationCaptureHandler();
    using var handlerB = new AuthorizationCaptureHandler();
    using var httpClientA = new HttpClient(handlerA, disposeHandler: false);
    using var httpClientB = new HttpClient(handlerB, disposeHandler: false);

    var clientA = new WebUI.Infrastructure.PaperlessApiClient(
        httpClientA,
        "https://paperless.invalid",
        tokenA);
    var clientB = new WebUI.Infrastructure.PaperlessApiClient(
        httpClientB,
        "https://paperless.invalid",
        tokenB);

    await clientA.ValidateCurrentTokenAsync();
    await clientB.ValidateCurrentTokenAsync();

    Assert(
        string.Equals(handlerA.AuthorizationScheme, "Token", StringComparison.Ordinal) &&
        string.Equals(handlerA.AuthorizationParameter, tokenA, StringComparison.Ordinal),
        "Client A hat nicht ausschließlich sein eigenes synthetisches Token verwendet.");
    Assert(
        string.Equals(handlerB.AuthorizationScheme, "Token", StringComparison.Ordinal) &&
        string.Equals(handlerB.AuthorizationParameter, tokenB, StringComparison.Ordinal),
        "Client B hat nicht ausschließlich sein eigenes synthetisches Token verwendet.");
    Assert(
        !string.Equals(handlerA.AuthorizationParameter, handlerB.AuthorizationParameter, StringComparison.Ordinal),
        "Die synthetischen Benutzer-Tokens wurden zwischen logischen Clients vermischt.");
}

static Task TestQ02ProxyGuardSpecialClientRemainsScopedAsync()
{
    var source = ReadProjectSource(
        "WebUI.Web/Services/ProxyGuardHealthCheckRunner.cs");

    Assert(
        source.Contains("using var handler = new SocketsHttpHandler", StringComparison.Ordinal),
        "Der bewusst lokale Proxy-Guard-Handler wurde unerwartet verändert.");
    Assert(
        source.Contains("using var client = new HttpClient(handler)", StringComparison.Ordinal),
        "Der bewusst lokale Proxy-Guard-HttpClient wurde unerwartet verändert.");

    return Task.CompletedTask;
}

static Task TestOidcDiagnosticsFileLoggingDisabledAsync()
{
    var settings =
        OidcDiagnosticsFileLoggerSettings.Load(
            Configuration());

    Assert(
        !settings.Enabled,
        "Dateiprotokollierung muss ohne LogDirectory deaktiviert sein.");
    Assert(
        settings.RetentionDays == 7,
        "Standardaufbewahrung muss sieben Tage betragen.");

    return Task.CompletedTask;
}

static Task TestOidcDiagnosticsFileLoggingInvalidConfigurationAsync()
{
    var relativePathRejected = false;

    try
    {
        _ = OidcDiagnosticsFileLoggerSettings.Load(
            Configuration(
                new Dictionary<string, string?>
                {
                    ["WebUi:OidcDiagnostics:LogDirectory"] =
                        "relative/oidc-diagnostics"
                }));
    }
    catch (InvalidOperationException)
    {
        relativePathRejected = true;
    }

    Assert(
        relativePathRejected,
        "Relatives OIDC-Diagnoseverzeichnis muss abgelehnt werden.");

    var retentionRejected = false;

    try
    {
        _ = OidcDiagnosticsFileLoggerSettings.Load(
            Configuration(
                new Dictionary<string, string?>
                {
                    ["WebUi:OidcDiagnostics:LogDirectory"] =
                        Path.GetTempPath(),
                    ["WebUi:OidcDiagnostics:RetentionDays"] =
                        "0"
                }));
    }
    catch (InvalidOperationException)
    {
        retentionRejected = true;
    }

    Assert(
        retentionRejected,
        "Aufbewahrungsdauer kleiner eins muss abgelehnt werden.");

    return Task.CompletedTask;
}

static Task TestOidcDiagnosticsFileLoggingCategoryAsync()
{
    using var directory =
        TemporaryDirectoryFixture.Create(
            "oidc-diagnostics-category-test");
    var settings =
        OidcDiagnosticsFileLoggerSettings.Load(
            Configuration(
                new Dictionary<string, string?>
                {
                    ["WebUi:OidcDiagnostics:LogDirectory"] =
                        directory.Path,
                    ["WebUi:OidcDiagnostics:RetentionDays"] =
                        "7"
                }));

    using var provider =
        new OidcDiagnosticsFileLoggerProvider(
            settings);

    provider
        .CreateLogger(
            OidcDiagnosticsFileLoggerProvider.LoggerCategory)
        .LogInformation(
            "OIDC-DIAG Testsicherheitsmeldung");

    provider
        .CreateLogger(
            "Andere.Kategorie")
        .LogWarning(
            "DARF_NICHT_IN_OIDC_DATEI");

    var content =
        string.Join(
            "\n",
            Directory.EnumerateFiles(
                    directory.Path,
                    "oidc-diagnostics-*.log",
                    SearchOption.TopDirectoryOnly)
                .Select(File.ReadAllText));

    Assert(
        content.Contains(
            "OIDC-DIAG Testsicherheitsmeldung",
            StringComparison.Ordinal),
        "Sichere OIDC-Diagnosemeldung fehlt.");
    Assert(
        !content.Contains(
            "DARF_NICHT_IN_OIDC_DATEI",
            StringComparison.Ordinal),
        "Fremde Logger-Kategorie wurde unerwartet persistiert.");

    return Task.CompletedTask;
}

static Task TestOidcDiagnosticsFileLoggingRetentionAsync()
{
    using var directory =
        TemporaryDirectoryFixture.Create(
            "oidc-diagnostics-retention-test");
    var expiredPath =
        Path.Combine(
            directory.Path,
            "oidc-diagnostics-expired.log");
    var currentPath =
        Path.Combine(
            directory.Path,
            "oidc-diagnostics-current.log");

    File.WriteAllText(
        expiredPath,
        "alt");
    File.SetLastWriteTimeUtc(
        expiredPath,
        DateTime.UtcNow.AddDays(-8));

    File.WriteAllText(
        currentPath,
        "aktuell");
    File.SetLastWriteTimeUtc(
        currentPath,
        DateTime.UtcNow.AddDays(-2));

    var settings =
        OidcDiagnosticsFileLoggerSettings.Load(
            Configuration(
                new Dictionary<string, string?>
                {
                    ["WebUi:OidcDiagnostics:LogDirectory"] =
                        directory.Path,
                    ["WebUi:OidcDiagnostics:RetentionDays"] =
                        "7"
                }));

    using var provider =
        new OidcDiagnosticsFileLoggerProvider(
            settings);

    provider
        .CreateLogger(
            OidcDiagnosticsFileLoggerProvider.LoggerCategory)
        .LogInformation(
            "OIDC-DIAG Retentionstest");

    Assert(
        !File.Exists(expiredPath),
        "OIDC-Diagnosebestand älter als sieben Tage wurde nicht entfernt.");
    Assert(
        File.Exists(currentPath),
        "Aktueller OIDC-Diagnosebestand wurde unerwartet entfernt.");

    return Task.CompletedTask;
}

static Task TestOidcDiagnosticsContainsVersionAsync()
{
    using var d=TemporaryDirectoryFixture.Create("oidc-version-test");
    var settings=OidcDiagnosticsFileLoggerSettings.Load(Configuration(new Dictionary<string,string?> { ["WebUi:OidcDiagnostics:LogDirectory"]=d.Path,["WebUi:OidcDiagnostics:RetentionDays"]="7" }));
    using var provider=new OidcDiagnosticsFileLoggerProvider(settings);
    provider.CreateLogger(OidcDiagnosticsFileLoggerProvider.LoggerCategory).LogInformation("OIDC-DIAG Versionstest");
    var content=string.Join("\n",Directory.EnumerateFiles(d.Path,"oidc-diagnostics-*.log").Select(File.ReadAllText));
    Assert(content.Contains($"Version={ApplicationDisplayInfo.FullVersion}",StringComparison.Ordinal),"OIDC-Version fehlt.");
    return Task.CompletedTask;
}

static Task TestPerformanceDiagnosticsInvalidConfigurationAsync()
{
    var badPath=false; try { PerformanceDiagnosticsLog.Configure("relative/performance",7,ApplicationDisplayInfo.FullVersion); } catch(InvalidOperationException){ badPath=true; }
    Assert(badPath,"Relativer Performance-Pfad muss abgelehnt werden.");
    var badRetention=false; try { PerformanceDiagnosticsLog.Configure(Path.GetTempPath(),0,ApplicationDisplayInfo.FullVersion); } catch(InvalidOperationException){ badRetention=true; }
    Assert(badRetention,"Retention <1 muss abgelehnt werden.");
    return Task.CompletedTask;
}

static async Task TestPerformanceDiagnosticsDailyFileAsync()
{
    using var d=TemporaryDirectoryFixture.Create("performance-daily-test");
    PerformanceDiagnosticsLog.Configure(d.Path,7,ApplicationDisplayInfo.FullVersion);
    var run=PerformanceDiagnosticsLog.CreateRunId();
    PerformanceDiagnosticsLog.Write(run,"TEST","DAILY","OK",1.0);
    var path=Path.Combine(d.Path,$"performance-diagnostics-{DateTimeOffset.UtcNow:yyyy-MM-dd}.log");
    for(var i=0;i<50 && !File.Exists(path);i++) await Task.Delay(20);
    Assert(File.Exists(path),"Performance-Tagesdatei fehlt.");
    string content=""; for(var i=0;i<50;i++){ content=File.ReadAllText(path); if(content.Contains("DAILY",StringComparison.Ordinal)) break; await Task.Delay(20); }
    Assert(content.Contains("ZeitUtc\tVersion\tMesslauf\tKlasse\tOperation\tErgebnis\tDauerMs\tDetails",StringComparison.Ordinal),"Performance-Kopf fehlt.");
    Assert(content.Contains(ApplicationDisplayInfo.FullVersion,StringComparison.Ordinal),"Performance-Version fehlt.");
    Assert(run.StartsWith("PERF-",StringComparison.Ordinal),"Messlaufkennung ist nicht PERF-.");
    Assert(!content.Contains("Q08",StringComparison.OrdinalIgnoreCase),"Q08 steht noch im Performance-Log.");
}

static async Task TestPerformanceDiagnosticsRetentionAsync()
{
    using var d=TemporaryDirectoryFixture.Create("performance-retention-test");
    var old=Path.Combine(d.Path,"performance-diagnostics-old.log"); var current=Path.Combine(d.Path,"performance-diagnostics-current.log"); var foreign=Path.Combine(d.Path,"fremd.log");
    File.WriteAllText(old,"alt"); File.SetLastWriteTimeUtc(old,DateTime.UtcNow.AddDays(-8));
    File.WriteAllText(current,"aktuell"); File.SetLastWriteTimeUtc(current,DateTime.UtcNow.AddDays(-2));
    File.WriteAllText(foreign,"fremd"); File.SetLastWriteTimeUtc(foreign,DateTime.UtcNow.AddDays(-20));
    PerformanceDiagnosticsLog.Configure(d.Path,7,ApplicationDisplayInfo.FullVersion); PerformanceDiagnosticsLog.Write(null,"TEST","RETENTION","OK",0);
    for(var i=0;i<50 && File.Exists(old);i++) await Task.Delay(20);
    Assert(!File.Exists(old),"Alte Performance-Datei wurde nicht gelöscht."); Assert(File.Exists(current),"Aktuelle Performance-Datei wurde gelöscht."); Assert(File.Exists(foreign),"Fremde Datei wurde gelöscht.");
}

static Task TestContainerbetriebComposeDiagnosticsPathsAsync()
{
    var c=File.ReadAllText(Path.Combine(FindSourceRoot(),"Containerbetrieb","compose.yaml.example"));
    Assert(c.Contains("source: ${WEBUI_RUNTIME_ROOT:-.}/diagnostic/oidc",StringComparison.Ordinal),"Containerbetrieb OIDC-Diagnosequellpfad fehlt.");
    Assert(c.Contains("target: /data/oidc-diagnostics",StringComparison.Ordinal),"Containerbetrieb OIDC-Diagnosezielpfad fehlt.");
    Assert(c.Contains("source: ${WEBUI_RUNTIME_ROOT:-.}/diagnostic/performance",StringComparison.Ordinal),"Containerbetrieb Performance-Diagnosequellpfad fehlt.");
    Assert(c.Contains("target: /data/performance-diagnostics",StringComparison.Ordinal),"Containerbetrieb Performance-Diagnosezielpfad fehlt.");
    return Task.CompletedTask;
}

static Task TestContainerbetriebComposeLegacyPathsAbsentAsync()
{
    var c=File.ReadAllText(Path.Combine(FindSourceRoot(),"Containerbetrieb","compose.yaml.example"));
    Assert(!c.Contains("../../shared/",StringComparison.Ordinal),"Alter Profil-B shared-Pfad ist im allgemeinen Containerbetrieb noch vorhanden.");
    Assert(!c.Contains("/volume",StringComparison.Ordinal),"Alter NAS-spezifischer absoluter Laufzeitpfad ist im allgemeinen Containerbetrieb noch vorhanden.");
    Assert(!c.Contains("paperless_default",StringComparison.Ordinal),"Altes externes Paperless-Docker-Netz ist im allgemeinen Containerbetrieb noch vorhanden.");
    Assert(!c.Contains("WebUi__RuntimeProfile",StringComparison.Ordinal),"Altes RuntimeProfile ist im allgemeinen Containerbetrieb noch vorhanden.");
    return Task.CompletedTask;
}

static Task TestDocumentPreviewApiPathAsync()
{
    var source = ReadProjectSource("WebUI.Infrastructure/PaperlessApiClient.cs");
    Assert(
        source.Contains("GetDocumentPreviewAsync", StringComparison.Ordinal) &&
        source.Contains("/api/documents/{documentId}/preview/", StringComparison.Ordinal),
        "Der Paperless-Preview-Endpunkt ist im API-Client nicht eindeutig angebunden.");
    return Task.CompletedTask;
}

static Task TestDocumentPreviewWebEndpointAsync()
{
    var source = ReadProjectSource("WebUI.Web/Program.cs");
    Assert(
        source.Contains("/preview/documents/{documentId:int}/document", StringComparison.Ordinal) &&
        source.Contains("GetDocumentPreviewAsync", StringComparison.Ordinal),
        "Der interne WebUI-Endpunkt für die vollständige Dokumentvorschau fehlt.");
    Assert(
        source.Contains("documentPreviewEndpoint.RequireAuthorization();", StringComparison.Ordinal),
        "Der interne WebUI-Endpunkt für die vollständige Dokumentvorschau ist nicht autorisierungspflichtig.");
    return Task.CompletedTask;
}

static Task TestDocumentThumbnailRemainsForNormalPreviewAsync()
{
    var source = ReadProjectSource("WebUI.Web/Components/Pages/Home.razor");
    Assert(
        source.Contains("GetThumbnailUrl(_selectedDocument.Id)", StringComparison.Ordinal),
        "Die normale Dokumentvorschau verwendet das Thumbnail nicht mehr.");
    Assert(
        source.Contains("GetDocumentPreviewUrl(_selectedDocument.Id)", StringComparison.Ordinal) &&
        source.Contains("quick-look-pdf-viewer", StringComparison.Ordinal) &&
        !source.Contains("<iframe", StringComparison.Ordinal),
        "Die Schnellansicht verwendet die vollständige mehrseitige Preview nicht über den kontrollierten PDF-Renderer.");
    return Task.CompletedTask;
}

static Task TestQuickLookDialogWidthControlsLayoutAsync()
{
    var source = ReadProjectSource("WebUI.Web/wwwroot/js/quickLookResize.js");
    Assert(
        source.Contains("const layoutBreakpoint = 520;", StringComparison.Ordinal) &&
        source.Contains("dialog.getBoundingClientRect().width <= layoutBreakpoint", StringComparison.Ordinal) &&
        source.Contains("content.dataset.quickLookLayout = useRows ? \"rows\" : \"columns\";", StringComparison.Ordinal),
        "Die Schnellansicht schaltet nicht eindeutig anhand ihrer eigenen Dialogbreite zwischen Zeilen- und Spaltenlayout.");
    return Task.CompletedTask;
}

static Task TestQuickLookRowDividerAsync()
{
    var js = ReadProjectSource("WebUI.Web/wwwroot/js/quickLookResize.js");
    var css = ReadProjectSource("WebUI.Web/Components/Pages/Home.razor.css");
    Assert(
        js.Contains("state.layout === \"rows\"", StringComparison.Ordinal) &&
        js.Contains("event.clientY - state.startY", StringComparison.Ordinal) &&
        js.Contains("--quick-look-info-height", StringComparison.Ordinal),
        "Die JavaScript-Logik unterstützt das vertikale Ziehen der horizontalen Trennlinie nicht eindeutig.");
    Assert(
        css.Contains(".quick-look-content[data-quick-look-layout=\"rows\"] .quick-look-divider", StringComparison.Ordinal) &&
        css.Contains("cursor: row-resize;", StringComparison.Ordinal),
        "Das CSS bildet die Trennlinie im Einspaltenmodus nicht eindeutig horizontal ab.");
    return Task.CompletedTask;
}

static Task TestQuickLookNarrowBrowserKeepsResizableDialogAsync()
{
    var css = ReadProjectSource("WebUI.Web/Components/Pages/Home.razor.css");
    Assert(
        !css.Contains(".quick-look-resize-handle {\n        display: none;", StringComparison.Ordinal) &&
        !css.Contains("width: calc(100vw - 1.5rem) !important;", StringComparison.Ordinal) &&
        !css.Contains("height: calc(100dvh - 1rem) !important;", StringComparison.Ordinal),
        "Schmale Browserregeln erzwingen weiterhin einen nicht frei skalierbaren Quick-Look-Vollbildmodus.");
    return Task.CompletedTask;
}

static Task TestQuickLookBreakpointBelowDefaultStartWidthAsync()
{
    var source = ReadProjectSource("WebUI.Web/wwwroot/js/quickLookResize.js");
    Assert(
        source.Contains("const defaultWidth = Math.min(900, Math.max(minDialogWidth, window.innerWidth * 0.56));", StringComparison.Ordinal) &&
        source.Contains("const layoutBreakpoint = 520;", StringComparison.Ordinal),
        "Die responsive Umschaltgrenze ist nicht eindeutig unterhalb der unveränderten Standardstartbreite festgelegt.");
    return Task.CompletedTask;
}

static Task TestQuickLookMinimumWidth300Async()
{
    var home = ReadProjectSource("WebUI.Web/Components/Pages/Home.razor");
    var css = ReadProjectSource("WebUI.Web/Components/Pages/Home.razor.css");
    Assert(
        home.Contains("                300,\n                420,", StringComparison.Ordinal) &&
        css.Contains("min-width: 300px;", StringComparison.Ordinal) &&
        !css.Contains("min-width: 620px;", StringComparison.Ordinal),
        "Die Schnellansicht verwendet nicht eindeutig 300 px als neue Mindestbreite.");
    return Task.CompletedTask;
}

static Task TestQuickLookBreakpoint520Async()
{
    var source = ReadProjectSource("WebUI.Web/wwwroot/js/quickLookResize.js");
    Assert(
        source.Contains("const layoutBreakpoint = 520;", StringComparison.Ordinal) &&
        source.Contains("dialog.getBoundingClientRect().width <= layoutBreakpoint", StringComparison.Ordinal),
        "Die Schnellansicht verwendet nicht eindeutig 520 px als responsive Umschaltgrenze.");
    return Task.CompletedTask;
}

static Task TestQuickLookInitialExplorerAnchorAsync()
{
    var home = ReadProjectSource("WebUI.Web/Components/Pages/Home.razor");
    var source = ReadProjectSource("WebUI.Web/wwwroot/js/quickLookResize.js");
    Assert(
        home.Contains("data-quick-look-anchor", StringComparison.Ordinal) &&
        source.Contains("document.querySelector(\"[data-quick-look-anchor]\")", StringComparison.Ordinal) &&
        source.Contains("titleAnchor?.getBoundingClientRect()", StringComparison.Ordinal) &&
        source.Contains("const defaultLeft = titleRect ? titleRect.left : (explorerRect ? explorerRect.left : margin);", StringComparison.Ordinal) &&
        source.Contains("const defaultTop = explorerRect ? explorerRect.top : margin;", StringComparison.Ordinal) &&
        !source.Contains("const defaultLeft = explorerRect ? explorerRect.left : margin;", StringComparison.Ordinal) &&
        !source.Contains("(window.innerHeight - defaultHeight) / 2", StringComparison.Ordinal),
        "Die Erstposition der Schnellansicht verwendet nicht die sichtbare Titelkante horizontal und die Explorer-Außenkante vertikal.");
    return Task.CompletedTask;
}

static Task TestQuickLookPdfWidthDrivenResizeAsync()
{
    var source = ReadProjectSource("WebUI.Web/wwwroot/js/quickLookPdf.js");
    Assert(
        source.Contains("if (width === lastWidth && attachedContainer.childElementCount > 0) return;", StringComparison.Ordinal) &&
        !source.Contains("lastHeight", StringComparison.Ordinal),
        "Eine reine Höhenänderung beeinflusst weiterhin die PDF-Neuskalierung.");
    return Task.CompletedTask;
}

static Task TestQuickLookPdfRendererAsync()
{
    var home = ReadProjectSource("WebUI.Web/Components/Pages/Home.razor");
    Assert(
        home.Contains("quick-look-pdf-viewer", StringComparison.Ordinal) &&
        home.Contains("./js/quickLookPdf.js", StringComparison.Ordinal) &&
        !home.Contains("<iframe", StringComparison.Ordinal),
        "Quick Look verwendet nicht eindeutig den kontrollierten lokalen PDF.js-Renderer.");
    return Task.CompletedTask;
}

static Task TestQuickLookPdfReadOnlyEndpointAsync()
{
    var home = ReadProjectSource("WebUI.Web/Components/Pages/Home.razor");
    Assert(
        home.Contains("$\"/preview/documents/{documentId}/document\"", StringComparison.Ordinal) &&
        !home.Contains("#view=Fit&zoom=page-fit", StringComparison.Ordinal),
        "Der PDF.js-Renderer verwendet nicht eindeutig den vorhandenen geschützten Dokumentendpunkt.");
    return Task.CompletedTask;
}

static Task TestQuickLookPdfResizeObserverAsync()
{
    var source = ReadProjectSource("WebUI.Web/wwwroot/js/quickLookPdf.js");
    Assert(
        source.Contains("new ResizeObserver", StringComparison.Ordinal) &&
        source.Contains("resizeObserver.observe(container)", StringComparison.Ordinal) &&
        source.Contains("scheduleRender()", StringComparison.Ordinal),
        "Der PDF.js-Renderer reagiert nicht eindeutig auf Größenänderungen seines Previewcontainers.");
    return Task.CompletedTask;
}

static Task TestQuickLookPdfFitCalculationAsync()
{
    var source = ReadProjectSource("WebUI.Web/wwwroot/js/quickLookPdf.js");
    Assert(
        source.Contains("const scale = availableWidth / baseViewport.width;", StringComparison.Ordinal) &&
        !source.Contains("availableHeight / baseViewport.height", StringComparison.Ordinal),
        "Der PDF.js-Renderer skaliert nicht eindeutig ausschließlich passend zur verfügbaren Breite.");
    return Task.CompletedTask;
}

static Task TestQuickLookPdfAllPagesAsync()
{
    var source = ReadProjectSource("WebUI.Web/wwwroot/js/quickLookPdf.js");
    Assert(
        source.Contains("pageNumber <= pdfDocument.numPages", StringComparison.Ordinal) &&
        source.Contains("pdfDocument.getPage(pageNumber)", StringComparison.Ordinal),
        "Der PDF.js-Renderer rendert nicht eindeutig alle Seiten des Dokuments.");
    return Task.CompletedTask;
}

static Task TestQuickLookPdfHiDpiAsync()
{
    var source = ReadProjectSource("WebUI.Web/wwwroot/js/quickLookPdf.js");
    Assert(
        source.Contains("window.devicePixelRatio", StringComparison.Ordinal) &&
        source.Contains("viewport.width * outputScale", StringComparison.Ordinal) &&
        source.Contains("viewport.height * outputScale", StringComparison.Ordinal),
        "Der PDF.js-Renderer berücksichtigt HiDPI-/Retina-Ausgabe nicht eindeutig.");
    return Task.CompletedTask;
}

static Task TestQuickLookPdfScrollPositionAsync()
{
    var source = ReadProjectSource("WebUI.Web/wwwroot/js/quickLookPdf.js");
    Assert(
        source.Contains("getScrollRatio", StringComparison.Ordinal) &&
        source.Contains("restoreScrollRatio", StringComparison.Ordinal),
        "Der PDF.js-Renderer erhält die relative Dokumentposition beim Neurendern nicht eindeutig.");
    return Task.CompletedTask;
}

static Task TestLocalPdfJsAssetsAsync()
{
    var root = FindSourceRoot();
    Assert(File.Exists(Path.Combine(root, "WebUI.Web", "wwwroot", "lib", "pdfjs", "build", "pdf.mjs")), "Lokales pdf.mjs fehlt.");
    Assert(File.Exists(Path.Combine(root, "WebUI.Web", "wwwroot", "lib", "pdfjs", "build", "pdf.worker.mjs")), "Lokales pdf.worker.mjs fehlt.");
    Assert(File.Exists(Path.Combine(root, "WebUI.Web", "wwwroot", "lib", "pdfjs", "LICENSE")), "PDF.js-Lizenznachweis fehlt.");
    return Task.CompletedTask;
}

static Task TestApplicationDisplayVersionAsync()
{
    var source = ReadProjectSource("WebUI.Web/Services/ApplicationDisplayInfo.cs");
    var buildProps = ReadProjectSource("Directory.Build.props");

    Assert(
        ApplicationDisplayInfo.FullVersion == "v09.91.1" &&
        source.Contains("public const string Version = \"09.91.1\";", StringComparison.Ordinal) &&
        source.Contains("public const string PreRelease = \"\";", StringComparison.Ordinal) &&
        source.Contains("PreRelease.Length == 0", StringComparison.Ordinal) &&
        !source.Contains("public const string Revision =", StringComparison.Ordinal),
        "Die sichtbare Anwendungsversion entspricht nicht dem Schema v09.91.1 / optional -rc.N.");

    Assert(
        buildProps.Contains("<Version>0.9.91</Version>", StringComparison.Ordinal) &&
        buildProps.Contains("<AssemblyVersion>0.9.91.0</AssemblyVersion>", StringComparison.Ordinal) &&
        buildProps.Contains("<FileVersion>0.9.91.0</FileVersion>", StringComparison.Ordinal) &&
        buildProps.Contains("<InformationalVersion>v09.91.1</InformationalVersion>", StringComparison.Ordinal) &&
        buildProps.Contains("<IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion>", StringComparison.Ordinal),
        "Die zentralen .NET-Versionsmetadaten entsprechen nicht v09.91.1 / 0.9.91.");

    return Task.CompletedTask;
}

static Task TestDisplayPrefixesFourAreasAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var path = Path.Combine(directory, "display-prefixes.json");
        File.WriteAllText(path, """
            {
              "Areas": ["A_"],
              "Correspondents": ["K_"],
              "DocumentTypes": ["T_"],
              "Documents": ["D_"]
            }
            """);
        using var service = CreateDisplayPrefixService(path);
        Assert(service.ApplyArea("A_Bereich") == "Bereich", "Bereichspräfix wurde nicht entfernt.");
        Assert(service.ApplyCorrespondent("K_Korrespondent") == "Korrespondent", "Korrespondentenpräfix wurde nicht entfernt.");
        Assert(service.ApplyDocumentType("T_Typ") == "Typ", "Dokumenttyppräfix wurde nicht entfernt.");
        Assert(service.ApplyDocument("D_Dokument") == "Dokument", "Dokumentpräfix wurde nicht entfernt.");
        Assert(service.ApplyArea("K_Bereich") == "K_Bereich", "Regellisten dürfen nicht bereichsübergreifend wirken.");
        return Task.CompletedTask;
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static Task TestDisplayPrefixesLongestExactPrefixAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var path = Path.Combine(directory, "display-prefixes.json");
        File.WriteAllText(path, """
            {
              "Documents": ["A_", "A_B_", "{yyyyMMdd}", "{yyyyMMdd}_"]
            }
            """);
        using var service = CreateDisplayPrefixService(path);
        Assert(service.ApplyDocument("A_B_Name") == "Name", "Der längste tatsächlich passende Präfix muss gewinnen.");
        Assert(service.ApplyDocument("A_A_Name") == "A_Name", "Pro Name darf höchstens eine Präfixregel angewendet werden.");
        Assert(service.ApplyDocument("20260903_Rechnung") == "Rechnung", "Separator darf nur entfernt werden, wenn er Teil der Regel ist.");

        File.WriteAllText(path, """
            {
              "Documents": ["{yyyyMMdd}"]
            }
            """);
        using var exactService = CreateDisplayPrefixService(path);
        Assert(exactService.ApplyDocument("20260903_Rechnung") == "_Rechnung", "Nicht konfigurierte Separatoren dürfen nicht automatisch entfernt werden.");
        return Task.CompletedTask;
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static Task TestDisplayPrefixPatternsAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var path = Path.Combine(directory, "display-prefixes.json");

        var dateCases = new[]
        {
            (Pattern: "yyyyMMdd", Valid: "20260228", Invalid: "20260230"),
            (Pattern: "yyMMdd", Valid: "260228", Invalid: "260230"),
            (Pattern: "yyyy-MM-dd", Valid: "2026-02-28", Invalid: "2026-02-30"),
            (Pattern: "yy-MM-dd", Valid: "26-02-28", Invalid: "26-02-30"),
            (Pattern: "yyyy.MM.dd", Valid: "2026.02.28", Invalid: "2026.02.30"),
            (Pattern: "yy.MM.dd", Valid: "26.02.28", Invalid: "26.02.30"),
            (Pattern: "ddMMyyyy", Valid: "28022026", Invalid: "30022026"),
            (Pattern: "ddMMyy", Valid: "280226", Invalid: "300226"),
            (Pattern: "dd-MM-yyyy", Valid: "28-02-2026", Invalid: "30-02-2026"),
            (Pattern: "dd-MM-yy", Valid: "28-02-26", Invalid: "30-02-26"),
            (Pattern: "dd.MM.yyyy", Valid: "28.02.2026", Invalid: "30.02.2026"),
            (Pattern: "dd.MM.yy", Valid: "28.02.26", Invalid: "30.02.26")
        };

        foreach (var testCase in dateCases)
        {
            var rule = $"{{{testCase.Pattern}}}_";
            File.WriteAllText(
                path,
                System.Text.Json.JsonSerializer.Serialize(
                    new
                    {
                        Documents = new[] { rule }
                    }));

            using var dateService = CreateDisplayPrefixService(path);
            var validValue = $"{testCase.Valid}_Rechnung";
            var invalidValue = $"{testCase.Invalid}_Rechnung";

            Assert(
                dateService.ApplyDocument(validValue) == "Rechnung",
                $"Gültiges Datumspräfix wurde nicht erkannt: {rule} auf {validValue}");

            Assert(
                dateService.ApplyDocument(invalidValue) == invalidValue,
                $"Unmögliches Kalenderdatum darf nicht als Datumspräfix gelten: {rule} auf {invalidValue}");
        }

        File.WriteAllText(
            path,
            System.Text.Json.JsonSerializer.Serialize(
                new
                {
                    Documents = new[] { "{yyMMdd}_" }
                }));

        using (var shortYearService = CreateDisplayPrefixService(path))
        {
            Assert(
                shortYearService.ApplyDocument("000229_Rechnung") == "Rechnung",
                "yy=00 muss für die Kalenderprüfung als Jahr 2000 und damit als Schaltjahr gelten.");

            Assert(
                shortYearService.ApplyDocument("010229_Rechnung") == "010229_Rechnung",
                "yy=01 muss für die Kalenderprüfung als Jahr 2001 und damit als Nicht-Schaltjahr gelten.");
        }

        File.WriteAllText(
            path,
            System.Text.Json.JsonSerializer.Serialize(
                new
                {
                    Documents = new[] { "{yyyyMMdd}_" }
                }));

        using (var startService = CreateDisplayPrefixService(path))
        {
            Assert(
                startService.ApplyDocument("X_20260228_Rechnung") == "X_20260228_Rechnung",
                "Präfixregeln dürfen nur am Textanfang wirken.");
        }

        File.WriteAllText(path, """
            {
              "Documents": ["{??????}_", "{??-##}_", "{##.??}_"]
            }
            """);

        using (var characterService = CreateDisplayPrefixService(path))
        {
            Assert(characterService.ApplyDocument("AB12ä9_Dokument") == "Dokument", "? muss Unicode-Buchstaben und ASCII-Ziffern akzeptieren.");
            Assert(characterService.ApplyDocument("𐐀7-42_Dokument") == "Dokument", "? muss auch Unicode-Buchstaben außerhalb des BMP als ein Zeichen behandeln.");
            Assert(characterService.ApplyDocument("24.ö7_Dokument") == "Dokument", "Feste Zeichen innerhalb eines Zeichenmusters wurden nicht korrekt ausgewertet.");
            Assert(characterService.ApplyDocument("AB-2ä9_Dokument") == "AB-2ä9_Dokument", "? darf keine Satz- oder Trennzeichen akzeptieren.");
        }

        File.WriteAllText(path, """
            {
              "Documents": ["{??}"]
            }
            """);

        using (var noSeparatorService = CreateDisplayPrefixService(path))
        {
            Assert(
                noSeparatorService.ApplyDocument("AB_Dokument") == "_Dokument",
                "{??} muss ausschließlich die ersten zwei passenden Zeichen entfernen.");

            Assert(
                noSeparatorService.ApplyDocument("Ä7_Dokument") == "_Dokument",
                "{??} muss auch bei Unicode-Buchstaben ohne nachfolgendes Trennzeichen funktionieren.");
        }

        File.WriteAllText(path, """
            {
              "Documents": ["{######}_"]
            }
            """);

        using (var digitService = CreateDisplayPrefixService(path))
        {
            Assert(digitService.ApplyDocument("240906_Dokument") == "Dokument", "# muss ASCII-Ziffern akzeptieren.");
            Assert(digitService.ApplyDocument("24A906_Dokument") == "24A906_Dokument", "# darf keine Buchstaben akzeptieren.");
            Assert(digitService.ApplyDocument("٢٤٠٩٠٦_Dokument") == "٢٤٠٩٠٦_Dokument", "# darf keine Nicht-ASCII-Ziffern akzeptieren.");
        }

        File.WriteAllText(path, """
            {
              "Documents": ["Scan_{yyyy-MM-dd}_{??}_"]
            }
            """);

        using (var combinedService = CreateDisplayPrefixService(path))
        {
            Assert(
                combinedService.ApplyDocument("Scan_2026-02-28_Ä7_Rechnung") == "Rechnung",
                "Fester Text, Datumsplatzhalter und Zeichenmuster müssen innerhalb einer Regel kombinierbar sein.");
        }

        return Task.CompletedTask;
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static Task TestDisplayPrefixesInvalidRulesAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var path = Path.Combine(directory, "display-prefixes.json");
        File.WriteAllText(path, """
            {
              "Documents": ["", "{yyyyXYZ}", "{YYYYMMdd}", "{ABC}", "{", "}", 12, "OK_"],
              "Unbekannt": ["X_"]
            }
            """);
        using var service = CreateDisplayPrefixService(path);
        Assert(service.ApplyDocument("OK_Name") == "Name", "Gültige Regel muss trotz ungültiger Einzelregeln aktiv bleiben.");
        Assert(service.ApplyDocument("20260903_Name") == "20260903_Name", "Unbekanntes oder falsch geschriebenes Datumsformat darf nicht angewendet werden.");
        Assert(service.ApplyDocument("ABC_Name") == "ABC_Name", "Klammerausdrücke ohne Datumsformat oder ?/#-Muster dürfen nicht angewendet werden.");
        return Task.CompletedTask;
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static Task TestDisplayPrefixesOptionalFileAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var path = Path.Combine(directory, "display-prefixes.json");
        using (var missingService = CreateDisplayPrefixService(path))
        {
            Assert(missingService.ApplyDocument("A_Name") == "A_Name", "Fehlende optionale Datei darf die Anzeige nicht verändern.");
        }

        File.WriteAllText(path, string.Empty);
        using (var emptyService = CreateDisplayPrefixService(path))
        {
            Assert(emptyService.ApplyDocument("A_Name") == "A_Name", "Leere optionale Datei darf die Anzeige nicht verändern.");
        }

        File.WriteAllText(path, "{ fehlerhaft");
        using (var malformedService = CreateDisplayPrefixService(path))
        {
            Assert(malformedService.ApplyDocument("A_Name") == "A_Name", "Initial fehlerhafte optionale Datei darf die Anzeige nicht verändern.");
        }

        return Task.CompletedTask;
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static async Task TestDisplayPrefixesReloadFallbackAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var path = Path.Combine(directory, "display-prefixes.json");
        File.WriteAllText(path, """{ "Documents": ["ALT_"] }""");
        using var service = CreateDisplayPrefixService(path);
        Assert(service.ApplyDocument("ALT_Name") == "Name", "Initiale gültige Konfiguration wurde nicht geladen.");

        File.WriteAllText(path, "{ ungültig");
        await Task.Delay(700);
        Assert(service.ApplyDocument("ALT_Name") == "Name", "Fehlerhafter Reload darf die letzte gültige Konfiguration nicht verwerfen.");

        File.WriteAllText(path, """{ "Documents": ["NEU_"] }""");
        var reloaded = false;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (service.ApplyDocument("NEU_Name") == "Name")
            {
                reloaded = true;
                break;
            }
            await Task.Delay(100);
        }
        Assert(reloaded, "Gültige Dateiänderung wurde nicht automatisch neu geladen.");
        Assert(service.ApplyDocument("ALT_Name") == "ALT_Name", "Nach gültigem Reload darf nur die neue Konfiguration gelten.");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static Task TestDisplayPrefixesHomeIntegrationAsync()
{
    var home = ReadProjectSource(Path.Combine("WebUI.Web", "Components", "Pages", "Home.razor"));
    var program = ReadProjectSource(Path.Combine("WebUI.Web", "Program.cs"));
    var serviceSource = ReadProjectSource(Path.Combine("WebUI.Web", "Services", "DisplayPrefixService.cs"));
    var example = ReadProjectSource(Path.Combine("config", "display-prefixes.example.json"));

    Assert(home.Contains("@inject DisplayPrefixService DisplayPrefixes", StringComparison.Ordinal), "Home injiziert den DisplayPrefixService nicht.");
    Assert(home.Contains("DisplayAreaName(area.DisplayName)", StringComparison.Ordinal), "Bereichsanzeige verwendet den Display-Präfix-Service nicht.");
    Assert(home.Contains("DisplayDocumentName(document.Title)", StringComparison.Ordinal), "Dokumentliste verwendet den Display-Präfix-Service nicht.");
    Assert(!home.Contains("DisplayDocumentName(_documentDetails.Title)", StringComparison.Ordinal) && CountOccurrences(home, "DisplayMetadataValue(_documentDetails.Title)") == 2, "Dokumentmetadaten müssen den unveränderten Originaltitel ohne Dokument-Präfixfilter anzeigen.");
    Assert(home.Contains("private string DisplayCorrespondentName(string name) =>", StringComparison.Ordinal) &&
           home.Contains("DisplayPrefixes.ApplyCorrespondent(name)", StringComparison.Ordinal) &&
           home.Contains("private string DisplayDocumentTypeName(string name) =>", StringComparison.Ordinal) &&
           home.Contains("DisplayPrefixes.ApplyDocumentType(name)", StringComparison.Ordinal), "Korrespondenten- und Dokumenttypanzeige delegieren nicht vollständig an den Display-Präfix-Service.");
    Assert(program.Contains("AddSingleton<DisplayPrefixService>()", StringComparison.Ordinal), "DisplayPrefixService ist nicht als Singleton registriert.");
    Assert(serviceSource.Contains("WebUi:DisplayPrefixesFilePath", StringComparison.Ordinal), "Konfigurierbarer Containerpfad für Display-Präfixe fehlt.");
    Assert(serviceSource.Contains("Application Support", StringComparison.Ordinal) &&
           serviceSource.Contains("display-prefixes.json", StringComparison.Ordinal), "Lokaler macOS-Runtimepfad für Display-Präfixe ist nicht im Service festgelegt.");
    Assert(example.Contains("\"Areas\"", StringComparison.Ordinal) &&
           example.Contains("\"Correspondents\"", StringComparison.Ordinal) &&
           example.Contains("\"DocumentTypes\"", StringComparison.Ordinal) &&
           example.Contains("\"Documents\"", StringComparison.Ordinal), "Öffentliche Beispieldatei enthält nicht alle vier Bereiche.");
    return Task.CompletedTask;
}

static DisplayPrefixService CreateDisplayPrefixService(string path)
{
    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WebUi:DisplayPrefixesFilePath"] = path
        })
        .Build();
    var loggerFactory = LoggerFactory.Create(builder => { });
    return new DisplayPrefixService(
        configuration,
        loggerFactory.CreateLogger<DisplayPrefixService>());
}

static string CreateTemporaryDirectory()
{
    var path = Path.Combine(Path.GetTempPath(), "webui-display-prefix-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(path);
    return path;
}

static Task TestOrdnerBrowseBrandingAsync()
{
    var source = ReadProjectSource(
        "WebUI.Web/Services/ApplicationDisplayInfo.cs");

    Assert(
        source.Contains("public const string ProductName = \"OrdnerBrowse\";", StringComparison.Ordinal) &&
        source.Contains("public const string ProductDescription = \"eine WebUI für paperless-ngx\";", StringComparison.Ordinal) &&
        source.Contains("public const string FullProductName = ProductName + \" – \" + ProductDescription;", StringComparison.Ordinal),
        "Das sichtbare OrdnerBrowse-Branding ist nicht mehr zentral und eindeutig definiert.");

    return Task.CompletedTask;
}

static Task TestOrdnerBrowseHomeTitleAsync()
{
    var source = ReadProjectSource(
        "WebUI.Web/Components/Pages/Home.razor");

    Assert(
        source.Contains("<PageTitle>@ApplicationDisplayInfo.FullProductName</PageTitle>", StringComparison.Ordinal),
        "Der Dokumenten-Explorer verwendet nicht den zentralen vollständigen Produktnamen als Seitentitel.");
    Assert(
        !source.Contains("<PageTitle>WebUI für paperless-ngx</PageTitle>", StringComparison.Ordinal),
        "Der alte sichtbare Produktname ist im Dokumenten-Explorer weiterhin fest verdrahtet.");

    return Task.CompletedTask;
}

static Task TestOrdnerBrowseStartBrandingAsync()
{
    var source = ReadProjectSource(
        "WebUI.Web/Components/Pages/Start.razor");
    var userKeySource = ReadProjectSource(
        "WebUI.Web/Components/Pages/UserKey.razor");

    Assert(
        source.Contains("Für OrdnerBrowse ist eine sichere Anmeldung erforderlich.", StringComparison.Ordinal) &&
        source.Contains("\"OrdnerBrowse öffnen\"", StringComparison.Ordinal) &&
        source.Contains("\"OrdnerBrowse wird geöffnet …\"", StringComparison.Ordinal),
        "Die sichtbaren Startseiten-Texte verwenden das OrdnerBrowse-Branding nicht vollständig.");
    Assert(
        userKeySource.Contains("Zurück zu OrdnerBrowse", StringComparison.Ordinal),
        "Der Rücklink der Benutzerschlüsselseite verwendet nicht das OrdnerBrowse-Branding.");

    return Task.CompletedTask;
}

static Task TestOrdnerBrowseAuthenticationBrandingAsync()
{
    var authSource = ReadProjectSource(
        "WebUI.Web/Services/AuthenticationHtmlPages.cs");
    var localSource = ReadProjectSource(
        "WebUI.Web/Services/LocalDevelopmentAuthenticationEndpoints.cs");

    Assert(
        authSource.Contains("ApplicationDisplayInfo.FullProductName", StringComparison.Ordinal) &&
        authSource.Contains("OrdnerBrowse-Sitzung", StringComparison.Ordinal),
        "Die sichtbaren OIDC-/Logout-Seiten verwenden das OrdnerBrowse-Branding nicht vollständig.");
    Assert(
        localSource.Contains("ApplicationDisplayInfo.FullProductName", StringComparison.Ordinal),
        "Die lokale Testanmeldeseite verwendet nicht den zentralen vollständigen Produktnamen.");

    return Task.CompletedTask;
}

static Task TestQuickLookPdfNoPaperlessWriteAsync()
{
    var client = ReadProjectSource("WebUI.Infrastructure/PaperlessApiClient.cs");
    var home = ReadProjectSource("WebUI.Web/Components/Pages/Home.razor");
    var homeCss = ReadProjectSource("WebUI.Web/Components/Pages/Home.razor.css");
    Assert(
        !client.Contains("PostAsync(", StringComparison.Ordinal) &&
        !client.Contains("PutAsync(", StringComparison.Ordinal) &&
        !client.Contains("PatchAsync(", StringComparison.Ordinal) &&
        !client.Contains("DeleteAsync(", StringComparison.Ordinal),
        "Der Paperless-API-Client enthält eine schreibende HTTP-Methode.");
    Assert(
        client.Contains("archive_serial_number,notes", StringComparison.Ordinal) &&
        client.Contains("[JsonPropertyName(\"created\")]", StringComparison.Ordinal) &&
        client.Contains("[JsonPropertyName(\"user\")]", StringComparison.Ordinal) &&
        client.Contains("[JsonPropertyName(\"username\")]", StringComparison.Ordinal) &&
        client.Contains("[JsonPropertyName(\"first_name\")]", StringComparison.Ordinal) &&
        client.Contains("[JsonPropertyName(\"last_name\")]", StringComparison.Ordinal),
        "Die Paperless-Notizdaten enthalten nicht Text, Datum und Benutzerinformationen.");
    Assert(
        home.Contains("<h3>Notizen</h3>", StringComparison.Ordinal) &&
        home.Contains("<h2>Notizen</h2>", StringComparison.Ordinal) &&
        home.Contains("class=\"quick-look-note-text\">@note.Note", StringComparison.Ordinal) &&
        home.Contains("class=\"quick-look-note-info\">@FormatQuickLookNoteInfo(note)", StringComparison.Ordinal) &&
        home.Contains("return $\"{author} - {created}\";", StringComparison.Ordinal) &&
        !home.Contains("<dt>Notiz</dt>", StringComparison.Ordinal),
        "Schnellansicht und normale Vorschau rendern die Notizen nicht im festgelegten read-only Aufbau.");
    Assert(
        home.Contains(".documents-column > .document-inspector {\n        order: 4;\n    }", StringComparison.Ordinal) &&
        homeCss.Contains(".document-inspector {\n    flex: 1 1 auto;\n    min-height: 0;\n    overflow-y: auto;", StringComparison.Ordinal),
        "Die normale Vorschau verwendet nicht den vorgesehenen scrollbar begrenzten Inspector.");
    Assert(
        homeCss.Contains(".quick-look-note-info", StringComparison.Ordinal) &&
        homeCss.Contains("font-size: .66rem;", StringComparison.Ordinal) &&
        homeCss.Contains(".quick-look-note-text", StringComparison.Ordinal),
        "Die Notiz-Infozeile ist nicht als kleinere Sekundärinformation formatiert.");
    return Task.CompletedTask;
}

static string FindSourceRoot()
{
    var current=new DirectoryInfo(AppContext.BaseDirectory);

    while(current is not null)
    {
        var root=current.FullName;
        var hasWeb=Directory.Exists(Path.Combine(root,"WebUI.Web"));
        var hasInfrastructure=Directory.Exists(Path.Combine(root,"WebUI.Infrastructure"));
        var hasTests=Directory.Exists(Path.Combine(root,"WebUI.Tests"));
        var hasContainerbetrieb=Directory.Exists(Path.Combine(root,"Containerbetrieb"));

        if(hasWeb && hasInfrastructure && hasTests && hasContainerbetrieb)
        {
            return root;
        }

        current=current.Parent;
    }

    throw new InvalidOperationException("Quellwurzel nicht gefunden.");
}

static Task ExpectInvalidSecretBytesAsync(byte[] bytes)
{
    using var fixture = SecretFixture.Create(bytes);
    return ExpectInvalidAsync(
        EnabledValues(fixture.Path));
}

static Task ExpectInvalidWithSecretAsync(
    Dictionary<string, string?> values)
{
    using var fixture = SecretFixture.CreateRandom();
    values["WebUi:ProxyGuard:SecretFilePath"] = fixture.Path;
    return ExpectInvalidAsync(
        values);
}

static Task ExpectInvalidAsync(
    Dictionary<string, string?> values)
{
    var threw = false;

    try
    {
        using var settings = ProxyGuardSettings.Load(
            Configuration(values));
    }
    catch (InvalidOperationException)
    {
        threw = true;
    }

    Assert(threw, "Erwarteter Startabbruch blieb aus.");
    return Task.CompletedTask;
}

static ProxyGuardSettings LoadEnabled(string secretPath)
{
    return ProxyGuardSettings.Load(
        Configuration(EnabledValues(secretPath)));
}

static Dictionary<string, string?> EnabledValues(
    string? secretPath = null)
{
    return new Dictionary<string, string?>
    {
        ["WebUi:ProxyGuard:Enabled"] = "true",
        ["WebUi:ProxyGuard:SecretFilePath"] = secretPath
    };
}

static IConfiguration Configuration(
    Dictionary<string, string?>? values = null)
{
    return new ConfigurationBuilder()
        .AddInMemoryCollection(
            values ?? new Dictionary<string, string?>())
        .Build();
}

static DefaultHttpContext NewContext()
{
    var context = new DefaultHttpContext();
    context.Response.Body = new MemoryStream();
    return context;
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

file sealed class TextFileFixture : IDisposable
{
    private TextFileFixture(
        TemporaryDirectoryFixture directory,
        string path)
    {
        _directory = directory;
        Path = path;
    }

    private readonly TemporaryDirectoryFixture _directory;

    public string Path { get; }

    public static TextFileFixture Create(string content)
    {
        var directory = TemporaryDirectoryFixture.Create(
            "paperless-base-url-file-test");
        var path = System.IO.Path.Combine(
            directory.Path,
            "paperless_base_url");
        File.WriteAllText(path, content);
        return new TextFileFixture(directory, path);
    }

    public void Dispose()
    {
        _directory.Dispose();
    }
}

file sealed class TemporaryDirectoryFixture : IDisposable
{
    private TemporaryDirectoryFixture(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public static TemporaryDirectoryFixture Create(string prefix)
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return new TemporaryDirectoryFixture(path);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch
        {
            // Nur temporäre Testdateien; keine Ausgabe von Pfaden oder Werten.
        }
    }
}

file sealed class SecretFixture : IDisposable
{
    private SecretFixture(string path, string value)
    {
        Path = path;
        Value = value;
    }

    public string Path { get; }

    public string Value { get; }

    public static SecretFixture CreateRandom()
    {
        var value = Convert.ToHexString(
            RandomNumberGenerator.GetBytes(32))
            .ToLowerInvariant();
        return Create(
            System.Text.Encoding.ASCII.GetBytes(value + "\n"),
            value);
    }

    public static SecretFixture Create(byte[] bytes)
    {
        var value = bytes.Length >= ProxyGuardSettings.HeaderLength
            ? System.Text.Encoding.ASCII.GetString(
                bytes,
                0,
                ProxyGuardSettings.HeaderLength)
            : string.Empty;
        return Create(bytes, value);
    }

    private static SecretFixture Create(byte[] bytes, string value)
    {
        var directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"proxy-guard-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = System.IO.Path.Combine(
            directory,
            "proxy_guard_secret");
        File.WriteAllBytes(path, bytes);
        return new SecretFixture(path, value);
    }

    public void Dispose()
    {
        try
        {
            File.Delete(Path);
            Directory.Delete(
                System.IO.Path.GetDirectoryName(Path)!,
                false);
        }
        catch
        {
            // Nur temporäre Testdateien; keine Ausgabe von Pfaden oder Werten.
        }
    }
}

file sealed record PaperlessCapturedRequest(
    string Method,
    string Scheme,
    string Host,
    int Port,
    string UserInfo,
    string PathAndQuery,
    bool HasContent);

file sealed class PaperlessReadCaptureHandler : HttpMessageHandler
{
    public List<PaperlessCapturedRequest> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var requestUri = request.RequestUri
            ?? throw new InvalidOperationException(
                "Synthetische Paperless-Testanfrage besitzt keine URI.");

        Requests.Add(
            new PaperlessCapturedRequest(
                request.Method.Method,
                requestUri.Scheme,
                requestUri.IdnHost,
                requestUri.Port,
                requestUri.UserInfo,
                requestUri.PathAndQuery,
                request.Content is not null));

        HttpContent content;

        if (requestUri.AbsolutePath.EndsWith(
                "/thumb/",
                StringComparison.Ordinal))
        {
            content = new ByteArrayContent(
                new byte[] { 0x01, 0x02, 0x03 });
            content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(
                    "image/webp");
        }
        else if (requestUri.AbsolutePath.EndsWith(
                     "/preview/",
                     StringComparison.Ordinal))
        {
            content = new ByteArrayContent(
                new byte[] { 0x25, 0x50, 0x44, 0x46 });
            content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(
                    "application/pdf");
        }
        else
        {
            content = new StringContent(
                "{\"count\":0,\"next\":null,\"previous\":null,\"results\":[]}",
                System.Text.Encoding.UTF8,
                "application/json");
        }

        return Task.FromResult(
            new HttpResponseMessage(
                HttpStatusCode.OK)
            {
                Content = content
            });
    }
}

sealed class AuthorizationCaptureHandler : HttpMessageHandler
{
    public string? AuthorizationScheme { get; private set; }
    public string? AuthorizationParameter { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        AuthorizationScheme = request.Headers.Authorization?.Scheme;
        AuthorizationParameter = request.Headers.Authorization?.Parameter;

        return Task.FromResult(
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            });
    }
}

sealed class SyntheticPaperlessValidationHttpClientFactory
    : IHttpClientFactory
{
    public HttpClient CreateClient(string name)
    {
        AssertFactoryName(name);

        return new HttpClient(
            new SyntheticPaperlessValidationHandler(),
            disposeHandler: true);
    }

    private static void AssertFactoryName(string name)
    {
        if (!string.Equals(
                name,
                PaperlessClientFactory.HttpClientName,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Der synthetische Paket-2-Test erhielt einen unerwarteten HttpClient-Namen.");
        }
    }
}

sealed class SyntheticPaperlessValidationHandler
    : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var uri = request.RequestUri
            ?? throw new InvalidOperationException(
                "Synthetische Tokenvalidierungsanfrage besitzt keine URI.");

        if (request.Method != HttpMethod.Get ||
            !string.Equals(
                uri.Host,
                "paperless.example.test",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                uri.AbsolutePath,
                "/api/documents/",
                StringComparison.Ordinal) ||
            !string.Equals(
                uri.Query,
                "?page_size=1&fields=id",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Die synthetische persönliche Tokenvalidierung verließ den erwarteten lokalen GET-Vertrag.");
        }

        return Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"count\":0,\"results\":[]}")
            });
    }
}

sealed class NoNetworkHttpClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name)
    {
        throw new InvalidOperationException(
            "Dieser Testpfad darf keinen HTTP-Client anfordern.");
    }
}
