namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Text.Json;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class AlcanceDaExigenciaPorModalidade
{
    public static bool PodeAlcancar(Aplicabilidade aplicabilidade, IReadOnlyCollection<CondicaoGatilho> condicoes, string modalidadeCodigo, FatosDeModalidade fatosDeModalidade)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modalidadeCodigo);
        ArgumentNullException.ThrowIfNull(fatosDeModalidade);

        if (aplicabilidade == Aplicabilidade.Geral)
        {
            return true;
        }

        return condicoes.Count > 0 && condicoes
            .GroupBy(static c => c.Clausula)
            .Any(clausula => ClausulaPodeAlcancar(clausula, modalidadeCodigo, fatosDeModalidade));
    }

    private static bool ClausulaPodeAlcancar(
        IEnumerable<CondicaoGatilho> clausula, string modalidadeCodigo, FatosDeModalidade fatosDeModalidade)
    {
        List<IGrouping<string, CondicaoGatilho>> porFatoDeModalidade = [.. clausula
            .Where(c => fatosDeModalidade.Contem(c.Fato))
            .GroupBy(static c => c.Fato, StringComparer.Ordinal)];

        return porFatoDeModalidade.Count == 0
            || porFatoDeModalidade.Any(fato => fato.All(c => CondicaoDeModalidadeSatisfeitaPor(c.ParaCondicaoDnf(), modalidadeCodigo)));
    }

    private static bool CondicaoDeModalidadeSatisfeitaPor(CondicaoDnf condicao, string modalidadeCodigo) =>
        condicao.Operador switch
        {
            Operador.Igual => Igual(condicao.Valor, modalidadeCodigo),
            Operador.AlgumDe => Contem(condicao.Valor, modalidadeCodigo),
            Operador.Diferente => condicao.Valor.ValueKind == JsonValueKind.String && !Igual(condicao.Valor, modalidadeCodigo),
            Operador.NenhumDe => condicao.Valor.ValueKind == JsonValueKind.Array && !Contem(condicao.Valor, modalidadeCodigo),
            _ => false,
        };

    private static bool Igual(JsonElement valor, string modalidadeCodigo) =>
        valor.ValueKind == JsonValueKind.String && string.Equals(valor.GetString(), modalidadeCodigo, StringComparison.Ordinal);

    private static bool Contem(JsonElement valor, string modalidadeCodigo) =>
        valor.ValueKind == JsonValueKind.Array && valor.EnumerateArray().Any(item => Igual(item, modalidadeCodigo));
}
