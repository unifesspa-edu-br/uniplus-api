namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// DTO read-only de <c>PesoAreaEnem</c> para consumo cross-módulo via
/// <see cref="IPesoAreaEnemReader"/> (ADR-0056). Expõe a linha viva (resolução +
/// grupo de área + os cinco pesos + corte de redação + base legal) que o Módulo
/// Seleção lê ao montar a classificação de um processo, antes de congelar por
/// valor no bloco de classificação do snapshot de publicação (ADR-0061).
/// </summary>
/// <param name="Id">Identificador único (Guid v7 — ADR-0032).</param>
/// <param name="Resolucao">Resolução do INEP (parte 1 da chave de negócio, ex.: "Res. 805/2024").</param>
/// <param name="GrupoCurso">Grupo de área do ENEM (parte 2 da chave de negócio).</param>
/// <param name="PesoRedacao">Peso da Redação.</param>
/// <param name="PesoCienciasNatureza">Peso de Ciências da Natureza.</param>
/// <param name="PesoCienciasHumanas">Peso de Ciências Humanas.</param>
/// <param name="PesoLinguagens">Peso de Linguagens e Códigos.</param>
/// <param name="PesoMatematica">Peso de Matemática.</param>
/// <param name="CorteRedacao">Nota mínima de redação (corte) que pode eliminar o candidato.</param>
/// <param name="BaseLegal">Dispositivo legal que fundamenta a linha de pesos.</param>
public sealed record PesoAreaEnemView(
    Guid Id,
    string Resolucao,
    string GrupoCurso,
    decimal PesoRedacao,
    decimal PesoCienciasNatureza,
    decimal PesoCienciasHumanas,
    decimal PesoLinguagens,
    decimal PesoMatematica,
    decimal CorteRedacao,
    string BaseLegal);
