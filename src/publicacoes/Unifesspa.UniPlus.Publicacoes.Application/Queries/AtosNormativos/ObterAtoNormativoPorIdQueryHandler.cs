namespace Unifesspa.UniPlus.Publicacoes.Application.Queries.AtosNormativos;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Application.Avisos;
using Unifesspa.UniPlus.Publicacoes.Application.DTOs;
using Unifesspa.UniPlus.Publicacoes.Application.Mappings;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

public static class ObterAtoNormativoPorIdQueryHandler
{
    public static async Task<Result<AtoNormativoDto>> Handle(
        ObterAtoNormativoPorIdQuery query,
        IAtoNormativoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        Result<AtoNormativo> ato = await LocalizarAsync(repository, query.Id, cancellationToken).ConfigureAwait(false);
        if (ato.IsFailure)
        {
            return Result<AtoNormativoDto>.Failure(ato.Error!);
        }

        IReadOnlyList<AvisoNumeracao> avisos = await CalcularAvisosDaLinhagemAsync(repository, ato.Value!, cancellationToken)
            .ConfigureAwait(false);

        return Result<AtoNormativoDto>.Success(ato.Value!.ToDto() with { Avisos = avisos });
    }

    // Recomputa o aviso, excluindo do conjunto de conflitantes a linhagem
    // inteira: o próprio ato, a raiz e as demais retificações empilhadas. Uma
    // republicação com o mesmo número dentro da cadeia não é colisão (ADR-0103).
    // A cadeia sempre contém o próprio ato.
    private static async Task<IReadOnlyList<AvisoNumeracao>> CalcularAvisosDaLinhagemAsync(
        IAtoNormativoRepository repository,
        AtoNormativo ato,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> cadeia = await repository
            .ListarIdsDaCadeiaAsync(ato.Id, cancellationToken)
            .ConfigureAwait(false);

        return await AvisosDaNumeracao
            .CalcularAsync(repository, ato, cadeia, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<Result<AtoNormativo>> LocalizarAsync(
        IAtoNormativoRepository repository,
        Guid id,
        CancellationToken cancellationToken)
    {
        AtoNormativo? ato = await repository.ObterPorIdParaLeituraAsync(id, cancellationToken).ConfigureAwait(false);

        return ato is null
            ? Result<AtoNormativo>.Failure(new DomainError(AtoNormativoErrorCodes.NaoEncontrado, "Ato normativo não encontrado."))
            : Result<AtoNormativo>.Success(ato);
    }
}
