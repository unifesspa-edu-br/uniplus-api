namespace Unifesspa.UniPlus.Testes.Compartilhado;

using System.Text.Json;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>A condição de exibição de um item de teste: uma cláusula, com as condições que o teste cita.</summary>
internal static class ExibicaoDeTeste
{
    /// <summary>Exibe o item quando o fato satisfaz a comparação com o valor.</summary>
    public static PredicadoDnf Quando(string fato, Operador operador, object valor) =>
        QuandoTodas((fato, operador, valor));

    /// <summary>Exibe o item quando todas as condições se cumprem, numa só cláusula.</summary>
    public static PredicadoDnf QuandoTodas(params (string Fato, Operador Operador, object Valor)[] condicoes) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas(
            [.. condicoes.Select(static c => (0, CondicaoDnf.Criar(c.Fato, c.Operador, JsonSerializer.SerializeToElement(c.Valor)).Value!))]).Value!;
}
