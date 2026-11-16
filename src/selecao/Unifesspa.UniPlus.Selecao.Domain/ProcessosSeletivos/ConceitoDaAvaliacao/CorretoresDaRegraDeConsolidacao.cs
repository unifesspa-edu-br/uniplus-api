namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;

using System.Text.Json;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// O número de corretores distintos que cada versão de regra de consolidação exige, lido do <c>esquema_args</c> da
/// própria versão (ADR-0150, emendas 1 e 2). É valor fixo da versão, e não argumento que o edital preenche: o número
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

        return regra.Tipo == TipoRegra.RegraConsolidacao
            && regra.EsquemaArgs.ValueKind == JsonValueKind.Object
            && regra.EsquemaArgs.TryGetProperty(ChaveDosCorretores, out JsonElement declarados)
            && declarados.ValueKind == JsonValueKind.Number
            && declarados.TryGetInt32(out corretores)
            && corretores >= 1;
    }

    private static ArgumentException DeclaracaoInvalida(RegraCatalogo regra) =>
        new($"A regra '{regra.Codigo}' versão '{regra.Versao}' deve ser de consolidação e declarar '{ChaveDosCorretores}' no esquema_args como inteiro maior ou igual a um.", nameof(regra));
}
