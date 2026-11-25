namespace Unifesspa.UniPlus.Configuracao.Application.Queries.FatosCandidato;

using Unifesspa.UniPlus.Application.Abstractions.Consultas;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;

public static class ListarFatosCandidatoParaManutencaoQueryHandler
{
    public static async Task<Result<ListarFatosCandidatoParaManutencaoResult>> Handle(
        ListarFatosCandidatoParaManutencaoQuery query,
        IFatoCandidatoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        Result<OrigemFato?> origem = FiltroPorCodigoPedido.Ler<OrigemFato>(query.Origem,
            new DomainError(FatoCandidatoErrorCodes.FiltroDeOrigemInvalida, "Origem inválida. Use um dos códigos publicados para a origem do dado do candidato."));
        if (origem.IsFailure)
        {
            return Result<ListarFatosCandidatoParaManutencaoResult>.Failure(origem.Error!);
        }

        (IReadOnlyList<FatoCandidato> itens, Guid? anterior, Guid? proximo) = await repository.ListarPaginadoAsync(
            query.AfterId, query.Limit, query.Direction, origem.Value, query.Ativo, cancellationToken).ConfigureAwait(false);
        return Result<ListarFatosCandidatoParaManutencaoResult>.Success(new([.. itens.Select(static f => f.ToDto())], anterior, proximo));
    }
}
