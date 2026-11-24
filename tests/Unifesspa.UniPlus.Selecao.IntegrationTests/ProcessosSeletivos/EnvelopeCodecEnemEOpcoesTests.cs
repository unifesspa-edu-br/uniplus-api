namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
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
    [Fact(DisplayName = "O decoder preserva baseadoEmEnem alterado ao recodificar o envelope inteiro")]
    public void Decoder_NaoPerdeCampo_BaseadoEmEnem()
    {
        ProcessoSeletivo processo = ProcessoSemEliminacaoEnem(baseadoEmEnem: false);
        SnapshotCanonico congelado = ExemploDeEnvelope.Canonicalizador.Canonicalizar(ExemploDeEnvelopeEntrada.Entrada(processo));
        JsonObject mutado = Envelope(congelado);
        Mutar(mutado, "classificacao.baseadoEmEnem", "True");
        byte[] bytesMutados = PerfilCanonicoV1.Instancia.Serializar(mutado);
        bytesMutados.Should().NotEqual(congelado.Bytes);
        VersaoConfiguracao versao = ExemploDeEnvelopeEntrada.VersaoDeAbertura(processo, bytesMutados);
        EnvelopeReidratado reidratado = Reidratado(versao);
        reidratado.Configuracao.Avaliacao.Classificacao!.BaseadoEmEnem.Should().BeTrue();
        byte[] recodificados = RecodificarClassificacaoLida(processo, versao, reidratado);
        recodificados.Should().NotEqual(congelado.Bytes);
        recodificados.Should().Equal(bytesMutados);
    }

    private static byte[] RecodificarClassificacaoLida(
        ProcessoSeletivo processo, VersaoConfiguracao versao, EnvelopeReidratado envelope)
    {
        processo.DefinirClassificacao(envelope.Configuracao.Avaliacao.Classificacao!).IsSuccess.Should().BeTrue();
        SnapshotCanonico recodificado = new SnapshotPublicacaoCanonicalizer().Canonicalizar(
            new EntradaCanonicalizacao(
                processo, envelope.Dados, envelope.HashDocumento, FusoInstitucional.ZoneId, envelope.Retificacao, envelope.Conformidade,
                envelope.DadosDeCatalogo));
        return recodificado.Bytes;
    }

    [Fact(DisplayName = "O decoder lê baseadoEmEnem falso do envelope sem ENEM, sem assumir verdadeiro")]
    public void Decoder_LeBaseadoEmEnemFalso()
    {
        ProcessoSeletivo processo = ProcessoSemEliminacaoEnem(baseadoEmEnem: false);
        SnapshotCanonico congelado = ExemploDeEnvelope.Canonicalizador.Canonicalizar(ExemploDeEnvelopeEntrada.Entrada(processo));
        ExemploDeEnvelopeEntrada.Publicar(processo);

        Result<EnvelopeReidratado> reidratado = ExemploDeEnvelope.Registro.Reidratar(ExemploDeEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes));

        reidratado.IsSuccess.Should().BeTrue(reidratado.Error?.Message);
        reidratado.Value!.Configuracao.Avaliacao.Classificacao!.BaseadoEmEnem.Should().BeFalse();
    }


    [Fact]
    public void Reidratar_PesosEnem_ConservaResolucaoNormalizadaEQuatroEntradas()
    {
        string resolucao = "Resolução 805/2024";
        ProcessoSeletivo processo = ProcessoMinimo(Classificacao(resolucao.Normalize(NormalizationForm.FormD)), etapaDeNotaDoEnem: false);
        processo.Classificacao!.ResolucaoPesoAreaEnem.Should().Be(resolucao.Normalize(NormalizationForm.FormC));
        processo.DefinirOpcoesDeclaradas("EDICAO_ENEM", [OpcaoDeclaradaFato.Criar("EDICAO_ENEM", "2026", "2026", 0).Value!]).IsSuccess.Should().BeTrue();
        EnvelopeReidratado lido = Conferir(processo, ExemploDeEnvelopeEntrada.Entrada(processo));
        lido.Configuracao.Avaliacao.Classificacao!.ResolucaoPesoAreaEnem.Should().Be(resolucao.Normalize(NormalizationForm.FormC));
        lido.Configuracao.Avaliacao.Classificacao!.QuadroPesoAreaEnem.Should().HaveCount(4);
    }

    [Fact]
    public void Reidratar_NotaDoEnem_LeOAtributoEmitidoPeloEncoder()
    {
        ProcessoSeletivo processo = ProcessoMinimo(Classificacao(QuadroPesoAreaEnemDeTeste.Resolucao), etapaDeNotaDoEnem: true);
        processo.DefinirOpcoesDeclaradas("EDICAO_ENEM", [OpcaoDeclaradaFato.Criar("EDICAO_ENEM", "2026", "2026", 0).Value!]).IsSuccess.Should().BeTrue();
        EntradaCanonicalizacao entrada = ExemploDeEnvelopeEntrada.Entrada(processo);
        SnapshotCanonico congelado = ExemploDeEnvelope.Canonicalizador.Canonicalizar(entrada);
        Envelope(congelado)["etapas"]![0]!["tipoEtapa"]!["notaDeOrigemNoEnem"]!.GetValue<bool>().Should().BeTrue();
        EnvelopeReidratado lido = FidelidadeDoEnvelopeDeTeste.Conferir(processo, ExemploDeEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes), congelado);
        lido.Configuracao.Avaliacao.Etapas.Should().ContainSingle().Which.DeclaraNotaDoEnem.Should().BeTrue();
    }

    [Fact]
    public void Reidratar_OpcoesDeclaradas_ConservaCodigoRotuloEOrdemSemColetaViva()
    {
        ProcessoSeletivo processo = ProcessoSemEliminacaoEnem(baseadoEmEnem: false);
        DefinirOpcoesDeTeste(processo);
        EntradaCanonicalizacao entrada = EntradaComOpcoesDeTeste(processo);
        SnapshotCanonico congelado = ExemploDeEnvelope.Canonicalizador.Canonicalizar(entrada);
        processo.DefinirItens([]).IsSuccess.Should().BeTrue();
        EnvelopeReidratado lido = Conferir(processo, entrada, congelado);
        lido.Configuracao.Coleta.OpcoesDeclaradas.Where(o => o.FatoCodigo == "FATO_OPCAO_DE_TESTE")
            .Select(o => (o.Codigo, o.Rotulo, o.Ordem)).Should().Equal(("2025", "Opção 2025", 0), ("2024", "Opção 2024", 1));
    }

    private static EnvelopeReidratado Conferir(ProcessoSeletivo processo, EntradaCanonicalizacao entrada, SnapshotCanonico? congelado = null)
    {
        SnapshotCanonico snapshot = congelado ?? ExemploDeEnvelope.Canonicalizador.Canonicalizar(entrada);
        return FidelidadeDoEnvelopeDeTeste.Conferir(processo, ExemploDeEnvelopeEntrada.VersaoDeAbertura(processo, snapshot.Bytes), snapshot);
    }

    private static ConfiguracaoClassificacao Classificacao(string resolucao) => ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
    {
        RegraCalculo = ExemploDeEnvelope.Regra(RegraDeCalculoDaNota.MediaPonderada.Codigo(), '2'),
        RegraArredondamento = ExemploDeEnvelope.Regra(RegraDeArredondamentoDaNota.Truncar.Codigo(), '3'),
        CasasArredondamento = 2,
        RegraOrdemAlocacao = ExemploDeEnvelope.Regra(RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), '4'),
        BaseadoEmEnem = true,
        ResolucaoPesoAreaEnem = resolucao,
        QuadroPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Completo(),
        EscalaNotaFinal = 100m,
    }).Value!;

    private static void DefinirOpcoesDeTeste(ProcessoSeletivo processo)
    {
        processo.DefinirItens([FatoColetadoDeTeste.Coletando("FATO_OPCAO_DE_TESTE", 0)
            .ComRotulo("Opção de teste")
            .DoTipo(TipoRenderizacao.EscolhaUnica)
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .ComOrigem(OrigemValoresColeta.OpcoesDoProcesso)
            .Construir()]).IsSuccess.Should().BeTrue();
        processo.DefinirOpcoesDeclaradas("FATO_OPCAO_DE_TESTE", [OpcaoDeclaradaFato.Criar("FATO_OPCAO_DE_TESTE", "2025", "Opção 2025", 0).Value!,
            OpcaoDeclaradaFato.Criar("FATO_OPCAO_DE_TESTE", "2024", "Opção 2024", 1).Value!]).IsSuccess.Should().BeTrue();
    }


    /// <summary>Exemplo mínimo sem ELIMINACAO_CORTE_REDACAO/ELIMINACAO_ZERO_EM_AREA — os dois valores de <c>baseadoEmEnem</c> continuam válidos.</summary>
    private static ProcessoSeletivo ProcessoSemEliminacaoEnem(bool baseadoEmEnem) =>
        ProcessoMinimo(ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = ExemploDeEnvelope.Regra(RegraDeCalculoDaNota.ClassificacaoImportada.Codigo(), '2'),
            RegraOrdemAlocacao = ExemploDeEnvelope.Regra(RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), '4'),
            BaseadoEmEnem = baseadoEmEnem,
        }).Value!, etapaDeNotaDoEnem: false);

    private static FaseCronograma FaseDeResultadoFinal() => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
            OrigemId: Guid.CreateVersion7(), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: true,
            ColetaSolicitacaoIsencao: false)),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
        HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologa(),
        bancasRequeridas: [BancasDeTeste.DaHomologacaoDasInscricoesDaFase()]).Value!;

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
            EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
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
                RegraDeDistribuicao = ExemploDeEnvelope.Regra(RegraDeDistribuicaoDeVagas.Institucional.Codigo(), '1'),
                Modalidades = [
                ModalidadeAmpla(),
            ]
            },
            new FatosDaOfertaNoCadastro { GrupoAreaEnem = ("TECNOLOGICA", "Tecnológica"), OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;
        return distribuicao;
    }


    private static ModalidadeSelecionada ModalidadeAmpla() => ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas) with { RegraRemanejamento = null, BaseLegal = "Res. Unifesspa 532/2021" }, 40).Value!;


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

    private static EntradaCanonicalizacao EntradaComOpcoesDeTeste(ProcessoSeletivo processo) =>
        ExemploDeEnvelopeEntrada.Entrada(processo) with
        {
            DadosDeCatalogo = ExemploDeEnvelopeEntrada.Entrada(processo).DadosDeCatalogo with
            {
                ValoresSelecionaveisCongelados = CatalogoDoConjuntoBasico.ComValoresCongelados(new Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>(StringComparer.Ordinal)
                { ["FATO_OPCAO_DE_TESTE"] = [new("2025", "Opção 2025", 0), new("2024", "Opção 2024", 1)] }),
            },
        };

}
