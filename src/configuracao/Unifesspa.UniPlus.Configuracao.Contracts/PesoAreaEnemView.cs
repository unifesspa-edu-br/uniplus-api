namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// DTO read-only de <c>PesoAreaEnem</c> para consumo cross-módulo via
/// <see cref="IPesoAreaEnemReader"/> (ADR-0056). Expõe a linha viva (resolução + grupo de
/// área + o peso e o corte de cada área + base legal) que o Módulo Seleção lê ao montar a
/// classificação de um processo, antes de congelar por valor no snapshot de publicação
/// (ADR-0061).
/// </summary>
/// <param name="Id">Identificador único (Guid v7 — ADR-0032).</param>
/// <param name="Resolucao">Resolução que fundamenta os pesos (parte 1 da chave de negócio, ex.: "Res. 805/2024").</param>
/// <param name="GrupoCurso">Grupo de área do ENEM, com código e rótulo (o código é a parte 2 da chave de negócio).</param>
/// <param name="Areas">As cinco áreas, na ordem canônica, com código, rótulo oficial, peso e corte.</param>
/// <param name="BaseLegal">Dispositivo legal que fundamenta a linha de pesos.</param>
public sealed record PesoAreaEnemView(
    Guid Id,
    string Resolucao,
    GrupoAreaEnemView GrupoCurso,
    IReadOnlyList<PesoAreaEnemAreaView> Areas,
    string BaseLegal);

/// <summary>Peso e corte de uma área numa linha de Pesos por Área.</summary>
/// <param name="Codigo">Código da área, sem abreviação e sem acento.</param>
/// <param name="Rotulo">Rótulo oficial da área.</param>
/// <param name="Peso">Peso da área na média.</param>
/// <param name="Corte">Nota mínima da área (0–1000), ou <see langword="null"/> sem corte.</param>
public sealed record PesoAreaEnemAreaView(
    string Codigo,
    string Rotulo,
    decimal Peso,
    decimal? Corte);
