namespace Unifesspa.UniPlus.Configuracao.Application.Commands.BaseLegalBonusRegional;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;

public sealed record AtualizarBaseLegalBonusRegionalCommand(
    Guid Id,
    [AceitaCodigosDe<TipoInstrumentoNormativo>] string? TipoInstrumento,
    string? Identificacao,
    string? Descricao,
    IEnumerable<CriarBaseLegalBonusRegionalMunicipioCommand>? Municipios) : ICommand<Result>;
