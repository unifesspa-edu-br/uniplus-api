namespace Unifesspa.UniPlus.Contratos.UnitTests.TestSupport;

using System.Reflection;

using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Portal.API;
using Unifesspa.UniPlus.Verificador.Configuracao;

/// <summary>Os três executáveis do produto, cada um por um tipo que o identifica do assembly do ponto de entrada.</summary>
internal static class ExecutaveisDoProduto
{
    internal static readonly Assembly AssemblyDoHost = typeof(HostAssemblyMarker).Assembly;

    internal static readonly Assembly AssemblyDoPortal = typeof(PortalApiAssemblyMarker).Assembly;

    internal static readonly Assembly AssemblyDoVerificador = typeof(VerificadorOptions).Assembly;

    internal static readonly Assembly[] Todos = [AssemblyDoHost, AssemblyDoPortal, AssemblyDoVerificador];
}
