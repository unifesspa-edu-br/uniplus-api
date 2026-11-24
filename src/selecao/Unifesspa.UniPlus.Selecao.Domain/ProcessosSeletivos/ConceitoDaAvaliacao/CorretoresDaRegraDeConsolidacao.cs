namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;

using System.Text.Json;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// O número de corretores distintos que cada versão de regra de consolidação exige, lido da <c>declaracao_da_regra</c> da
/// própria versão (ADR-0150, emendas 1 e 2). É valor fixo da versão, e não parâmetro que o edital preenche: o número
/// congelado é o da versão que a etapa referencia.
/// </summary>
public static class CorretoresDaRegraDeConsolidacao
{
    private const string ChaveDosCorretores = "corretores_distintos";

    /// <summary>Os corretores distintos que a versão da regra declara.</summary>
    /// <exception cref="ArgumentException">
    /// A regra não é de consolidação, ou a versão não declara a chave como inteiro maior ou igual a um: o catálogo é
    /// semeado por código, e declaração assim é defeito de quem semeia, e nenhum número é presumido.
    /// </exception>
    public static int De(RegraCatalogo regra) =>
        Tentar(regra, out int corretores) ? corretores : throw DeclaracaoInvalida(regra);

    /// <summary>
    /// Os corretores que a versão declara, ou falso quando ela não os declara de forma válida: quem lê o catálogo para
    /// recusar a etapa em vez de lançar usa este, e não presume número.
    /// </summary>
    public static bool Tentar(RegraCatalogo regra, out int corretores)
    {
        ArgumentNullException.ThrowIfNull(regra);

        corretores = 0;

        return regra.Tipo == TipoRegra.RegraApuracaoAvaliacao
            && regra.DeclaracaoDaRegra.ValueKind == JsonValueKind.Object
            && regra.DeclaracaoDaRegra.TryGetProperty(ChaveDosCorretores, out JsonElement declarados)
            && declarados.ValueKind == JsonValueKind.Number
            && declarados.TryGetInt32(out corretores)
            && corretores >= 1;
    }

    /// <summary>
    /// Os corretores que a regra põe em campo: os distintos e, quando o tratamento declarado para o critério que não
    /// converge é o novo corretor, mais um, porque o novo corretor é pessoa da banca fora dos originais. A versão
    /// que não declara tratamento, como as regras numéricas, não tem o adicional. Falso quando os distintos não são
    /// declarados de forma válida ou o tratamento declarado é inválido: nenhum número é presumido.
    /// </summary>
    public static bool TentarEmCampo(RegraCatalogo regra, out int corretoresEmCampo)
    {
        corretoresEmCampo = 0;
        if (!Tentar(regra, out int distintos) || !TratamentoDaRegraDeConsolidacao.Tentar(regra, out TratamentoDaFaltaDeApuracao? tratamento))
        {
            return false;
        }

        corretoresEmCampo = distintos + (tratamento == TratamentoDaFaltaDeApuracao.NovoCorretor ? 1 : 0);
        return true;
    }

    private static ArgumentException DeclaracaoInvalida(RegraCatalogo regra) =>
        new("A regra de consolidação deve declarar uma quantidade inteira de corretores maior ou igual a um.", nameof(regra));
}
