namespace Unifesspa.UniPlus.Regras.Formularios;

using System.Text.Json;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>O que um agregado faz com as ocorrências de um grupo repetível.</summary>
public enum OperacaoAgregado
{
    Nenhuma = 0,

    /// <summary>Verdadeiro quando alguma ocorrência respondeu verdadeiro: "existe membro que…".</summary>
    Existe = 1,

    /// <summary>A união dos valores respondidos nas ocorrências: "categorias presentes na família".</summary>
    ValoresPresentes = 2,
}

/// <summary>
/// O fato agregado sobre um grupo repetível (UNI-REQ-0146, UNI-REQ-0075, ADR-0138): calculado por
/// mecanismo genérico a partir do fato de membro que ele aponta, o único jeito de uma regra de fora
/// do grupo depender das ocorrências. A operação sai do domínio do fato de membro — booleano dá
/// <see cref="OperacaoAgregado.Existe"/>, categórico dá <see cref="OperacaoAgregado.ValoresPresentes"/>
/// — e o agregado não tem valores próprios: os do categórico são os do fato de membro.
/// </summary>
public static class AgregadoDeGrupo
{
    /// <summary>A operação do agregado sobre um fato de membro do domínio dado; os demais domínios não agregam.</summary>
    public static OperacaoAgregado OperacaoDoDominio(string? dominio) => dominio switch
    {
        DominioDoCatalogo.Booleano => OperacaoAgregado.Existe,
        DominioDoCatalogo.Categorico => OperacaoAgregado.ValoresPresentes,
        _ => OperacaoAgregado.Nenhuma,
    };

    /// <summary>
    /// O agregado no estado do grupo, como o derivado por regra: grupo oculto ou não informado não
    /// contribui, e o agregado resolve vazio — <c>EXISTE</c> falso, <c>VALORES_PRESENTES</c> sem
    /// valor; grupo pendente deixa o agregado pendente; grupo resolvido agrega as ocorrências, e a
    /// ocorrência em que o fato de membro não se aplica ou não foi informado não contribui.
    /// </summary>
    public static FatoResolvido Calcular(AvaliacaoGrupo grupo, string fatoDeMembro, OperacaoAgregado operacao)
    {
        ArgumentNullException.ThrowIfNull(grupo);
        ArgumentException.ThrowIfNullOrWhiteSpace(fatoDeMembro);
        if (operacao == OperacaoAgregado.Nenhuma || !Enum.IsDefined(operacao))
        {
            throw new ArgumentOutOfRangeException(nameof(operacao), operacao, "O agregado é EXISTE ou VALORES_PRESENTES.");
        }

        if (grupo.Estado == EstadoFato.Indeterminado)
        {
            return FatoResolvido.Indeterminado();
        }

        IEnumerable<JsonElement> respostas = grupo.Estado == EstadoFato.Resolvido
            ? grupo.Ocorrencias
                .Select(o => o.Fatos.TryGetValue(fatoDeMembro, out FatoResolvido? fato) ? fato.Valor : null)
                .OfType<JsonElement>()
            : [];

        return FatoResolvido.Resolvido(operacao == OperacaoAgregado.Existe
            ? JsonSerializer.SerializeToElement(respostas.Any(static r => r.ValueKind == JsonValueKind.True))
            : JsonSerializer.SerializeToElement(respostas
                .SelectMany(static r => RespostaDeCampo.Codigos(r) ?? [])
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)));
    }
}
