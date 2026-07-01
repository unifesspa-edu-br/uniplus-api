namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Snapshot mínimo de uma <c>Modalidade</c> de concorrência que o Módulo Seleção
/// congela por valor ao vinculá-la a um edital (snapshot-copy desacoplado,
/// ADR-0061): guarda a identidade de origem, o código classificatório e a ação de
/// indeferimento vigente no momento do congelamento — imune a edições posteriores
/// no cadastro vivo de Configuração.
/// </summary>
/// <param name="OrigemId">Id (Guid v7) da modalidade viva de origem, no momento do congelamento.</param>
/// <param name="Codigo">Código classificatório congelado (ex.: "LB_PPI").</param>
/// <param name="AcaoQuandoIndeferido">Ação ao indeferir (token UPPER_SNAKE) congelada, ou null.</param>
public sealed record ModalidadeSnapshot(
    Guid OrigemId,
    string Codigo,
    string? AcaoQuandoIndeferido);
