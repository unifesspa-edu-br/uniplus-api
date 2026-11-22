namespace Unifesspa.UniPlus.TestSupport;

using Unifesspa.UniPlus.Autorizacao.API.Controllers;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Reclassificacoes;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Revisoes;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.RevisoesDasConcessoes;
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
using Unifesspa.UniPlus.Selecao.Application.Commands.Rascunhos;
using Unifesspa.UniPlus.Selecao.Application.Queries.CampoDaInscricao;
using Unifesspa.UniPlus.Selecao.Application.Queries.DocumentosDaInscricao;
using Unifesspa.UniPlus.Selecao.Application.Queries.InscricoesDoProcesso;

internal static class ExcecoesDeAcessoDasAcoes
{
    /// <summary>
    /// Actions que decidem no handler, e não em atributo, porque a decisão depende de algo que só o handler carrega. A
    /// decisão continua no ponto único, e o teste confere que o handler da mensagem recebe <see cref="IVerificadorDeAcesso"/>,
    /// direto ou pelo serviço de aplicação que o recebe no construtor.
    /// </summary>
    internal static readonly Dictionary<string, Type> DecididasNoHandler = new(StringComparer.Ordinal)
    {
        // A criação do processo depende da unidade informada no corpo, porque o processo ainda não existe.
        [Acao(typeof(ProcessoSeletivoController), nameof(ProcessoSeletivoController.Criar))] = typeof(CriarProcessoSeletivoCommand),
        // A criação do rascunho depende da unidade que o documento informa, porque o rascunho ainda não existe.
        [Acao(typeof(RascunhosDeProcessosSeletivosController), nameof(RascunhosDeProcessosSeletivosController.Criar))] = typeof(CriarRascunhoDeProcessoSeletivoCommand),
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
        // Na revisão das concessões, o envio de decisões, a retida e o encerramento são da gestão de acessos. A proteção
        // de dados decide sua etapa; a leitura é de quem mantém perfis ou aprova por qualquer das duas etapas.
        [Acao(typeof(RevisaoDasConcessoesController), nameof(RevisaoDasConcessoesController.Obter))] = typeof(ObterRevisaoDasConcessoesDoPerfilQuery),
        [Acao(typeof(RevisaoDasConcessoesController), nameof(RevisaoDasConcessoesController.Fila))] = typeof(ListarFilaDeRevisoesDasConcessoesQuery),
        [Acao(typeof(RevisaoDasConcessoesController), nameof(RevisaoDasConcessoesController.EnviarDecisoesDaGestao))] = typeof(EnviarDecisoesDaRevisaoCommand),
        [Acao(typeof(RevisaoDasConcessoesController), nameof(RevisaoDasConcessoesController.DecidirRetida))] = typeof(DecidirRetidaDaRevisaoCommand),
        [Acao(typeof(RevisaoDasConcessoesController), nameof(RevisaoDasConcessoesController.DecidirProtecaoDeDados))] = typeof(DecidirEtapaDaProtecaoDaRevisaoCommand),
        [Acao(typeof(RevisaoDasConcessoesController), nameof(RevisaoDasConcessoesController.Encerrar))] = typeof(EncerrarRevisaoDoPerfilCommand),
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
    internal static readonly HashSet<string> DePessoaAutenticada = new(StringComparer.Ordinal)
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
    internal static readonly HashSet<string> DoTitular = new(StringComparer.Ordinal)
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

    internal static string Acao(Type controller, string acao) => $"{controller.FullName}.{acao}";
}
