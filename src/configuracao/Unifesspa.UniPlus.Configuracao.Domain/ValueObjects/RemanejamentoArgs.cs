namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using System.Text.Json.Serialization;

/// <summary>
/// Argumentos da regra de remanejamento de uma <see cref="Entities.Modalidade"/>
/// de concorrência (UNI-REQ-0011): códigos de outras modalidades usados como
/// destino/par/fallback conforme a <c>RegraRemanejamento</c>. Value object
/// imutável serializado como <c>jsonb</c> na coluna <c>remanejamento_args</c>.
/// </summary>
/// <remarks>
/// <para>As chaves JSON são fixadas por <see cref="JsonPropertyNameAttribute"/>
/// (<c>destino</c>, <c>par</c>, <c>fallback</c>) — o repositório consulta esses
/// campos por operador <c>-&gt;&gt;</c> do Postgres para detectar referências vivas
/// ao remover uma modalidade, e a estabilidade das chaves é parte do contrato.</para>
/// <para>Quais campos são obrigatórios/proibidos depende da regra e é validado no
/// agregado <c>Modalidade</c> (invariante "args por regra"); este VO apenas
/// normaliza (Trim → null) e transporta.</para>
/// </remarks>
public sealed record RemanejamentoArgs
{
    /// <summary>Código da modalidade de destino único (regra <c>DestinoUnico</c>).</summary>
    [JsonPropertyName("destino")]
    public string? Destino { get; init; }

    /// <summary>Código da modalidade par no remanejamento cruzado (regra <c>Cruzado</c>).</summary>
    [JsonPropertyName("par")]
    public string? Par { get; init; }

    /// <summary>Código da modalidade de fallback no remanejamento cruzado (regra <c>Cruzado</c>).</summary>
    [JsonPropertyName("fallback")]
    public string? Fallback { get; init; }

    /// <summary>Instância canônica sem nenhum argumento (regra <c>SegueCascata</c> ou ausente).</summary>
    public static RemanejamentoArgs Vazio { get; } = new();

    /// <summary>
    /// Cria um <see cref="RemanejamentoArgs"/> normalizando cada campo por
    /// <c>Trim</c> (valores em branco viram <see langword="null"/>).
    /// </summary>
    public static RemanejamentoArgs Criar(string? destino, string? par, string? fallback) =>
        new()
        {
            Destino = Normalizar(destino),
            Par = Normalizar(par),
            Fallback = Normalizar(fallback),
        };

    /// <summary>Indica se há ao menos um argumento preenchido.</summary>
    [JsonIgnore]
    public bool TemAlgum => Destino is not null || Par is not null || Fallback is not null;

    private static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
