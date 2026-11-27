namespace Unifesspa.UniPlus.Selecao.Application.Queries.ObrigatoriedadesLegais;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Mappings;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Handler convention-based de <see cref="ObterObrigatoriedadeLegalQuery"/>.
/// Carrega a regra (filtrada por soft-delete via query filter padrão) e mapeia
/// para o DTO. A regra é cross-cutting por tipo de processo — sem proprietário
/// nem áreas de interesse.
/// </summary>
public static class ObterObrigatoriedadeLegalQueryHandler
{
    private static readonly DomainError NaoEncontrada = new("ObrigatoriedadeLegal.NaoEncontrada", "Obrigatoriedade legal não encontrada.");

    public static async Task<Result<ObrigatoriedadeLegalDto>> Handle(
        ObterObrigatoriedadeLegalQuery query,
        IObrigatoriedadeLegalRepository repository,
        ITipoProcessoReader tipos,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(tipos);

        ObrigatoriedadeLegal? regra = await repository.ObterPorIdAsync(query.Id, cancellationToken).ConfigureAwait(false);

        if (regra is null)
        {
            return Result<ObrigatoriedadeLegalDto>.Failure(NaoEncontrada);
        }

        IReadOnlyDictionary<string, string> nomes = await tipos.ObterNomesPorCodigosAsync(
            regra.TipoProcessoCodigo == TodosOsTiposDeProcesso.Codigo ? [] : [regra.TipoProcessoCodigo], cancellationToken).ConfigureAwait(false);
        return Result<ObrigatoriedadeLegalDto>.Success(ObrigatoriedadeLegalMapping.ToDto(regra, nomes));
    }
}
