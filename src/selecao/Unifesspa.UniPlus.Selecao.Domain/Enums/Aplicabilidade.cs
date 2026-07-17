namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Declara explicitamente a quem uma <see cref="Entities.DocumentoExigido"/> se aplica
/// (ADR-0071) — nunca inferida da presença/ausência de condições de gatilho.
/// </summary>
public enum Aplicabilidade
{
    Nenhuma = 0,

    /// <summary>Exigida de todos os candidatos — o gatilho não é avaliado.</summary>
    Geral = 1,

    /// <summary>Exigida apenas de quem satisfaz o gatilho — zero condições vivas significa, sem ambiguidade, exigida de ninguém.</summary>
    Condicional = 2,
}
