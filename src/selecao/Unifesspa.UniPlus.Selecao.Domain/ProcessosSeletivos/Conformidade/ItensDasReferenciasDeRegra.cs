namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Projeta no painel as referências que não correspondem ao catálogo.</summary>
internal static class ItensDasReferenciasDeRegra
{
    internal static IReadOnlyList<ItemConformidade> Avaliar(ConfiguracaoDeclarada configuracao, CadastrosDaConfiguracao cadastros) =>
        [.. ConferenciaDasReferenciasDeRegra.Conferir(configuracao, cadastros)
            .Select(erro => new ItemConformidade($"regra_citada_{erro.Field}", Dimensao(erro.Field!),
                erro.Error.Message, false))];

    private static string Dimensao(string campo) => campo switch
    {
        var c when c.StartsWith("distribuicaoVagas", StringComparison.Ordinal) => DimensaoConformidade.DistribuicaoVagas,
        var c when c.StartsWith("cascataRemanejamento", StringComparison.Ordinal) => DimensaoConformidade.CascataRemanejamento,
        var c when c.StartsWith("fases", StringComparison.Ordinal) => DimensaoConformidade.Cronograma,
        var c when c.StartsWith("etapas", StringComparison.Ordinal) => DimensaoConformidade.Etapas,
        var c when c.StartsWith("classificacao.criteriosDeDesempate", StringComparison.Ordinal) => DimensaoConformidade.Desempate,
        var c when c.StartsWith("classificacao", StringComparison.Ordinal) => DimensaoConformidade.Classificacao,
        var c when c.StartsWith("bonusRegional", StringComparison.Ordinal) => DimensaoConformidade.BonusRegional,
        _ => DimensaoConformidade.ContagemDePrazos,
    };
}
