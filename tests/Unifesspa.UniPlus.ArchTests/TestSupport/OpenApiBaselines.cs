namespace Unifesspa.UniPlus.ArchTests.TestSupport;

using System.IO;

/// <summary>
/// O inventário dos contratos publicados em <c>contracts/</c>, compartilhado pelas
/// fitness functions que os leem. A lista vive num lugar só: um módulo que passe a
/// publicar baseline precisa entrar em todas as guardas de uma vez, e uma cópia
/// esquecida estreita a cobertura sem que nenhum teste falhe.
/// </summary>
internal static class OpenApiBaselines
{
    private const string ContractsDir = "contracts";

    internal static IReadOnlyList<string> FileNames { get; } =
    [
        "openapi.selecao.json",
        "openapi.ingresso.json",
        "openapi.organizacao.json",
        "openapi.configuracao.json",
        "openapi.publicacoes.json",
        "openapi.portal.json",
    ];

    /// <summary>Caminho absoluto de cada baseline, na ordem de <see cref="FileNames"/>.</summary>
    internal static IReadOnlyList<string> Paths()
    {
        string solutionRoot = SolutionRootLocator.Locate();
        return [.. FileNames.Select(fileName => Path.Join(solutionRoot, ContractsDir, fileName))];
    }
}
