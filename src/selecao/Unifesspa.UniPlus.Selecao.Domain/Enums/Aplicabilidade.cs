namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Declara explicitamente a quem uma <see cref="Entities.DocumentoExigido"/> se aplica
/// (ADR-0071) — nunca inferida da presença/ausência de condições. A declaração ausente é
/// <see langword="null"/> no pedido, e o documento exigido só existe com uma das duas.
/// </summary>
public enum Aplicabilidade
{
    [CodigoDeContrato(
        "GERAL",
        "Todos os candidatos",
        Descricao = "Exigida de todos os candidatos: as condições não são avaliadas.")]
    Geral = 1,

    [CodigoDeContrato(
        "CONDICIONAL",
        "Só quem atende às condições",
        Descricao = "Exigida apenas de quem atende às condições declaradas; sem nenhuma condição ativa, não é exigida de ninguém.")]
    Condicional = 2,
}
