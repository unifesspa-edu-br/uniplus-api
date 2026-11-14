namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.ProcessosSeletivos.Publicacao;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>
/// Congela uma versão de configuração: confere a configuração (<see cref="ConferenciaDaConfiguracaoAoCongelar"/>),
/// monta o pedido, deixa o agregado transitar e grava. A ordem das recusas é uma só para a publicação, a retificação
/// de ato único e o fechamento da sessão, e o que difere entre elas é a <see cref="OrigemDoCongelamento"/> e a
/// transição que o handler passa. O handler carrega o agregado com trava e confere o que precede a base (ADR-0110).
/// </summary>
public sealed class CongelamentoDaVersao(
    ConferenciaDaConfiguracaoAoCongelar conferencia,
    MontagemDoCongelamento montagem,
    GravacaoDaVersaoCongelada gravacao,
    IUnitOfWork unitOfWork)
{
    /// <summary>
    /// A recópia das cópias do item altera o agregado rastreado antes dos gates seguintes; a recusa não devolve
    /// exceção, então o que ela mudou não pode ir para o banco: o rastreamento é descartado (ADR-0119).
    /// </summary>
    internal async Task<(Result Resposta, IEnumerable<object> Eventos)> CongelarAsync(
        ProcessoSeletivo processo,
        EditalDeclarado edital,
        AtoEDocumentoConferidos ato,
        OrigemDoCongelamento origem,
        Func<PedidoDeCongelamento, Task<Result<VersaoConfiguracao>>> transicao,
        CancellationToken cancellationToken)
    {
        (Result Resposta, IEnumerable<object> Eventos) resultado = await ConferirTransitarEGravarAsync(
            processo, edital, ato, origem, transicao, cancellationToken).ConfigureAwait(false);
        if (resultado.Resposta.IsFailure)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
        }

        return resultado;
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
        if (conferida.IsFailure)
        {
            return (Result.ValidationFailure(conferida.Errors), []);
        }

        PedidoDeCongelamento pedido = await montagem.MontarAsync(conferida.Value!, cancellationToken).ConfigureAwait(false);
        Result<VersaoConfiguracao> versao = await transicao(pedido).ConfigureAwait(false);
        return versao.IsFailure
            ? (Result.Failure(versao.Error!), [])
            : await gravacao
                .GravarAsync(processo, versao.Value!, ato, conferida.Value!.Entrada.Dados, origem.Retificacao, cancellationToken)
                .ConfigureAwait(false);
    }
}
