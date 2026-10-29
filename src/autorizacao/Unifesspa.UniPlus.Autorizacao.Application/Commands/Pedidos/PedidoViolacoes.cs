namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

/// <summary>Violações de gravação do pedido que os handlers traduzem em erro de domínio.</summary>
internal static class PedidoViolacoes
{
    private const string AtivoUnicoConstraint = "ux_pedidos_ativo_unico";

    /// <summary>Já existe pedido ativo igual: dois pedidos iguais correram juntos.</summary>
    public static bool EhPedidoDuplicado(Exception ex) =>
        ViolacoesDeGravacao.ViolaIndiceUnico(ex, AtivoUnicoConstraint);
}
