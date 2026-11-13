namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.Reflection;

using AwesomeAssertions;

using Microsoft.AspNetCore.Authorization;

using Unifesspa.UniPlus.ArchTests.Fixtures;
using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.API.Controllers;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Reclassificacoes;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Revisoes;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Reclassificacoes;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Revisoes;
using Unifesspa.UniPlus.Configuracao.API.Controllers;
using Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Configuracao.Application.Queries.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Selecao.API.Authorization;
using Unifesspa.UniPlus.Selecao.API.Controllers;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Queries.CampoDaInscricao;
using Unifesspa.UniPlus.Selecao.Application.Queries.DocumentosDaInscricao;
using Unifesspa.UniPlus.Selecao.Application.Queries.InscricoesDoProcesso;

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
        // O detalhe, a revelação de um campo e o acesso ao arquivo dependem da inscrição (e, no campo, da classificação congelada do fato
        // e, no arquivo, da da exigência), e a recusa responde o mesmo 404 do que não existe.
        [Acao(typeof(InscricoesDoProcessoController), nameof(InscricoesDoProcessoController.Obter))] = typeof(ObterInscricaoAdministrativaQuery),
        [Acao(typeof(InscricoesDoProcessoController), nameof(InscricoesDoProcessoController.RevelarCampo))] = typeof(RevelarCampoDaInscricaoQuery),
        [Acao(typeof(InscricoesDoProcessoController), nameof(InscricoesDoProcessoController.AcessoAoDocumento))] = typeof(ObterAcessoAoDocumentoDaInscricaoQuery),
        // A emissão do comprovante decide no handler, que confere o vínculo antes das duas permissões: consultar responde o 404 do que
        // não existe, e emitir responde 403 a quem já alcança a inscrição.
        [Acao(typeof(InscricoesDoProcessoController), nameof(InscricoesDoProcessoController.EmitirComprovante))] = typeof(EmitirComprovanteDaInscricaoQuery),
        // O pedido de concessão: a etapa que se decide vem da situação do pedido, a fila pedida vem da query, e quem
        // não é parte nem decide o pedido recebe o mesmo 404 do que não existe.
        [Acao(typeof(PedidosDeConcessaoController), nameof(PedidosDeConcessaoController.Listar))] = typeof(ListarPedidosDeConcessaoQuery),
        [Acao(typeof(PedidosDeConcessaoController), nameof(PedidosDeConcessaoController.PerfisParaPedir))] = typeof(ListarPerfisParaPedirQuery),
        [Acao(typeof(PedidosDeConcessaoController), nameof(PedidosDeConcessaoController.Obter))] = typeof(ObterPedidoDeConcessaoQuery),
        [Acao(typeof(PedidosDeConcessaoController), nameof(PedidosDeConcessaoController.Trilha))] = typeof(ObterTrilhaDoPedidoQuery),
        [Acao(typeof(AnexosDoPedidoController), nameof(AnexosDoPedidoController.LerAnexo))] = typeof(LerAnexoDoPedidoCommand),
        [Acao(typeof(PedidosDeConcessaoController), nameof(PedidosDeConcessaoController.Aprovar))] = typeof(AprovarPedidoDeConcessaoCommand),
        [Acao(typeof(PedidosDeConcessaoController), nameof(PedidosDeConcessaoController.Recusar))] = typeof(RecusarPedidoDeConcessaoCommand),
        // A governança das versões do perfil: a permissão que vale é a da etapa pedida, o cancelamento é de quem criou a
        // versão ou de quem mantém perfis, e a leitura do documento é de quem mantém perfis ou aprova pela proteção de dados.
        [Acao(typeof(AprovacaoDeVersoesController), nameof(AprovacaoDeVersoesController.Aprovar))] = typeof(AprovarVersaoDoPerfilCommand),
        [Acao(typeof(AprovacaoDeVersoesController), nameof(AprovacaoDeVersoesController.Recusar))] = typeof(RecusarVersaoDoPerfilCommand),
        [Acao(typeof(AprovacaoDeVersoesController), nameof(AprovacaoDeVersoesController.Cancelar))] = typeof(CancelarVersaoDoPerfilCommand),
        [Acao(typeof(AprovacaoDeVersoesController), nameof(AprovacaoDeVersoesController.Fila))] = typeof(ListarFilaDeVersoesQuery),
        [Acao(typeof(VersoesDoPerfilController), nameof(VersoesDoPerfilController.Ler))] = typeof(LerDocumentoDaVersaoCommand),
        // A leitura do perfil e da listagem é de quem mantém perfis ou aprova por qualquer das duas etapas; as ações de
        // manutenção seguem no atributo.
        [Acao(typeof(PerfisController), nameof(PerfisController.Listar))] = typeof(ListarPerfisQuery),
        [Acao(typeof(PerfisController), nameof(PerfisController.ObterPorId))] = typeof(ObterPerfilQuery),
        // A janela de migração: o lote, a retida e o encerramento são da gestão de acessos, a etapa da proteção de dados é
        // da proteção de dados, e a leitura é de quem mantém perfis ou aprova por qualquer das duas etapas.
        [Acao(typeof(JanelaDeMigracaoController), nameof(JanelaDeMigracaoController.Obter))] = typeof(ObterJanelaDoPerfilQuery),
        [Acao(typeof(JanelaDeMigracaoController), nameof(JanelaDeMigracaoController.Fila))] = typeof(ListarFilaDeJanelasQuery),
        [Acao(typeof(JanelaDeMigracaoController), nameof(JanelaDeMigracaoController.EnviarLote))] = typeof(EnviarLoteDaJanelaCommand),
        [Acao(typeof(JanelaDeMigracaoController), nameof(JanelaDeMigracaoController.DecidirRetida))] = typeof(DecidirRetidaDaJanelaCommand),
        [Acao(typeof(JanelaDeMigracaoController), nameof(JanelaDeMigracaoController.DecidirProtecaoDeDados))] = typeof(DecidirEtapaDaProtecaoDaJanelaCommand),
        [Acao(typeof(JanelaDeMigracaoController), nameof(JanelaDeMigracaoController.Encerrar))] = typeof(EncerrarJanelaDoPerfilCommand),
        // A contenção do perfil: suspender e retomar pedem manter perfis ou a proteção de dados, e a etapa da retomada
        // define qual vale; a leitura da trilha é de quem tem uma das duas.
        [Acao(typeof(ContencaoDoPerfilController), nameof(ContencaoDoPerfilController.Suspender))] = typeof(SuspenderPerfilCommand),
        [Acao(typeof(ContencaoDoPerfilController), nameof(ContencaoDoPerfilController.Retomar))] = typeof(RetomarSuspensaoDoPerfilCommand),
        [Acao(typeof(ContencaoDoPerfilController), nameof(ContencaoDoPerfilController.ObterTrilha))] = typeof(ObterContencaoDoPerfilQuery),
        // A reclassificação do catálogo: a etapa pedida define a permissão da fila e da decisão, a leitura é de quem mantém
        // perfis ou aprova pela proteção de dados, e a divergência da governança é de quem tem uma das duas.
        [Acao(typeof(ReclassificacoesController), nameof(ReclassificacoesController.Listar))] = typeof(ListarReclassificacoesQuery),
        [Acao(typeof(ReclassificacoesController), nameof(ReclassificacoesController.Obter))] = typeof(ObterReclassificacaoQuery),
        [Acao(typeof(ReclassificacoesController), nameof(ReclassificacoesController.Decidir))] = typeof(DecidirReclassificacaoCommand),
        [Acao(typeof(ReclassificacoesController), nameof(ReclassificacoesController.Divergencias))] = typeof(ListarDivergenciasDaGovernancaQuery),
        // A revisão dos acessos: a permissão que vale depende da governança que decide o item, e a leitura é de quem
        // conduz a revisão ou de quem revê os acessos da proteção de dados.
        [Acao(typeof(RevisoesDeAcessosController), nameof(RevisoesDeAcessosController.Abrir))] = typeof(AbrirRevisaoDeAcessosCommand),
        [Acao(typeof(RevisoesDeAcessosController), nameof(RevisoesDeAcessosController.Listar))] = typeof(ListarRevisoesQuery),
        [Acao(typeof(RevisoesDeAcessosController), nameof(RevisoesDeAcessosController.Obter))] = typeof(ObterRevisaoQuery),
        [Acao(typeof(RevisoesDeAcessosController), nameof(RevisoesDeAcessosController.Confirmar))] = typeof(ConfirmarItemDaRevisaoCommand),
        [Acao(typeof(RevisoesDeAcessosController), nameof(RevisoesDeAcessosController.Revogar))] = typeof(RevogarItemDaRevisaoCommand),
        // A solicitação e a consulta da proteção do catálogo alcançam os dois cadastros de uma vez, e a permissão de cada um
        // é decidida no handler, que fica com o único DbContext da Configuração.
        [Acao(typeof(ProtecaoDoCatalogoController), nameof(ProtecaoDoCatalogoController.Solicitar))] = typeof(SolicitarAprovacaoDaProtecaoCommand),
        [Acao(typeof(ProtecaoDoCatalogoController), nameof(ProtecaoDoCatalogoController.Listar))] = typeof(ListarProtecaoDoCatalogoQuery),
    };

    /// <summary>
    /// Actions de qualquer pessoa autenticada: pedir acesso não dá acesso, porque o filtro é a aprovação; o aceite é só
    /// do beneficiário nomeado, conferido pelo e-mail verificado do token; anexar e cancelar são só do solicitante,
    /// antes da primeira decisão; o termo de responsabilidade, texto institucional sem dado pessoal, é lido por quem vai
    /// pedir ou aceitar; as permissões efetivas são as do próprio token de quem consulta.
    /// </summary>
    private static readonly HashSet<string> DePessoaAutenticada = new(StringComparer.Ordinal)
    {
        Acao(typeof(PedidosDeConcessaoController), nameof(PedidosDeConcessaoController.Criar)),
        Acao(typeof(PedidosDeConcessaoController), nameof(PedidosDeConcessaoController.Aceitar)),
        Acao(typeof(AnexosDoPedidoController), nameof(AnexosDoPedidoController.EnviarAnexo)),
        Acao(typeof(AnexosDoPedidoController), nameof(AnexosDoPedidoController.ConfirmarAnexo)),
        Acao(typeof(TermosDeResponsabilidadeController), nameof(TermosDeResponsabilidadeController.ObterVigente)),
        Acao(typeof(TermosDeResponsabilidadeController), nameof(TermosDeResponsabilidadeController.ObterVersao)),
        Acao(typeof(PedidosDeConcessaoController), nameof(PedidosDeConcessaoController.Cancelar)),
        Acao(typeof(PermissoesController), nameof(PermissoesController.ConsultarEfetivas)),
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
        ConferirAsFixturesDaRegra();

        IReadOnlyList<Type> controllers = ControllersDaSolucao.Todos();
        controllers.SelectMany(static t => ControllersDaSolucao.AcoesDe(t).Where(a => DeclaraDecisao(t, a)))
            .Select(static a => a.DeclaringType!.Assembly.GetName().Name)
            .Distinct()
            .Should().HaveCountGreaterThan(1, "o ponto único protege actions de mais de um módulo");
        ConferirAsListasFechadas(controllers);
        Sozinhas(controllers).Should().BeEmpty(
            "action administrativa declara [ExigePermissao<…>], [ExigePermissaoNoProcesso] ou [ExigeLeituraDoProcesso]; só [Authorize] não decide acesso");
    }

    // A regra acusa a action só autenticada e a sem atributo, e aceita a decidida pelo ponto único e a da política de identidade.
    private static void ConferirAsFixturesDaRegra()
    {
        Sozinhas([typeof(FixturesDeAutorizacao.AcaoAdministrativaSoAutenticada)])
            .Should().ContainSingle().Which.Should().Contain("Publicar");
        Sozinhas([typeof(FixturesDeAutorizacao.AcaoAdministrativaSemAtributo)])
            .Should().ContainSingle().Which.Should().Contain("Publicar");
        Sozinhas([typeof(FixturesDeAutorizacao.AcaoAdministrativaDecididaPeloPontoUnico)]).Should().BeEmpty();
        Sozinhas([typeof(FixturesDeAutorizacao.PoliticaDeIdentidade)]).Should().BeEmpty("a ação da instalação é identificada pela política");
    }

    private static void ConferirAsListasFechadas(IReadOnlyList<Type> controllers)
    {
        HashSet<string> acoes = [.. controllers.SelectMany(static t => ControllersDaSolucao.AcoesDe(t).Select(a => Acao(t, a.Name)))];
        DecididasNoHandler.Keys.Concat(DePessoaAutenticada).Concat(DoTitular).Should().BeSubsetOf(acoes,
            "a lista fechada só nomeia actions que existem; action removida ou renomeada sai da lista");
        DecididasNoHandler.Values.Should().AllSatisfy(static mensagem => HandlerDe(mensagem)
            .GetParameters().Should().Contain(static p => p.ParameterType == typeof(IVerificadorDeAcesso)));
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
