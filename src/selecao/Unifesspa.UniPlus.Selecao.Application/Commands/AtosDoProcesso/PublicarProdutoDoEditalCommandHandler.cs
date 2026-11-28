namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Publica um produto declarado pela versão vigente do edital.</summary>
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
            ? (Result<AtoPublicadoDto>.ValidationFailure(ato.Errors), [])
            : await publicacao.PublicarAsync(ato.Value!, cancellationToken).ConfigureAwait(false);
    }
}
