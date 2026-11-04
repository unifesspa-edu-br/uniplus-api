namespace Unifesspa.UniPlus.Selecao.ArchTests;

using System.Runtime.CompilerServices;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

/// <summary>
/// O caminho único de escrita no acervo público (ADR-0132): só a materialização da divulgação, que
/// roda depois de o ato ser registrado, grava lá. Publicar é irreversível na prática — um endereço
/// divulgado pode ter sido citado —, e o controle tem de estar na entrada, verificável por teste.
/// </summary>
public sealed class AcervoPublicoCaminhoUnicoDeEscritaTests
{
    /// <summary>
    /// As partes da mesma materialização: a do edital que cria versão e a do ato que não cria, mais os
    /// tipos que carregam o port para elas. O nome vale também para os tipos gerados dentro de cada uma.
    /// </summary>
    private const string CaminhoPrevisto =
        @"^Unifesspa\.UniPlus\.Selecao\.Application\.Commands\.ProcessosSeletivos\."
        + @"(DivulgarCertameAoRegistrarAtoHandler|DivulgarAtoDoProcessoNoAcervo|MontagemDaDivulgacao|AcervoDaDivulgacao)(\+|$)";

    private static readonly Architecture Arquitetura = new ArchLoader()
        .LoadAssemblies([typeof(IAcervoPublico).Assembly])
        .Build();

    [Fact(DisplayName = "Só a materialização da divulgação depende do port de escrita no acervo público")]
    public void SoADivulgacaoEscreveNoAcervo()
    {
        IArchRule regra = Types()
            .That()
            .DoNotHaveFullNameMatching(CaminhoPrevisto)
            .Should()
            .NotDependOnAny(typeof(IAcervoPublico))
            .Because("confirmar o envio não é publicar: o documento só entra no acervo depois de o ato existir");

        regra.Check(Arquitetura);
    }

    [Fact(DisplayName = "Só a materialização da divulgação depende do tipo que carrega o port do acervo público")]
    public void SoADivulgacaoUsaOTipoQueCarregaOAcervo()
    {
        IArchRule regra = Types()
            .That()
            .DoNotHaveFullNameMatching(CaminhoPrevisto)
            .Should()
            .NotDependOnAnyTypesThat()
            .HaveFullNameMatching(@"\.(MontagemDaDivulgacao|AcervoDaDivulgacao)(\+|$)")
            .Because("passar o acervo adiante por outro tipo reabriria a escrita fora do caminho previsto");

        regra.Check(Arquitetura);
    }

    [Fact(DisplayName = "O caminho previsto existe: o port e o tipo que o carrega têm dependentes, e a regra não passa vazia")]
    public void OCaminhoPrevistoDependeDoPort()
    {
        string[] doPort = DependentesDe(typeof(IAcervoPublico).FullName!);
        string[] doCarregador = DependentesDe("Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos.AcervoDaDivulgacao");

        doPort.Should().Contain(
            ["DivulgarCertameAoRegistrarAtoHandler", "AcervoDaDivulgacao"],
            "sem dependente nenhum o teste aprovaria um acervo que nunca recebe o modelo publicado");
        doCarregador.Should().Contain(
            ["DivulgarCertameAoRegistrarAtoHandler", "DivulgarAtoDoProcessoNoAcervo", "MontagemDaDivulgacao"],
            "o acervo chega às etapas da divulgação por intermédio do tipo que o carrega");
    }

    private static string[] DependentesDe(string nomeCompleto) =>
    [
        .. Arquitetura.Types
            .Where(static t => System.Text.RegularExpressions.Regex.IsMatch(t.FullName, CaminhoPrevisto))
            .Where(t => t.Dependencies.Any(d => d.Target.FullName == nomeCompleto))
            .Select(static t => t.Name.Split('+')[0])
            .Distinct(),
    ];

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
