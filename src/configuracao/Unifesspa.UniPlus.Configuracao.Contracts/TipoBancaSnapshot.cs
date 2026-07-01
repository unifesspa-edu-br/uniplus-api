namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Snapshot mínimo de um <c>TipoBanca</c> que o Módulo Seleção congela por valor ao
/// registrá-lo nas bancas requeridas por fase de um processo (snapshot-copy
/// desacoplado, ADR-0061): guarda a identidade de origem e o código classificatório
/// vigente no momento do congelamento — imune a edições posteriores no cadastro vivo
/// de Configuração.
/// </summary>
/// <param name="OrigemId">Id (Guid v7) do tipo de banca vivo de origem, no momento do congelamento.</param>
/// <param name="Codigo">Código classificatório congelado (ex.: "BANCA_ANALISE_DOCUMENTAL").</param>
public sealed record TipoBancaSnapshot(
    Guid OrigemId,
    string Codigo);
