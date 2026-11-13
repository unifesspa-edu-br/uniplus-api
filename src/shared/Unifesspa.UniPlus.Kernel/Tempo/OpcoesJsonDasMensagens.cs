namespace Unifesspa.UniPlus.Kernel.Tempo;

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

/// <summary>
/// O contrato de instantes das mensagens que os processos trocam por JSON: todo instante é lido em microssegundos,
/// como o banco o persiste, e todo instante não anulável é obrigatório no corpo. Sem a obrigatoriedade, uma mensagem
/// sem o instante seria aceita com <see cref="DateTimeOffset.MinValue"/>, e o consumidor decidiria sobre um instante
/// que ninguém informou. A mensagem sem o instante é recusada na leitura, antes de chegar ao consumidor.
/// </summary>
/// <remarks>
/// A obrigatoriedade vale para a propriedade que o serializador consegue preencher (<c>set</c> ou <c>init</c>, o que
/// inclui os parâmetros de um <c>record</c> posicional): marcar como obrigatória a propriedade sem <c>set</c>, como a
/// calculada ou a só de leitura preenchida pelo construtor, faz o serializador recusar o tipo inteiro, inclusive na
/// escrita. Os dois lados de cada fila aplicam este mesmo contrato ao serializador, por isso ele mora no Kernel, o único
/// projeto que o produtor e o verificador compartilham.
/// </remarks>
public static class OpcoesJsonDasMensagens
{
    public static void Aplicar(JsonSerializerOptions opcoes)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        opcoes.Converters.Add(new InstanteEmMicrossegundosJsonConverter());
        opcoes.TypeInfoResolver = (opcoes.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver())
            .WithAddedModifier(ExigirInstanteNaoAnulavel);
    }

    private static void ExigirInstanteNaoAnulavel(JsonTypeInfo tipo)
    {
        if (tipo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        foreach (JsonPropertyInfo propriedade in tipo.Properties.Where(static p => p.PropertyType == typeof(DateTimeOffset) && p.Set is not null))
        {
            propriedade.IsRequired = true;
        }
    }
}
