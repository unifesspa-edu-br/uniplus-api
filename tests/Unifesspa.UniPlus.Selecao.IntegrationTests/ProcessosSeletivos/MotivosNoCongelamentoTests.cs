namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.AdulteracaoDeEnvelopeDeTeste;

public sealed class MotivosNoCongelamentoTests
{
    [Fact]
    public void Canonicalizar_DoisMotivos_ReidrataEReproduzBytes()
    {
        ProcessoSeletivo processo = ExemploDeEnvelopeProcessoRico.ProcessoRico();
        MotivoDeDecisaoDeclarado[] motivos = Motivos();
        SnapshotCanonico snapshot = Congelar(processo, motivos);
        EnvelopeReidratado lido = FidelidadeDoEnvelopeDeTeste.Conferir(processo, ExemploDeEnvelopeEntrada.VersaoDeAbertura(processo, snapshot.Bytes), snapshot);
        JsonArray bloco = ApoioDoEnvelopeCodec.Envelope(snapshot)["motivosDeDecisao"]!.AsArray();

        lido.Configuracao.MotivosDeDecisao.Should().BeEquivalentTo(motivos);
        bloco.Select(m => m!["codigo"]!.GetValue<string>()).Should().Equal("A", "B");
    }

    [Fact]
    public void Canonicalizar_MotivoAlterado_MudaBytesEResumo()
    {
        ProcessoSeletivo processo = ExemploDeEnvelopeProcessoRico.ProcessoRico();
        MotivoDeDecisaoDeclarado[] motivos = Motivos();
        SnapshotCanonico original = Congelar(processo, motivos);
        motivos[0] = motivos[0] with { Descricao = "Outra descrição" };
        SnapshotCanonico alterado = Congelar(processo, motivos);

        alterado.Bytes.Should().NotEqual(original.Bytes);
        PerfilCanonicoV1.Instancia.HashHex(alterado.Bytes).Should().NotBe(PerfilCanonicoV1.Instancia.HashHex(original.Bytes));
    }

    [Fact]
    public void Canonicalizar_SemMotivos_CongelaListaVazia()
    {
        ProcessoSeletivo processo = ExemploDeEnvelopeProcessoRico.ProcessoRico();
        SnapshotCanonico snapshot = Congelar(processo, []);
        ApoioDoEnvelopeCodec.Envelope(snapshot)["motivosDeDecisao"]!.AsArray().Should().BeEmpty();
    }

    [Fact]
    public void Reidratar_SemBloco_Recusa() =>
        ReidratarComEnvelopeAdulterado(envelope => envelope.Remove("motivosDeDecisao")).IsFailure.Should().BeTrue();

    [Fact]
    public void Reidratar_Retificacao_PreservaMotivoAnteriorEInclusao()
    {
        ProcessoSeletivo processo = ExemploDeEnvelopeProcessoRico.ProcessoRico();
        MotivoDeDecisaoDeclarado[] motivos = Motivos();
        VersaoConfiguracao anterior = ExemploDeEnvelopeEntrada.VersaoDeAbertura(processo, Congelar(processo, [motivos[1]]).Bytes);
        EntradaCanonicalizacao entrada = ExemploDeEnvelopeEntrada.Entrada(processo, new RetificacaoDoEdital(anterior.AtoCriadorId, "Inclusão de motivo"))
            with
        { Configuracao = processo.Declaracao(motivos) };
        SnapshotCanonico snapshot = ExemploDeEnvelope.Canonicalizador.Canonicalizar(entrada);
        VersaoConfiguracao versao = ExemploDeEnvelopeEntrada.VersaoDeRetificacao(anterior, snapshot.Bytes);

        EnvelopeReidratado lido = FidelidadeDoEnvelopeDeTeste.Conferir(processo, versao, snapshot);
        lido.Configuracao.MotivosDeDecisao.Should().BeEquivalentTo(motivos);
    }

    private static SnapshotCanonico Congelar(ProcessoSeletivo processo, IReadOnlyList<MotivoDeDecisaoDeclarado> motivos) =>
        ExemploDeEnvelope.Canonicalizador.Canonicalizar(ExemploDeEnvelopeEntrada.Entrada(processo) with { Configuracao = processo.Declaracao(motivos) });

    private static MotivoDeDecisaoDeclarado[] Motivos() =>
    [
        Motivo("B", LugarDaDecisao.DaEtapa(Guid.Parse("22222222-2222-2222-2222-222222222222"))) with { VersaoDeEntrada = 2, JustificativaDaInclusao = "Nova hipótese" },
        Motivo("A", LugarDaDecisao.DaFase(Guid.Parse("11111111-1111-1111-1111-111111111111"))),
    ];

    private static MotivoDeDecisaoDeclarado Motivo(string codigo, LugarDaDecisao lugar) => new()
    {
        Tipo = TipoMotivoDeDecisao.AnaliseDocumental,
        Codigo = codigo,
        Lugar = lugar,
        Descricao = "Documentação conferida",
        Sentido = SentidoDoMotivo.Favoravel,
        VersaoDeEntrada = 1,
    };
}
