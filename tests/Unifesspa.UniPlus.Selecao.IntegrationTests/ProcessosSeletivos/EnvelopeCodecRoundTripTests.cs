namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ApoioDoEnvelopeCodec;

/// <summary>
/// <b>A prova de fidelidade da reidratação</b> (Story #859, ADR-0110): reidratar um envelope
/// congelado e recanonicalizá-lo reproduz os mesmos bytes, byte a byte, incluindo a preservação
/// de identidade (etapa.Id). Prova o codec CORRENTE — no regime pré-produção (<c>EnvelopeCodec</c>,
/// codec único, forma reescrita no lugar a cada bump), um bump de <c>schema_version</c> troca o
/// codec corrente sem preservar o anterior no registro; o versionamento forense por codec
/// (um por <c>schema_version</c>, nenhum jamais aposentado) só passa a valer a partir do primeiro
/// certame publicado em qualquer ambiente, inclusive homologação — não da primeira release de
/// produção.
/// </summary>
/// <remarks>
/// <para>
/// Esta é a suíte de risco da Feature. Todo o resto dela é máquina de estados; aqui um
/// agregado é reconstruído a partir de bytes com peso jurídico. <b>Um campo perdido não
/// aparece em lugar nenhum</b>: o descarte de uma sessão editorial repõe a configuração
/// sem ele, e o certame publicado passa a divergir do documento que o publicou — sem
/// erro, sem log, sem ninguém ver.
/// </para>
/// <para>
/// A projeção é pura (ADR-0109 D6), então nada aqui precisa de banco. O que precisa de
/// banco — o identity map, as FKs, o cascade — está em
/// <c>RestaurarConfiguracaoPersistenciaTests</c>.
/// </para>
/// </remarks>
public sealed class EnvelopeCodecRoundTripTests
{
    // ── Round-trip byte-a-byte, com o encoder DA VERSÃO ──

    [Fact(DisplayName = "Reidratar a versão 1 e recanonicalizá-la reproduz os bytes congelados, inteiros")]
    public void RoundTrip_VersaoDeAbertura()
    {
        ConferirRoundTripDaVersaoDeAbertura();
    }

    private static FaseCronograma FaseDeHabilitacaoDepoisDasDemais(ProcessoSeletivo processo) => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), FormularioProcesso.CodigoFaseHabilitacao, OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false,
            ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            processo.CronogramaFases.Max(static f => f.Ordem) + 1, Inicio: new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 5, 10, 18, 0, 0, TimeSpan.Zero),
            FaseConcluinteCodigo: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar(FormularioProcesso.CodigoFaseHabilitacao, PapelProdutoFase.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    [Fact(DisplayName = "Dois formulários, cada um com item de ordem 0 e um termo, reproduzem os bytes")]
    public void RoundTrip_DoisFormularios_OrdemRepetidaEntreEles()
    {
        // A ordem é única dentro de cada formulário: a inscrição e a habilitação têm, as duas,
        // um item de ordem 0, e o decoder não pode tomar isso por repetição. O item da habilitação
        // depende da cor/raça respondida na inscrição (UNI-REQ-0145).
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico();
        FaseCronograma habilitacao = FaseDeHabilitacaoDepoisDasDemais(processo);
        processo.DefinirCronogramaFases([.. processo.CronogramaFases, habilitacao], [], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirFormulario(FinalidadeFormulario.Habilitacao, habilitacao.Id, "Habilitação", FormularioDeTeste.Etapas(), PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirItens(
            [FatoColetado.Criar("CERTIFICADO_EMITIDO", 0, "Certificado emitido", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, [
                CondicaoPrecondicaoFato.Criar(0, "COR_RACA", Operador.Igual, JsonSerializer.SerializeToElement("PRETA")).Value!,
            ], classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!],
            finalidade: FinalidadeFormulario.Habilitacao).IsSuccess.Should().BeTrue();
        processo.DefinirTermos([CorpusEnvelopeEntrada.Termo("VERACIDADE", 0)], finalidade: FinalidadeFormulario.Habilitacao)
            .IsSuccess.Should().BeTrue();

        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo));
        CorpusEnvelopeEntrada.Publicar(processo);

        AssertRoundTrip(processo, CorpusEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes), congelado);
        Envelope(congelado)["formularios"]!.AsArray().Select(static f => f!["finalidade"]!.GetValue<string>())
            .Should().Equal("INSCRICAO", "HABILITACAO");
    }

    [Fact(DisplayName = "Item com obrigatoriedade QUANDO, ajuda e confirmação reproduz os bytes")]
    public void RoundTrip_RegrasDoItem()
    {
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico();
        processo.DefinirItens(
        [
            FatoColetado.Criar("COR_RACA", 0, "Cor ou raça", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!,
            FatoColetado.Criar("RENDA", 1, "Faixa de renda familiar", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!,
            FatoColetado.Criar(
                "TEM_BOLSA", 2, "Tem bolsa", TipoRenderizacao.Booleano,
                Obrigatoriedade.Quando(PredicadoDnf.CriarDeCondicoesAgrupadas(
                    [(0, CondicaoDnf.Criar("COR_RACA", Operador.Igual, JsonSerializer.SerializeToElement("PRETA")).Value!)]).Value!),
                null, ajuda: "Renda por pessoa da família", pedirConfirmacao: true, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!,
            FormularioDeTeste.CampoDaEdicaoDoEnem(3),
            FatoColetado.Criar(OpcaoPelasCotasDaLei.FatoConcorrerEscolaPublica, 4, "Deseja concorrer às vagas de escola pública?", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!,
        ], grupos: []).IsSuccess.Should().BeTrue();

        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo));
        CorpusEnvelopeEntrada.Publicar(processo);

        AssertRoundTrip(processo, CorpusEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes), congelado);
        JsonObject bolsa = Envelope(congelado)["fatosColetados"]!.AsArray()
            .Single(static f => f!["fatoCodigo"]!.GetValue<string>() == "TEM_BOLSA")!.AsObject();
        bolsa["obrigatoriedade"]!["tipo"]!.GetValue<string>().Should().Be("QUANDO");
        bolsa["pedirConfirmacao"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact(DisplayName = "Termo que aparece só para quem optou pelas cotas da lei reproduz os bytes")]
    public void RoundTrip_FormularioCitaAOpcaoPelasCotas()
    {
        // A opção pelas cotas da lei sai da oferta e não é gravada entre as derivações: o decoder
        // reconhece a citação pelas derivações efetivas, como a publicação.
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico();
        PredicadoDnf optou = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar(OpcaoPelasCotasDaLei.Fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!;
        processo.DefinirTermos(
            [CorpusEnvelopeEntrada.Termo("CIENCIA_DA_RESERVA", 0, exibicao: optou, obrigatoriedade: Obrigatoriedade.Quando(optou))],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo));
        CorpusEnvelopeEntrada.Publicar(processo);

        VersaoConfiguracao versao = CorpusEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes);
        AssertRoundTrip(processo, versao, congelado);
        Envelope(congelado)["regrasDerivacao"]!.AsArray()
            .Select(static d => d!["codigoFato"]!.GetValue<string>())
            .Should().NotContain(OpcaoPelasCotasDaLei.Fato);

        // O formulário público leva a derivação da opção e o fechamento dela, para o interpretador do
        // front decidir a exibição do termo (ADR-0139).
        Result<EnvelopeReidratado> reidratado = CorpusEnvelope.Registro.Reidratar(versao);
        reidratado.IsSuccess.Should().BeTrue(reidratado.Error?.Message);
        Result<DefinicaoAvaliavel> avaliavel = DefinicaoAvaliavelDoProcesso.DoCongelado(reidratado.Value!);
        avaliavel.IsSuccess.Should().BeTrue(avaliavel.Error?.Message);
        RecorteDaFinalidade recorte = RecorteDaFinalidade.De(
            avaliavel.Value!.Definicao, DefinicaoDoProcesso.CodigoDaEtapa(FinalidadeFormulario.Inscricao, null));
        recorte.Regras.Derivacoes.Select(static d => d.FatoCodigo)
            .Should().Contain([OpcaoPelasCotasDaLei.Fato, OrigemEscolar.FatoEgresso]);
        recorte.Pressupostos.Should().NotContain(OpcaoPelasCotasDaLei.Fato);
    }

    [Fact(DisplayName = "Campos de texto com formato, de data e de endereço reproduzem os bytes")]
    public void RoundTrip_CampoDeTexto()
    {
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico();
        processo.DefinirItens(
        [
            FatoColetado.Criar("COR_RACA", 0, "Cor ou raça", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!,
            FatoColetado.Criar("RENDA", 1, "Faixa de renda familiar", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, [
                CondicaoPrecondicaoFato.Criar(0, "COR_RACA", Operador.Igual, JsonSerializer.SerializeToElement("PRETA")).Value!,
            ], classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!,
            FatoColetado.Criar("NOME_SOCIAL", 2, "Nome social", TipoRenderizacao.Texto, Obrigatoriedade.Nunca, null, formato: "NOME_PESSOA", classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!,
            FatoColetado.Criar("DATA_NASCIMENTO", 3, "Data de nascimento", TipoRenderizacao.Data, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!,
            FatoColetado.Criar("ENDERECO_RESIDENCIAL", 4, "Endereço residencial", TipoRenderizacao.Endereco, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!,
            FormularioDeTeste.CampoDaEdicaoDoEnem(5),
            FatoColetado.Criar(OpcaoPelasCotasDaLei.FatoConcorrerEscolaPublica, 6, "Deseja concorrer às vagas de escola pública?", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!,
        ], grupos: []).IsSuccess.Should().BeTrue();

        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo));
        CorpusEnvelopeEntrada.Publicar(processo);

        AssertRoundTrip(processo, CorpusEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes), congelado);
        Envelope(congelado)["fatosColetados"]!.AsArray()
            .Single(static f => f!["fatoCodigo"]!.GetValue<string>() == "NOME_SOCIAL")!["formato"]!.GetValue<string>()
            .Should().Be("NOME_PESSOA");
        Envelope(congelado)["fatosColetados"]!.AsArray()
            .Select(static f => f!["tipoRenderizacao"]!.GetValue<string>()).Should().Contain(["DATA", "ENDERECO"]);
    }

    [Fact(DisplayName = "A versão N>1 tem o bloco retificacao: o round-trip usa a RetificacaoDoEdital ORIGINAL, recuperada do próprio envelope")]
    public void RoundTrip_VersaoRetificada()
    {
        // O round-trip de uma versão retificada não é o dos blocos da abertura sozinhos. Ela
        // tem também o bloco `retificacao`, que não vem do agregado — é parâmetro externo da
        // canonicalização. Recanonicalizar sem ele produziria um envelope sem esse bloco
        // e a comparação falharia por uma razão que nada tem a ver com a reidratação.
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico();
        RetificacaoDoEdital retificacao = new(CorpusEnvelope.AtoAbertura, "Correção do quadro de vagas do curso de Direito");

        SnapshotCanonico abertura = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo));
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo, retificacao));
        CorpusEnvelopeEntrada.Publicar(processo);

        VersaoConfiguracao v1 = CorpusEnvelopeEntrada.VersaoDeAbertura(processo, abertura.Bytes);
        VersaoConfiguracao v2 = CorpusEnvelopeEntrada.VersaoDeRetificacao(v1, congelado.Bytes);

        EnvelopeReidratado envelope = AssertRoundTrip(processo, v2, congelado);

        envelope.Retificacao.Should().NotBeNull("a versão N>1 carrega o bloco retificacao");
        envelope.Retificacao!.EditalRetificadoId.Should().Be(CorpusEnvelope.AtoAbertura);
        envelope.Retificacao.Motivo.Should().Be("Correção do quadro de vagas do curso de Direito");
    }

    [Fact(DisplayName = "O round-trip parte de uma configuração viva DIFERENTE da congelada (é o que o descarte faz)")]
    public void RoundTrip_SobreConfiguracaoViva_Divergente()
    {
        // Restaurar sobre uma configuração viva IDÊNTICA à congelada esconderia metade do
        // risco: as etapas seriam reconciliadas nos mesmos Ids e as demais dimensões
        // trocadas por instâncias equivalentes. É a divergência que exercita o caminho
        // real do descarte — repor o que a sessão editorial havia substituído.
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico();
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo));
        CorpusEnvelopeEntrada.Publicar(processo);

        VersaoConfiguracao versao = CorpusEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes);

        // A "sessão editorial": a configuração viva vira outra coisa.
        processo.RestaurarConfiguracaoCongelada(versao, CorpusEnvelopeEntrada.GrafoPobre()).IsSuccess.Should().BeTrue();
        CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo)).Bytes
            .Should().NotEqual(congelado.Bytes, "pré-condição: a configuração viva TEM de estar divergente da congelada");

        AssertRoundTrip(processo, versao, congelado);
    }

    // ── Identificador legível: o endereço público volta com a versão, nos dois estados ──

    [Fact(DisplayName = "Reidratar devolve o identificador legível congelado, e recanonicalizar o reproduz")]
    public void RoundTrip_IdentificadorLegivelCongelado()
    {
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico();
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo));
        CorpusEnvelopeEntrada.Publicar(processo);

        VersaoConfiguracao versao = CorpusEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes);

        EnvelopeReidratado envelope = AssertRoundTrip(processo, versao, congelado);

        envelope.Grafo.IdentificadorLegivel.Should().Be(processo.IdentificadorLegivel,
            "o público lê a versão congelada, e o endereço dele tem de estar nela");
    }

    [Fact(DisplayName = "Versão congelada sem identificador legível reidrata a ausência, e o descarte a repõe na raiz")]
    public void RoundTrip_SemIdentificadorLegivel_ReporAusencia()
    {
        // O encoder emite null quando a raiz não tem identificador. A publicação recusa esse
        // estado, então os bytes são congelados antes de o identificador voltar à raiz, e a versão
        // é montada sobre eles.
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico();
        IdentificadorLegivel? declarado = processo.IdentificadorLegivel;
        IdentificadoresDeTeste.Retirar(processo);
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo));
        processo.DefinirIdentificadorLegivel(declarado!.Value, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        CorpusEnvelopeEntrada.Publicar(processo);

        VersaoConfiguracao versao = CorpusEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes);

        EnvelopeReidratado envelope = AssertRoundTrip(processo, versao, congelado);

        envelope.Grafo.IdentificadorLegivel.Should().BeNull(
            "a versão foi congelada sem identificador — a reidratação não pode inventar um");
        processo.IdentificadorLegivel.Should().BeNull(
            "repor a versão devolve o que ela de fato tinha, inclusive a ausência");
    }

    // ── Story #575 — cascataRemanejamento reidrata nos dois estados (presente/ausente) ──

    /// <summary>
    /// O corpus rico (<see cref="CorpusEnvelopeProcessoRico.ProcessoRico"/>) tem as 8 modalidades
    /// federais de <c>DistribuicaoLei12711</c> como <c>SegueCascata</c> e, desde a
    /// Story #575, carrega a cascata que as cobre (sem ela <c>Publicar</c> recusaria com
    /// <c>ProcessoSeletivo.CascataOrigemAusente</c>) — é por isso que este teste, como todos
    /// os outros desta classe que publicam o corpus rico, já exercita o estado
    /// <c>presente:true</c> do bloco. Esta asserção é a prova EXPLÍCITA disso, em vez de
    /// deixá-la implícita no round-trip byte-a-byte genérico.
    /// </summary>
    [Fact(DisplayName = "Reidratar um envelope com cascataRemanejamento presente reconstrói ConfiguracaoCascataRemanejamento com a matriz legal completa")]
    public void RoundTrip_ComCascataPresente_ReconstroiConfiguracaoCascataRemanejamento()
    {
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico();
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo));
        CorpusEnvelopeEntrada.Publicar(processo);

        VersaoConfiguracao versao = CorpusEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes);

        EnvelopeReidratado envelope = AssertRoundTrip(processo, versao, congelado);

        envelope.Grafo.CascataRemanejamento.Should().NotBeNull(
            "o corpus rico tem a cascata configurada (as 8 federais de DistribuicaoLei12711 são " +
            "SegueCascata) — a reidratação tem de reconstruí-la, não perdê-la");
        envelope.Grafo.CascataRemanejamento!.FallbackCodigo.Should().Be("AC");
        envelope.Grafo.CascataRemanejamento.Destinos.Should().HaveCount(56,
            "a matriz legal completa tem 8 origens × 7 destinos");
    }

    /// <summary>
    /// O outro estado do bloco: um processo SEM nenhuma modalidade <c>SegueCascata</c> (só
    /// ampla concorrência institucional) não precisa de cascata — <c>PendenciaDaCascata</c>
    /// não a exige — e o envelope congela <c>{"presente":false}</c>. O decoder tem de
    /// reconstruir <see langword="null"/>, não um objeto vazio nem lançar.
    /// </summary>
    [Fact(DisplayName = "Reidratar um envelope com cascataRemanejamento ausente ({\"presente\":false}) reconstrói CascataRemanejamento como null")]
    public void RoundTrip_SemCascata_ReconstroiCascataRemanejamentoComoNull()
    {
        ProcessoSeletivo processo = ProcessoSeletivoPublicacaoSeeder.NovoProcessoConforme("PS Sem Cascata");
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo));

        Result<VersaoConfiguracao> publicacao = processo.Publicar(
            new CenarioDeCongelamento { Dados = CorpusEnvelopeDadosDoEdital.DadosRicos(), Documento = new DocumentoCongelado(congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash, CorpusEnvelope.HashDocumento), AtorUsuarioContaId = CorpusEnvelope.Ator, Contagem = CorpusEnvelopeDadosDoEdital.ContextoRico() });
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);

        Result<EnvelopeReidratado> reidratado = CorpusEnvelope.Registro.Reidratar(publicacao.Value!);
        reidratado.IsSuccess.Should().BeTrue(reidratado.Error?.Message);

        reidratado.Value!.Grafo.CascataRemanejamento.Should().BeNull(
            "o processo publicado não tinha nenhuma modalidade SegueCascata — a reidratação não pode inventar uma cascata");
    }
}
