namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TermosConsentimento;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ObterTermoConsentimentoPorIdQueryHandler
{
    public static async Task<TermoConsentimentoDto?> Handle(
        ObterTermoConsentimentoPorIdQuery query,
        ITermoConsentimentoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        TermoConsentimento? termo = await repository
            .ObterPorIdParaLeituraAsync(query.Id, cancellationToken)
            .ConfigureAwait(false);

        return termo?.ToDto();
    }
}
