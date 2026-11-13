namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ProcessosSeletivos.Publicacao;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// Fecha a sessão editorial (ADR-0110): congela a versão N+1 <b>com a
/// configuração editada</b>, registra o ato por mensagem durável e encerra a sessão — tudo
/// na mesma transação.
/// </summary>
/// <remarks>
/// <para>
/// <b>É aqui que a Feature entrega o que ela existe para entregar.</b> Abrir e descartar
/// deixam o certame como estava; só o fechamento faz a configuração alterada virar
/// documento.
/// </para>
/// <para>
/// As conferências são <b>as mesmas</b> do atalho atômico, e pela mesma razão: o ato é
/// registrado depois, por mensagem durável (ADR-0108), então o que o catálogo de Publicações
/// recusaria tem de ser recusado <b>aqui</b>, com 422, antes de qualquer escrita — senão a
/// versão sai congelada, o cliente recebe 204, e a recusa vira dead letter.
/// </para>
/// <para>
/// <b>Uma recusa não destrói a sessão.</b> Se o congelamento for negado — conformidade
/// insuficiente, ato já retificado, documento não confirmado —, o rascunho <b>permanece
/// aberto</b>, com a configuração editada intacta: o administrador corrige e tenta de novo.
/// </para>
/// </remarks>
public static class FecharRetificacaoCommandHandler
{
    public static async Task<(Result Resposta, IEnumerable<object> Eventos)> Handle(
        FecharRetificacaoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ConferenciaDoAtoEDoDocumento atoEDocumento,
        CongelamentoDaVersao congelamento,
        LeituraDaBaseDaRetificacao leituraDaBase,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(atoEDocumento);
        ArgumentNullException.ThrowIfNull(congelamento);
        ArgumentNullException.ThrowIfNull(leituraDaBase);

        Result<ProcessoSeletivo> obtido = await ObterParaFecharAsync(command, processoSeletivoRepository, cancellationToken).ConfigureAwait(false);
        return obtido.IsFailure
            ? (Result.Failure(obtido.Error!), [])
            : await FecharAsync(obtido.Value!, command, atoEDocumento, congelamento, leituraDaBase, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<ProcessoSeletivo>> ObterParaFecharAsync(
        FecharRetificacaoCommand command, IProcessoSeletivoRepository repositorio, CancellationToken cancellationToken)
    {
        ProcessoSeletivo? processo = await repositorio
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result<ProcessoSeletivo>.Failure(DefinirFormularioCommandHandler.ProcessoNaoEncontrado(command.ProcessoSeletivoId));
        }

        return PrecondicoesDoFechamento.Recusa(command.Precondicao, processo) is { } recusa
            ? Result<ProcessoSeletivo>.Failure(recusa)
            : Result<ProcessoSeletivo>.Success(processo);
    }

    private static async Task<(Result Resposta, IEnumerable<object> Eventos)> FecharAsync(
        ProcessoSeletivo processo,
        FecharRetificacaoCommand command,
        ConferenciaDoAtoEDoDocumento atoEDocumento,
        CongelamentoDaVersao congelamento,
        LeituraDaBaseDaRetificacao leituraDaBase,
        CancellationToken cancellationToken)
    {
        Result<AtoEDocumentoConferidos> ato = await atoEDocumento
            .ConferirAsync(processo.Id, command.DocumentoEditalId, command.Ato, "no fechamento da retificação", cancellationToken)
            .ConfigureAwait(false);
        if (ato.IsFailure)
        {
            return (Result.Failure(ato.Error!), []);
        }

        Result<BaseDaRetificacao> baseLida = await leituraDaBase.DaSessaoAbertaAsync(processo, cancellationToken).ConfigureAwait(false);
        return baseLida.IsFailure
            ? (Result.ValidationFailure(baseLida.Errors), [])
            : await CongelarAsync(processo, command, ato.Value, baseLida.Value!, congelamento, leituraDaBase, cancellationToken).ConfigureAwait(false);
    }

    private static Task<(Result Resposta, IEnumerable<object> Eventos)> CongelarAsync(
        ProcessoSeletivo processo,
        FecharRetificacaoCommand command,
        AtoEDocumentoConferidos ato,
        BaseDaRetificacao baseDaRetificacao,
        CongelamentoDaVersao congelamento,
        LeituraDaBaseDaRetificacao leituraDaBase,
        CancellationToken cancellationToken)
    {
        EditalDeclarado edital = new(
            command.Numero, command.PeriodoInscricaoInicio, command.PeriodoInscricaoFim, command.DocumentoEditalId, command.Anexos);
        FechamentoDaSessao origem = new(baseDaRetificacao, processo.Rascunho!.FatosDasVersoesPublicadas);

        return congelamento.CongelarAsync(
            processo, edital, ato, origem,
            pedido => FecharComAUnidadeDaFaseConferidaAsync(processo, pedido, baseDaRetificacao.VersaoVigente, command.Precondicao, leituraDaBase, cancellationToken),
            cancellationToken);
    }

    // A fase pode ter começado depois da edição do cronograma: a unidade trocada é conferida contra o que a versão
    // publicada tem no instante do fechamento.
    private static async Task<Result<VersaoConfiguracao>> FecharComAUnidadeDaFaseConferidaAsync(
        ProcessoSeletivo processo,
        PedidoDeCongelamento pedido,
        VersaoConfiguracao versaoAtual,
        PrecondicaoIfMatch precondicao,
        LeituraDaBaseDaRetificacao leituraDaBase,
        CancellationToken cancellationToken) =>
        await leituraDaBase.RecusaPorFaseIniciadaAsync(processo, cancellationToken).ConfigureAwait(false) is { } recusa
            ? Result<VersaoConfiguracao>.Failure(recusa)
            : processo.FecharRetificacao(pedido, versaoAtual, precondicao);
}
