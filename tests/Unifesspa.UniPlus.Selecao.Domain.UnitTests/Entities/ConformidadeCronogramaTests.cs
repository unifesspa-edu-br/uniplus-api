namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaConformidadeDoCronograma;

/// <summary>
/// Cobertura do gate de publicação para as pendências do cronograma que só afloram no
/// congelamento (<c>ProcessoSeletivo.PendenciaDoCronograma</c>):
/// piso mínimo derivado da origem dos candidatos (CA-11), indistinguibilidade por tipo
/// (CA-12), vagas sem fase que produz resultado (CA-13), a direção lazy da
/// bicondicional fase×etapa (CA-14) e o divisor da média sob fórmula local (CA-15).
/// </summary>
public sealed class ConformidadeCronogramaTests
{
    private static Result<FaseCronograma> Fase(
        int ordem,
        string codigo,
        bool agrupaEtapas = false,
        bool produzResultado = false,
        bool coletaInscricao = false,
        bool coletaSolicitacaoIsencao = false) =>
        FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(OrigemId: Guid.CreateVersion7(), Codigo: codigo, OrigemData: OrigemDataFase.NaExecucao, AgrupaEtapas: agrupaEtapas, PermiteComplementacao: false, ColetaInscricao: coletaInscricao, ColetaSolicitacaoIsencao: coletaSolicitacaoIsencao)),
            new DecisoesDeCronogramaDaFase(Ordem: ordem, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(Produtos: produzResultado ? [ProdutoDaFase.Criar(codigo, PapelDaPublicacaoPrevista.Definitivo)] : [], EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []);


    [Fact(DisplayName = "CA-11: origem InscricaoPropria sem NENHUMA fase que colete inscrição reprova a publicação")]
    public void InscricaoPropria_SemFaseDeColeta_Reprova()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        processo.DefinirAtendimentoEspecializado(AtendimentoEspecializado.Criar([], [], []).Value!);
        processo.DefinirDistribuicaoVagas([Distribuicao(40)], FatosDeModalidadeDeTeste.DoCatalogo);
        processo.DefinirClassificacao(ClassificacaoImportada());
        processo.DefinirCronogramaFases(
            [Fase(1, "RESULTADO_FINAL", produzResultado: true).Value!], []);
        DeclararNaoCobra(processo);

        Result<VersaoConfiguracao> resultado = Publicar(processo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.InscricaoPropriaSemFaseDeColeta");
    }

    public static IEnumerable<object[]> TiposDeProcessoDistintos =>
    [
        [TiposDeProcessoDeTeste.SiSU],
        [TiposDeProcessoDeTeste.PSIQ],
    ];

    [Theory(DisplayName = "CA-12: ImportacaoExterna publica sem inscrição/etapa/fórmula local — o veredicto NÃO muda com o tipo (indistinguibilidade)")]
    [MemberData(nameof(TiposDeProcessoDistintos))]
    public void ImportacaoExterna_MesmaConfiguracao_TiposDiferentes_MesmoVeredicto(TipoProcessoSnapshot tipo)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", tipo, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        processo.DefinirAtendimentoEspecializado(AtendimentoEspecializado.Criar([], [], []).Value!);
        processo.DefinirDistribuicaoVagas([Distribuicao(40)], FatosDeModalidadeDeTeste.DoCatalogo);
        processo.DefinirClassificacao(ClassificacaoImportada());
        processo.DefinirCronogramaFases([HomologacaoDasInscricoesDeTeste.FaseQueHomologaComRecurso(1, "RESULTADO_FINAL")], []);
        DeclararNaoCobra(processo);
        ContagemDePrazosDeTeste.DeclararConvencao(processo);

        Result<VersaoConfiguracao> resultado = Publicar(processo);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "CA-13: havendo vagas ofertadas e NENHUMA fase que produz resultado, a publicação é recusada")]
    public void ComVagas_SemFaseQueProduzResultado_Reprova()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        processo.DefinirAtendimentoEspecializado(AtendimentoEspecializado.Criar([], [], []).Value!);
        processo.DefinirDistribuicaoVagas([Distribuicao(40)], FatosDeModalidadeDeTeste.DoCatalogo);
        processo.DefinirClassificacao(ClassificacaoImportada());
        // Só a fase de matrícula — nenhuma produz resultado.
        processo.DefinirCronogramaFases([Fase(1, "MATRICULA").Value!], []);
        DeclararNaoCobra(processo);

        Result<VersaoConfiguracao> resultado = Publicar(processo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.VagasSemFaseQueProduzResultado");
    }

    [Fact(DisplayName = "Etapa pontuada sem fase declarada reprova a publicação, citando a etapa")]
    public void Etapa_SemFaseDeclarada_Reprova()
    {
        Result<VersaoConfiguracao> resultado = Publicar(ProcessoComEtapaPontuadaSemFaseDeclarada());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaSemFaseDeclarada");
        resultado.Error!.Message.Should().Contain("'Prova'");
    }

    private static ProcessoSeletivo ProcessoComEtapaPontuadaSemFaseDeclarada()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        processo.DefinirCronogramaFases([Fase(1, "RESULTADO_FINAL", produzResultado: true).Value!], []);
        processo.DefinirEtapas(
            [ApoioDaPublicacaoDoProcessoSeletivo.EtapaDaProvaObjetiva("Prova", CaraterEtapa.Classificatoria, 1m)]);
        processo.DefinirAtendimentoEspecializado(AtendimentoEspecializado.Criar([], [], []).Value!);
        processo.DefinirDistribuicaoVagas([Distribuicao(40)], FatosDeModalidadeDeTeste.DoCatalogo);
        processo.DefinirClassificacao(ClassificacaoMediaPonderada());
        DeclararNaoCobra(processo);

        return processo;
    }

    [Fact(DisplayName = "CA-15 (regressão): processo SEM etapa, com CLASSIFICACAO_IMPORTADA, publica — hoje um processo sem etapa não publicava (o teste falha sem o fix)")]
    public void SemEtapa_ClassificacaoImportada_Publica()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS SiSU", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        processo.DefinirEtapas([]).IsSuccess.Should().BeTrue(
            "Story #851 §3.5: lista vazia é estado válido — um processo sem prova (SiSU) não tem etapa");
        processo.DefinirAtendimentoEspecializado(AtendimentoEspecializado.Criar([], [], []).Value!);
        processo.DefinirDistribuicaoVagas([Distribuicao(40)], FatosDeModalidadeDeTeste.DoCatalogo);
        processo.DefinirClassificacao(ClassificacaoImportada());
        processo.DefinirCronogramaFases([HomologacaoDasInscricoesDeTeste.FaseQueHomologaComRecurso(1, "RESULTADO_FINAL")], []);
        DeclararNaoCobra(processo);
        ContagemDePrazosDeTeste.DeclararConvencao(processo);

        Result<VersaoConfiguracao> resultado = Publicar(processo);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "CA-15: sob FORMULA_MEDIA_PONDERADA, divisor ZERO reprova a publicação (etapas só eliminatórias, sem peso)")]
    public void FormulaLocal_DivisorZero_Reprova()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        // Etapa eliminatória pura não compõe a nota (compor exige classificatória ou ambas, com peso):
        // o domínio recusa a lista já em DefinirEtapas, antes de a publicação chegar ao divisor zero.
        Result definirEtapas = processo.DefinirEtapas(
            [ApoioDaPublicacaoDoProcessoSeletivo.EtapaDaProvaObjetiva("Entrevista", CaraterEtapa.Eliminatoria, null, notaMinima: 5m)]);
        definirEtapas.IsFailure.Should().BeTrue(
            "pré-condição: DefinirEtapas já recusa uma lista sem NENHUMA etapa que componha a nota — " +
            "prova que o guard estrutural do domínio intercepta o divisor zero antes mesmo do gate de publicação");
        definirEtapas.Error!.Code.Should().Be("ProcessoSeletivo.NenhumaEtapaComponeNota");
    }

    private static ConfiguracaoClassificacao ClassificacaoImportada() =>
        ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = Regra(RegraDeCalculoDaNota.ClassificacaoImportada.Codigo(), 'b'),
            RegraOrdemAlocacao = Regra(RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), 'c'),
        }).Value!;

    private static ConfiguracaoClassificacao ClassificacaoMediaPonderada() =>
        ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = Regra(RegraDeCalculoDaNota.MediaPonderada.Codigo(), 'b'),
            RegraArredondamento = Regra(RegraDeArredondamentoDaNota.Truncar.Codigo(), 'd'),
            CasasArredondamento = 2,
            RegraOrdemAlocacao = Regra(RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), 'c'),
            EscalaNotaFinal = 100m,
        }).Value!;
}
