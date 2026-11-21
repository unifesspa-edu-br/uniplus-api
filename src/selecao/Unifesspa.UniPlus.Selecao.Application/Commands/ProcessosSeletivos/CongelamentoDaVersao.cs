namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.ProcessosSeletivos.Publicacao;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>
/// Congela uma versão de configuração, na ordem de recusa que a publicação e a retificação de ato único compartilham: a conferência do estado, o ato e o documento (que dão o instante único da operação), a conferência
/// da configuração (<see cref="ConferenciaDaConfiguracaoAoCongelar"/>), a montagem do pedido, a transição do agregado e a
/// gravação. O que difere entre as operações é a <see cref="OrigemDoCongelamento"/> e a transição que o handler passa. O
/// handler carrega o agregado com bloqueio e confere o que precede a base.
/// </summary>
public sealed class CongelamentoDaVersao(
    ConferenciaDoAtoEDoDocumento atoEDocumento,
    ConferenciaDaConfiguracaoAoCongelar conferencia,
    MontagemDoCongelamento montagem,
    GravacaoDaVersaoCongelada gravacao,
    IUnitOfWork unitOfWork)
{
    /// <summary>
    /// A cópia refeita dos dados do item altera o agregado rastreado antes da conferência da configuração; a recusa não devolve exceção, então
    /// o que ela mudou não pode ir para o banco: o rastreamento é descartado (ADR-0119).
    /// </summary>
    internal async Task<(Result Resposta, IEnumerable<object> Eventos)> CongelarAsync(
        ProcessoSeletivo processo,
        EditalDeclarado edital,
        DadosDoAto atoInformado,
        OrigemDoCongelamento origem,
        Func<PedidoDeCongelamento, Task<Result<VersaoConfiguracao>>> transicao,
        CancellationToken cancellationToken)
    {
        (Result Resposta, IEnumerable<object> Eventos) resultado = await TentarCongelarAsync(
            processo, edital, atoInformado, origem, transicao, cancellationToken).ConfigureAwait(false);
        if (resultado.Resposta.IsFailure)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
        }

        return resultado;
    }

    // O estado recusado é conflito e impede todo o resto; o ato e o documento impedem a conferência, porque dão o instante único.
    private async Task<(Result Resposta, IEnumerable<object> Eventos)> TentarCongelarAsync(
        ProcessoSeletivo processo,
        EditalDeclarado edital,
        DadosDoAto atoInformado,
        OrigemDoCongelamento origem,
        Func<PedidoDeCongelamento, Task<Result<VersaoConfiguracao>>> transicao,
        CancellationToken cancellationToken)
    {
        Result estado = origem.ConferirEstado(processo);
        if (estado.IsFailure)
        {
            return (estado, []);
        }

        Result<AtoEDocumentoConferidos> ato = await atoEDocumento
            .ConferirAsync(processo.Id, edital.DocumentoEditalId, atoInformado, origem, cancellationToken)
            .ConfigureAwait(false);

        return ato.IsFailure
            ? (Result.ValidationFailure(ato.Errors), [])
            : await ConferirTransitarEGravarAsync(processo, edital, ato.Value, origem, transicao, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(Result Resposta, IEnumerable<object> Eventos)> ConferirTransitarEGravarAsync(
        ProcessoSeletivo processo,
        EditalDeclarado edital,
        AtoEDocumentoConferidos ato,
        OrigemDoCongelamento origem,
        Func<PedidoDeCongelamento, Task<Result<VersaoConfiguracao>>> transicao,
        CancellationToken cancellationToken)
    {
        Result<ConfiguracaoConferida> conferida = await conferencia
            .ConferirAsync(processo, edital, ato, origem, cancellationToken).ConfigureAwait(false);

        return conferida.IsFailure
            ? (Result.ValidationFailure(conferida.Errors), [])
            : await TransitarEGravarAsync(processo, ato, origem, conferida.Value!, transicao, cancellationToken).ConfigureAwait(false);
    }

    // A transição reaplica a conferência do estado como guarda, inalcançável depois dela: sua falha não é a recusa da configuração.
    private async Task<(Result Resposta, IEnumerable<object> Eventos)> TransitarEGravarAsync(
        ProcessoSeletivo processo,
        AtoEDocumentoConferidos ato,
        OrigemDoCongelamento origem,
        ConfiguracaoConferida conferida,
        Func<PedidoDeCongelamento, Task<Result<VersaoConfiguracao>>> transicao,
        CancellationToken cancellationToken)
    {
        Result<VersaoConfiguracao> versao = await transicao(montagem.Montar(conferida, ato.Publicacao.Instante)).ConfigureAwait(false);

        return versao.IsFailure
            ? (Result.Failure(versao.Error!), [])
            : await gravacao
                .GravarAsync(processo, versao.Value!, ato, conferida.Entrada.Dados, origem.Retificacao, cancellationToken)
                .ConfigureAwait(false);
    }
}
