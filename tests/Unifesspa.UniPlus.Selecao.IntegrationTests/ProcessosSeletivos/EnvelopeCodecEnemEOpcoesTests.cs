namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ApoioDoEnvelopeCodec;

public sealed class EnvelopeCodecEnemEOpcoesTests
{

    [Fact(DisplayName = "O decoder lê baseadoEmEnem falso do envelope sem ENEM, sem assumir verdadeiro")]
    public void Decoder_LeBaseadoEmEnemFalso()
    {
        ProcessoSeletivo processo = ProcessoSemEliminacaoEnem(baseadoEmEnem: false);
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo));
        CorpusEnvelopeEntrada.Publicar(processo);

        Result<EnvelopeReidratado> reidratado = CorpusEnvelope.Registro.Reidratar(CorpusEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes));

        reidratado.IsSuccess.Should().BeTrue(reidratado.Error?.Message);
        reidratado.Value!.Grafo.Classificacao.BaseadoEmEnem.Should().BeFalse();
    }

    /// <summary>Corpus mínimo sem ELIM-CORTE-REDACAO/ELIM-ZERO-EM-AREA — os dois valores de <c>baseadoEmEnem</c> continuam válidos.</summary>
    private static ProcessoSeletivo ProcessoSemEliminacaoEnem(bool baseadoEmEnem) =>
        ProcessoMinimo(ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = CorpusEnvelope.Regra(RegraCalculoCodigo.ClassificacaoImportada, '2'),
            RegraOrdemAlocacao = CorpusEnvelope.Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, '4'),
            BaseadoEmEnem = baseadoEmEnem,
        }).Value!, etapaDeNotaDoEnem: false);

    private static FaseCronograma FaseDeResultadoFinal() => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            OrigemId: Guid.CreateVersion7(), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: true,
            ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), FaseConcluinteCodigo: null,
            UnidadeResponsavel: UnidadesDeTeste.Ceps),
        HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologa(),
        bancasRequeridas: []).Value!;

    /// <summary>
    /// O menor processo publicável, com a classificação informada e uma etapa só, que é a de nota
    /// do ENEM quando <paramref name="etapaDeNotaDoEnem"/> é verdadeiro.
    /// </summary>
    private static ProcessoSeletivo ProcessoMinimo(ConfiguracaoClassificacao classificacao, bool etapaDeNotaDoEnem)
    {
        ProcessoSeletivo processo = NovoProcessoPsiq();

        DefinirCronogramaEEtapa(processo, etapaDeNotaDoEnem);

        processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar([], [], []).Value!).IsSuccess.Should().BeTrue();

        processo.DefinirDistribuicaoVagas([OfertaDaAreaTecnologica()], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();

        processo.DefinirClassificacao(classificacao).IsSuccess.Should().BeTrue();

        DefinirInscricao(processo, classificacao.BaseadoEmEnem);

        return processo;
    }

    private static void DefinirCronogramaEEtapa(ProcessoSeletivo processo, bool etapaDeNotaDoEnem)
    {
        processo.DefinirCronogramaFases([
            FaseDeResultadoFinal(),
        ], []).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([
            EtapaProcesso.Criar(
                new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(new Guid("019fee1e-7000-7000-8000-000000000001"), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: etapaDeNotaDoEnem).Value!, Ordem: 1, FaseCodigo: "RESULTADO_FINAL"),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: etapaDeNotaDoEnem ? null : 10m, PassoDoLancamento: null, FormaDeAvaliacao: etapaDeNotaDoEnem ? null : FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!,
        ]).IsSuccess.Should().BeTrue();
    }


    private static ConfiguracaoDistribuicaoVagas OfertaDaAreaTecnologica()
    {
        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada
            {
                VagasOfertadasBase = 40,
                PercentualDaReservaLegal = 1m,
                RegraDeDistribuicao = CorpusEnvelope.Regra(RegraDistribuicaoVagasCodigo.Institucional, '1'),
                Modalidades = [
                ModalidadeAmpla(),
            ]
            },
            new FatosDaOfertaNoCadastro { GrupoAreaEnem = ("TECNOLOGICA", "Tecnológica"), OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;
        return distribuicao;
    }


    private static ModalidadeSelecionada ModalidadeAmpla() => ModalidadeSelecionada.Criar(
                    Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla,
                    ComposicaoVagasModalidade.RestanteDasVagasOfertadas, null, RegraRemanejamentoModalidade.Nenhuma,
                    null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "Res. Unifesspa 532/2021", quantidadeDeclarada: 40).Value!;


    private static void DefinirInscricao(ProcessoSeletivo processo, bool baseadoEmEnem)
    {
        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
        if (baseadoEmEnem)
        {
            processo.DefinirItens([FormularioDeTeste.CampoDaEdicaoDoEnem(0)]).IsSuccess.Should().BeTrue();
        }

        // Issue #1112: publicar sem declarar cobrança de taxa é recusado (CA-01).
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);
    }


    private static ProcessoSeletivo NovoProcessoPsiq()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS BaseadoEmEnem", TiposDeProcessoDeTeste.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

        return processo;
    }

}
