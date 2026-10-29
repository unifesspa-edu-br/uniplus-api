namespace Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>As quorum queues do RabbitMQ entre a API e o verificador (ADR-0146).</summary>
public static class FilasDaVerificacao
{
    /// <summary>Os pedidos que a API publica e o verificador consome.</summary>
    public const string Pedidos = "verificacao-de-arquivos.pedidos";

    /// <summary>Os resultados dos documentos do candidato, que o verificador publica e a Seleção consome.</summary>
    public const string Resultados = "verificacao-de-arquivos.resultados";

    /// <summary>Os resultados do documento de designação dos gestores, que a Autorização consome.</summary>
    public const string ResultadosAutorizacao = "verificacao-de-arquivos.resultados.autorizacao";

    /// <summary>A fila de resultados do <paramref name="destino"/>.</summary>
    public static string ResultadosDe(DestinoDoResultado destino) => destino switch
    {
        DestinoDoResultado.Selecao => Resultados,
        DestinoDoResultado.Autorizacao => ResultadosAutorizacao,
        _ => throw new ArgumentOutOfRangeException(nameof(destino), destino, "Destino de resultado desconhecido."),
    };
}
