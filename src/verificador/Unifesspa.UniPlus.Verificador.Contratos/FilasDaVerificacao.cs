namespace Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>As quorum queues do RabbitMQ entre a API e o verificador (ADR-0146).</summary>
public static class FilasDaVerificacao
{
    /// <summary>Os pedidos que a API publica e o verificador consome.</summary>
    public const string Pedidos = "verificacao-de-arquivos.pedidos";

    /// <summary>Os resultados que o verificador publica e a API consome.</summary>
    public const string Resultados = "verificacao-de-arquivos.resultados";
}
