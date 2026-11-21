namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Entities;

using static LinhasDaEntradaDoPreliminar;

/// <summary>
/// O que da etapa entra no resultado preliminar que ela publica: a fase onde acontece, o caráter, a origem do
/// tipo, o peso, a escala e o passo da nota, a forma de avaliação, a regra de consolidação, a janela, o parecer,
/// os itens de avaliação, os tipos de decisão que declara, as bancas e as janelas recursais. Fica de fora o que identifica a etapa ou a descreve
/// (<c>Id</c>, nome, ordem, cópia do tipo) e o que ela publica, cuja mudança é conferida à parte.
/// </summary>
internal static class EntradaDoPreliminarDaEtapa
{
    internal static EntradaDoPreliminar De(EtapaProcesso etapa)
    {
        ArgumentNullException.ThrowIfNull(etapa);

        return new(
        [
            .. Escalares(etapa),
            .. DaReferencia("regraConsolidacao", etapa.RegraConsolidacao),
            .. DaAvaliacao(etapa),
            .. DosTiposDeDecisao(etapa.TiposDeDecisao),
            .. etapa.Bancas.SelectMany(DaBanca),
            .. etapa.Recursos.SelectMany(recurso => DoRecurso(etapa, recurso)),
        ]);
    }

    private static IEnumerable<string> Escalares(EtapaProcesso etapa) =>
    [
        Linha("faseCodigo", etapa.FaseCodigo),
        Linha("carater", etapa.Carater),
        Linha("tipoEtapaOrigemId", etapa.TipoEtapaOrigemId),
        Linha("peso", etapa.Peso),
        Linha("notaMinima", etapa.NotaMinima),
        Linha("notaMaxima", etapa.NotaMaxima),
        Linha("passoDoLancamento", etapa.PassoDoLancamento),
        Linha("formaDeAvaliacao", etapa.FormaDeAvaliacao),
        Linha("inicio", etapa.Inicio),
        Linha("fim", etapa.Fim),
        Linha("emiteParecerIndividual", etapa.EmiteParecerIndividual),
    ];

    private static IEnumerable<string> DaAvaliacao(EtapaProcesso etapa) =>
    [
        .. etapa.EscalaDeAtendimento?.Select(static nivel => Linha($"escalaDeAtendimento[{Valor(nivel.Rotulo)}].fracao", nivel.Fracao)) ?? [],
        .. etapa.ItensDeAvaliacao.Select(static item => Linha($"itensDeAvaliacao[{Valor(item.Rotulo)}].pontuacaoMaxima", item.PontuacaoMaxima)),
    ];

    private static IEnumerable<string> DaBanca(BancaDaEtapa banca)
    {
        string campo = $"bancas[{Valor(banca.Tipo.OrigemId)}]";

        return
        [
            Linha(campo, true),
            .. DaCopiaDoTipo($"{campo}.tipo", banca.Tipo),
            .. DaDeliberacao($"{campo}.regraDeDeliberacao", banca.RegraDeDeliberacao),
        ];
    }

    // A janela recursal se distingue pelo fato de que o prazo corre e pelo ato em que ancora.
    private static IEnumerable<string> DoRecurso(EtapaProcesso etapa, RecursoDaEtapa recurso)
    {
        IProdutoPublicado? ancora = ProdutoAncora(etapa.Produtos, recurso.ProdutoAncoraId);
        string campo = $"recursos[{Valor(recurso.Ancora)}|{Valor(ancora?.AtoCodigo)}|{Valor(ancora?.Papel)}]";

        return
        [
            Linha(campo, true),
            .. DaReferencia($"{campo}.regra", recurso.Regra),
            .. DosParametrosDoRecurso($"{campo}.parametros", recurso.Parametros),
            .. DosProcedimentos(campo, recurso.Procedimentos),
        ];
    }
}
