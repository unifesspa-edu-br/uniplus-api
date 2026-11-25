namespace Unifesspa.UniPlus.Regras.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Situação da análise da base legal.</summary>
public enum StatusBaseLegal
{
    [CodigoDeContrato("PENDENTE", "Pendente", Descricao = "A base legal ainda está pendente.")]
    Pendente = 1,
    [CodigoDeContrato("RESOLVIDO", "Resolvido", Descricao = "A base legal está resolvida.")]
    Resolvido = 2,
}
