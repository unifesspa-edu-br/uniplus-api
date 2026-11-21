namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

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
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ApoioDoEnvelopeCodec;

public sealed class EnvelopeLidoFormulariosTests
{
    [Fact(DisplayName = "Dois formulários, cada um com item de ordem 0 e um termo, reproduzem os bytes")]
    public void RoundTrip_DoisFormularios_OrdemRepetidaEntreEles()
    {
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico();
        DefinirHabilitacao(processo);

        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo));

        FidelidadeDoEnvelopeDeTeste.Conferir(processo, CorpusEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes), congelado);
        Envelope(congelado)["formularios"]!.AsArray().Select(static f => f!["finalidade"]!.GetValue<string>())
            .Should().Equal("INSCRICAO", "HABILITACAO");
    }

    [Fact(DisplayName = "Item com obrigatoriedade QUANDO, ajuda e confirmação reproduz os bytes")]
    public void RoundTrip_RegrasDoItem()
    {
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico();
        DefinirItensComBolsa(processo);

        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo));

        FidelidadeDoEnvelopeDeTeste.Conferir(processo, CorpusEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes), congelado);
        JsonObject bolsa = Envelope(congelado)["fatosColetados"]!.AsArray()
            .Single(static f => f!["fatoCodigo"]!.GetValue<string>() == "TEM_BOLSA")!.AsObject();
        bolsa["obrigatoriedade"]!["tipo"]!.GetValue<string>().Should().Be("QUANDO");
        bolsa["pedirConfirmacao"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact(DisplayName = "Termo que aparece só para quem optou pelas cotas da lei reproduz os bytes")]
    public void RoundTrip_FormularioCitaAOpcaoPelasCotas()
    {
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico();
        PredicadoDnf optou = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar(OpcaoPelasCotasDaLei.Fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!;
        processo.DefinirTermos(
            [CorpusEnvelopeEntrada.Termo("CIENCIA_DA_RESERVA", 0, exibicao: optou, obrigatoriedade: Obrigatoriedade.Quando(optou))]).IsSuccess.Should().BeTrue();

        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo));

        VersaoConfiguracao versao = CorpusEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes);
        FidelidadeDoEnvelopeDeTeste.Conferir(processo, versao, congelado);
        Envelope(congelado)["regrasDerivacao"]!.AsArray()
            .Select(static d => d!["codigoFato"]!.GetValue<string>())
            .Should().NotContain(OpcaoPelasCotasDaLei.Fato);
        ConferirDerivacoesDoRecortePublico(versao);
    }

    [Fact(DisplayName = "Campos de texto com formato, de data e de endereço reproduzem os bytes")]
    public void RoundTrip_CampoDeTexto()
    {
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico();
        DefinirCamposDeTexto(processo);

        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo));

        FidelidadeDoEnvelopeDeTeste.Conferir(processo, CorpusEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes), congelado);
        Envelope(congelado)["fatosColetados"]!.AsArray()
            .Single(static f => f!["fatoCodigo"]!.GetValue<string>() == "NOME_SOCIAL")!["formato"]!.GetValue<string>()
            .Should().Be("NOME_PESSOA");
        Envelope(congelado)["fatosColetados"]!.AsArray()
            .Select(static f => f!["tipoRenderizacao"]!.GetValue<string>()).Should().Contain(["DATA", "ENDERECO"]);
    }

    [Fact(DisplayName = "A versão N>1 tem o bloco retificacao: o round-trip usa a RetificacaoDoEdital ORIGINAL, recuperada do próprio envelope")]
    public void RoundTrip_VersaoRetificada()
    {
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico();
        RetificacaoDoEdital retificacao = new(CorpusEnvelope.AtoAbertura, "Correção do quadro de vagas do curso de Direito");

        SnapshotCanonico abertura = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo));
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo, retificacao));

        VersaoConfiguracao v1 = CorpusEnvelopeEntrada.VersaoDeAbertura(processo, abertura.Bytes);
        VersaoConfiguracao v2 = CorpusEnvelopeEntrada.VersaoDeRetificacao(v1, congelado.Bytes);

        EnvelopeReidratado envelope = FidelidadeDoEnvelopeDeTeste.Conferir(processo, v2, congelado);

        envelope.Retificacao.Should().NotBeNull("a versão N>1 carrega o bloco retificacao");
        envelope.Retificacao!.EditalRetificadoId.Should().Be(CorpusEnvelope.AtoAbertura);
        envelope.Retificacao.Motivo.Should().Be("Correção do quadro de vagas do curso de Direito");
    }

    private static FaseCronograma FaseDeHabilitacao(ProcessoSeletivo processo) => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(Guid.CreateVersion7(), FormularioProcesso.CodigoFaseHabilitacao, OrigemDataFase.NoEdital,
            AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(processo.CronogramaFases.Max(f => f.Ordem) + 1,
            new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 5, 10, 18, 0, 0, TimeSpan.Zero), null, UnidadesDeTeste.Ceps),
        new PublicacoesDaFase([ProdutoDaFase.Criar(FormularioProcesso.CodigoFaseHabilitacao, PapelProdutoFase.Definitivo)], false, null), []).Value!;

    private static void DefinirHabilitacao(ProcessoSeletivo processo)
    {
        FaseCronograma habilitacao = FaseDeHabilitacao(processo);
        processo.DefinirCronogramaFases([.. processo.CronogramaFases, habilitacao], [])
            .IsSuccess.Should().BeTrue();
        processo.DefinirFormulario(FinalidadeFormulario.Habilitacao, habilitacao.Id, "Habilitação", FormularioDeTeste.Etapas())
            .IsSuccess.Should().BeTrue();
        processo.DefinirItens(
            [FatoColetadoDeTeste.Coletando("CERTIFICADO_EMITIDO", 0)
                .ComRotulo("Certificado emitido")
                .ComObrigatoriedade(Obrigatoriedade.Sempre)
                .ExibidoQuando(ExibicaoDeTeste.Quando("COR_RACA", Operador.Igual, "PRETA"))
                .Construir()],
            finalidade: FinalidadeFormulario.Habilitacao).IsSuccess.Should().BeTrue();
        processo.DefinirTermos([CorpusEnvelopeEntrada.Termo("VERACIDADE", 0)], finalidade: FinalidadeFormulario.Habilitacao)
            .IsSuccess.Should().BeTrue();
    }

    private static void DefinirItensComBolsa(ProcessoSeletivo processo)
    {
        processo.DefinirItens(
        [
            FatoColetadoDeTeste.Coletando("COR_RACA", 0)
                .ComRotulo("Cor ou raça")
                .DoTipo(TipoRenderizacao.SelecaoUnica)
                .ComObrigatoriedade(Obrigatoriedade.Sempre)
                .Construir(),
            FatoColetadoDeTeste.Coletando("RENDA", 1)
                .ComRotulo("Faixa de renda familiar")
                .DoTipo(TipoRenderizacao.SelecaoUnica)
                .ComObrigatoriedade(Obrigatoriedade.Sempre)
                .Construir(),
            FatoDaBolsa(),
            FormularioDeTeste.CampoDaEdicaoDoEnem(3),
            FatoColetadoDeTeste.Coletando(OpcaoPelasCotasDaLei.FatoConcorrerEscolaPublica, 4)
                .ComRotulo("Deseja concorrer às vagas de escola pública?")
                .ComObrigatoriedade(Obrigatoriedade.Sempre)
                .Construir(),
        ], grupos: []).IsSuccess.Should().BeTrue();
    }

    private static FatoColetado FatoDaBolsa() =>
        FatoColetadoDeTeste.Coletando("TEM_BOLSA", 2)
            .ComRotulo("Tem bolsa")
            .ComObrigatoriedade(Obrigatoriedade.Quando(PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar("COR_RACA", Operador.Igual, JsonSerializer.SerializeToElement("PRETA")).Value!)]).Value!))
            .ComAjuda("Renda por pessoa da família")
            .PedindoConfirmacao()
            .Construir();

    private static void DefinirCamposDeTexto(ProcessoSeletivo processo)
    {
        processo.DefinirItens(
        [
            FatoColetadoDeTeste.Coletando("COR_RACA", 0)
                .ComRotulo("Cor ou raça")
                .DoTipo(TipoRenderizacao.SelecaoUnica)
                .ComObrigatoriedade(Obrigatoriedade.Sempre)
                .Construir(),
            FatoColetadoDeTeste.Coletando("RENDA", 1)
                .ComRotulo("Faixa de renda familiar")
                .DoTipo(TipoRenderizacao.SelecaoUnica)
                .ComObrigatoriedade(Obrigatoriedade.Sempre)
                .ExibidoQuando(ExibicaoDeTeste.Quando("COR_RACA", Operador.Igual, "PRETA"))
                .Construir(),
            FatoColetadoDeTeste.Coletando("NOME_SOCIAL", 2).ComRotulo("Nome social").DeTexto("NOME_PESSOA").Construir(),
            FatoColetadoDeTeste.Coletando("DATA_NASCIMENTO", 3)
                .ComRotulo("Data de nascimento")
                .DoTipo(TipoRenderizacao.Data)
                .ComObrigatoriedade(Obrigatoriedade.Sempre)
                .Construir(),
            FatoColetadoDeTeste.Coletando("ENDERECO_RESIDENCIAL", 4)
                .ComRotulo("Endereço residencial")
                .DoTipo(TipoRenderizacao.Endereco)
                .ComObrigatoriedade(Obrigatoriedade.Sempre)
                .Construir(),
            FormularioDeTeste.CampoDaEdicaoDoEnem(5),
            FatoColetadoDeTeste.Coletando(OpcaoPelasCotasDaLei.FatoConcorrerEscolaPublica, 6)
                .ComRotulo("Deseja concorrer às vagas de escola pública?")
                .ComObrigatoriedade(Obrigatoriedade.Sempre)
                .Construir(),
        ], grupos: []).IsSuccess.Should().BeTrue();
    }

    private static void ConferirDerivacoesDoRecortePublico(VersaoConfiguracao versao)
    {
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
}
