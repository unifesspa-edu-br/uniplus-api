namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport.RegraDeHomologacaoDeTeste;

/// <summary>
/// A regra de homologação na fase do cronograma do envelope: a chave existe em toda fase, nula quando a fase não
/// homologa, a regra muda os bytes e o hash, e a gramática da fase continua fechada.
/// </summary>
public sealed class CronogramaComRegraDeHomologacaoTests
{
    private static readonly Guid IdDaFase = new("6666aaaa-0000-4000-8000-000000000001");

    [Fact(DisplayName = "A fase sem regra de homologação grava a chave com null")]
    public void FaseSemRegra_GravaAChaveNula()
    {
        JsonObject item = SacarFase(Fase(regra: null));

        item.ContainsKey("regraDeHomologacao").Should().BeTrue();
        item["regraDeHomologacao"].Should().BeNull();
    }

    [Fact(DisplayName = "A fase com regra de homologação grava a chave e a restaura fielmente")]
    public void FaseComRegra_GravaERestaura()
    {
        RegraDeHomologacaoDasInscricoes regra = RegraCompleta();
        JsonObject item = SacarFase(Fase(regra));

        FaseCronograma restaurada = LerFase(item);

        item.ContainsKey("regraDeHomologacao").Should().BeTrue();
        restaurada.RegraDeHomologacao.Should().NotBeNull();
        restaurada.RegraDeHomologacao!.MesmoConteudo(regra).Should().BeTrue();
    }

    [Fact(DisplayName = "A fase sem regra volta sem regra")]
    public void FaseSemRegra_VoltaSemRegra() =>
        LerFase(SacarFase(Fase(regra: null))).RegraDeHomologacao.Should().BeNull();

    [Fact(DisplayName = "Mudar a regra de homologação muda os bytes canônicos e o hash do envelope")]
    public void MudarARegra_MudaBytesEHash()
    {
        byte[] original = Bytes(Fase(RegraCompleta()));
        byte[] outraRegra = Bytes(Fase(Regra("RESULTADO_PRELIMINAR_HOMOLOGACAO", Alternativa(Condicao(2024, "MOTIVO_A", null)))));
        byte[] semRegra = Bytes(Fase(regra: null));

        outraRegra.Should().NotEqual(original);
        semRegra.Should().NotEqual(original);
        HashCanonicalComputer.ComputeSha256Hex(outraRegra).Should().NotBe(HashCanonicalComputer.ComputeSha256Hex(original));
        HashCanonicalComputer.ComputeSha256Hex(semRegra).Should().NotBe(HashCanonicalComputer.ComputeSha256Hex(original));
    }

    [Fact(DisplayName = "A mesma regra produz os mesmos bytes e o mesmo hash")]
    public void MesmaRegra_MesmosBytes() =>
        Bytes(Fase(RegraCompleta())).Should().Equal(Bytes(Fase(RegraCompleta())));

    [Fact(DisplayName = "A chave desconhecida na fase continua recusada")]
    public void ChaveDesconhecida_Recusada()
    {
        JsonObject item = SacarFase(Fase(RegraCompleta()));
        item["regraDeHomologacaoDaFase"] = new JsonObject();

        LeitorEnvelope leitor = Ler(item);

        leitor.Falhou.Should().BeTrue();
        leitor.Erro.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        leitor.Erro.Message.Should().Contain("regraDeHomologacaoDaFase");
    }

    [Fact(DisplayName = "A fase sem a chave da regra é recusada: a escrita grava null quando a fase não homologa")]
    public void ChaveAusente_Recusada()
    {
        JsonObject item = SacarFase(Fase(RegraCompleta()));
        item.Remove("regraDeHomologacao");

        LeitorEnvelope leitor = Ler(item);

        leitor.Falhou.Should().BeTrue();
        leitor.Erro.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        leitor.Erro.Message.Should().Contain("regraDeHomologacao");
    }

    [Fact(DisplayName = "A regra com forma inválida dentro da fase é recusada como envelope malformado")]
    public void RegraInvalida_Recusada()
    {
        JsonObject item = SacarFase(Fase(RegraCompleta()));
        item["regraDeHomologacao"]!["alternativas"]![0]!["condicoes"]![0]!["edicaoDoExame"] = 1997;

        LeitorEnvelope leitor = Ler(item);

        leitor.Falhou.Should().BeTrue();
        leitor.Erro.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        leitor.Erro.Message.Should().Contain("regraDeHomologacao");
    }

    [Fact(DisplayName = "A fase que não é um objeto é recusada como envelope malformado")]
    public void FaseQueNaoEObjeto_Recusada()
    {
        JsonObject payload = Payload(new JsonObject());
        payload["cronogramaFases"]!["fases"] = new JsonArray(1);
        LeitorEnvelope leitor = new();

        LeituraDoCronograma.LerCronogramaFases(leitor, payload);

        leitor.Falhou.Should().BeTrue();
        leitor.Erro.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    private static RegraDeHomologacaoDasInscricoes RegraCompleta() => Regra(
        "RESULTADO_PRELIMINAR_HOMOLOGACAO",
        Alternativa(Condicao(2025, "MOTIVO_A", null), Condicao(2024, "MOTIVO_B", EnquadramentoDaModalidade("AMPLA"))));

    private static FaseCronograma Fase(RegraDeHomologacaoDasInscricoes? regra) => FaseCronograma.Reidratar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        IdDaFase,
        OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
            OrigemId: new Guid("4444dddd-0000-4000-8000-000000000001"), Codigo: "HOMOLOGACAO_INSCRICOES", OrigemData: OrigemDataFase.NoEdital,
            AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false)
        { Nome = "Homologação" }),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: null, Fim: null, UnidadeResponsavel: new UnidadeResponsavelSnapshot(new Guid("5555eeee-0000-4000-8000-000000000001"), "CEPS", "CEPS", "Centro de Processos Seletivos")),
        new PublicacoesDaFase(
            Produtos: [ProdutoDaFase.Reidratar(new Guid("7777bbbb-0000-4000-8000-000000000001"), "RESULTADO_PRELIMINAR_HOMOLOGACAO", PapelProdutoFase.Preliminar)],
            EmiteParecerIndividual: false,
            RegraRecurso: null,
            RegraDeHomologacao: regra),
        bancasRequeridas: []);

    // O envelope é lido dos bytes canônicos, nunca da árvore em memória: é o que o codec faz.
    private static JsonObject SacarFase(FaseCronograma fase) => JsonNode.Parse(Bytes(fase))!.AsObject();

    private static byte[] Bytes(FaseCronograma fase) => PerfilCanonicoV1.Instancia.Serializar(SerializacaoDoCronograma.SerializarFase(fase));

    private static FaseCronograma LerFase(JsonObject item)
    {
        LeitorEnvelope leitor = new();
        IReadOnlyList<FaseCronograma> fases = LeituraDoCronograma.LerCronogramaFases(leitor, Payload(item));

        leitor.Falhou.Should().BeFalse(leitor.Falhou ? leitor.Erro.Message : null);
        return fases.Single();
    }

    private static LeitorEnvelope Ler(JsonObject item)
    {
        LeitorEnvelope leitor = new();
        LeituraDoCronograma.LerCronogramaFases(leitor, Payload(item));
        return leitor;
    }

    private static JsonObject Payload(JsonObject item) => new()
    {
        ["cronogramaFases"] = new JsonObject
        {
            ["origemCandidatos"] = OrigemCandidatos.InscricaoPropria.ToString(),
            ["fases"] = new JsonArray(item),
        },
    };
}
