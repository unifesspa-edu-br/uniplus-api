namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Publica um item que a versão vigente do edital declara: confere o que o edital exige, grava o registro
/// do ato do processo com o item e o entrega à porta de escrita do registro central, na mesma transação
/// (ADR-0170).
/// </summary>
/// <remarks>
/// <para>
/// O item conta como publicado assim que a transação confirma: o definitivo que espera o preliminar passa a
/// poder sair, e a segunda publicação do item é recusada.
/// </para>
/// <para>
/// A primeira publicação do item é a que não corrige ato algum, e o índice único por processo e item a
/// garante contra a corrida. A correção leva o mesmo item e aponta a publicação corrigida, e por isso não
/// esbarra na unicidade; a versão que vale é a cabeça da cadeia de correções.
/// </para>
/// </remarks>
public static class PublicarProdutoDoEditalCommandHandler
{
    public static async Task<(Result<AtoPublicadoDto> Resposta, IEnumerable<object> Eventos)> Handle(
        PublicarProdutoDoEditalCommand command,
        IProcessoSeletivoRepository processos,
        PreparacaoDaPublicacaoDoProdutoDoEdital preparacao,
        PublicacaoDoAtoDoProcesso publicacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processos);
        ArgumentNullException.ThrowIfNull(preparacao);
        ArgumentNullException.ThrowIfNull(publicacao);
        Result correcao = AtoDoProcesso.ValidarCorrecao(command.AtoCorrigidoId, command.MotivoDaCorrecao);
        if (correcao.IsFailure)
        {
            return (Result<AtoPublicadoDto>.ValidationFailure(correcao.Errors), []);
        }
        await processos.TravarAsync(command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        Result<AtoAPublicar> ato = await preparacao.PrepararAsync(command, cancellationToken).ConfigureAwait(false);
        return ato.IsFailure
            ? (Result<AtoPublicadoDto>.Failure(ato.Error!), [])
            : await publicacao.PublicarAsync(ato.Value!, cancellationToken).ConfigureAwait(false);
    }
}
