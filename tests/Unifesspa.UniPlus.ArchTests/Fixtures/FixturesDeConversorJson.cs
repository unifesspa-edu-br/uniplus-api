namespace Unifesspa.UniPlus.ArchTests.Fixtures;

using System.Text.Json;
using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Tipos mínimos que servem de contraprova às regras do atributo de conversão JSON: cada forma de declarar o atributo
/// num membro cujo tipo menciona um enum com código, e no próprio enum, tem de ser reprovada com o nome do tipo e do
/// membro; o membro sem o atributo, o atributo num tipo que não menciona enum com código e o atributo em enum sem código
/// têm de passar.
/// </summary>
internal static class FixturesDeConversorJson
{
    internal enum EstadoComCodigo
    {
        [CodigoDeContrato("em_analise", "Em análise")]
        EmAnalise = 4,

        [CodigoDeContrato("concluido", "Concluído")]
        Concluido = 8,
    }

    internal enum EstadoSemCodigo
    {
        Aberto = 1,
        Fechado = 2,
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    internal enum EstadoComCodigoEConversorNoTipo
    {
        [CodigoDeContrato("rascunho", "Rascunho")]
        Rascunho = 1,
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    internal enum EstadoSemCodigoComConversorNoTipo
    {
        Aberto = 1,
    }

    internal sealed class ConversorDeTextoDeTeste : JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetString();

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WriteStringValue(value);
    }

    internal sealed class ConversorNaPropriedadeDoEnum
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public EstadoComCodigo Estado { get; set; }
    }

    internal sealed class ConversorNaPropriedadeDoAnulavel
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public EstadoComCodigo? Estado { get; set; }
    }

    internal sealed class ConversorNaPropriedadeDeLista
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public List<EstadoComCodigo> Estados { get; set; } = [];
    }

    internal sealed class ConversorNaPropriedadeDeArray
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public EstadoComCodigo[] Estados { get; set; } = [];
    }

    internal sealed class ConversorNaPropriedadeDeDicionarioComEnumNaChave
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public Dictionary<EstadoComCodigo, string> PorEstado { get; set; } = [];
    }

    internal sealed class ConversorNaPropriedadeDeDicionarioComEnumNoValor
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public Dictionary<string, EstadoComCodigo> PorNome { get; set; } = [];
    }

    internal sealed class ConversorNaPropriedadeDeDicionarioComEnumAninhado
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public Dictionary<string, List<EstadoComCodigo?>> PorNome { get; set; } = [];
    }

    internal sealed class ConversorNoCampoDoEnum
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public EstadoComCodigo Estado = EstadoComCodigo.Concluido;
    }

    internal sealed class ConversorNoCampoDeLista
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public List<EstadoComCodigo> Estados = [];
    }

    internal sealed class SemConversorNoMembroComEnumComCodigo
    {
        public EstadoComCodigo Estado { get; set; }

        public EstadoComCodigo? Anulavel { get; set; }

        public List<EstadoComCodigo> Estados { get; set; } = [];

        public Dictionary<EstadoComCodigo, List<EstadoComCodigo>> PorEstado { get; set; } = [];

        public EstadoComCodigo Campo = EstadoComCodigo.Concluido;
    }

    internal sealed class ConversorEmTipoQueNaoMencionaEnumComCodigo
    {
        [JsonConverter(typeof(ConversorDeTextoDeTeste))]
        public string Texto { get; set; } = string.Empty;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public Dictionary<string, int> Contagens { get; set; } = [];

        [JsonConverter(typeof(ConversorDeTextoDeTeste))]
        public string Campo = string.Empty;
    }

    internal sealed class ConversorEmEnumSemCodigo
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public EstadoSemCodigo Estado { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public List<EstadoSemCodigo?> Estados { get; set; } = [];

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public EstadoSemCodigoComConversorNoTipo Campo = EstadoSemCodigoComConversorNoTipo.Aberto;
    }
}
