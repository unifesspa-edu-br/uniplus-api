namespace Unifesspa.UniPlus.Autorizacao.Domain.Errors;

/// <summary>Mensagens da recusa do prazo de validade, únicas para a concessão e o pedido.</summary>
public static class MensagensDoPrazo
{
    public const string PerfilSemPrazo =
        "Os perfis de gestor de acessos e de Representante do controlador não admitem prazo: a concessão deles só termina pela revogação, que confere o mínimo de cada papel.";
}
