namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ApoioDoEnvelopeCodec;

public sealed class EnvelopeCodecQuadroDeVagasTests
{
    private const int VagasOfertadasBaseDoQuadroFixado = 40;

    [Fact(DisplayName = "O envelope descreve cada ramo só com os campos dele: o quadro fixado não tem conferência da reserva legal")]
    public void Envelope_QuadroFixado_NaoTemConferenciaDaReservaLegal()
    {
        ProcessoSeletivo processo = ProcessoComQuadroFixadoReduzido(new ParametrosReduzirDe("AC"));

        JsonObject envelope = Envelope(CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo)));

        JsonObject distribuicao = envelope["distribuicao"]!.AsArray()[0]!.AsObject();
        distribuicao["percentualDaReservaLegal"].Should().BeNull();
        distribuicao["ajusteDoQuadro"]!["$tipo"]!.GetValue<string>().Should().Be("reduzirDe");
        JsonObject vagas = envelope["vagas"]!.AsArray()[0]!.AsObject();
        vagas["conferenciaDaReservaLegal"].Should().BeNull();
        vagas["ajusteDoQuadroFixado"]!["totalDeVagasRetiradas"]!.GetValue<int>().Should().Be(4);
        vagas["totalDeVagasDoEdital"]!.GetValue<int>().Should().Be(VagasOfertadasBaseDoQuadroFixado);
    }

    [Theory(DisplayName = "Envelope com o quadro adulterado, diferente do que a declaração monta, é recusado na leitura")]
    [InlineData("vagas.0.totalDeVagasDoEdital", "41")]
    [InlineData("vagas.0.ajusteDoQuadroFixado.totalDeVagasRetiradas", "9")]
    [InlineData("vagas.0.quadro.0.quantidade", "7")]
    [InlineData("distribuicao.0.vagasOfertadasBase", "39")]
    public void Reidratar_QuadroDivergenteDaMontagem_Recusa(string caminho, string valorNovo)
    {
        ArgumentNullException.ThrowIfNull(caminho);

        ProcessoSeletivo processo = ProcessoComQuadroFixadoReduzido(new ParametrosReduzirDe("AC"));
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo));
        CorpusEnvelopeEntrada.Publicar(processo);
        JsonObject adulterado = Envelope(congelado);
        Mutar(adulterado, caminho, valorNovo);

        VersaoConfiguracao versao = CorpusEnvelopeEntrada.VersaoDeAbertura(processo, PerfilCanonicoV1.Instancia.Serializar(adulterado));
        Result<EnvelopeReidratado> reidratado = CorpusEnvelope.Registro.Reidratar(versao);

        reidratado.IsFailure.Should().BeTrue($"'{caminho}' não bate com o quadro que a declaração monta");
        reidratado.Error!.Message.Should().Contain("o quadro congelado não é o que a declaração da oferta monta");
    }

    [Fact(DisplayName = "Envelope com quantidade negativa declarada em uma modalidade é recusado na restauração")]
    public void Reidratar_QuantidadeDeclaradaNegativa_Recusa()
    {
        ProcessoSeletivo processo = ProcessoComQuadroFixadoReduzido(new ParametrosReduzirDe("AC"));
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo));
        CorpusEnvelopeEntrada.Publicar(processo);
        JsonObject adulterado = Envelope(congelado);
        Mutar(adulterado, "modalidades.1.quantidadeDeclarada", "-3");

        VersaoConfiguracao versao = CorpusEnvelopeEntrada.VersaoDeAbertura(processo, PerfilCanonicoV1.Instancia.Serializar(adulterado));
        Result<EnvelopeReidratado> reidratado = CorpusEnvelope.Registro.Reidratar(versao);

        reidratado.IsFailure.Should().BeTrue();
        reidratado.Error!.Code.Should().Be(DistribuicaoDeVagasErrorCodes.QuantidadeVagaNegativa);
    }

    private static ProcessoSeletivo ProcessoComQuadroFixadoReduzido(ParametrosRegraAjusteDistribuicao ajuste)
    {
        (ProcessoSeletivo processo, FaseCronograma _) = ProcessoDeInscricao("PS Quadro Fixado Reduzido");
        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada
            {
                VagasOfertadasBase = VagasOfertadasBaseDoQuadroFixado,
                RegraDeDistribuicao = CorpusEnvelope.Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'),
                RegraDeAjuste = CorpusEnvelope.Regra(RegraAjusteDistribuicaoVagasCodigo.ReconciliacaoArt11ParagrafoUnico, 'b'),
                AjusteDoQuadro = ajuste,
                Modalidades = [ModalidadeDoQuadroFixado("AC", 25, null), ModalidadeDoQuadroFixado("IND", 10, "AC"), ModalidadeDoQuadroFixado("QUI", 9, "AC")],
            },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], FatosDeModalidadeDeTeste.DoCatalogo)
            .IsSuccess.Should().BeTrue();
        return processo;
    }

    private static ModalidadeSelecionada ModalidadeDoQuadroFixado(string codigo, int quantidade, string? origem) =>
        ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), codigo, null,
            origem is null ? NaturezaLegalModalidade.Ampla : NaturezaLegalModalidade.AcaoAfirmativa,
            origem is null ? ComposicaoVagasModalidade.RestanteDasVagasOfertadas : ComposicaoVagasModalidade.RetiradaDeOutraModalidade,
            origem,
            origem is null ? RegraRemanejamentoModalidade.Nenhuma : RegraRemanejamentoModalidade.Cruzado,
            null,
            origem is null ? null : (codigo == "IND" ? "QUI" : "IND"),
            origem,
            RegrasDeAplicabilidadeDeTeste.DaNatureza(origem is null ? NaturezaLegalModalidade.Ampla : NaturezaLegalModalidade.AcaoAfirmativa),
            null, "Res. Unifesspa 532/2021", quantidadeDeclarada: quantidade).Value!;
}
