namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging.SchemaRegistry;

using System.Text.Json;

using Confluent.SchemaRegistry;

/// <summary>
/// O formato de um schema registrado e a política de registro que vale para ele: o tipo no Schema Registry, a regra de
/// compatibilidade que o subject precisa ter e se a falha transitória do registro é repetida em segundo plano.
/// </summary>
/// <remarks>
/// A política é dado do formato, e não argumento de quem registra: todo Avro segue a regra do Schema Registry e depende
/// do serializador para registrar de novo em runtime; todo JSON Schema não tem serializador, então o registro se
/// repete, e exige a regra <c>NONE</c> no subject, porque o Apicurio 3.2.4 devolve erro 500 na segunda versão de um
/// JSON Schema 2020-12 sob regra de compatibilidade (a garantia de compatibilidade fica no teste de contrato).
/// </remarks>
public sealed class FormatoDoSchema
{
    /// <summary>Schema Avro: sem regra exigida do subject e sem repetição.</summary>
    public static readonly FormatoDoSchema Avro = new("Avro", SchemaType.Avro, regraExigida: null, repeteFalhaTransitoria: false);

    /// <summary>JSON Schema: regra <c>NONE</c> no subject e repetição do registro que falhou por indisponibilidade.</summary>
    public static readonly FormatoDoSchema JsonSchema = new("JSON Schema", SchemaType.Json, Compatibility.None, repeteFalhaTransitoria: true);

    private FormatoDoSchema(string nome, SchemaType tipo, Compatibility? regraExigida, bool repeteFalhaTransitoria)
    {
        Nome = nome;
        Tipo = tipo;
        RegraDeCompatibilidadeExigida = regraExigida;
        RepeteRegistroAposFalhaTransitoria = repeteFalhaTransitoria;
    }

    public string Nome { get; }

    public SchemaType Tipo { get; }

    /// <summary>A regra de compatibilidade efetiva que o subject precisa ter; nula quando o formato não exige.</summary>
    public Compatibility? RegraDeCompatibilidadeExigida { get; }

    public bool RepeteRegistroAposFalhaTransitoria { get; }

    /// <summary>
    /// O JSON Schema que não é JSON bem formado é defeito de empacotamento e falha antes de ir ao Schema Registry. O
    /// Avro não passa por aqui: o parse dele acontece na classe do registro específico.
    /// </summary>
    internal void ConferirConteudo(string conteudo, string recurso)
    {
        if (Tipo != SchemaType.Json)
        {
            return;
        }

        try
        {
            using JsonDocument documento = JsonDocument.Parse(conteudo);
        }
        catch (JsonException falha)
        {
            throw new InvalidOperationException($"O {Nome} '{recurso}' não é um JSON bem formado.", falha);
        }
    }
}
