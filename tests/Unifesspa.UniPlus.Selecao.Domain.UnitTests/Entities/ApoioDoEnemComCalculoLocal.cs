namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioConformidadePublicabilidadeEstrutural;

/// <summary>A classificação baseada em ENEM com cálculo local pelos pesos por área, montada sobre o processo conforme.</summary>
internal static class ApoioDoEnemComCalculoLocal
{
    internal static readonly (string? Codigo, string? Rotulo) GrupoHumanisticaI = ("HUMANISTICA_I", "Humanística I");

    internal static ConfiguracaoClassificacao ClassificacaoEnemLocal(IReadOnlyList<GrupoPesoAreaEnemCongelado> quadro) =>
        ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'a'),
            RegraArredondamento = Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'b'),
            CasasArredondamento = 2,
            RegraOrdemAlocacao = Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'),
            BaseadoEmEnem = true,
            ResolucaoPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Resolucao,
            QuadroPesoAreaEnem = quadro,
            EscalaNotaFinal = 100m,
        }).Value!;

    internal static ProcessoSeletivo ProcessoEnemLocal(
        IReadOnlyList<ConfiguracaoDistribuicaoVagas> ofertas,
        IReadOnlyList<GrupoPesoAreaEnemCongelado>? quadro = null)
    {
        ProcessoSeletivo processo = ProcessoConformeComEtapaQueCompoeNota();
        processo.DefinirDistribuicaoVagas(ofertas, FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(ClassificacaoEnemLocal(quadro ?? QuadroPesoAreaEnemDeTeste.Completo()))
            .IsSuccess.Should().BeTrue();
        // Classificação baseada em ENEM exige ao menos uma edição admitida (issue #1620) — sem
        // ela, o item "classificacao_edicao_enem_nao_declarada" ficaria vermelho e interferiria
        // nos testes desta classe, que focam nos itens do quadro de pesos por área.
        processo.DefinirOpcoesDeclaradas("EDICAO_ENEM", [OpcaoDeclaradaFato.Criar("EDICAO_ENEM", "2026", "2026", 0).Value!])
            .IsSuccess.Should().BeTrue();
        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
        return processo;
    }

    /// <summary>A fábrica recusa quadro vazio; o EF hidrata a linha sem passar por ela.</summary>
    internal static void EsvaziarQuadro(ConfiguracaoClassificacao classificacao) =>
        ((List<GrupoPesoAreaEnemCongelado>)typeof(ConfiguracaoClassificacao)
            .GetField("_quadroPesoAreaEnem", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(classificacao)!).Clear();

    /// <summary>
    /// Processo conforme com uma etapa que compõe nota e a fase que a agrupa: sem elas o
    /// divisor da média é zero e a publicação recusa antes, por outro item. A oferta tem um
    /// grupo do quadro de teste, que a classificação ENEM com cálculo local exige.
    /// </summary>
    internal static ProcessoSeletivo ProcessoConformeComEtapaQueCompoeNota()
    {
        ProcessoSeletivo processo = ProcessoConforme([OfertaAmpla(10, GrupoTecnologica)]);
        processo.DefinirCronogramaFases(
            [FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
                new OrigemCanonicaDaFase(
                    Guid.CreateVersion7(), "RESULTADO_FINAL", OrigemDataFase.NaExecucao, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
                new DecisoesDeCronogramaDaFase(1, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
                HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologaComRecurso(),
                bancasRequeridas: HomologacaoDasInscricoesDeTeste.BancasDaFaseQueHomologaComRecurso()).Value!],
            []).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas(
            [EtapaDeProvaObjetiva("Prova", CaraterEtapa.Classificatoria, 1, 1m, null, "RESULTADO_FINAL")])
            .IsSuccess.Should().BeTrue();
        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
        return processo;
    }
}
