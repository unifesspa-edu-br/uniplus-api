namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Âncora de <see cref="ValueObjects.ReferenciaTemporalFatos"/> (Story #554, PR-b) — de onde
/// vem a <c>DateOnly</c> que apura <c>FAIXA_ETARIA</c> na publicação. <c>DATA_SUBMISSAO</c>
/// deliberadamente NÃO é uma variante aqui: ela só entra na idade de emissão do
/// <b>documento</b> (PR-d), nunca na idade do <b>candidato</b> (ADR-0111:235-236).
/// </summary>
public enum ReferenciaTipo
{
    Nenhuma = 0,

    /// <summary>Fim da fase que coleta inscrição (<c>FaseCronograma.ColetaInscricao</c>).</summary>
    FimInscricao = 1,

    /// <summary>Início de uma fase específica do cronograma — exige <c>FaseId</c>.</summary>
    InicioFase = 2,

    /// <summary>Fim de uma fase específica do cronograma — exige <c>FaseId</c>.</summary>
    FimFase = 3,

    /// <summary>Data fixa declarada pelo administrador — exige <c>Data</c>.</summary>
    DataEspecifica = 4,
}
