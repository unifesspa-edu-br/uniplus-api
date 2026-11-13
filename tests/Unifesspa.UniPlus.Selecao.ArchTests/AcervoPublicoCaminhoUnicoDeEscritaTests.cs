namespace Unifesspa.UniPlus.Selecao.ArchTests;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Storage;
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
        + @"(DivulgarCertameAoRegistrarAtoHandler|DivulgarAtoDoProcessoNoAcervo|AcervoDaDivulgacao)(\+|$)";

    private static readonly Architecture Arquitetura = new ArchLoader()
        .LoadAssemblies([typeof(IAcervoPublico).Assembly])
        .Build();

    private static readonly Architecture ArquiteturaDeStorage = new ArchLoader()
        .LoadAssemblies(
            [
                typeof(IStorageService).Assembly,
                .. Directory.GetFiles(AppContext.BaseDirectory, "Unifesspa.UniPlus.*.dll")
                    .Where(static caminho => !Path.GetFileName(caminho).Contains("Tests", StringComparison.Ordinal))
                    .Select(System.Reflection.Assembly.LoadFrom),
            ])
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
            .HaveFullNameMatching(@"\.AcervoDaDivulgacao(\+|$)")
            .Because("passar o acervo adiante por outro tipo reabriria a escrita fora do caminho previsto");

        regra.Check(Arquitetura);
    }

    [Fact(DisplayName = "O caminho previsto existe: o port e o tipo que o carrega têm dependentes, e a regra não passa vazia")]
    public void OCaminhoPrevistoDependeDoPort()
    {
        string[] doPort = DependentesDe(typeof(IAcervoPublico).FullName!);
        string[] doCarregador = DependentesDe("Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos.AcervoDaDivulgacao");

        doPort.Should().Contain(
            ["AcervoDaDivulgacao"],
            "sem dependente nenhum o teste aprovaria um acervo que nunca recebe o modelo publicado");
        doCarregador.Should().Contain(
            ["DivulgarCertameAoRegistrarAtoHandler", "DivulgarAtoDoProcessoNoAcervo"],
            "o acervo chega às etapas da divulgação por intermédio do tipo que o carrega");
    }

    private static string[] DependentesDe(string nomeCompleto) =>
    [
        .. Arquitetura.Types
            .Where(static t => EhCaminhoPrevisto(t.FullName))
            .Where(t => t.Dependencies.Any(d => d.Target.FullName == nomeCompleto))
            .Select(static t => t.Name.Split('+')[0])
            .Distinct(),
    ];

    private static bool EhCaminhoPrevisto(string nome)
    {
        const string caminho = "Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos.";
        return new[] { "DivulgarCertameAoRegistrarAtoHandler", "DivulgarAtoDoProcessoNoAcervo", "AcervoDaDivulgacao" }
            .Any(tipo => nome == caminho + tipo || nome.StartsWith(caminho + tipo + "+", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Só o serviço do acervo público copia para o bucket público")]
    public void SoOAcervoDeSelecaoUsaACopiaPublica()
    {
        ArchUnitNET.Fluent.Syntax.Elements.Members.MethodMembers.GivenMethodMembersConjunction copia = MethodMembers().That().AreDeclaredIn(typeof(IStorageService))
            .And().HaveNameStartingWith("CopiarComoObjetoPublicoAsync(");
        const string servico = "Unifesspa.UniPlus.Selecao.Infrastructure.ExternalServices.AcervoPublicoService";

        Types().That().HaveFullName(servico).And().CallAny(copia)
            .GetObjects(ArquiteturaDeStorage).Should().NotBeEmpty("a chamada permitida deve ser detectada");

        Types().That().ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.")
            .And().DoNotHaveFullNameMatching(@"^Unifesspa\.UniPlus\.Selecao\.Infrastructure\.ExternalServices\.AcervoPublicoService(\+|$)")
            .Should().NotCallAny(copia)
            .Check(ArquiteturaDeStorage);
    }
}
