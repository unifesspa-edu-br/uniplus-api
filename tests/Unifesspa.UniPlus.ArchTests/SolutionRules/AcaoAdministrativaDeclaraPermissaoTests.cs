namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.Reflection;

using AwesomeAssertions;

using Microsoft.AspNetCore.Authorization;

using Unifesspa.UniPlus.ArchTests.Fixtures;
using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.API.Controllers;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Revisoes;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Revisoes;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Selecao.API.Authorization;
using Unifesspa.UniPlus.Selecao.API.Controllers;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Queries.CpfDaInscricao;
using Unifesspa.UniPlus.Selecao.Application.Queries.DocumentosDaInscricao;

/// <summary>
/// Toda action de qualquer módulo que não é anônima nem identificada por política de identidade declara a permissão
/// que a protege no ponto único de decisão (ADR-0078), e não fica só com <c>[Authorize]</c>, que apenas diz que a
/// pessoa está autenticada. As exceções são uma lista fechada: o que decide no handler e o que é, por natureza, de
/// qualquer pessoa autenticada.
/// </summary>
public sealed class AcaoAdministrativaDeclaraPermissaoTests
{
    /// <summary>
    /// Actions que decidem no handler, e não em atributo, porque a decisão depende de algo que só o handler carrega. A
    /// decisão continua no ponto único, e o teste confere que o handler da mensagem recebe <see cref="IVerificadorDeAcesso"/>.
    /// </summary>
    private static readonly Dictionary<string, Type> DecididasNoHandler = new(StringComparer.Ordinal)
    {
        // A criação do processo depende da unidade informada no corpo, porque o processo ainda não existe.
        [Acao(typeof(ProcessoSeletivoController), nameof(ProcessoSeletivoController.Criar))] = typeof(CriarProcessoSeletivoCommand),
        // A revelação do CPF e o acesso ao arquivo dependem da inscrição (e, no arquivo, da classificação congelada da
        // exigência), e a recusa responde o mesmo 404 do que não existe.
        [Acao(typeof(InscricoesDoProcessoController), nameof(InscricoesDoProcessoController.RevelarCpf))] = typeof(RevelarCpfDaInscricaoQuery),
        [Acao(typeof(InscricoesDoProcessoController), nameof(InscricoesDoProcessoController.AcessoAoDocumento))] = typeof(ObterAcessoAoDocumentoDaInscricaoQuery),
        // O pedido de concessão: a etapa que se decide vem da situação do pedido, a fila pedida vem da query, e quem
        // não é parte nem decide o pedido recebe o mesmo 404 do que não existe.
        [Acao(typeof(PedidosDeConcessaoController), nameof(PedidosDeConcessaoController.Listar))] = typeof(ListarPedidosDeConcessaoQuery),
        [Acao(typeof(PedidosDeConcessaoController), nameof(PedidosDeConcessaoController.PerfisParaPedir))] = typeof(ListarPerfisParaPedirQuery),
        [Acao(typeof(PedidosDeConcessaoController), nameof(PedidosDeConcessaoController.Obter))] = typeof(ObterPedidoDeConcessaoQuery),
        [Acao(typeof(PedidosDeConcessaoController), nameof(PedidosDeConcessaoController.Trilha))] = typeof(ObterTrilhaDoPedidoQuery),
        [Acao(typeof(PedidosDeConcessaoController), nameof(PedidosDeConcessaoController.LerAnexo))] = typeof(LerAnexoDoPedidoCommand),
        [Acao(typeof(PedidosDeConcessaoController), nameof(PedidosDeConcessaoController.Aprovar))] = typeof(AprovarPedidoDeConcessaoCommand),
        [Acao(typeof(PedidosDeConcessaoController), nameof(PedidosDeConcessaoController.Recusar))] = typeof(RecusarPedidoDeConcessaoCommand),
        // A revisão dos acessos: a permissão que vale depende da governança que decide o item, e a leitura é de quem
        // conduz a revisão ou de quem revê os acessos da proteção de dados.
        [Acao(typeof(RevisoesDeAcessosController), nameof(RevisoesDeAcessosController.Abrir))] = typeof(AbrirRevisaoDeAcessosCommand),
        [Acao(typeof(RevisoesDeAcessosController), nameof(RevisoesDeAcessosController.Listar))] = typeof(ListarRevisoesQuery),
        [Acao(typeof(RevisoesDeAcessosController), nameof(RevisoesDeAcessosController.Obter))] = typeof(ObterRevisaoQuery),
        [Acao(typeof(RevisoesDeAcessosController), nameof(RevisoesDeAcessosController.Confirmar))] = typeof(ConfirmarItemDaRevisaoCommand),
        [Acao(typeof(RevisoesDeAcessosController), nameof(RevisoesDeAcessosController.Revogar))] = typeof(RevogarItemDaRevisaoCommand),
    };

    /// <summary>
    /// Actions de qualquer pessoa autenticada: pedir acesso não dá acesso, porque o filtro é a aprovação; o aceite é só
    /// do beneficiário nomeado, conferido pelo e-mail verificado do token; anexar e cancelar são só do solicitante,
    /// antes da primeira decisão.
    /// </summary>
    private static readonly HashSet<string> DePessoaAutenticada = new(StringComparer.Ordinal)
    {
        Acao(typeof(PedidosDeConcessaoController), nameof(PedidosDeConcessaoController.Criar)),
        Acao(typeof(PedidosDeConcessaoController), nameof(PedidosDeConcessaoController.Aceitar)),
        Acao(typeof(PedidosDeConcessaoController), nameof(PedidosDeConcessaoController.EnviarAnexo)),
        Acao(typeof(PedidosDeConcessaoController), nameof(PedidosDeConcessaoController.ConfirmarAnexo)),
        Acao(typeof(PedidosDeConcessaoController), nameof(PedidosDeConcessaoController.Cancelar)),
    };

    /// <summary>
    /// Actions do titular: agem sobre a inscrição de quem chama, que o handler resolve pelo CPF do token; o que não
    /// é dele responde como o que não existe.
    /// </summary>
    private static readonly HashSet<string> DoTitular = new(StringComparer.Ordinal)
    {
        Acao(typeof(InscricoesController), nameof(InscricoesController.Obter)),
        Acao(typeof(InscricoesController), nameof(InscricoesController.ObterFormulario)),
        Acao(typeof(InscricoesController), nameof(InscricoesController.Avaliar)),
        Acao(typeof(InscricoesController), nameof(InscricoesController.Iniciar)),
        Acao(typeof(InscricoesController), nameof(InscricoesController.SalvarRascunho)),
        Acao(typeof(InscricoesController), nameof(InscricoesController.Descartar)),
        Acao(typeof(InscricoesController), nameof(InscricoesController.Cancelar)),
        Acao(typeof(InscricoesController), nameof(InscricoesController.Submeter)),
        Acao(typeof(InscricoesController), nameof(InscricoesController.ObterComprovante)),
        Acao(typeof(InscricoesController), nameof(InscricoesController.ObterComprovanteDaInscricao)),
        Acao(typeof(InscricoesController), nameof(InscricoesController.MinhasInscricoes)),
        Acao(typeof(DocumentosDaInscricaoController), nameof(DocumentosDaInscricaoController.IniciarEnvio)),
        Acao(typeof(DocumentosDaInscricaoController), nameof(DocumentosDaInscricaoController.ConfirmarEnvio)),
        Acao(typeof(DocumentosDaInscricaoController), nameof(DocumentosDaInscricaoController.Acesso)),
        Acao(typeof(DocumentosDaInscricaoController), nameof(DocumentosDaInscricaoController.RemoverArquivo)),
    };

    [Fact(DisplayName = "Fitness_DecisaoDeNegocioPassaPeloPontoUnico")]
    public void Fitness_DecisaoDeNegocioPassaPeloPontoUnico()
    {
        Sozinhas([typeof(FixturesDeAutorizacao.AcaoAdministrativaSoAutenticada)])
            .Should().ContainSingle().Which.Should().Contain("Publicar");
        Sozinhas([typeof(FixturesDeAutorizacao.AcaoAdministrativaSemAtributo)])
            .Should().ContainSingle().Which.Should().Contain("Publicar");
        Sozinhas([typeof(FixturesDeAutorizacao.AcaoAdministrativaDecididaPeloPontoUnico)]).Should().BeEmpty();
        Sozinhas([typeof(FixturesDeAutorizacao.PoliticaDeIdentidade)]).Should().BeEmpty("a ação da instalação é identificada pela política");

        IReadOnlyList<Type> controllers = ControllersDaSolucao.Todos();
        HashSet<string> acoes = [.. controllers.SelectMany(static t => ControllersDaSolucao.AcoesDe(t).Select(a => Acao(t, a.Name)))];

        controllers.SelectMany(static t => ControllersDaSolucao.AcoesDe(t).Where(a => DeclaraDecisao(t, a)))
            .Select(static a => a.DeclaringType!.Assembly.GetName().Name)
            .Distinct()
            .Should().HaveCountGreaterThan(1, "o ponto único protege actions de mais de um módulo");
        DecididasNoHandler.Keys.Concat(DePessoaAutenticada).Concat(DoTitular).Should().BeSubsetOf(acoes,
            "a lista fechada só nomeia actions que existem; action removida ou renomeada sai da lista");
        DecididasNoHandler.Values.Should().AllSatisfy(static mensagem => HandlerDe(mensagem)
            .GetParameters().Should().Contain(static p => p.ParameterType == typeof(IVerificadorDeAcesso)));
        Sozinhas(controllers).Should().BeEmpty(
            "action administrativa declara [ExigePermissao<…>], [ExigePermissaoNoProcesso] ou [ExigeLeituraDoProcesso]; só [Authorize] não decide acesso");
    }

    private static List<string> Sozinhas(IEnumerable<Type> controllers)
        => [.. controllers.SelectMany(static t => ControllersDaSolucao.AcoesDe(t)
            .Select(a => (Acao: a, Nome: Acao(t, a.Name)))
            .Where(a => !Anonima(t, a.Acao) && !PorPoliticaDeIdentidade(t, a.Acao)
                && !DeclaraDecisao(t, a.Acao) && !DecididasNoHandler.ContainsKey(a.Nome) && !DePessoaAutenticada.Contains(a.Nome) && !DoTitular.Contains(a.Nome))
            .Select(static a => a.Nome))];

    /// <summary>Chave da action com o nome completo do controller: controller homônimo de outro módulo não herda a exceção.</summary>
    private static string Acao(Type controller, string acao) => $"{controller.FullName}.{acao}";

    /// <summary>O handler fica na Application, junto da mensagem, como todo handler convention-based do Wolverine.</summary>
    private static MethodInfo HandlerDe(Type mensagem)
    {
        List<MethodInfo> handlers = [.. ControllersDaSolucao.TiposCarregaveis(mensagem.Assembly)
            .SelectMany(static t => t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.Name == "Handle" && m.GetParameters() is [var primeiro, ..] && primeiro.ParameterType == mensagem)];
        handlers.Should().ContainSingle($"{mensagem.Name} tem um único handler em {mensagem.Assembly.GetName().Name}");
        return handlers[0];
    }

    private static bool Anonima(Type controller, MethodInfo acao)
        => ControllersDaSolucao.AtributosDe<AllowAnonymousAttribute>(controller, acao).Any();

    private static bool PorPoliticaDeIdentidade(Type controller, MethodInfo acao)
        => ControllersDaSolucao.AtributosDe<AuthorizeAttribute>(controller, acao)
            .Any(static a => ControllersDaSolucao.PoliticasDeIdentidade.Contains(a.Policy, StringComparer.Ordinal));

    private static bool DeclaraDecisao(Type controller, MethodInfo acao)
        => ControllersDaSolucao.AtributosDe<Attribute>(controller, acao).Any(static a => a is ExigePermissaoNoProcessoAttribute
            or ExigeLeituraDoProcessoAttribute
            || (a.GetType().IsGenericType && a.GetType().GetGenericTypeDefinition() == typeof(ExigePermissaoAttribute<>)));
}
