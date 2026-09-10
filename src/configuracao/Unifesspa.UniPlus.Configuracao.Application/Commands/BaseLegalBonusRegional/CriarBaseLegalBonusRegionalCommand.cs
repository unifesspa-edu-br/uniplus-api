namespace Unifesspa.UniPlus.Configuracao.Application.Commands.BaseLegalBonusRegional;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

public sealed record CriarBaseLegalBonusRegionalMunicipioCommand(
    string? CodigoIbge,
    string? Nome,
    string? Uf);

public sealed record CriarBaseLegalBonusRegionalCommand(
    string? TipoInstrumento,
    string? Identificacao,
    string? Descricao,
    IEnumerable<CriarBaseLegalBonusRegionalMunicipioCommand>? Municipios) : ICommand<Result<Guid>>;
