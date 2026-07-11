namespace Unifesspa.UniPlus.Publicacoes.Application.Queries.AtosNormativos;

using Unifesspa.UniPlus.Publicacoes.Application.Avisos;
using Unifesspa.UniPlus.Publicacoes.Application.DTOs;
using Unifesspa.UniPlus.Publicacoes.Application.Mappings;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

public static class ObterAtoNormativoPorIdQueryHandler
{
    public static async Task<AtoNormativoDto?> Handle(
        ObterAtoNormativoPorIdQuery query,
        IAtoNormativoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        AtoNormativo? ato = await repository
            .ObterPorIdParaLeituraAsync(query.Id, cancellationToken)
            .ConfigureAwait(false);
        if (ato is null)
        {
            return null;
        }

        // Recomputa o aviso (AC4), excluindo do conjunto de conflitantes a linhagem
        // inteira: o próprio ato, a raiz e as demais retificações empilhadas. Uma
        // republicação com o mesmo número dentro da cadeia não é colisão (ADR-0103).
        // A cadeia sempre contém o próprio ato.
        IReadOnlyList<Guid> cadeia = await repository
            .ListarIdsDaCadeiaAsync(ato.Id, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<AvisoNumeracao> avisos = await AvisoNumeracaoCalculator
            .CalcularAsync(repository, ato, cadeia, cancellationToken)
            .ConfigureAwait(false);

        return ato.ToDto() with { Avisos = avisos };
    }
}
