namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Grupo de área do ENEM com código e rótulo fixos (Anexo I da Resolução nº
/// 805/2024/Consepe), como os leitores cross-módulo o entregam.
/// </summary>
/// <param name="Codigo">Código do grupo, sem abreviação e sem acento — a identidade do grupo.</param>
/// <param name="Rotulo">Rótulo do grupo.</param>
public sealed record GrupoAreaEnemView(
    string Codigo,
    string Rotulo);
