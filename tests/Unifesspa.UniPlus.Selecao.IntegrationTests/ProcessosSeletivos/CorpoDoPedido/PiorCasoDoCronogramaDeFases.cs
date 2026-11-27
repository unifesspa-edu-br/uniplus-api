namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.CorpoDoPedido;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// O maior cronograma que o cadastro aceita: cada fase canônica uma vez, porque o cronograma recusa a mesma fase duas vezes, e
/// em cada uma todos os tipos de banca com todas as categorias de documento, todos os tipos de ato como produto, todos os
/// tipos de decisão e a regra de recurso. O que não tem teto no cadastro — procedimentos de recurso, bancas do mesmo tipo — usa a quantidade do catálogo.
/// </summary>
internal static class PiorCasoDoCronogramaDeFases
{
    private const int TamanhoDoCodigoDeRegra = 60;
    private const int AreasDoEnemPorProcedimento = 5;
    private const int OrdemDaFaseQueHomologa = 1;

    private static readonly DateTimeOffset InicioDasFases = new(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public static DefinirCronogramaFasesCommand Definicao(int fases, int tiposDeBanca, int categorias, IReadOnlyList<string> tiposDeAto) => new(
        Guid.NewGuid(),
        [.. Enumerable.Range(1, fases).Select(ordem => Fase(ordem, tiposDeBanca, categorias, tiposDeAto))]);

    private static FaseCronogramaInput Fase(int ordem, int tiposDeBanca, int categorias, IReadOnlyList<string> tiposDeAto) => new(
        ordem, Guid.NewGuid(), InicioDasFases, InicioDasFases.AddDays(30),
        [.. tiposDeAto.Select(static ato => new ProdutoDaFaseInput(ato, PapelDaPublicacaoPrevista.Preliminar.Codigo()))],
        true,
        [.. Enumerable.Range(0, tiposDeBanca).Select(_ => Banca(categorias))],
        RegraDeRecurso(tiposDeAto),
        Guid.NewGuid(),
        [.. CatalogoDe<TipoMotivoDeDecisao>.Todos().Select(static c => c.Codigo)],
        ordem == OrdemDaFaseQueHomologa ? RegraDeHomologacaoNoLimite.Regra(tiposDeAto[0]) : null)
    {
        NoMesmoMomentoDaAnterior = true,
    };

    private static BancaRequeridaInput Banca(int categorias) => new(
        Guid.NewGuid(),
        [.. Enumerable.Range(0, categorias).Select(static _ => Guid.NewGuid())],
        new RegraDeDeliberacaoInput
        {
            Modo = MaiorCodigo<ModoDeDeliberacao>(),
            Integrantes = 99,
            Suplentes = 99,
            Quorum = 99,
            Criterio = MaiorCodigo<CriterioDeVotacao>(),
            Desempate = MaiorCodigo<DesempateDaVotacao>(),
            Encerramento = MaiorCodigo<EncerramentoDaVotacao>(),
            SemUnanimidade = MaiorCodigo<ResultadoSemUnanimidade>(),
        },
        Guid.NewGuid(),
        MaiorCodigo<ConsequenciaDoDescumprimento>());

    private static RegraRecursoFaseInput RegraDeRecurso(IReadOnlyList<string> tiposDeAto) => new(
        new string('A', TamanhoDoCodigoDeRegra), new string('9', TamanhoDoCodigoDeRegra), 9999.99m, MaiorCodigo<UnidadePrazo>(),
        tiposDeAto.MaxBy(static ato => ato.Length)!, true, 9999.99m, MaiorCodigo<UnidadePrazo>(),
        MaiorCodigo<EfeitoSuspensivo>(), MaiorCodigo<GuardaDaVaga>(), Procedimentos());

    private static List<ProcedimentoDeRecursoInput> Procedimentos() =>
    [
        .. CatalogoDe<TipoMotivoDeDecisao>.Todos().SelectMany(static decisao =>
            Enumerable.Range(0, AreasDoEnemPorProcedimento).Select(_ => new ProcedimentoDeRecursoInput(
                decisao.Codigo, new AlvoDoProcedimentoDeRecursoInput(null, new string('A', TamanhoDoCodigoDeRegra)),
                new string('A', TamanhoDoCodigoDeRegra), new string('9', TamanhoDoCodigoDeRegra)))),
    ];

    private static string MaiorCodigo<TEnum>()
        where TEnum : struct, Enum =>
        CatalogoDe<TEnum>.Todos().Select(static c => c.Codigo).MaxBy(static c => c.Length)!;
}
