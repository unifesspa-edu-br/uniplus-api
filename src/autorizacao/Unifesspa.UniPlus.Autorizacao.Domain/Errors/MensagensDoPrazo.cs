namespace Unifesspa.UniPlus.Autorizacao.Domain.Errors;

/// <summary>Mensagens da recusa do prazo de validade, únicas para a concessão e o pedido.</summary>
public static class MensagensDoPrazo
{
    public const string PerfilSemPrazo = "O perfil de gestor de acessos não admite prazo: a concessão do gestor só termina pela revogação, que confere o mínimo de dois gestores.";
}
