namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.TiposAtoPublicado;

using System.Text.Json.Serialization;

using static TipoAtoPublicadoApoio;

/// <summary>
/// O corpo do POST e do PUT de tipo de ato, com os valores de um tipo comum; cada teste muda só o que o cenário exige.
/// Os seis atributos e o início da vigência são obrigatórios no contrato: <see cref="Sem"/> os omite do corpo e
/// <see cref="ComNulo"/> os envia como <c>null</c>.
/// </summary>
internal sealed record CorpoDoTipoDeAto(string Codigo)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? Id { get; init; }

    public string Nome { get; init; } = "Tipo de ato de teste";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? CongelaConfiguracao { get; init; } = true;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? UnicoPorObjeto { get; init; } = false;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? EfeitoIrreversivel { get; init; } = false;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? EhResultado { get; init; } = false;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? PublicavelComoAvulso { get; init; } = false;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ExigeAtoCorrigido { get; init; } = false;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VigenciaInicio { get; init; } = Inicio;

    public string? VigenciaFim { get; init; }

    /// <summary>Chaves que vão ao corpo com valor <c>null</c>, mesmo sem propriedade correspondente preenchida.</summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? NulosExplicitos { get; init; }

    /// <summary>O corpo sem o campo: a chave não vai ao JSON. O nome é o camelCase do contrato.</summary>
    internal CorpoDoTipoDeAto Sem(string campo) => campo switch
    {
        "congelaConfiguracao" => this with { CongelaConfiguracao = null },
        "unicoPorObjeto" => this with { UnicoPorObjeto = null },
        "efeitoIrreversivel" => this with { EfeitoIrreversivel = null },
        "ehResultado" => this with { EhResultado = null },
        "publicavelComoAvulso" => this with { PublicavelComoAvulso = null },
        "exigeAtoCorrigido" => this with { ExigeAtoCorrigido = null },
        "vigenciaInicio" => this with { VigenciaInicio = null },
        _ => throw new ArgumentOutOfRangeException(nameof(campo), campo, "Campo desconhecido."),
    };

    /// <summary>O corpo com o campo enviado como <c>null</c>.</summary>
    internal CorpoDoTipoDeAto ComNulo(string campo) =>
        Sem(campo) with { NulosExplicitos = new Dictionary<string, object?> { [campo] = null } };

    internal CorpoDoTipoDeAto SemOsAtributosDoDocumentoAvulso() => Sem("publicavelComoAvulso").Sem("exigeAtoCorrigido");
}
