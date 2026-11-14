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
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

public sealed partial class EnvelopeCodecRoundTripTests
{
    /// <summary>
    /// Não reaproveita <see cref="MutarEReidratar"/>: o corpus rico (<see cref="CorpusEnvelope.ProcessoRico"/>)
    /// já tem <c>ELIM-CORTE-REDACAO</c>/<c>ELIM-ZERO-EM-AREA</c> (<c>baseadoEmEnem: true</c>) —
    /// mutar o campo para <c>false</c> sobre esse corpus faria o decoder CORRETAMENTE recusar a
    /// reidratação (a invariante ENEM×eliminação é validada dentro de
    /// <see cref="ConfiguracaoClassificacao.Criar"/>), o que quebraria a prova de round-trip em
    /// vez de exercitá-la. Aqui o corpus não tem eliminação ENEM nenhuma e a classificação é
    /// importada — com cálculo local, <c>baseadoEmEnem</c> exigiria a resolução de Pesos por
    /// Área num sentido e a recusaria no outro —, então os dois valores continuam válidos nos
    /// dois sentidos. A mutação vai de falso para verdadeiro: a contrária produziria um envelope
    /// que pede a edição do ENEM sem classificação ENEM, que o codec recusa. O outro sentido é a
    /// leitura do corpus sem ENEM sem mutação, no teste seguinte.
    /// </summary>
    [Fact(DisplayName = "O decoder não perde baseadoEmEnem — corpus sem eliminação ENEM, mutado para verdadeiro")]
    public void Decoder_NaoPerdeCampo_BaseadoEmEnem()
    {
        ProcessoSeletivo processo = ProcessoSemEliminacaoEnem(baseadoEmEnem: false);
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo));
        CorpusEnvelope.Publicar(processo);

        JsonObject mutado = Envelope(congelado);
        Mutar(mutado, "classificacao.baseadoEmEnem", "True");

        byte[] bytesMutados = PerfilCanonicoV1.Instancia.Serializar(mutado);
        bytesMutados.Should().NotEqual(congelado.Bytes, "pré-condição: mutar baseadoEmEnem tem de mudar os bytes");

        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, bytesMutados);
        Result<EnvelopeReidratado> reidratado = CorpusEnvelope.Registro.Reidratar(versao);
        reidratado.IsSuccess.Should().BeTrue(reidratado.Error?.Message);

        Result reposicao = processo.RestaurarConfiguracaoCongelada(versao, reidratado.Value!.Grafo);
        reposicao.IsSuccess.Should().BeTrue(reposicao.Error?.Message);

        byte[] recodificados = CorpusEnvelope.Registro.Recodificar(
            versao.SchemaVersion,
            new EntradaCanonicalizacao(
                processo,
                reidratado.Value.Dados,
                reidratado.Value.HashDocumento, FusoInstitucional.ZoneId,
                reidratado.Value.Retificacao,
                reidratado.Value.Conformidade,
                reidratado.Value.MetadadosFatosCongelados,
                reidratado.Value.ValoresSelecionaveisCongelados,
                reidratado.Value.CalendarioDiasUteis, reidratado.Value.AgregadosDosGrupos, reidratado.Value.ApresentacaoDosPressupostos,
                TiposDeAtoCongelados: reidratado.Value.TiposDeAtoCongelados, FatosDerivadosCongelados: reidratado.Value.FatosDerivadosCongelados)).Value!.Bytes;

        recodificados.Should().NotEqual(congelado.Bytes,
            "o decoder tem de LER 'classificacao.baseadoEmEnem' — se o ignorasse, a recodificação traria o valor " +
            "original de volta");
        recodificados.Should().Equal(bytesMutados,
            "baseadoEmEnem não é chave de ordenação — o resto do envelope reidratado reproduz o mutado inteiro");
    }

    [Fact(DisplayName = "O decoder lê baseadoEmEnem falso do envelope sem ENEM, sem assumir verdadeiro")]
    public void Decoder_LeBaseadoEmEnemFalso()
    {
        ProcessoSeletivo processo = ProcessoSemEliminacaoEnem(baseadoEmEnem: false);
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo));
        CorpusEnvelope.Publicar(processo);

        Result<EnvelopeReidratado> reidratado = CorpusEnvelope.Registro.Reidratar(CorpusEnvelope.VersaoDeAbertura(processo, congelado.Bytes));

        reidratado.IsSuccess.Should().BeTrue(reidratado.Error?.Message);
        reidratado.Value!.Grafo.Classificacao.BaseadoEmEnem.Should().BeFalse();
    }

    [Fact(DisplayName = "A resolução de Pesos por Área congelada em NFC volta igual depois de publicar e restaurar")]
    public void ResolucaoPesoAreaEnem_CongelarPublicarRestaurar_ContinuaAMesma()
    {
        // O cadastro grava a resolução em NFC; o operador pode enviá-la decomposta.
        string doCadastro = "Resolução 805/2024".Normalize(System.Text.NormalizationForm.FormC);
        string enviada = doCadastro.Normalize(System.Text.NormalizationForm.FormD);
        ProcessoSeletivo processo = ProcessoMinimo(ConfiguracaoClassificacao.Criar(
            regraCalculo: CorpusEnvelope.Regra(RegraCalculoCodigo.FormulaMediaPonderada, '2'),
            regraArredondamento: CorpusEnvelope.Regra(RegraArredondamentoCodigo.PrecisaoTruncar, '3'),
            casasArredondamento: 2,
            regraOrdemAlocacao: CorpusEnvelope.Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, '4'),
            regrasEliminacao: [],
            baseadoEmEnem: true,
            resolucaoPesoAreaEnem: enviada,
            quadroPesoAreaEnem: QuadroPesoAreaEnemDeTeste.Completo(), escalaNotaFinal: 100m).Value!, etapaDeNotaDoEnem: false);
        processo.Classificacao!.ResolucaoPesoAreaEnem.Should().Be(doCadastro, "congelada na forma do cadastro");
        processo.DefinirOpcoesDeclaradas(
            "EDICAO_ENEM", [OpcaoDeclaradaFato.Criar("EDICAO_ENEM", "2026", "2026", 0).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo));
        CorpusEnvelope.Publicar(processo);
        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, congelado.Bytes);
        Result<EnvelopeReidratado> reidratado = CorpusEnvelope.Registro.Reidratar(versao);
        reidratado.IsSuccess.Should().BeTrue(reidratado.Error?.Message);
        processo.RestaurarConfiguracaoCongelada(versao, reidratado.Value!.Grafo).IsSuccess.Should().BeTrue();

        processo.Classificacao!.ResolucaoPesoAreaEnem.Should().Be(doCadastro,
            "depois da restauração a resolução continua igual à do cadastro: o vínculo é pelo valor");
        processo.Classificacao.QuadroPesoAreaEnem.Should().HaveCount(4);
    }

    /// <summary>
    /// Os goldens e o corpus rico só têm etapas cuja nota não vem do ENEM. Aqui o atributo ligado
    /// sai do encoder, e não é injetado no JSON, e volta na etapa reidratada.
    /// </summary>
    [Fact(DisplayName = "A etapa de nota do ENEM congelada no envelope volta declarando a nota do ENEM")]
    public void EtapaDeNotaDoEnem_CongelarPublicarRestaurar_ContinuaDeclarandoAnotaDoEnem()
    {
        ProcessoSeletivo processo = ProcessoMinimo(ConfiguracaoClassificacao.Criar(
            regraCalculo: CorpusEnvelope.Regra(RegraCalculoCodigo.FormulaMediaPonderada, '2'),
            regraArredondamento: CorpusEnvelope.Regra(RegraArredondamentoCodigo.PrecisaoTruncar, '3'),
            casasArredondamento: 2,
            regraOrdemAlocacao: CorpusEnvelope.Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, '4'),
            regrasEliminacao: [],
            baseadoEmEnem: true,
            resolucaoPesoAreaEnem: QuadroPesoAreaEnemDeTeste.Resolucao,
            quadroPesoAreaEnem: QuadroPesoAreaEnemDeTeste.Completo(), escalaNotaFinal: 100m).Value!, etapaDeNotaDoEnem: true);
        processo.DefinirOpcoesDeclaradas(
            "EDICAO_ENEM", [OpcaoDeclaradaFato.Criar("EDICAO_ENEM", "2026", "2026", 0).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo));
        Envelope(congelado)["etapas"]![0]!["tipoEtapa"]!["notaDeOrigemNoEnem"]!.GetValue<bool>().Should().BeTrue(
            "o encoder congela o atributo ligado");
        CorpusEnvelope.Publicar(processo);
        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, congelado.Bytes);
        Result<EnvelopeReidratado> reidratado = CorpusEnvelope.Registro.Reidratar(versao);
        reidratado.IsSuccess.Should().BeTrue(reidratado.Error?.Message);
        processo.RestaurarConfiguracaoCongelada(versao, reidratado.Value!.Grafo).IsSuccess.Should().BeTrue();

        processo.Etapas.Should().ContainSingle().Which.DeclaraNotaDoEnem.Should().BeTrue(
            "a etapa reidratada declara a nota do ENEM pelo atributo que o envelope congelou");
    }

    [Fact(DisplayName = "As opções que o processo declarou voltam na restauração, reconstruídas dos valores selecionáveis congelados")]
    public void OpcoesDeclaradas_CongelarPublicarRestaurar_VoltamComRotuloEOrdem()
    {
        ProcessoSeletivo processo = ProcessoSemEliminacaoEnem(baseadoEmEnem: false);
        processo.DefinirItens(
            [FatoColetado.Criar("FATO_OPCAO_DE_TESTE", 0, "Opção de Teste", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null,
                origemValores: OrigemValoresColeta.OpcoesDoProcesso, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirOpcoesDeclaradas(
            "FATO_OPCAO_DE_TESTE",
            [OpcaoDeclaradaFato.Criar("FATO_OPCAO_DE_TESTE", "2025", "Opção 2025", 0).Value!,
             OpcaoDeclaradaFato.Criar("FATO_OPCAO_DE_TESTE", "2024", "Opção 2024", 1).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        EntradaCanonicalizacao entrada = CorpusEnvelope.Entrada(processo) with
        {
            ValoresSelecionaveisCongelados = CatalogoDoConjuntoBasico.ComValoresCongelados(new Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>(StringComparer.Ordinal)
            {
                ["FATO_OPCAO_DE_TESTE"] = [.. processo.OpcoesDoProcesso("FATO_OPCAO_DE_TESTE")
                    .Select(static o => new ValorDominioDeclaradoCongelado(o.Codigo, o.Rotulo, o.Ordem))],
            }),
        };
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(entrada);
        CorpusEnvelope.Publicar(processo, entrada);
        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, congelado.Bytes);
        Result<EnvelopeReidratado> reidratado = CorpusEnvelope.Registro.Reidratar(versao);
        reidratado.IsSuccess.Should().BeTrue(reidratado.Error?.Message);

        processo.LimparColetaEDerivacaoParaRestauracao();
        processo.RestaurarConfiguracaoCongelada(versao, reidratado.Value!.Grafo).IsSuccess.Should().BeTrue();

        processo.OpcoesDoProcesso("FATO_OPCAO_DE_TESTE").Should().Equal(
            new OpcaoDoProcesso("2025", "Opção 2025", 0),
            new OpcaoDoProcesso("2024", "Opção 2024", 1));
    }

    [Fact(DisplayName = "O descarte repõe as opções congeladas e preserva as de fato que a versão não coleta")]
    public void OpcoesDeclaradas_EdicaoDaSessao_DescarteRepoeCongeladasEPreservaAsVivas()
    {
        ProcessoSeletivo processo = ProcessoSemEliminacaoEnem(baseadoEmEnem: false);
        processo.DefinirItens(
            [FatoColetado.Criar("FATO_OPCAO_DE_TESTE", 0, "Opção de Teste", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null,
                origemValores: OrigemValoresColeta.OpcoesDoProcesso, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirOpcoesDeclaradas(
            "FATO_OPCAO_DE_TESTE",
            [OpcaoDeclaradaFato.Criar("FATO_OPCAO_DE_TESTE", "2025", "Opção 2025", 0).Value!,
             OpcaoDeclaradaFato.Criar("FATO_OPCAO_DE_TESTE", "2024", "Opção 2024", 1).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirOpcoesDeclaradas(
            "LOCAL_PROVA", [OpcaoDeclaradaFato.Criar("LOCAL_PROVA", "MARABA", "Marabá", 0).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        EntradaCanonicalizacao entrada = CorpusEnvelope.Entrada(processo) with
        {
            ValoresSelecionaveisCongelados = CatalogoDoConjuntoBasico.ComValoresCongelados(new Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>(StringComparer.Ordinal)
            {
                ["FATO_OPCAO_DE_TESTE"] = [.. processo.OpcoesDoProcesso("FATO_OPCAO_DE_TESTE")
                    .Select(static o => new ValorDominioDeclaradoCongelado(o.Codigo, o.Rotulo, o.Ordem))],
            }),
        };
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(entrada);
        CorpusEnvelope.Publicar(processo, entrada);
        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, congelado.Bytes);
        Result<EnvelopeReidratado> reidratado = CorpusEnvelope.Registro.Reidratar(versao);
        reidratado.IsSuccess.Should().BeTrue(reidratado.Error?.Message);

        // A sessão de retificação edita o rótulo e a ordem, acrescenta uma opção e passa a
        // coletar um fato cujas opções já estavam declaradas.
        processo.AbrirRetificacao("Ajusta as opções", versao, identificadorDaVersaoBase: null, VersoesPublicadasDeTeste.SoAVigente(processo), "teste", DateTimeOffset.UtcNow)
            .IsSuccess.Should().BeTrue();
        processo.DefinirItens(
            [FatoColetado.Criar("FATO_OPCAO_DE_TESTE", 0, "Opção de Teste", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null,
                origemValores: OrigemValoresColeta.OpcoesDoProcesso, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!,
             FatoColetado.Criar("LOCAL_PROVA", 1, "Local de prova", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null,
                origemValores: OrigemValoresColeta.OpcoesDoProcesso, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!],
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        processo.DefinirOpcoesDeclaradas(
            "FATO_OPCAO_DE_TESTE",
            [OpcaoDeclaradaFato.Criar("FATO_OPCAO_DE_TESTE", "2024", "Opção 2024", 0).Value!,
             OpcaoDeclaradaFato.Criar("FATO_OPCAO_DE_TESTE", "2025", "Editado na sessão", 1).Value!,
             OpcaoDeclaradaFato.Criar("FATO_OPCAO_DE_TESTE", "2023", "Opção 2023", 2).Value!],
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        processo.LimparColetaEDerivacaoParaRestauracao();
        processo.RestaurarConfiguracaoCongelada(versao, reidratado.Value!.Grafo).IsSuccess.Should().BeTrue();

        processo.OpcoesDoProcesso("FATO_OPCAO_DE_TESTE").Should().Equal(
            new OpcaoDoProcesso("2025", "Opção 2025", 0),
            new OpcaoDoProcesso("2024", "Opção 2024", 1));
        processo.OpcoesDeclaradas.Should().Contain(static o => o.FatoCodigo == "LOCAL_PROVA" && o.Codigo == "MARABA",
            "a versão não congela as opções de fato que não coleta: elas são configuração viva");
    }

    /// <summary>Corpus mínimo sem ELIM-CORTE-REDACAO/ELIM-ZERO-EM-AREA — os dois valores de <c>baseadoEmEnem</c> continuam válidos.</summary>
    private static ProcessoSeletivo ProcessoSemEliminacaoEnem(bool baseadoEmEnem) =>
        ProcessoMinimo(ConfiguracaoClassificacao.Criar(
            regraCalculo: CorpusEnvelope.Regra(RegraCalculoCodigo.ClassificacaoImportada, '2'),
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: CorpusEnvelope.Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, '4'),
            regrasEliminacao: [],
            baseadoEmEnem: baseadoEmEnem,
            resolucaoPesoAreaEnem: null,
            quadroPesoAreaEnem: [], escalaNotaFinal: null).Value!, etapaDeNotaDoEnem: false);

    /// <summary>
    /// O menor processo publicável, com a classificação informada e uma etapa só, que é a de nota
    /// do ENEM quando <paramref name="etapaDeNotaDoEnem"/> é verdadeiro.
    /// </summary>
    private static ProcessoSeletivo ProcessoMinimo(ConfiguracaoClassificacao classificacao, bool etapaDeNotaDoEnem)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS BaseadoEmEnem", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

        processo.DefinirCronogramaFases([
            FaseCronograma.Criar(
                ordem: 1,
                faseCanonicaOrigemId: Guid.CreateVersion7(),
                codigo: "RESULTADO_FINAL",
                origemData: OrigemDataFase.NoEdital,
                agrupaEtapas: true,
                permiteComplementacao: false,
                coletaInscricao: true, coletaSolicitacaoIsencao: false,
                inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
                produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)],
                faseConcluinteCodigo: null,
                emiteParecerIndividual: false,
                bancasRequeridas: [],
                regraRecurso: null,
                unidadeResponsavel: UnidadesDeTeste.Ceps).Value!,
        ], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([
            EtapaProcesso.Criar(
                "Prova Objetiva", CaraterEtapa.Classificatoria,
                TipoEtapaSnapshot.Criar(new Guid("019fee1e-7000-7000-8000-000000000001"), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: etapaDeNotaDoEnem).Value!,
                peso: 1m, ordem: 1, notaMaxima: etapaDeNotaDoEnem ? null : 10m,
            formaDeAvaliacao: etapaDeNotaDoEnem ? null : FormaDeAvaliacao.Objetiva, faseCodigo: "RESULTADO_FINAL").Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: Guid.CreateVersion7(),
            voBase: 40,
            pr: 1m,
            regraDistribuicao: CorpusEnvelope.Regra(RegraDistribuicaoVagasCodigo.Institucional, '1'),
            regraAjuste: null,
            referenciaDemografica: null,
            modalidades: [
                ModalidadeSelecionada.Criar(
                    Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla,
                    ComposicaoVagasModalidade.ResidualDoVo, null, RegraRemanejamentoModalidade.Nenhuma,
                    null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "Res. Unifesspa 532/2021", quantidadeDeclarada: 40).Value!,
            ],
            grupoAreaEnem: ("TECNOLOGICA", "Tecnológica"), ofertaCurso: OfertaCursoDeTeste.Medicina).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirClassificacao(classificacao, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
        if (classificacao.BaseadoEmEnem)
        {
            processo.DefinirItens([FormularioDeTeste.CampoDaEdicaoDoEnem(0)]).IsSuccess.Should().BeTrue();
        }

        // Issue #1112: publicar sem declarar cobrança de taxa é recusado (CA-01).
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);

        return processo;
    }
}
