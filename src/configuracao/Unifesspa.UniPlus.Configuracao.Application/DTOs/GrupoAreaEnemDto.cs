namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Grupo de área do ENEM com código e rótulo fixos (Anexo I da Resolução nº
/// 805/2024/Consepe). Aparece na leitura de cursos e de Pesos por Área e no vocabulário
/// dos quatro grupos; a gravação recebe só o código.
/// </summary>
/// <param name="Codigo">Código do grupo, sem abreviação e sem acento — o valor aceito na gravação.</param>
/// <param name="Rotulo">Rótulo do grupo, posto pelo sistema.</param>
public sealed record GrupoAreaEnemDto(
    string Codigo,
    string Rotulo);
