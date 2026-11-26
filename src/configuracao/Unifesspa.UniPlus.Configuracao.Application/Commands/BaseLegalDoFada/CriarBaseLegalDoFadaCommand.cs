namespace Unifesspa.UniPlus.Configuracao.Application.Commands.BaseLegalDoFada;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;

public sealed record CriarMunicipioDaBaseLegalDoFadaCommand(
    string? CodigoIbge,
    string? Nome,
    string? Uf)
{
    // m pode ser null (JSON "municipios": [null]) — os options padrão não rejeitam elemento nulo em coleção genérica; sem
    // o ?., o projetado estouraria NullReferenceException em vez de virar 422 (ReferenciaCidadeGeo.Validar já recusa
    // código/nome/UF nulos como campo obrigatório ausente).
    internal static IEnumerable<(string? CodigoIbge, string? Nome, string? Uf)>? ParaABaseLegal(
        IEnumerable<CriarMunicipioDaBaseLegalDoFadaCommand?>? municipios) =>
        municipios?.Select(m => (m?.CodigoIbge, m?.Nome, m?.Uf));
}

public sealed record CriarBaseLegalDoFadaCommand(
    [AceitaCodigosDe<TipoInstrumentoNormativo>] string? TipoInstrumento,
    string? Identificacao,
    string? Descricao,
    IEnumerable<CriarMunicipioDaBaseLegalDoFadaCommand>? Municipios) : ICommand<Result<Guid>>;
