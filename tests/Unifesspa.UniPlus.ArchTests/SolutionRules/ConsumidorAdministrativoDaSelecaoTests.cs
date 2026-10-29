namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.Reflection;

using AwesomeAssertions;

using Microsoft.AspNetCore.Authorization;

using Unifesspa.UniPlus.ArchTests.Fixtures;
using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Selecao.API.Authorization;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Queries.CpfDaInscricao;
using Unifesspa.UniPlus.Selecao.Application.Queries.DocumentosDaInscricao;

/// <summary>
/// A Seleção é o primeiro consumidor administrativo do ponto único de decisão (ADR-0078): toda
/// action que não é anônima nem do candidato declara a permissão que a protege, e não fica só
/// com <c>[Authorize]</c>, que apenas diz que a pessoa está autenticada.
/// </summary>
public sealed class ConsumidorAdministrativoDaSelecaoTests
{
    /// <summary>
    /// Actions que decidem no handler, e não em atributo: a criação do processo depende da unidade informada no corpo,
    /// porque o processo ainda não existe; a revelação do CPF e o acesso ao arquivo dependem da inscrição
    /// (e, no arquivo, da classificação congelada da exigência), que só o handler carrega, e a recusa responde o mesmo
    /// 404 do que não existe. A decisão continua no ponto único, e o
    /// teste confere que o handler recebe <see cref="IVerificadorDeAcesso"/>.
    /// </summary>
    private static readonly Dictionary<string, Type> DecididasNoHandler = new(StringComparer.Ordinal)
    {
        ["InscricoesDoProcessoController.RevelarCpf"] = typeof(RevelarCpfDaInscricaoQueryHandler),
        ["ProcessoSeletivoController.Criar"] = typeof(CriarProcessoSeletivoCommandHandler),
        ["InscricoesDoProcessoController.AcessoAoDocumento"] = typeof(ObterAcessoAoDocumentoDaInscricaoQueryHandler),
    };

    [Fact(DisplayName = "Fitness_DecisaoDeNegocioPassaPeloPontoUnico")]
    public void Fitness_DecisaoDeNegocioPassaPeloPontoUnico()
    {
        Sozinhas([typeof(FixturesDeAutorizacao.AcaoAdministrativaSoAutenticada)])
            .Should().ContainSingle().Which.Should().Contain("Publicar");
        Sozinhas([typeof(FixturesDeAutorizacao.AcaoAdministrativaDecididaPeloPontoUnico)]).Should().BeEmpty();
        Sozinhas([typeof(FixturesDeAutorizacao.PoliticaDeIdentidade)]).Should().BeEmpty("a ação do candidato é identificada pela política");

        IReadOnlyList<Type> controllers = ControllersDaSolucao.De("Selecao");
        IReadOnlyList<MethodInfo> acoes = [.. controllers.SelectMany(static t => ControllersDaSolucao.AcoesDe(t))];

        acoes.Where(static a => DeclaraDecisao(a.DeclaringType!, a)).Should().NotBeEmpty(
            "o consumidor administrativo existe: há actions da Seleção protegidas pelo ponto único");
        DecididasNoHandler.Values.Should().AllSatisfy(static handler => handler
            .GetMethod("Handle")!.GetParameters().Should().Contain(static p => p.ParameterType == typeof(IVerificadorDeAcesso)));
        Sozinhas(controllers).Should().BeEmpty(
            "action administrativa da Seleção declara [ExigePermissao<…>], [ExigePermissaoNoProcesso] ou [ExigeLeituraDoProcesso]; só [Authorize] não decide acesso");
    }

    private static List<string> Sozinhas(IEnumerable<Type> controllers)
        => [.. controllers.SelectMany(static t => ControllersDaSolucao.AcoesDe(t)
            .Where(a => !Anonima(t, a) && !DoCandidato(t, a) && !DeclaraDecisao(t, a) && !DecididasNoHandler.ContainsKey($"{t.Name}.{a.Name}"))
            .Select(a => $"{t.Name}.{a.Name}"))];

    private static bool Anonima(Type controller, MethodInfo acao)
        => ControllersDaSolucao.AtributosDe<AllowAnonymousAttribute>(controller, acao).Any();

    private static bool DoCandidato(Type controller, MethodInfo acao)
        => ControllersDaSolucao.AtributosDe<AuthorizeAttribute>(controller, acao).Any(static a => a.Policy == "Candidato");

    private static bool DeclaraDecisao(Type controller, MethodInfo acao)
        => ControllersDaSolucao.AtributosDe<Attribute>(controller, acao).Any(static a => a is ExigePermissaoNoProcessoAttribute
            or ExigeLeituraDoProcessoAttribute
            || (a.GetType().IsGenericType && a.GetType().GetGenericTypeDefinition() == typeof(ExigePermissaoAttribute<>)));
}
