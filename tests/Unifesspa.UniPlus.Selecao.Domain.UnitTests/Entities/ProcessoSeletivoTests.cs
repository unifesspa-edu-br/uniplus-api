namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura das invariantes do agregado-raiz <see cref="ProcessoSeletivo"/>
/// nas fatias F0 (fundação), F2 (distribuição de vagas), F3 (bônus regional e
/// critérios de desempate) e F4 (classificação): criação em rascunho, etapas
/// pontuadas + divisor da média, oferta de atendimento especializado
/// (ADR-0067), distribuição de vagas por oferta (Story #773), bônus/desempate
/// (Story #774) e classificação — 15º bloco canônico (Story #775).
/// </summary>
public sealed class ProcessoSeletivoTests
{
    private static ProcessoSeletivo NovoProcesso() =>
        ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU);

    [Fact(DisplayName = "Criar inicia o processo em rascunho (CA-01)")]
    public void CriarProcesso_IniciaEmRascunho()
    {
        ProcessoSeletivo processo = NovoProcesso();

        processo.Status.Should().Be(StatusProcesso.Rascunho);
        processo.Nome.Should().Be("PS 2026 — SiSU");
        processo.Tipo.Should().Be(TipoProcesso.SiSU);
        processo.Etapas.Should().BeEmpty();
        processo.OfertaAtendimento.Should().BeNull();
    }

    [Fact(DisplayName = "Criar sem tipo lanca ArgumentException")]
    public void Criar_SemTipo_Lanca()
    {
        Action act = () => ProcessoSeletivo.Criar("PS 2026", TipoProcesso.Nenhum);

        act.Should().Throw<ArgumentException>().WithParameterName("tipo");
    }

    [Fact(DisplayName = "Etapa classificatoria ou ambas com peso compõe o divisor da media (CA-02)")]
    public void Etapa_ComponeDivisorDaMedia()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result result = processo.DefinirEtapas(
        [
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 3m, ordem: 1),
            EtapaProcesso.Criar("Redação", CaraterEtapa.Ambas, peso: 2m, notaMinima: 5m, ordem: 2),
            EtapaProcesso.Criar("Banca de heteroidentificação", CaraterEtapa.Eliminatoria, ordem: 3),
        ]);

        result.IsSuccess.Should().BeTrue();
        // Só as etapas que pontuam (classificatória/ambas, com peso) entram
        // no divisor: 3 + 2 = 5. A eliminatória pura fica fora.
        processo.CalcularDivisorMedia().Should().Be(5m);
    }

    [Fact(DisplayName = "DefinirEtapas com ordem duplicada é recusado")]
    public void DefinirEtapas_OrdemDuplicada_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result result = processo.DefinirEtapas(
        [
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 3m, ordem: 1),
            EtapaProcesso.Criar("Redação", CaraterEtapa.Classificatoria, peso: 2m, ordem: 1),
        ]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.OrdemEtapaDuplicada");
    }

    [Fact(DisplayName = "DefinirEtapas só com etapa eliminatoria (nenhuma compõe nota) é recusado")]
    public void DefinirEtapas_NenhumaComponeNota_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result result = processo.DefinirEtapas(
        [
            EtapaProcesso.Criar("Banca de heteroidentificação", CaraterEtapa.Eliminatoria, ordem: 1),
        ]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.NenhumaEtapaComponeNota");
    }

    [Fact(DisplayName = "DefinirEtapas com classificatoria sem peso (nenhuma compõe nota) é recusado")]
    public void DefinirEtapas_ClassificatoriaSemPeso_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result result = processo.DefinirEtapas(
        [
            EtapaProcesso.Criar("Análise de histórico", CaraterEtapa.Classificatoria, peso: null, ordem: 1),
        ]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.NenhumaEtapaComponeNota");
    }

    [Fact(DisplayName = "DefinirEtapas substitui a colecao anterior por inteiro")]
    public void DefinirEtapas_Substitui()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirEtapas([EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 3m)]);

        processo.DefinirEtapas([EtapaProcesso.Criar("Entrevista", CaraterEtapa.Ambas, peso: 1m)]);

        processo.Etapas.Should().HaveCount(1);
        processo.Etapas.Single().Nome.Should().Be("Entrevista");
    }

    [Fact(DisplayName = "Tipo de deficiencia sob condicao PcD é aceito (CA-06, ADR-0067)")]
    public void Atendimento_TipoDeficienciaSobPcd_Aceito()
    {
        Result<OfertaAtendimentoEspecializado> result = OfertaAtendimentoEspecializado.Criar(
            condicoes: [OfertaCondicao.Criar(Guid.CreateVersion7(), "PCD", "Pessoa com deficiência")],
            recursos: [OfertaRecurso.Criar(Guid.CreateVersion7(), "Ledor")],
            tiposDeficiencia: [OfertaTipoDeficiencia.Criar(Guid.CreateVersion7(), "Deficiência visual")]);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TiposDeficiencia.Should().HaveCount(1);
    }

    [Fact(DisplayName = "Tipo de deficiencia sem condicao PcD é recusado (CA-06, ADR-0067)")]
    public void Atendimento_TipoDeficiencia_SoSobPcd()
    {
        Result<OfertaAtendimentoEspecializado> result = OfertaAtendimentoEspecializado.Criar(
            condicoes: [OfertaCondicao.Criar(Guid.CreateVersion7(), "LACTANTE", "Lactante")],
            recursos: [],
            tiposDeficiencia: [OfertaTipoDeficiencia.Criar(Guid.CreateVersion7(), "Deficiência visual")]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("OfertaAtendimento.TipoDeficienciaSemCondicaoPcd");
    }

    [Fact(DisplayName = "Condicao de atendimento duplicada é recusada")]
    public void Atendimento_CondicaoDuplicada_Recusa()
    {
        Guid condicaoId = Guid.CreateVersion7();

        Result<OfertaAtendimentoEspecializado> result = OfertaAtendimentoEspecializado.Criar(
            condicoes:
            [
                OfertaCondicao.Criar(condicaoId, "PCD", "Pessoa com deficiência"),
                OfertaCondicao.Criar(condicaoId, "PCD", "Pessoa com deficiência"),
            ],
            recursos: [],
            tiposDeficiencia: []);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("OfertaAtendimento.CondicaoDuplicada");
    }

    [Fact(DisplayName = "Recurso de acessibilidade duplicado é recusado")]
    public void Atendimento_RecursoDuplicado_Recusa()
    {
        Guid recursoId = Guid.CreateVersion7();

        Result<OfertaAtendimentoEspecializado> result = OfertaAtendimentoEspecializado.Criar(
            condicoes: [],
            recursos: [OfertaRecurso.Criar(recursoId, "Ledor"), OfertaRecurso.Criar(recursoId, "Ledor")],
            tiposDeficiencia: []);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("OfertaAtendimento.RecursoDuplicado");
    }

    [Fact(DisplayName = "Tipo de deficiencia duplicado é recusado")]
    public void Atendimento_TipoDeficienciaDuplicado_Recusa()
    {
        Guid tipoId = Guid.CreateVersion7();

        Result<OfertaAtendimentoEspecializado> result = OfertaAtendimentoEspecializado.Criar(
            condicoes: [OfertaCondicao.Criar(Guid.CreateVersion7(), "PCD", "Pessoa com deficiência")],
            recursos: [],
            tiposDeficiencia:
            [
                OfertaTipoDeficiencia.Criar(tipoId, "Deficiência visual"),
                OfertaTipoDeficiencia.Criar(tipoId, "Deficiência visual"),
            ]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("OfertaAtendimento.TipoDeficienciaDuplicado");
    }

    [Fact(DisplayName = "DefinirOfertaAtendimento vincula a oferta e as filhas à raiz")]
    public void DefinirOfertaAtendimento_Vincula()
    {
        ProcessoSeletivo processo = NovoProcesso();
        OfertaAtendimentoEspecializado oferta = OfertaAtendimentoEspecializado.Criar(
            condicoes: [OfertaCondicao.Criar(Guid.CreateVersion7(), "PCD", "Pessoa com deficiência")],
            recursos: [OfertaRecurso.Criar(Guid.CreateVersion7(), "Prova ampliada")],
            tiposDeficiencia: []).Value!;

        Result result = processo.DefinirOfertaAtendimento(oferta);

        result.IsSuccess.Should().BeTrue();
        processo.OfertaAtendimento.Should().NotBeNull();
        processo.OfertaAtendimento!.ProcessoSeletivoId.Should().Be(processo.Id);
        processo.OfertaAtendimento.Condicoes.Single().OfertaAtendimentoEspecializadoId
            .Should().Be(oferta.Id);
    }

    private static ConfiguracaoDistribuicaoVagas NovaDistribuicao(Guid ofertaCursoId)
    {
        ModalidadeSelecionada ampla = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, [], null, "base legal").Value!;

        ReferenciaRegra regra = ReferenciaRegra.Criar(
            RegraDistribuicaoVagasCodigo.Institucional, "v1", new string('a', 64)).Value!;

        return ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoId, voBase: 50, pr: 1m, regra, referenciaDemografica: null, [ampla]).Value!;
    }

    [Fact(DisplayName = "DefinirDistribuicaoVagas vincula a configuração à raiz (Story #773)")]
    public void DefinirDistribuicaoVagas_Vincula()
    {
        ProcessoSeletivo processo = NovoProcesso();
        ConfiguracaoDistribuicaoVagas configuracao = NovaDistribuicao(Guid.CreateVersion7());

        Result result = processo.DefinirDistribuicaoVagas([configuracao]);

        result.IsSuccess.Should().BeTrue();
        processo.DistribuicaoVagas.Should().ContainSingle();
        processo.DistribuicaoVagas.Single().ProcessoSeletivoId.Should().Be(processo.Id);
    }

    [Fact(DisplayName = "DefinirDistribuicaoVagas vazia é recusada")]
    public void DefinirDistribuicaoVagas_Vazia_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result result = processo.DefinirDistribuicaoVagas([]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.DistribuicaoVagasVazia");
    }

    [Fact(DisplayName = "DefinirDistribuicaoVagas com oferta de curso duplicada é recusada")]
    public void DefinirDistribuicaoVagas_OfertaCursoDuplicada_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Guid ofertaCursoId = Guid.CreateVersion7();

        Result result = processo.DefinirDistribuicaoVagas(
        [
            NovaDistribuicao(ofertaCursoId),
            NovaDistribuicao(ofertaCursoId),
        ]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.OfertaCursoDuplicada");
    }

    private static ReferenciaRegra RegraDesempate(string codigo) =>
        ReferenciaRegra.Criar(codigo, "v1", new string('a', 64)).Value!;

    [Fact(DisplayName = "DefinirBonusRegional define e depois remove (toggle por presença, RN05)")]
    public void DefinirBonusRegional_DefineERemove()
    {
        ProcessoSeletivo processo = NovoProcesso();
        ConfiguracaoBonusRegional bonus = ConfiguracaoBonusRegional.Criar(
            ReferenciaRegra.Criar(RegraBonusCodigo.Multiplicativo, "v1", new string('a', 64)).Value!,
            1.20m, null, "Marabá", "RN05").Value!;

        processo.DefinirBonusRegional(bonus).IsSuccess.Should().BeTrue();
        processo.BonusRegional.Should().NotBeNull();
        processo.BonusRegional!.ProcessoSeletivoId.Should().Be(processo.Id);

        processo.DefinirBonusRegional(null).IsSuccess.Should().BeTrue();
        processo.BonusRegional.Should().BeNull();
    }

    [Fact(DisplayName = "DefinirCriteriosDesempate vincula os critérios em ordem à raiz")]
    public void DefinirCriteriosDesempate_Vincula()
    {
        ProcessoSeletivo processo = NovoProcesso();
        CriterioDesempate idoso = CriterioDesempate.Criar(1, RegraDesempate(CriterioDesempateCodigo.Idoso), new ArgsDesempateIdoso(60)).Value!;
        CriterioDesempate maiorIdade = CriterioDesempate.Criar(2, RegraDesempate(CriterioDesempateCodigo.MaiorIdade), new ArgsDesempateMaiorIdade()).Value!;

        Result result = processo.DefinirCriteriosDesempate([idoso, maiorIdade]);

        result.IsSuccess.Should().BeTrue();
        processo.CriteriosDesempate.Should().HaveCount(2);
        processo.CriteriosDesempate.Should().OnlyContain(c => c.ProcessoSeletivoId == processo.Id);
    }

    [Fact(DisplayName = "DefinirCriteriosDesempate vazia remove todos os critérios (dimensão opcional)")]
    public void DefinirCriteriosDesempate_Vazia_RemoveTodos()
    {
        ProcessoSeletivo processo = NovoProcesso();
        CriterioDesempate maiorIdade = CriterioDesempate.Criar(1, RegraDesempate(CriterioDesempateCodigo.MaiorIdade), new ArgsDesempateMaiorIdade()).Value!;
        processo.DefinirCriteriosDesempate([maiorIdade]);

        Result result = processo.DefinirCriteriosDesempate([]);

        result.IsSuccess.Should().BeTrue();
        processo.CriteriosDesempate.Should().BeEmpty();
    }

    [Fact(DisplayName = "DefinirCriteriosDesempate com ordem duplicada é recusado")]
    public void DefinirCriteriosDesempate_OrdemDuplicada_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        CriterioDesempate a = CriterioDesempate.Criar(1, RegraDesempate(CriterioDesempateCodigo.MaiorIdade), new ArgsDesempateMaiorIdade()).Value!;
        CriterioDesempate b = CriterioDesempate.Criar(1, RegraDesempate(CriterioDesempateCodigo.Idoso), new ArgsDesempateIdoso(60)).Value!;

        Result result = processo.DefinirCriteriosDesempate([a, b]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.OrdemDesempateDuplicada");
    }

    [Fact(DisplayName = "DefinirCriteriosDesempate com etapa_ref existente no processo tem sucesso (INV-B6)")]
    public void DefinirCriteriosDesempate_EtapaRefExistente_Sucesso()
    {
        ProcessoSeletivo processo = NovoProcesso();
        EtapaProcesso etapa = EtapaProcesso.Criar("Redação", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1);
        processo.DefinirEtapas([etapa]);

        CriterioDesempate criterio = CriterioDesempate.Criar(
            1, RegraDesempate(CriterioDesempateCodigo.MaiorNotaEtapa), new ArgsDesempateMaiorNotaEtapa(etapa.Id)).Value!;

        Result result = processo.DefinirCriteriosDesempate([criterio]);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "DefinirCriteriosDesempate com etapa_ref inexistente no processo é recusado (INV-B6)")]
    public void DefinirCriteriosDesempate_EtapaRefInexistente_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirEtapas([EtapaProcesso.Criar("Redação", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1)]);

        CriterioDesempate criterio = CriterioDesempate.Criar(
            1, RegraDesempate(CriterioDesempateCodigo.MaiorNotaEtapa), new ArgsDesempateMaiorNotaEtapa(Guid.CreateVersion7())).Value!;

        Result result = processo.DefinirCriteriosDesempate([criterio]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaRefDesempateInexistente");
    }

    [Fact(DisplayName = "DefinirEtapas recusa remover uma etapa referenciada por critério de desempate (achado Codex)")]
    public void DefinirEtapas_RemoverEtapaReferenciadaPorDesempate_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        EtapaProcesso redacao = EtapaProcesso.Criar("Redação", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1);
        processo.DefinirEtapas([redacao]);

        CriterioDesempate criterio = CriterioDesempate.Criar(
            1, RegraDesempate(CriterioDesempateCodigo.MaiorNotaEtapa), new ArgsDesempateMaiorNotaEtapa(redacao.Id)).Value!;
        processo.DefinirCriteriosDesempate([criterio]);

        // Troca as etapas por uma completamente nova — a antiga (referenciada
        // pelo desempate) desaparece do conjunto.
        Result result = processo.DefinirEtapas(
            [EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1)]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaReferenciadaPorDesempate");
        // A troca é rejeitada por inteiro — a etapa original permanece.
        processo.Etapas.Should().ContainSingle(e => e.Id == redacao.Id);
    }

    [Fact(DisplayName = "DefinirEtapas permite manter a mesma etapa referenciada por desempate")]
    public void DefinirEtapas_MesmaEtapaReferenciadaPorDesempate_Sucesso()
    {
        ProcessoSeletivo processo = NovoProcesso();
        EtapaProcesso redacao = EtapaProcesso.Criar("Redação", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1);
        processo.DefinirEtapas([redacao]);

        CriterioDesempate criterio = CriterioDesempate.Criar(
            1, RegraDesempate(CriterioDesempateCodigo.MaiorNotaEtapa), new ArgsDesempateMaiorNotaEtapa(redacao.Id)).Value!;
        processo.DefinirCriteriosDesempate([criterio]);

        // Redefine as etapas incluindo a MESMA instância (mesmo Id) — não deve
        // ser bloqueado, já que o desempate continua executável.
        Result result = processo.DefinirEtapas(
        [
            redacao,
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 2m, ordem: 2),
        ]);

        result.IsSuccess.Should().BeTrue();
    }

    private static ConfiguracaoClassificacao NovaClassificacao(IReadOnlyList<RegraEliminacao>? regrasEliminacao = null) =>
        ConfiguracaoClassificacao.Criar(
            ReferenciaRegra.Criar(RegraCalculoCodigo.FormulaMediaPonderada, "v1", new string('a', 64)).Value!,
            ReferenciaRegra.Criar(RegraArredondamentoCodigo.PrecisaoTruncar, "v1", new string('b', 64)).Value!,
            2,
            ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", new string('c', 64)).Value!,
            1,
            regrasEliminacao ?? []).Value!;

    [Fact(DisplayName = "DefinirClassificacao vincula a configuração à raiz")]
    public void DefinirClassificacao_Vincula()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result result = processo.DefinirClassificacao(NovaClassificacao());

        result.IsSuccess.Should().BeTrue();
        processo.Classificacao.Should().NotBeNull();
        processo.Classificacao!.ProcessoSeletivoId.Should().Be(processo.Id);
    }

    [Fact(DisplayName = "DefinirClassificacao com etapa_ref de eliminação existente no processo tem sucesso (INV-B4)")]
    public void DefinirClassificacao_EtapaRefEliminacaoExistente_Sucesso()
    {
        ProcessoSeletivo processo = NovoProcesso();
        EtapaProcesso etapa = EtapaProcesso.Criar("Objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1);
        processo.DefinirEtapas([etapa]);

        RegraEliminacao eliminacao = RegraEliminacao.Criar(
            ReferenciaRegra.Criar(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, "v1", new string('d', 64)).Value!,
            new ArgsElimNotaMinimaEtapa(etapa.Id, 4m)).Value!;

        Result result = processo.DefinirClassificacao(NovaClassificacao([eliminacao]));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "DefinirEtapas remover etapa referenciada por eliminação da classificação é recusado (INV-B4 sobrevive)")]
    public void DefinirEtapas_RemoverEtapaReferenciadaPorClassificacao_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        EtapaProcesso etapa = EtapaProcesso.Criar("Objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1);
        processo.DefinirEtapas([etapa]);

        RegraEliminacao eliminacao = RegraEliminacao.Criar(
            ReferenciaRegra.Criar(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, "v1", new string('d', 64)).Value!,
            new ArgsElimNotaMinimaEtapa(etapa.Id, 4m)).Value!;
        processo.DefinirClassificacao(NovaClassificacao([eliminacao])).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirEtapas(
            [EtapaProcesso.Criar("Entrevista", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1)]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaReferenciadaPorClassificacao");
    }

    [Fact(DisplayName = "DefinirEtapas mantendo a mesma etapa referenciada pela classificação tem sucesso")]
    public void DefinirEtapas_MesmaEtapaReferenciadaPorClassificacao_Sucesso()
    {
        ProcessoSeletivo processo = NovoProcesso();
        EtapaProcesso etapa = EtapaProcesso.Criar("Objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1);
        processo.DefinirEtapas([etapa]);

        RegraEliminacao eliminacao = RegraEliminacao.Criar(
            ReferenciaRegra.Criar(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, "v1", new string('d', 64)).Value!,
            new ArgsElimNotaMinimaEtapa(etapa.Id, 4m)).Value!;
        processo.DefinirClassificacao(NovaClassificacao([eliminacao])).IsSuccess.Should().BeTrue();

        Result result = processo.DefinirEtapas([etapa]);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "DefinirClassificacao com etapa_ref de eliminação inexistente no processo é recusado (INV-B4)")]
    public void DefinirClassificacao_EtapaRefEliminacaoInexistente_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirEtapas([EtapaProcesso.Criar("Objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1)]);

        RegraEliminacao eliminacao = RegraEliminacao.Criar(
            ReferenciaRegra.Criar(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, "v1", new string('d', 64)).Value!,
            new ArgsElimNotaMinimaEtapa(Guid.CreateVersion7(), 4m)).Value!;

        Result result = processo.DefinirClassificacao(NovaClassificacao([eliminacao]));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaRefEliminacaoInexistente");
    }

    [Fact(DisplayName = "DefinirClassificacao com ELIM-CORTE-REDACAO fora de processo ENEM é recusado")]
    public void DefinirClassificacao_EliminacaoEnemForaDeProcessoEnem_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSIQ 2026", TipoProcesso.PSIQ);

        RegraEliminacao eliminacao = RegraEliminacao.Criar(
            ReferenciaRegra.Criar(RegraEliminacaoCodigo.ElimCorteRedacao, "v1", new string('e', 64)).Value!,
            new ArgsElimCorteRedacao(400m)).Value!;

        Result result = processo.DefinirClassificacao(NovaClassificacao([eliminacao]));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.EliminacaoEnemForaDeProcessoEnem");
    }

    [Fact(DisplayName = "DefinirClassificacao com ELIM-CORTE-REDACAO em processo PSVR (baseado em ENEM) tem sucesso")]
    public void DefinirClassificacao_EliminacaoEnemEmProcessoEnem_Sucesso()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PSVR 2026", TipoProcesso.PSVR);

        RegraEliminacao eliminacao = RegraEliminacao.Criar(
            ReferenciaRegra.Criar(RegraEliminacaoCodigo.ElimCorteRedacao, "v1", new string('e', 64)).Value!,
            new ArgsElimCorteRedacao(400m)).Value!;

        Result result = processo.DefinirClassificacao(NovaClassificacao([eliminacao]));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "ConcorrenciaDuplaAplicavel é falsa sem distribuição de vagas com cota reservada")]
    public void ConcorrenciaDuplaAplicavel_SemCotaReservada_Falsa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirDistribuicaoVagas([NovaDistribuicao(Guid.CreateVersion7())]);

        processo.ConcorrenciaDuplaAplicavel().Should().BeFalse();
    }

    [Fact(DisplayName = "ConcorrenciaDuplaAplicavel é derivada (true) quando há modalidade de cota reservada (INV-B7)")]
    public void ConcorrenciaDuplaAplicavel_ComCotaReservada_Verdadeira()
    {
        ProcessoSeletivo processo = NovoProcesso();

        ModalidadeSelecionada ampla = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, [], null, "base legal").Value!;
        ModalidadeSelecionada cotaReservada = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "LB_PPI", null, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDoVr,
            null, RegraRemanejamentoModalidade.SegueCascata, null, null, null, [], null, "Lei 12.711/2012 art. 3º").Value!;

        // Só para exercitar o cenário mínimo de derivação — INV-6 (8 federais+AC)
        // não precisa estar satisfeita aqui, já que quem valida isso é
        // ConfiguracaoDistribuicaoVagas.Criar, não este teste (usa regra
        // institucional para não disparar essa validação).
        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 50, pr: 1m,
            ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Institucional, "v1", new string('f', 64)).Value!,
            referenciaDemografica: null, [ampla, cotaReservada]).Value!;

        processo.DefinirDistribuicaoVagas([distribuicao]);

        processo.ConcorrenciaDuplaAplicavel().Should().BeTrue();
    }
}
