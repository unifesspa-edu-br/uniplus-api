namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>As referências aceitas da configuração, na ordem dos blocos e das posições do documento.</summary>
public static class ReferenciasDeRegraDaConfiguracao
{
    public static IReadOnlyList<(string Campo, ReferenciaRegra Regra)> De(ConfiguracaoDeclarada configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        return [.. Identificacao(configuracao), .. Vagas(configuracao), .. Fases(configuracao),
            .. Etapas(configuracao), .. Classificacao(configuracao), .. Desempate(configuracao), .. Bonus(configuracao)];
    }

    private static IEnumerable<(string Campo, ReferenciaRegra Regra)> Identificacao(ConfiguracaoDeclarada configuracao) =>
        Referencia(IdentidadeDoProcessoDeclarada.CampoDaConvencaoDeContagem, configuracao.Cronograma.AlgoritmoContagemPrazo);

    private static IEnumerable<(string Campo, ReferenciaRegra Regra)> Vagas(ConfiguracaoDeclarada configuracao) =>
    [
        .. Posicoes(configuracao.Vagas.DistribuicaoVagas.Count, "distribuicaoVagas", configuracao.Origem)
            .SelectMany(i => DaOferta(configuracao.Vagas.DistribuicaoVagas[i], i)),
        .. Referencia("ordemDeRemanejamento.regra", configuracao.Vagas.Cascata?.Regra),
    ];

    private static IEnumerable<(string Campo, ReferenciaRegra Regra)> DaOferta(ConfiguracaoDistribuicaoVagas oferta, int i) =>
    [
        .. Referencia($"distribuicaoVagas[{i}].regraDistribuicao", oferta.RegraDistribuicao),
        .. Referencia($"distribuicaoVagas[{i}].regraAjuste", oferta.RegraAjuste),
    ];

    private static IEnumerable<(string Campo, ReferenciaRegra Regra)> Fases(ConfiguracaoDeclarada configuracao) =>
        Posicoes(configuracao.Cronograma.CronogramaFases.Count, "fases", configuracao.Origem)
            .SelectMany(i => DaFase(configuracao.Cronograma.CronogramaFases[i], i, configuracao.Origem));

    private static IEnumerable<(string Campo, ReferenciaRegra Regra)> DaFase(FaseCronograma fase, int i, OrigemNoDocumento origem) =>
        fase.RegraRecurso is not { } recurso ? [] :
        [
            ($"fases[{i}].regraRecurso.regraCodigo", recurso.Regra),
            .. Procedimentos(recurso.Procedimentos, $"fases[{i}].{FaseDeclarada.ListaDosProcedimentosDeRecurso}", origem),
        ];

    private static IEnumerable<(string Campo, ReferenciaRegra Regra)> Etapas(ConfiguracaoDeclarada configuracao) =>
        Posicoes(configuracao.Avaliacao.Etapas.Count, "etapas", configuracao.Origem)
            .SelectMany(i => DaEtapa(configuracao.Avaliacao.Etapas[i], i, configuracao.Origem));

    private static IEnumerable<(string Campo, ReferenciaRegra Regra)> DaEtapa(EtapaProcesso etapa, int i, OrigemNoDocumento origem)
    {
        RecursoDaEtapa[] recursos = [.. etapa.Recursos];
        string lista = $"etapas[{i}].recursos";
        return
        [
            .. Referencia($"etapas[{i}].regraConsolidacao", etapa.RegraConsolidacao),
            .. Posicoes(recursos.Length, lista, origem).SelectMany(j => DoRecurso(recursos[j], $"{lista}[{j}]", origem)),
        ];
    }

    private static IEnumerable<(string Campo, ReferenciaRegra Regra)> DoRecurso(RecursoDaEtapa recurso, string campo, OrigemNoDocumento origem) =>
    [
        ($"{campo}.regraCodigo", recurso.Regra),
        .. Procedimentos(recurso.Procedimentos, $"{campo}.procedimentosDeRecurso", origem),
    ];

    private static IEnumerable<(string Campo, ReferenciaRegra Regra)> Classificacao(ConfiguracaoDeclarada configuracao) =>
        configuracao.Avaliacao.Classificacao is not { } classificacao ? [] :
        [
            ("classificacao.regraCalculo", classificacao.RegraCalculo),
            .. Referencia("classificacao.regraArredondamento", classificacao.RegraArredondamento),
            ("classificacao.regraOrdemAlocacao", classificacao.RegraOrdemAlocacao),
            .. Eliminacoes(classificacao, configuracao.Origem),
        ];

    private static IEnumerable<(string Campo, ReferenciaRegra Regra)> Eliminacoes(ConfiguracaoClassificacao classificacao, OrigemNoDocumento origem)
    {
        RegraEliminacao[] eliminacoes = [.. classificacao.RegrasEliminacao];
        const string lista = "classificacao.regrasEliminacao";
        return Posicoes(eliminacoes.Length, lista, origem).Select(i => ($"{lista}[{i}].regra", eliminacoes[i].Regra));
    }

    private static IEnumerable<(string Campo, ReferenciaRegra Regra)> Desempate(ConfiguracaoDeclarada configuracao)
    {
        IReadOnlyList<CriterioDesempate> criterios = configuracao.Avaliacao.CriteriosDesempate;
        const string lista = "classificacao.criteriosDeDesempate";
        return Posicoes(criterios.Count, lista, configuracao.Origem).Select(i => ($"{lista}[{i}].regra", criterios[i].Regra));
    }

    private static IEnumerable<(string Campo, ReferenciaRegra Regra)> Bonus(ConfiguracaoDeclarada configuracao) =>
        Referencia("bonusRegional.regra", configuracao.Vagas.BonusRegional?.Regra);

    private static IEnumerable<(string Campo, ReferenciaRegra Regra)> Procedimentos(
        ProcedimentosDeRecurso procedimentos, string lista, OrigemNoDocumento origem)
    {
        foreach (int i in Posicoes(procedimentos.Itens.Count, lista, origem))
        {
            yield return ($"{lista}[{i}].regra", procedimentos.Itens[i].Regra);
        }
    }

    private static IEnumerable<(string Campo, ReferenciaRegra Regra)> Referencia(string campo, ReferenciaRegra? regra) =>
        regra is null ? [] : [(campo, regra)];

    private static IEnumerable<int> Posicoes(int quantidade, string lista, OrigemNoDocumento origem) =>
        Enumerable.Range(0, quantidade).OrderBy(i => origem.PosicaoDeOrigem(lista, i));
}
