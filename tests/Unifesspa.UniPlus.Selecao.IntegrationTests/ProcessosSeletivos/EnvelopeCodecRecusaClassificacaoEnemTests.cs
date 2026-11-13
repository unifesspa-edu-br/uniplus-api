namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.AdulteracaoDeEnvelopeDeTeste;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.VersoesDoEnvelopeDeTeste;

/// <summary>
/// A classificação do envelope: o grupo de área do ENEM de cada distribuição, a resolução de Pesos por Área,
/// o quadro de pesos congelado e a edição do ENEM, que o decoder recusa pelas mesmas regras da gravação.
/// </summary>
public sealed class EnvelopeCodecRecusaClassificacaoEnemTests
{
    [Theory(DisplayName = "distribuicao[].grupoAreaEnem com código ou rótulo acima do limite da coluna é recusado")]
    [InlineData("codigo", 31, "30")]
    [InlineData("rotulo", 61, "60")]
    public void Distribuicao_GrupoAreaEnemAcimaDoLimite_Recusa(string campo, int tamanho, string limite)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonObject grupo = new() { ["codigo"] = "TECNOLOGICA", ["rotulo"] = "Tecnológica" };
            grupo[campo] = new string('A', tamanho);
            envelope["distribuicao"]!.AsArray()[0]!["grupoAreaEnem"] = grupo;
        });

        resultado.IsFailure.Should().BeTrue(
            $"um {campo} de {tamanho} caracteres não cabe na coluna (varchar {limite}) — recusar aqui evita " +
            "DbUpdateException (500) no meio do descarte");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain($"distribuicao[0].grupoAreaEnem.{campo}").And.Contain(limite);
    }

    [Fact(DisplayName = "distribuicao[].grupoAreaEnem sem o rótulo é recusado")]
    public void Distribuicao_GrupoAreaEnemSemRotulo_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["distribuicao"]!.AsArray()[0]!["grupoAreaEnem"] = new JsonObject { ["codigo"] = "TECNOLOGICA" });

        resultado.IsFailure.Should().BeTrue("o grupo congelado carrega código e rótulo, os dois");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("distribuicao[0].grupoAreaEnem");
    }

    [Fact(DisplayName = "distribuicao[].grupoAreaEnem como texto solto, no formato anterior, é recusado")]
    public void Distribuicao_GrupoAreaEnemComoTexto_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["distribuicao"]!.AsArray()[0]!["grupoAreaEnem"] = "Tecnológica");

        resultado.IsFailure.Should().BeTrue("o grupo congelado é um objeto com código e rótulo");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("distribuicao[0].grupoAreaEnem");
    }

    [Fact(DisplayName = "classificacao com resolução de Pesos por Área e quadro vazio é recusada")]
    public void Classificacao_ResolucaoComQuadroVazio_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["classificacao"]!["quadroPesoAreaEnem"] = new JsonArray());

        resultado.IsFailure.Should().BeTrue("a resolução declarada sem grupo congelado não teria pesos para a nota");
        resultado.Error!.Code.Should().Be("ConfiguracaoClassificacao.QuadroPesoAreaEnemVazio");
    }

    [Fact(DisplayName = "classificacao baseada em ENEM com cálculo local sem a resolução de Pesos por Área é recusada")]
    public void Classificacao_EnemLocalSemResolucao_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["classificacao"]!["resolucaoPesoAreaEnem"] = null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoClassificacao.ResolucaoPesoAreaEnemObrigatoria");
    }

    [Fact(DisplayName = "classificacao.quadroPesoAreaEnem com grupo repetido é recusado")]
    public void Classificacao_QuadroComGrupoRepetido_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonArray quadro = envelope["classificacao"]!["quadroPesoAreaEnem"]!.AsArray();
            quadro.Add(quadro[0]!.DeepClone());
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoClassificacao.QuadroPesoAreaEnemGrupoRepetido");
    }

    [Fact(DisplayName = "classificacao.quadroPesoAreaEnem com área repetida no mesmo grupo é recusado")]
    public void Classificacao_QuadroComAreaRepetida_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonArray areas = envelope["classificacao"]!["quadroPesoAreaEnem"]!.AsArray()[0]!["areas"]!.AsArray();
            areas.Add(areas[0]!.DeepClone());
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("GrupoPesoAreaEnemCongelado.AreaRepetida");
    }

    [Fact(DisplayName = "classificacao.quadroPesoAreaEnem com corte acima da nota máxima é recusado")]
    public void Classificacao_QuadroComCorteAcimaDaNotaMaxima_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["classificacao"]!["quadroPesoAreaEnem"]!.AsArray()[0]!["areas"]!.AsArray()[0]!["corte"] = "1000.0001");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("GrupoPesoAreaEnemCongelado.CorteForaDaFaixa");
    }

    [Theory(DisplayName = "classificacao.quadroPesoAreaEnem com texto acima da coluna é recusado")]
    [InlineData("codigo", 31, "30")]
    [InlineData("rotulo", 101, "100")]
    public void Classificacao_QuadroComTextoDeAreaAcimaDaColuna_Recusa(string campo, int tamanho, string limite)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["classificacao"]!["quadroPesoAreaEnem"]!.AsArray()[0]!["areas"]!.AsArray()[0]![campo] = new string('A', tamanho));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain($"classificacao.quadroPesoAreaEnem[0].areas[0].{campo}").And.Contain(limite);
    }

    [Fact(DisplayName = "classificacao.quadroPesoAreaEnem com peso de mais dígitos que a coluna é recusado")]
    public void Classificacao_QuadroComPesoAcimaDaPrecisao_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["classificacao"]!["quadroPesoAreaEnem"]!.AsArray()[0]!["areas"]!.AsArray()[0]!["peso"] = "100.0000");

        resultado.IsFailure.Should().BeTrue("numeric(6,4) comporta até 99,9999 — o excesso só estouraria no SaveChanges");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("numeric(6,4)");
    }

    [Theory(DisplayName = "classificacao com não-caractere na resolução, no grupo, na base legal ou na área é recusada como malformada, sem exceção")]
    [InlineData("resolucao")]
    [InlineData("grupo")]
    [InlineData("baseLegal")]
    [InlineData("area")]
    public void Classificacao_NaoCaractere_RecusaComoMalformada(string onde)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComNaoCaractere(onde switch
        {
            "resolucao" => static (envelope, texto) => envelope["classificacao"]!["resolucaoPesoAreaEnem"] = texto,
            "grupo" => static (envelope, texto) => Quadro(envelope)[0]!["grupoAreaEnem"]!["rotulo"] = texto,
            "baseLegal" => static (envelope, texto) => Quadro(envelope)[0]!["baseLegal"] = texto,
            _ => static (envelope, texto) => Quadro(envelope)[0]!["areas"]!.AsArray()[0]!["rotulo"] = texto,
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);

        static JsonArray Quadro(JsonObject envelope) => envelope["classificacao"]!["quadroPesoAreaEnem"]!.AsArray();
    }

    [Fact(DisplayName = "Envelope que coleta a edição do ENEM com classificação sem ENEM é recusado pela regra da publicação")]
    public void EdicaoDoEnemSemClassificacaoEnem_Recusa()
    {
        byte[] bytes = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(ProcessoComClassificacaoSemEnem())).Bytes;

        Result<EnvelopeReidratado> resultado = ReidratarComoAbertura(bytes);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error!.Message.Should().Contain("edição do ENEM");
    }

    private static ProcessoSeletivo ProcessoComClassificacaoSemEnem()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        processo.DefinirCriteriosDesempate(
            [CriterioDesempate.Criar(1, CorpusEnvelope.Regra(CriterioDesempateCodigo.MaiorIdade, 'f'), new ArgsDesempateMaiorIdade()).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        Result definir = processo.DefinirClassificacao(ClassificacaoSemEnem(), PrecondicaoIfMatch.Ausente);
        definir.IsSuccess.Should().BeTrue(string.Join("; ", definir.Errors.Select(static e => e.Error.Message)) + definir.Error?.Message);

        return processo;
    }

    private static ConfiguracaoClassificacao ClassificacaoSemEnem() =>
        ConfiguracaoClassificacao.Criar(
            CorpusEnvelope.Regra(RegraCalculoCodigo.ClassificacaoImportada, 'a'), null, null,
            CorpusEnvelope.Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, '3'), [],
            baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null).Value!;

    /// <summary>
    /// Como <see cref="ReidratarComEnvelopeAdulterado"/>, mas com um não-caractere (U+FFFE) no
    /// texto que <paramref name="adulterar"/> grava. O não-caractere entra direto nos bytes:
    /// o perfil canônico não o serializaria, porque ele não tem forma NFC.
    /// </summary>
    private static Result<EnvelopeReidratado> ReidratarComNaoCaractere(Action<JsonObject, string> adulterar)
    {
        const string Marcador = "MARCADOR-DO-NAO-CARACTERE";
        JsonObject envelope = EnvelopeDosBytes(BytesDoCorpus());
        adulterar(envelope, "Texto" + Marcador);

        string comMarcador = Encoding.UTF8.GetString(PerfilCanonicoV1.Instancia.Serializar(envelope));
        comMarcador.Should().Contain(Marcador, "pré-condição: a adulteração tem de chegar aos bytes");
        byte[] adulterados = Encoding.UTF8.GetBytes(comMarcador.Replace(Marcador, ((char)0xFFFE).ToString(), StringComparison.Ordinal));

        return ReidratarComoAbertura(adulterados);
    }
}
