namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Domain.Errors;

using ProcessosSeletivos;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Confere no catálogo de Publicações, antes de enviar o ato, que o tipo declarado serve a um ato
/// avulso: vigora na data do documento, não congela configuração e não é resultado.
/// </summary>
/// <remarks>
/// O registro acontece depois, por mensagem durável (ADR-0108), e a recusa tardia iria para a fila de
/// mensagens que falharam, onde ninguém a recebe. Convocação e chamada não são avulsas porque
/// pertencem ao Ingresso; o catálogo as distingue pelo que determinam — a situação do candidato —, e
/// nenhum código compara o tipo com um literal (ADR-0103).
/// </remarks>
internal static class ConferenciaDoTipoDoAtoAvulso
{
    public static async Task<Result<TipoAtoPublicadoView>> ConferirAsync(
        ITipoAtoPublicadoReader reader,
        string tipoAtoCodigo,
        DateOnly dataPublicacao,
        CancellationToken cancellationToken)
    {
        Result<TipoAtoPublicadoView> vigente = await ConferenciaDoTipoDeAto
            .VigenteAsync(reader, tipoAtoCodigo, dataPublicacao, cancellationToken)
            .ConfigureAwait(false);
        if (vigente.IsFailure)
        {
            return vigente;
        }

        TipoAtoPublicadoView tipo = vigente.Value!;

        if (tipo.CongelaConfiguracao)
        {
            return Result<TipoAtoPublicadoView>.Failure(new DomainError(
                AtoDoProcessoErrorCodes.TipoCongelaConfiguracao,
                $"O tipo de ato '{tipo.Codigo}' congela configuração e só se publica com a publicação ou a retificação do edital — o documento avulso não altera a configuração do processo."));
        }

        return tipo.EhResultado
            ? Result<TipoAtoPublicadoView>.Failure(new DomainError(
                AtoDoProcessoErrorCodes.TipoEhResultado,
                $"O tipo de ato '{tipo.Codigo}' determina a situação do candidato, e resultado, convocação e chamada não são documento avulso: o resultado declarado no edital se publica pelo produto do edital, e convocação e chamada são do Ingresso."))
            : Result<TipoAtoPublicadoView>.Success(tipo);
    }
}
