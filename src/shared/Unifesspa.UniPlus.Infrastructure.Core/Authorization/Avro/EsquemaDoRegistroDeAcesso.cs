namespace Unifesspa.UniPlus.Infrastructure.Core.Authorization.Avro;

using System.Reflection;

using global::Avro;

using Unifesspa.UniPlus.Authorization.Contracts;

/// <summary>
/// O esquema Avro do valor da mensagem do registro de acesso, carregado uma única vez do recurso embutido no projeto
/// do contrato de autorização: o <c>.avsc</c> é a fonte única, e o que o produtor serializa e o que o Schema Registry
/// recebe saem do mesmo texto.
/// </summary>
public static class EsquemaDoRegistroDeAcesso
{
    /// <summary>Nome do recurso embutido com o esquema do lote.</summary>
    public const string NomeDoRecurso = "Unifesspa.UniPlus.Authorization.Contracts.Schemas.LoteDoRegistroDeAcesso.avsc";

    /// <summary>Subject do esquema no Schema Registry: o do tópico, com o sufixo do valor (ADR-0051).</summary>
    public const string Subject = "autorizacao_registros_de_acesso-value";

    private static readonly Lazy<RecordSchema> EsquemaDoLote = new(Carregar);

    /// <summary>Assembly que embute o esquema.</summary>
    public static Assembly AssemblyDoRecurso => typeof(RegistroDecisaoAcesso).Assembly;

    /// <summary>Esquema do lote inteiro, que é o do valor da mensagem.</summary>
    public static RecordSchema Lote => EsquemaDoLote.Value;

    /// <summary>Esquema de uma entrada do lote: a chave de deduplicação e o registro.</summary>
    public static RecordSchema Entrada => (RecordSchema)((ArraySchema)Lote.Fields[0].Schema).ItemSchema;

    /// <summary>Esquema do registro de acesso de uma entrada.</summary>
    public static RecordSchema Registro => (RecordSchema)Entrada.Fields[1].Schema;

    private static RecordSchema Carregar()
    {
        Assembly assembly = AssemblyDoRecurso;
        using Stream fluxo = assembly.GetManifestResourceStream(NomeDoRecurso)
            ?? throw new InvalidOperationException(
                $"O esquema Avro '{NomeDoRecurso}' não está embutido em '{assembly.FullName}'.");
        using StreamReader leitor = new(fluxo);

        return (RecordSchema)Schema.Parse(leitor.ReadToEnd());
    }
}
