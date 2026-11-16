namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Entities;

using static LinhasDaEntradaDoPreliminar;

/// <summary>
/// O que da fase entra no resultado preliminar que ela publica: o código, que diz qual fase ela é, a janela, a unidade responsável, a fase
/// concluinte, o parecer, as decisões de origem, a regra de recurso e as bancas. Fica de fora o que identifica
/// a fase ou a descreve (<c>Id</c>, ordem, sigla e nome da unidade) e o que ela publica, cuja mudança é
/// conferida à parte.
/// </summary>
internal static class EntradaDoPreliminarDaFase
{
    internal static EntradaDoPreliminar De(FaseCronograma fase)
    {
        ArgumentNullException.ThrowIfNull(fase);

        return new([.. Escalares(fase), .. DaRegraDeRecurso(fase), .. fase.BancasRequeridas.SelectMany(DaBanca)]);
    }

    private static IEnumerable<string> Escalares(FaseCronograma fase) =>
    [
        Linha("codigo", fase.Codigo),
        Linha("inicio", fase.Inicio),
        Linha("fim", fase.Fim),
        Linha("unidadeResponsavelOrigemId", fase.UnidadeResponsavelOrigemId),
        Linha("faseConcluinteCodigo", fase.FaseConcluinteCodigo),
        Linha("emiteParecerIndividual", fase.EmiteParecerIndividual),
        Linha("origemData", fase.OrigemData),
        Linha("agrupaEtapas", fase.AgrupaEtapas),
        Linha("permiteComplementacao", fase.PermiteComplementacao),
        Linha("coletaInscricao", fase.ColetaInscricao),
        Linha("coletaSolicitacaoIsencao", fase.ColetaSolicitacaoIsencao),
    ];

    private static IEnumerable<string> DaRegraDeRecurso(FaseCronograma fase)
    {
        if (fase.RegraRecurso is not { } recurso)
        {
            return [];
        }

        return
        [
            Linha("regraRecurso", true),
            .. DaReferencia("regraRecurso.regra", recurso.Regra),
            .. DosArgumentosDoRecurso("regraRecurso.args", recurso.Args),
            .. DaAncora("regraRecurso.ancora", fase.Produtos, recurso.ProdutoAncoraId),
        ];
    }

    private static IEnumerable<string> DaBanca(BancaRequerida banca)
    {
        string campo = $"bancasRequeridas[{ChaveDaBanca(banca)}]";

        return [Linha(campo, true), .. DaDeliberacao($"{campo}.regraDeDeliberacao", banca.RegraDeDeliberacao)];
    }

    // O tipo sozinho nem sempre distingue a banca: duas do mesmo tipo se separam pelo recorte de competência.
    private static string ChaveDaBanca(BancaRequerida banca)
    {
        IEnumerable<string> recorte = banca.RecorteDeCompetencia
            .Select(static categoria => Valor(categoria.CategoriaDocumentoOrigemId))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        return $"{Valor(banca.TipoBancaOrigemId)}/{string.Join(',', recorte)}";
    }
}
