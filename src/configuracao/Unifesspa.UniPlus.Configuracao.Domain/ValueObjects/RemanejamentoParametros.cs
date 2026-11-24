namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using System.Text.Json.Serialization;

/// <summary>Parâmetros das modalidades envolvidas no remanejamento (UNI-REQ-0011).</summary>
public sealed record RemanejamentoParametros
{
    /// <summary>Código da modalidade de destino único (regra <c>DestinoUnico</c>).</summary>
    public string? Destino { get; init; }

    /// <summary>Código da modalidade par no remanejamento cruzado (regra <c>Cruzado</c>).</summary>
    public string? Par { get; init; }

    /// <summary>Código da modalidade de destino final no remanejamento cruzado (regra <c>Cruzado</c>).</summary>
    public string? DestinoFinal { get; init; }

    /// <summary>Parâmetros sem nenhum valor (regra <c>SegueOrdemDeRemanejamento</c> ou ausente).</summary>
    public static RemanejamentoParametros Vazio => new();

    /// <summary>
    /// Cria um <see cref="RemanejamentoParametros"/> normalizando cada campo por
    /// <c>Trim</c> (valores em branco viram <see langword="null"/>).
    /// </summary>
    public static RemanejamentoParametros Criar(string? destino, string? par, string? destinoFinal) =>
        new()
        {
            Destino = Normalizar(destino),
            Par = Normalizar(par),
            DestinoFinal = Normalizar(destinoFinal),
        };

    /// <summary>Indica se há ao menos um parâmetro preenchido.</summary>
    [JsonIgnore]
    public bool TemAlgum => Destino is not null || Par is not null || DestinoFinal is not null;

    private static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
