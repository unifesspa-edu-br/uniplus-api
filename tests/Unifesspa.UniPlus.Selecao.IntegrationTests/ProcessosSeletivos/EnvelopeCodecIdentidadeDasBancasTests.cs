namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.AdulteracaoDeEnvelopeDeTeste;

/// <summary>
/// A banca da fase e a da etapa congelam o <c>id</c> e a posição dela no tipo. O envelope
/// recusa a banca sem um dos dois, e o hash da publicação acompanha os dois.
/// </summary>
public sealed class EnvelopeCodecIdentidadeDasBancasTests
{
    private const string CaminhoDaBancaDaEtapa = "etapas.0.bancas.0";
    private const string CaminhoDaBancaDaFase = "cronogramaFases.fases.1.bancasRequeridas.0";

    [Theory(DisplayName = "Banca sem o id ou sem a posição no tipo é recusada, na etapa e na fase")]
    [InlineData(CaminhoDaBancaDaEtapa, "id")]
    [InlineData(CaminhoDaBancaDaEtapa, "posicaoNoTipo")]
    [InlineData(CaminhoDaBancaDaFase, "id")]
    [InlineData(CaminhoDaBancaDaFase, "posicaoNoTipo")]
    public void Banca_SemChaveDeIdentidade_Recusa(string caminho, string chave)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            Navegar(envelope, caminho).Remove(chave));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Theory(DisplayName = "Banca com a posição no tipo menor que 1 é recusada")]
    [InlineData(CaminhoDaBancaDaEtapa, 0)]
    [InlineData(CaminhoDaBancaDaFase, -1)]
    public void Banca_PosicaoNoTipoMenorQueUm_Recusa(string caminho, int posicao)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            Navegar(envelope, caminho)["posicaoNoTipo"] = posicao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Duas bancas da fase com o mesmo id são recusadas")]
    public void BancasDaFase_ComOMesmoId_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonArray bancas = Navegar(envelope, "cronogramaFases.fases.1")["bancasRequeridas"]!.AsArray();
            bancas[1]!["id"] = bancas[0]!["id"]!.GetValue<string>();
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("cronogramaFases.fases[1].bancasRequeridas");
    }

    [Theory(DisplayName = "Banca com a posição no tipo que a fase ou a etapa não atribuiria é recusada, mesmo com o hash recalculado")]
    [InlineData("cronogramaFases.fases.1", "bancasRequeridas", 2, 1, "cronogramaFases.fases[1].bancasRequeridas[0].posicaoNoTipo")]
    [InlineData("cronogramaFases.fases.1", "bancasRequeridas", 1, 3, "cronogramaFases.fases[1].bancasRequeridas[1].posicaoNoTipo")]
    [InlineData("etapas.0", "bancas", 2, 1, "etapas[0].bancas[0].posicaoNoTipo")]
    public void Banca_ComPosicaoNoTipoForaDaOrdem_Recusa(string lugar, string lista, int primeira, int segunda, string campoRecusado)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonArray bancas = Navegar(envelope, lugar)[lista]!.AsArray();
            bancas[0]!["posicaoNoTipo"] = primeira;
            bancas[1]!["posicaoNoTipo"] = segunda;
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain(campoRecusado).And.Contain("a posição no tipo não segue a ordem das bancas do mesmo tipo");
    }

    [Fact(DisplayName = "Trocar o id das bancas da etapa muda os bytes canônicos")]
    public void IdDaBanca_FazParteDoConteudoCongelado()
    {
        byte[] umaVariante = Codificar(variante: 0);
        byte[] outraVariante = Codificar(variante: 1);

        outraVariante.Should().NotEqual(umaVariante);
    }

    [Fact(DisplayName = "Trocar a ordem de duas bancas do mesmo tipo, com os mesmos ids, muda os bytes canônicos")]
    public void PosicaoNoTipo_FazParteDoConteudoCongelado()
    {
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico();
        EtapaProcesso objetiva = processo.Etapas.Single(e => e.Nome == "Prova Objetiva");
        BancaDaEtapa primeira = objetiva.Bancas.First();
        objetiva.DefinirBancas([.. objetiva.Bancas.Select(ComOMesmoId), OutraDoMesmoTipo(primeira)]);
        BancaDaEtapa segunda = objetiva.Bancas.Single(b => b.Id != primeira.Id && b.Tipo.Codigo == primeira.Tipo.Codigo);
        byte[] naOrdemDeChegada = CorpusEnvelope.Canonicalizador.Canonicalizar(CorpusEnvelopeEntrada.Entrada(processo)).Bytes;

        objetiva.DefinirBancas([ComOMesmoId(segunda), .. objetiva.Bancas.Where(b => b.Id != segunda.Id).Select(ComOMesmoId)]);
        byte[] comAsDoMesmoTipoTrocadas = CorpusEnvelope.Canonicalizador.Canonicalizar(CorpusEnvelopeEntrada.Entrada(processo)).Bytes;

        segunda.PosicaoNoTipo.Should().Be(1);
        comAsDoMesmoTipoTrocadas.Should().NotEqual(naOrdemDeChegada);
    }

    [Fact(DisplayName = "Permutar a ordem de entrada de bancas de tipos diferentes não muda os bytes canônicos")]
    public void OrdemDeEntrada_DeTiposDiferentes_NaoMudaOsBytes()
    {
        Codificar(variante: 0, permutar: true).Should().Equal(Codificar(variante: 0, permutar: false));
    }

    private static byte[] Codificar(int variante, bool permutar = false) =>
        CorpusEnvelope.Canonicalizador.Canonicalizar(
            CorpusEnvelopeEntrada.Entrada(CorpusEnvelopeProcessoRico.ProcessoRico(variante: variante, permutar: permutar))).Bytes;

    private static BancaDaEtapa ComOMesmoId(BancaDaEtapa banca) =>
        BancaDaEtapa.Criar(ChaveLocal.Criar("banca").Value, banca.Tipo, banca.RegraDeDeliberacao, idDaBancaQueSubstitui: banca.Id);

    private static BancaDaEtapa OutraDoMesmoTipo(BancaDaEtapa banca) =>
        BancaDaEtapa.Criar(ChaveLocal.Criar("banca").Value, banca.Tipo, banca.RegraDeDeliberacao);
}
