namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using System.Text.Json;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A forma gravada de um predicado: lista de cláusulas, cada uma lista de condições
/// <c>{codigoDoDado, operador, valor}</c>. A leitura remonta o predicado pelas factories do domínio e falha
/// alto se o conteúdo gravado não as satisfaz.
/// </summary>
internal static class PredicadoDnfGravado
{
    public static IReadOnlyList<IReadOnlyList<CondicaoGravada>> Gravar(PredicadoDnf predicado) =>
        [.. predicado.Clausulas.Select(static c => (IReadOnlyList<CondicaoGravada>)
            [.. c.Condicoes.Select(static d => new CondicaoGravada(d.Fato, d.Operador.Codigo(), d.Valor))])];

    public static PredicadoDnf Remontar(IReadOnlyList<IReadOnlyList<CondicaoGravada>> clausulas)
    {
        List<(int Clausula, Result<CondicaoDnf> Condicao)> lidas =
            [.. clausulas.SelectMany(static (condicoes, i) => condicoes.Select(c => (i, CondicaoDnf.CriarInformada(c.CodigoDoDado, c.Operador, c.Valor))))];
        List<FieldError> recusas = [.. lidas.SelectMany(static l => l.Condicao.Errors)];

        return ObjetoDeValorGravado.Reidratar(
            recusas.Count > 0
                ? Result<PredicadoDnf>.ValidationFailure(recusas)
                : PredicadoDnf.CriarDeCondicoesAgrupadas([.. lidas.Select(static l => (l.Clausula, l.Condicao.Value!))]),
            nameof(PredicadoDnf));
    }

    internal sealed record CondicaoGravada(string CodigoDoDado, string Operador, JsonElement Valor);
}
