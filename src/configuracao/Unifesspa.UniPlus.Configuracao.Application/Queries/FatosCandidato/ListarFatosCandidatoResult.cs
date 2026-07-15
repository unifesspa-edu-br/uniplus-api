namespace Unifesspa.UniPlus.Configuracao.Application.Queries.FatosCandidato;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Resultado da <see cref="ListarFatosCandidatoQuery"/>: o vocabulário fechado de
/// fatos do candidato projetado em <see cref="FatoCandidatoView"/> (contrato de
/// leitura cross-módulo). Não vaza a entidade de domínio.
/// </summary>
public sealed record ListarFatosCandidatoResult(IReadOnlyList<FatoCandidatoView> Itens);
