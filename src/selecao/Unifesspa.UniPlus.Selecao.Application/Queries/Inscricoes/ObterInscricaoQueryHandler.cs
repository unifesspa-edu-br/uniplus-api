namespace Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;

using Domain.Entities;

using DTOs;

using Kernel.Results;

using Services;
using Services.Inscricoes;

/// <summary>Devolve ao titular a inscrição ativa com a avaliação do preenchimento gravado.</summary>
public static class ObterInscricaoQueryHandler
{
    public static async Task<Result<InscricaoDto>> Handle(
        ObterInscricaoQuery query, InscricaoDoDono dono, LeituraDaInscricao leitura, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(dono);
        ArgumentNullException.ThrowIfNull(leitura);
        Result<Inscricao> inscricao = await dono.ObterAtivaAsync(query.ProcessoSeletivoId, "ler a inscrição", cancellationToken).ConfigureAwait(false);
        return inscricao.IsFailure
            ? Result<InscricaoDto>.Failure(inscricao.Error!)
            : await leitura.LerAsync(inscricao.Value!, cancellationToken).ConfigureAwait(false);
    }
}
