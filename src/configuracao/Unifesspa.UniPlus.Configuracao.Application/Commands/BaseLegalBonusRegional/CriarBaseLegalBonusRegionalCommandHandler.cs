namespace Unifesspa.UniPlus.Configuracao.Application.Commands.BaseLegalBonusRegional;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class CriarBaseLegalBonusRegionalCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarBaseLegalBonusRegionalCommand command,
        IBaseLegalBonusRegionalRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        // m pode ser null (JSON "municipios": [null]) — os options padrão não rejeitam
        // elemento nulo em coleção genérica; sem o ?., o projetado estouraria
        // NullReferenceException em vez de virar 422 (ReferenciaCidadeGeo.Validar já
        // recusa código/nome/UF nulos como campo obrigatório ausente).
        IEnumerable<(string? CodigoIbge, string? Nome, string? Uf)>? municipiosTuples =
            command.Municipios?.Select(m => (m?.CodigoIbge, m?.Nome, m?.Uf));

        Result<BaseLegalBonusRegional> entityResult = BaseLegalBonusRegional.Criar(
            command.TipoInstrumento,
            command.Identificacao,
            command.Descricao,
            municipiosTuples);

        if (entityResult.IsFailure)
        {
            return Result<Guid>.ValidationFailure(entityResult.Errors);
        }

        BaseLegalBonusRegional entity = entityResult.Value!;

        await repository.AdicionarAsync(entity, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<Guid>.Success(entity.Id);
    }
}
