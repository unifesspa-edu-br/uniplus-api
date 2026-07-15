namespace Unifesspa.UniPlus.Configuracao.Application.Queries.FatosCandidato;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Lista todos os fatos do catálogo <c>rol_de_fatos_candidato</c> (ADR-0111), ordenados
/// por código. O vocabulário é fechado e de baixo volume (nove fatos), portanto a
/// leitura não é paginada.
/// </summary>
public sealed record ListarFatosCandidatoQuery : IQuery<ListarFatosCandidatoResult>;
