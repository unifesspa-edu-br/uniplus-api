namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal static class ApoioDaCascataDeRemanejamento
{
    internal static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];

    internal static JsonElement Json(string raw)
    {
        using JsonDocument document = JsonDocument.Parse(raw);
        return document.RootElement.Clone();
    }

    internal static RegraCatalogo RegraCascataValida() => RegraCatalogo.Criar(
        RegraRemanejamentoCodigo.Cascata, "v1", TipoRegra.CriterioRemanejamento,
        Json("""{"fallbackCodigo":"AC","ordens":[{"origem":"LB_PPI","destinos":["LB_Q","LB_PCD"]}]}"""),
        Json("[]"), "ADR-0120");

    internal sealed record Mocks(
        IProcessoSeletivoRepository Repository, IRegraCatalogoReader RegraCatalogoReader, IUnitOfWork UnitOfWork);

    internal static Mocks NovosMocks(ProcessoSeletivo? processo, Guid processoId)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processoId, Arg.Any<CancellationToken>()).Returns(processo);
        return new Mocks(repository, Substitute.For<IRegraCatalogoReader>(), Substitute.For<IUnitOfWork>());
    }

    internal static ProcessoSeletivo NovoProcesso() =>
        ProcessoSeletivo.Criar("PS 2026", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

    public static TheoryData<string?, string?, string?, IReadOnlyList<DestinoRemanejamentoInput>?> CombinacoesParciais() =>
        new()
        {
            { RegraRemanejamentoCodigo.Cascata, null, null, null },
            { null, "v1", null, null },
            { null, null, "AC", null },
            { null, null, null, [new DestinoRemanejamentoInput("LB_PPI", 1, "LB_Q")] },
            { RegraRemanejamentoCodigo.Cascata, "v1", null, null },
            { RegraRemanejamentoCodigo.Cascata, "v1", "AC", null },
        };

    internal static FaseCronograma FaseDeResultadoFinal() => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            OrigemId: Guid.CreateVersion7(), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: true,
            ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), FaseConcluinteCodigo: null,
            UnidadeResponsavel: UnidadesDeTeste.Ceps),
        HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologa(resultado: "RESULTADO_FINAL"),
        bancasRequeridas: []).Value!;

    /// <summary>Processo publicado, que <see cref="ProcessoSeletivo.MutacaoBloqueada"/> recusa.</summary>
    internal static ProcessoSeletivo NovoProcessoPublicado()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — Publicado", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        DefinirOAtendimento(processo);
        DefinirAsVagasDeAmpla(processo);
        DefinirAClassificacaoImportada(processo);
        DefinirOCronogramaEAEtapa(processo);
        DefinirATaxaEAsDeclaracoes(processo);
        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
        processo.Publicar(new CenarioDeCongelamento { Dados = DadosDoEdital(), Documento = DocumentoDoEdital() }).IsSuccess.Should().BeTrue();

        return processo;
    }

    private static void DefinirOAtendimento(ProcessoSeletivo processo) =>
        processo.DefinirAtendimentoEspecializado(AtendimentoEspecializado.Criar([], [], []).Value!).IsSuccess.Should().BeTrue();

    private static void DefinirAsVagasDeAmpla(ProcessoSeletivo processo)
    {
        ReferenciaRegra regraDistribuicao = ReferenciaRegra.De(RegraDistribuicaoVagasCodigo.Institucional, "v1", HashFixo);
        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "Res. Unifesspa 532/2021", quantidadeDeclarada: 40).Value!;
        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = regraDistribuicao, Modalidades = [modalidade] },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();
    }

    private static void DefinirAClassificacaoImportada(ProcessoSeletivo processo)
    {
        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = ReferenciaRegra.De(RegraCalculoCodigo.ClassificacaoImportada, "v1", HashFixo),
            RegraOrdemAlocacao = ReferenciaRegra.De(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "v1", HashFixo),
        }).Value!;
        processo.DefinirClassificacao(classificacao).IsSuccess.Should().BeTrue();
    }

    private static void DefinirOCronogramaEAEtapa(ProcessoSeletivo processo)
    {
        FaseCronograma faseConforme = FaseDeResultadoFinal();
        processo.DefinirCronogramaFases([faseConforme], []).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([
            EtapaProcesso.Criar(
                new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: "RESULTADO_FINAL"),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!,
        ]).IsSuccess.Should().BeTrue();
    }

    private static void DefinirATaxaEAsDeclaracoes(ProcessoSeletivo processo)
    {
        // Publicar sem declarar a cobrança de taxa é recusado.
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);
    }

    private static DadosEdital DadosDoEdital() => DadosEdital.Criar(
        "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)), new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), Guid.CreateVersion7()).Value!;

    private static DocumentoCongelado DocumentoDoEdital() => new(
        System.Text.Encoding.UTF8.GetBytes(new JsonObject { ["status"] = "ok" }.ToJsonString()), "1.0", "canonical-json/sha256@v1", HashFixo);
}
