namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// DTO read-only de <c>ReferenciaReservaDemografica</c> para consumo cross-módulo
/// via <see cref="IReferenciaReservaDemograficaReader"/> (ADR-0056). Expõe a
/// referência viva (Censo + os três percentuais demográficos + base legal) que o
/// Módulo Seleção lê ao montar a distribuição de vagas, antes de congelar por
/// valor no snapshot de publicação (ADR-0061).
/// </summary>
/// <param name="Id">Identificador único (Guid v7 — ADR-0032).</param>
/// <param name="CensoReferencia">Censo de referência (chave de negócio, ex.: "2022").</param>
/// <param name="PpiPercentual">Percentual de pretos, pardos e indígenas (art. 10, III, "a").</param>
/// <param name="QuilombolaPercentual">Percentual de quilombolas (art. 10, III, "b").</param>
/// <param name="PcdPercentual">Percentual de pessoas com deficiência (art. 10, III, "c", p.u.).</param>
/// <param name="BaseLegal">Dispositivo legal que fundamenta a referência.</param>
public sealed record ReferenciaReservaDemograficaView(
    Guid Id,
    string CensoReferencia,
    decimal PpiPercentual,
    decimal QuilombolaPercentual,
    decimal PcdPercentual,
    string BaseLegal);
