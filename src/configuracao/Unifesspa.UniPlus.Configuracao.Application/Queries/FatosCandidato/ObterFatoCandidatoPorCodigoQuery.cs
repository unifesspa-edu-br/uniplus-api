namespace Unifesspa.UniPlus.Configuracao.Application.Queries.FatosCandidato;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Resolve um fato do catálogo pela sua chave natural — o <c>Codigo</c>
/// (ex.: <c>COR_RACA</c>). Recusa com o erro de fato não encontrado quando o código
/// não existe no vocabulário.
/// </summary>
public sealed record ObterFatoCandidatoPorCodigoQuery(string Codigo) : IQuery<Result<FatoDoCatalogoDto>>;
