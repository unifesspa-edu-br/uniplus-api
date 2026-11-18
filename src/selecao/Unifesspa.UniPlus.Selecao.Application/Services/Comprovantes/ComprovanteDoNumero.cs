namespace Unifesspa.UniPlus.Selecao.Application.Services.Comprovantes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O comprovante número <c>numero</c> de uma inscrição, lido e montado: o titular e a unidade que o emite pedem o comprovante do
/// mesmo modo, e só o pedido é lido e decifrado. O número que a inscrição não tem é recusado antes de qualquer montagem.
/// </summary>
internal static class ComprovanteDoNumero
{
    public static async Task<Result<ComprovanteDaInscricaoDto>> MontarAsync(
        Inscricao inscricao,
        int numero,
        IComprovanteDaInscricaoRepository comprovanteRepository,
        MontadorDoComprovante montador,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inscricao);
        ArgumentNullException.ThrowIfNull(comprovanteRepository);
        ArgumentNullException.ThrowIfNull(montador);

        ComprovanteNaPosicao? posicao = await comprovanteRepository
            .ObterComprovanteAsync(inscricao.Id, numero, cancellationToken).ConfigureAwait(false);

        return posicao is null
            ? Result<ComprovanteDaInscricaoDto>.Failure(new DomainError(
                InscricaoErrorCodes.ComprovanteNaoEncontrado, "O comprovante solicitado não foi encontrado."))
            : await montador.MontarAsync(inscricao, posicao, cancellationToken).ConfigureAwait(false);
    }
}
