namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.AdulteracaoDeEnvelopeDeTeste;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ApoioDoEnvelopeCodec;

/// <summary>
/// A regra de homologação das inscrições que a fase declara é congelada no envelope e entra no hash da versão; a leitura
/// a restaura fielmente e recusa o que a escrita nunca produz.
/// </summary>
public sealed class EnvelopeCodecRegraDeHomologacaoTests
{
    private const string FasePreliminar = "RESULTADO_PRELIMINAR";
    private const string FaseDeInscricao = "INSCRICAO";

    [Fact(DisplayName = "A fase com regra de homologação a escreve no envelope e a fase sem regra traz a chave com null")]
    public void Fase_GravaAChaveNulaQuandoNaoTemARegra()
    {
        JsonObject envelope = Envelope(Codificar(ExemploDeEnvelopeProcessoRico.ProcessoRico()));

        FaseNoEnvelope(envelope, FasePreliminar)["regraDeHomologacao"].Should().BeOfType<JsonObject>();
        FaseNoEnvelope(envelope, FaseDeInscricao).ContainsKey("regraDeHomologacao").Should().BeTrue();
        FaseNoEnvelope(envelope, FaseDeInscricao)["regraDeHomologacao"].Should().BeNull();
    }

    [Fact(DisplayName = "Ida e volta do envelope com a regra de homologação reproduz os bytes e o hash congelados")]
    public void IdaEVolta_ComARegra_ReproduzBytesEHash()
    {
        ProcessoSeletivo processo = ExemploDeEnvelopeProcessoRico.ProcessoRico(comBancasExigidasNaPublicacao: true);
        SnapshotCanonico congelado = Codificar(processo);
        ExemploDeEnvelopeEntrada.Publicar(processo);
        VersaoConfiguracao versao = ExemploDeEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes);

        EnvelopeReidratado reidratado = AssertSerializacaoEReidratacao(processo, versao, congelado);

        HashCanonicalComputer.ComputeSha256Hex(congelado.Bytes).Should().Be(versao.HashConfiguracao);
        FaseReidratada(reidratado, FasePreliminar).RegraDeHomologacao!.MesmoConteudo(ExemploDeEnvelopeCronograma.RegraDeHomologacaoDoExemplo())
            .Should().BeTrue();
        FaseReidratada(reidratado, FaseDeInscricao).RegraDeHomologacao.Should().BeNull();
    }

    [Fact(DisplayName = "Mudar a regra de homologação da fase muda os bytes canônicos e o hash da versão")]
    public void MudarARegra_MudaBytesEHash()
    {
        RegraDeHomologacaoDasInscricoes outra = RegraDeHomologacaoDasInscricoes.Declarar(
            "RESULTADO_CLASSIFICACAO",
            [AlternativaDeCondicoes.Declarar([CondicaoDeHomologacao.Declarar("RESULTADO_NO_RETORNO_DO_INEP", 2023, null, null).Value!]).Value!]);

        SnapshotCanonico exemplo = Codificar(ExemploDeEnvelopeProcessoRico.ProcessoRico());
        SnapshotCanonico mudada = Codificar(ExemploDeEnvelopeProcessoRico.ProcessoRico(regraDeHomologacao: outra));

        mudada.Bytes.Should().NotEqual(exemplo.Bytes);
        HashCanonicalComputer.ComputeSha256Hex(mudada.Bytes).Should().NotBe(HashCanonicalComputer.ComputeSha256Hex(exemplo.Bytes));
    }

    [Fact(DisplayName = "O decodificador lê a regra de homologação da fase: mudá-la no envelope muda o que a leitura devolve")]
    public void Decodificador_LeARegra()
    {
        EnvelopeReidratado envelope = ReidratarMutado(mutado =>
            FaseNoEnvelope(mutado, FasePreliminar)["regraDeHomologacao"]!["alternativas"]![1]!["condicoes"]![0]!["edicaoDoExame"] = 2023);

        FaseReidratada(envelope, FasePreliminar).RegraDeHomologacao!.Alternativas[1].Condicoes[0].EdicaoDoExame.Should().Be(2023);
    }

    [Fact(DisplayName = "A leitura recusa a regra de homologação com forma inválida")]
    public void RegraInvalida_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            FaseNoEnvelope(envelope, FasePreliminar)["regraDeHomologacao"]!["alternativas"]![0]!["condicoes"]![0]!["edicaoDoExame"] = 1997);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("regraDeHomologacao");
    }

    [Fact(DisplayName = "A leitura continua recusando a chave desconhecida na fase")]
    public void ChaveDesconhecida_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            FaseNoEnvelope(envelope, FaseDeInscricao)["regraDeHomologacaoDaFase"] = new JsonObject());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("regraDeHomologacaoDaFase");
    }

    private static SnapshotCanonico Codificar(ProcessoSeletivo processo) =>
        ExemploDeEnvelope.Canonicalizador.Canonicalizar(ExemploDeEnvelopeEntrada.Entrada(processo));

    private static JsonObject FaseNoEnvelope(JsonObject envelope, string codigo) =>
        envelope["cronogramaFases"]!["fases"]!.AsArray().Single(fase => fase!["codigo"]!.GetValue<string>() == codigo)!.AsObject();

    private static FaseCronograma FaseReidratada(EnvelopeReidratado envelope, string codigo) =>
        envelope.Configuracao.Cronograma.CronogramaFases.Single(fase => fase.Codigo == codigo);
}
