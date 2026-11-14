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
/// O caminho único de escrita no acervo público (ADR-0132): só a cópia dos documentos do ato ao acervo, efeito posterior à
/// publicação do ato, grava lá. Publicar é irreversível na prática — um endereço divulgado pode ter sido citado —, e o
/// controle tem de estar na entrada, verificável por teste.
/// </summary>
public sealed class AcervoPublicoCaminhoUnicoDeEscritaTests
{
    private const string Caminho = "Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos.";

    /// <summary>
    /// As partes da mesma cópia: a que escolhe o que copiar de um ato, com versão ou sem ela, e o tipo que carrega o
    /// port. O nome vale também para os tipos gerados dentro de cada uma. O registro de serviços só os instancia.
    /// </summary>
    private const string CaminhoPrevisto =
        @"^Unifesspa\.UniPlus\.Selecao\.Application\.(Commands\.ProcessosSeletivos\.(CopiaDosDocumentosDoAto|AcervoDaDivulgacao)"
        + @"|Mappings\.SelecaoApplicationServiceRegistration)(\+|$)";

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

    [Fact(DisplayName = "Só a cópia dos documentos do ato depende do port de escrita no acervo público")]
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

    [Fact(DisplayName = "Só a cópia dos documentos do ato depende do tipo que carrega o port do acervo público")]
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
        string[] doCarregador = DependentesDe(Caminho + "AcervoDaDivulgacao");

        doPort.Should().Contain(
            ["AcervoDaDivulgacao"],
            "sem dependente nenhum o teste aprovaria um acervo que nunca recebe o modelo publicado");
        doCarregador.Should().Contain(
            ["CopiaDosDocumentosDoAto"],
            "o acervo chega à cópia dos documentos do ato por intermédio do tipo que o carrega");
    }

    private static string[] DependentesDe(string nomeCompleto) =>
    [
        .. Arquitetura.Types
            .Where(static t => EhTipoDaCopia(t.FullName))
            .Where(t => t.Dependencies.Any(d => d.Target.FullName == nomeCompleto))
            .Select(static t => t.Name.Split('+')[0])
            .Distinct(),
    ];

    private static bool EhTipoDaCopia(string nome) =>
        new[] { "CopiaDosDocumentosDoAto", "AcervoDaDivulgacao" }
            .Any(tipo => nome == Caminho + tipo || nome.StartsWith(Caminho + tipo + "+", StringComparison.Ordinal));

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
