namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal static class ApoioDoDesempatePorAreaEnem
{
    internal static ProcessoSeletivo NovoProcesso() =>
        ProcessoSeletivo.Criar("PS ENEM 2027", TiposDeProcessoDeTeste.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    internal static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.De(codigo, "v1", new string(semente, 64));

    internal static CriterioDesempate PorArea(int ordem, params string[] areas) =>
        CriterioDesempate.Criar(ordem, Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, 'e'), new ParametrosDesempateMaiorNotaAreaEnem(areas)).Value!;

    internal static CriterioDesempate MaiorIdade(int ordem) =>
        CriterioDesempate.Criar(ordem, Regra(CriterioDesempateCodigo.MaiorIdade, 'f'), new ParametrosDesempateMaiorIdade()).Value!;

    internal static ConfiguracaoClassificacao ClassificacaoEnem(IReadOnlyList<GrupoPesoAreaEnemCongelado>? quadro = null) =>
        ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'a'),
            RegraArredondamento = Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'b'),
            CasasArredondamento = 2,
            RegraOrdemAlocacao = Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'),
            BaseadoEmEnem = true,
            ResolucaoPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Resolucao,
            QuadroPesoAreaEnem = quadro ?? QuadroPesoAreaEnemDeTeste.Completo(),
            EscalaNotaFinal = 100m,
        }).Value!;

    internal static ConfiguracaoClassificacao ClassificacaoSemEnem() =>
        ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'a'),
            RegraArredondamento = Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'b'),
            CasasArredondamento = 2,
            RegraOrdemAlocacao = Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'),
            EscalaNotaFinal = 100m,
        }).Value!;

    internal static ConfiguracaoClassificacao ClassificacaoImportadaDoEnem() =>
        ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = Regra(RegraCalculoCodigo.ClassificacaoImportada, 'a'),
            RegraOrdemAlocacao = Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'),
            BaseadoEmEnem = true,
        }).Value!;

    internal static ConfiguracaoClassificacao ClassificacaoImportadaSemEnem() =>
        ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = Regra(RegraCalculoCodigo.ClassificacaoImportada, 'a'),
            RegraOrdemAlocacao = Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'),
        }).Value!;

    /// <summary>Quadro em que um dos grupos não tem a área de Matemática.</summary>
    internal static IReadOnlyList<GrupoPesoAreaEnemCongelado> QuadroComGrupoSemMatematica() =>
    [
        QuadroPesoAreaEnemDeTeste.Grupo("HUMANISTICA_I", "Humanística I"),
        GrupoPesoAreaEnemCongelado.Criar(
            "TECNOLOGICA",
            "Tecnológica",
            "Resolução nº 805/2024/Consepe – Anexo I",
            [
                ("REDACAO", "Redação", 2.00m, 400m),
                ("LINGUAGENS", "Linguagens e suas Tecnologias", 1.00m, null),
            ]).Value!,
    ];

    internal static void DuplicarEtapaDeNotaDoEnem(ProcessoSeletivo processo) => AdicionarEtapasDeNotaDoEnem(processo, 2);

    internal static void AdicionarEtapasDeNotaDoEnem(ProcessoSeletivo processo, int quantidade)
    {
        EtapaProcesso Etapa(int ordem) => EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: "Nota do ENEM", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "NOTA_ENEM", "Nota do ENEM", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: true).Value!, Ordem: ordem, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        System.Reflection.FieldInfo etapas = typeof(ProcessoSeletivo).GetField("_etapas", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        ((List<EtapaProcesso>)etapas.GetValue(processo)!).AddRange(Enumerable.Range(1, quantidade).Select(Etapa));
    }
}
