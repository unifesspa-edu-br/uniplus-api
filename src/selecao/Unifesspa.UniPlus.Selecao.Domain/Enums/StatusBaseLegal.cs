namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Valores de StatusBaseLegal.</summary>
public enum StatusBaseLegal
{
    [CodigoDeContrato("PENDENTE", "Pendente", Descricao = "A base legal ainda está pendente.")]
    Pendente = 1,
    [CodigoDeContrato("RESOLVIDO", "Resolvido", Descricao = "A base legal está resolvida.")]
    Resolvido = 2,
}
