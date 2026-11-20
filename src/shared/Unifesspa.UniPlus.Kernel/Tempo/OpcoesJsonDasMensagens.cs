namespace Unifesspa.UniPlus.Kernel.Tempo;

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// O contrato JSON das mensagens que os processos trocam: todo enum que declara código circula pelo código, e a leitura
/// recusa o código desconhecido; todo instante é lido em microssegundos, como o banco o persiste, e todo instante não
/// anulável é obrigatório no corpo. Sem a obrigatoriedade, uma mensagem sem o instante seria aceita com
/// <see cref="DateTimeOffset.MinValue"/>, e o consumidor decidiria sobre um instante que ninguém informou; com ela, a
/// leitura recusa a mensagem antes de chegar ao consumidor.
/// </summary>
/// <remarks>
/// A obrigatoriedade vale para a propriedade que o serializador consegue preencher (<c>set</c> ou <c>init</c>, o que
/// inclui os parâmetros de um <c>record</c> posicional): marcar como obrigatória a propriedade sem <c>set</c>, como a
/// calculada ou a só de leitura preenchida pelo construtor, faz o serializador recusar o tipo inteiro, inclusive na
/// escrita. Os dois lados de cada fila aplicam este mesmo contrato ao serializador, por isso ele mora no Kernel, que o
/// produtor e o verificador referenciam.
/// </remarks>
public static class OpcoesJsonDasMensagens
{
    /// <summary>
    /// Instala o contrato nas opções que o serializador do Wolverine entrega, mantendo o resolvedor que elas já tinham. Não
    /// devolve as opções porque o Wolverine espera <c>Action&lt;JsonSerializerOptions&gt;</c>.
    /// </summary>
    /// <exception cref="ArgumentNullException">As opções são nulas.</exception>
    public static void Configurar(JsonSerializerOptions opcoes)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        opcoes.Converters.Add(new InstanteEmMicrossegundosJsonConverter());
        opcoes.AdicionarConversorDeCodigosDeContrato();
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
