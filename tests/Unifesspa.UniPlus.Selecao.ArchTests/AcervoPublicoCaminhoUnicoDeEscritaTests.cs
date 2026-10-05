namespace Unifesspa.UniPlus.Selecao.ArchTests;

using System.Runtime.CompilerServices;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;

/// <summary>
/// O caminho único de escrita no acervo público (ADR-0132): só a materialização da divulgação, que
/// roda depois de o ato ser registrado, grava lá. Publicar é irreversível na prática — um endereço
/// divulgado pode ter sido citado —, e o controle tem de estar na entrada, verificável por teste.
/// </summary>
public sealed class AcervoPublicoCaminhoUnicoDeEscritaTests
{
    [Fact(DisplayName = "Só a materialização da divulgação depende do port de escrita no acervo público")]
    public void SoADivulgacaoEscreveNoAcervo()
    {
        string[] dependentes = [.. Directory
            .EnumerateFiles(Raiz("src", "selecao", "Unifesspa.UniPlus.Selecao.Application"), "*.cs", SearchOption.AllDirectories)
            .Where(static arquivo => !EhGerado(arquivo))
            .Where(static arquivo => File.ReadAllText(arquivo).Contains(nameof(IAcervoPublico), StringComparison.Ordinal))
            .Select(static arquivo => Path.GetFileName(arquivo))
            .Where(static nome => nome != $"{nameof(IAcervoPublico)}.cs")
            .Order(StringComparer.Ordinal)];

        // Igualdade, e não "contido em": sem dependente nenhum o teste aprovaria um acervo que nunca
        // recebe o modelo publicado.
        dependentes.Should().Equal(
            ["DivulgarCertameAoRegistrarAtoHandler.cs"],
            "confirmar o envio não é publicar: o documento só entra no acervo depois de o ato existir");
    }

    [Fact(DisplayName = "Só a implementação do acervo de Seleção usa a cópia para objeto público do storage")]
    public void SoOAcervoDeSelecaoUsaACopiaPublica()
    {
        string[] usos = [.. Directory
            .EnumerateFiles(Raiz("src"), "*.cs", SearchOption.AllDirectories)
            .Where(static arquivo => !EhGerado(arquivo))
            .Where(static arquivo => File.ReadAllText(arquivo).Contains("CopiarComoObjetoPublicoAsync", StringComparison.Ordinal))
            .Select(static arquivo => Path.GetFileName(arquivo))
            .Where(static nome => nome is not ("IStorageService.cs" or "MinioStorageService.cs"))
            .Order(StringComparer.Ordinal)];

        usos.Should().Equal(
            ["AcervoPublicoService.cs"],
            "nenhum outro ponto do sistema pode ter o bucket público como destino de escrita");
    }

    private static bool EhGerado(string arquivo) =>
        arquivo.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || arquivo.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string Raiz(string primeiro, params string[] resto) =>
        Path.GetFullPath(Path.Join([RaizDoRepositorio(), primeiro, .. resto]));

    private static string RaizDoRepositorio([CallerFilePath] string origem = "") =>
        Path.Join(Path.GetDirectoryName(origem)!, "..", "..");
}
