namespace Cedeva.Tests.Integration;

/// <summary>
/// Groups all WebApplicationFactory-based integration tests into a single non-parallel
/// collection. The factory boots the real app via top-level <c>Program</c>, which relies on
/// process-wide static state (Serilog's static Log, the HostFactoryResolver); running several
/// hosts concurrently races and throws "entry point exited without ever building an IHost".
/// </summary>
[CollectionDefinition("WebApp", DisableParallelization = true)]
public class WebAppCollection;
