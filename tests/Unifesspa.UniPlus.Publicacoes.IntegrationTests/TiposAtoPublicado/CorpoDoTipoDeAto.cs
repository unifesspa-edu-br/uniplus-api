namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.TiposAtoPublicado;

using System.Text.Json.Serialization;

using static TipoAtoPublicadoApoio;

/// <summary>O corpo do POST e do PUT de tipo de ato, com os valores de um tipo comum; cada teste muda só o que o cenário exige.</summary>
internal sealed record CorpoDoTipoDeAto(string Codigo)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? Id { get; init; }

    public string Nome { get; init; } = "Tipo de ato de teste";

    public bool CongelaConfiguracao { get; init; } = true;

    public bool UnicoPorObjeto { get; init; }

    public bool EfeitoIrreversivel { get; init; }

    public bool EhResultado { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? PublicavelComoAvulso { get; init; } = false;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ExigeAtoCorrigido { get; init; } = false;

    public string VigenciaInicio { get; init; } = Inicio;

    public string? VigenciaFim { get; init; }

    /// <summary>O corpo sem os dois atributos que o contrato exige declarar (JsonRequired).</summary>
    internal CorpoDoTipoDeAto SemOsAtributosDoDocumentoAvulso() =>
        this with { PublicavelComoAvulso = null, ExigeAtoCorrigido = null };
}
