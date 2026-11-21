namespace Unifesspa.UniPlus.Configuracao.Application.Queries.FatosCandidato;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Resultado da <see cref="ListarFatosCandidatoQuery"/>: o vocabulário fechado de fatos do candidato, cada
/// fato com os operadores que uma condição pode usar sobre ele. Não vaza a entidade de domínio.
/// </summary>
public sealed record ListarFatosCandidatoResult(IReadOnlyList<FatoDoCatalogoDto> Itens);
