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
/// A banca da fase e a da etapa congelam a cópia do tipo de banca: o tipo de origem, o código, o nome, o papel, as duas marcas e
/// os tipos de decisão julgados. O envelope devolve a mesma cópia ao ser lido, recusa a banca que não a traz inteira e nunca
/// repete o texto recebido na recusa.
/// </summary>
public sealed class EnvelopeCodecCopiaDoTipoDeBancaTests
{
    private const string CaminhoDaBancaDaEtapa = "etapas.0.bancas.0";
    private const string CaminhoDaBancaDaFase = "cronogramaFases.fases.1.bancasRequeridas.0";
    private const string TextoDigitado = "VALOR_DIGITADO_PELO_CLIENTE";

    [Fact(DisplayName = "O envelope lido devolve, em cada banca da fase e da etapa, a mesma cópia do tipo que foi congelada")]
    public void RoundTrip_DevolveACopiaDoTipoDeCadaBanca()
    {
        ProcessoSeletivo original = CorpusEnvelopeProcessoRico.ProcessoRico();

        Result<EnvelopeReidratado> lido = VersoesDoEnvelopeDeTeste.ReidratarComoAbertura(CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(original)).Bytes);

        lido.IsSuccess.Should().BeTrue(lido.Error?.Message);
        lido.Value!.Configuracao.Cronograma.CronogramaFases.SelectMany(static f => f.BancasRequeridas).Select(static b => (b.Id, b.Tipo))
            .Should().BeEquivalentTo(original.CronogramaFases.SelectMany(static f => f.BancasRequeridas).Select(static b => (b.Id, b.Tipo)));
        lido.Value.Configuracao.Avaliacao.Etapas.SelectMany(static e => e.Bancas).Select(static b => (b.Id, b.Tipo))
            .Should().BeEquivalentTo(original.Etapas.SelectMany(static e => e.Bancas).Select(static b => (b.Id, b.Tipo)));
    }

    [Theory(DisplayName = "Banca sem uma das chaves da cópia do tipo é recusada, na fase e na etapa")]
    [InlineData(CaminhoDaBancaDaEtapa, "tipoBancaOrigemId")]
    [InlineData(CaminhoDaBancaDaEtapa, "codigo")]
    [InlineData(CaminhoDaBancaDaEtapa, "nome")]
    [InlineData(CaminhoDaBancaDaEtapa, "papel")]
    [InlineData(CaminhoDaBancaDaEtapa, "exigeDuasOuMaisBancas")]
    [InlineData(CaminhoDaBancaDaEtapa, "encerraRecursoNaPrimeiraInstancia")]
    [InlineData(CaminhoDaBancaDaEtapa, "tiposDeDecisaoJulgados")]
    [InlineData(CaminhoDaBancaDaFase, "tipoBancaOrigemId")]
    [InlineData(CaminhoDaBancaDaFase, "codigo")]
    [InlineData(CaminhoDaBancaDaFase, "nome")]
    [InlineData(CaminhoDaBancaDaFase, "papel")]
    [InlineData(CaminhoDaBancaDaFase, "exigeDuasOuMaisBancas")]
    [InlineData(CaminhoDaBancaDaFase, "encerraRecursoNaPrimeiraInstancia")]
    [InlineData(CaminhoDaBancaDaFase, "tiposDeDecisaoJulgados")]
    public void Banca_SemChaveDaCopia_Recusa(string caminho, string chave)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope => Navegar(envelope, caminho).Remove(chave));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Theory(DisplayName = "Banca com chave que o envelope não define é recusada, na fase e na etapa")]
    [InlineData(CaminhoDaBancaDaEtapa)]
    [InlineData(CaminhoDaBancaDaFase)]
    public void Banca_ComChaveDesconhecida_Recusa(string caminho)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope => Navegar(envelope, caminho)["descricaoDoTipo"] = "texto");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Theory(DisplayName = "Código ou papel fora do vocabulário é recusado sem repetir o texto recebido")]
    [InlineData(CaminhoDaBancaDaEtapa, "codigo")]
    [InlineData(CaminhoDaBancaDaEtapa, "papel")]
    [InlineData(CaminhoDaBancaDaFase, "codigo")]
    [InlineData(CaminhoDaBancaDaFase, "papel")]
    public void Banca_CodigoOuPapelForaDoVocabulario_RecusaSemRepetirOTexto(string caminho, string chave)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope => Navegar(envelope, caminho)[chave] = TextoDigitado);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain(chave).And.NotContain(TextoDigitado);
    }

    [Theory(DisplayName = "Tipo de decisão julgado fora do vocabulário é recusado sem repetir o texto recebido")]
    [InlineData(CaminhoDaBancaDaEtapa)]
    [InlineData(CaminhoDaBancaDaFase)]
    public void Banca_TipoDeDecisaoForaDoVocabulario_Recusa(string caminho)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            Navegar(envelope, caminho)["tiposDeDecisaoJulgados"] = new JsonArray(TextoDigitado));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().NotContain(TextoDigitado);
    }

    [Theory(DisplayName = "Nome do tipo em branco ou acima do limite da coluna (200) é recusado")]
    [InlineData(CaminhoDaBancaDaEtapa, "")]
    [InlineData(CaminhoDaBancaDaFase, "   ")]
    [InlineData(CaminhoDaBancaDaEtapa, "limite")]
    [InlineData(CaminhoDaBancaDaFase, "limite")]
    public void Banca_NomeDoTipoInvalido_Recusa(string caminho, string nome)
    {
        string valor = nome == "limite" ? new string('A', 201) : nome;

        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope => Navegar(envelope, caminho)["nome"] = valor);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("nome");
    }

    [Theory(DisplayName = "A marca que não é verdadeiro nem falso é recusada")]
    [InlineData(CaminhoDaBancaDaEtapa, "exigeDuasOuMaisBancas")]
    [InlineData(CaminhoDaBancaDaFase, "encerraRecursoNaPrimeiraInstancia")]
    public void Banca_MarcaQueNaoEBooleana_Recusa(string caminho, string chave)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope => Navegar(envelope, caminho)[chave] = "sim");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Trocar o tipo de origem da banca, com outra cópia, muda os bytes canônicos")]
    public void CopiaDeOutroTipo_FazParteDoConteudoCongelado()
    {
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico();
        byte[] antes = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo)).Bytes;

        TrocarACopiaDaPrimeiraBancaDaEtapa(processo.Etapas.Single(static e => e.Nome == "Prova Objetiva"));

        CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo)).Bytes.Should().NotEqual(antes);
    }

    private static void TrocarACopiaDaPrimeiraBancaDaEtapa(EtapaProcesso etapa)
    {
        BancaDaEtapa primeira = etapa.Bancas.First();
        TipoDeBancaCopiado c = primeira.Tipo;
        TipoDeBancaCopiado outra = new(Guid.CreateVersion7(), c.Codigo, c.Nome, c.Papel, !c.ExigeDuasOuMaisBancas, c.EncerraRecursoNaPrimeiraInstancia, c.TiposDeDecisaoJulgados);

        etapa.DefinirBancas([BancaDaEtapa.Criar(outra, primeira.RegraDeDeliberacao, primeira.Id), .. etapa.Bancas.Skip(1)]);
    }
}
