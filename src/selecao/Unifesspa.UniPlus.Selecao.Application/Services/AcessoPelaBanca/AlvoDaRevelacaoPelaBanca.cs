namespace Unifesspa.UniPlus.Selecao.Application.Services.AcessoPelaBanca;

/// <summary>O que a banca quer revelar: o campo identificador de uma inscrição, na etapa e na fase em que a banca atua.</summary>
/// <param name="ProcessoId">O processo seletivo.</param>
/// <param name="FaseId">A fase que contém a etapa.</param>
/// <param name="EtapaId">A etapa em que a avaliação acontece.</param>
/// <param name="BancaId">A banca à qual a pessoa precisa estar designada.</param>
/// <param name="InscricaoId">A inscrição avaliada.</param>
/// <param name="FatoCodigo">O código do campo identificador.</param>
/// <param name="Ocorrencia">A ocorrência, quando o campo está num grupo que se repete.</param>
public sealed record AlvoDaRevelacaoPelaBanca(
    Guid ProcessoId, Guid FaseId, Guid EtapaId, Guid BancaId, Guid InscricaoId, string FatoCodigo, string? Ocorrencia);
