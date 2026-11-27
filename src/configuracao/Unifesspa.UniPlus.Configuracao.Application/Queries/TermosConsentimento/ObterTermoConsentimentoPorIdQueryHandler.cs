namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TermosConsentimento;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class ObterTermoConsentimentoPorIdQueryHandler
{
    private static readonly DomainError NaoEncontrado = new(
        TermoConsentimentoErrorCodes.NaoEncontrado,
        "Termo de consentimento não encontrado.");

    public static async Task<Result<TermoConsentimentoDto>> Handle(
        ObterTermoConsentimentoPorIdQuery query,
        ITermoConsentimentoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        TermoConsentimento? termo = await repository
            .ObterPorIdParaLeituraAsync(query.Id, cancellationToken)
            .ConfigureAwait(false);

        return termo is null
            ? Result<TermoConsentimentoDto>.Failure(NaoEncontrado)
            : Result<TermoConsentimentoDto>.Success(termo.ToDto());
    }
}
