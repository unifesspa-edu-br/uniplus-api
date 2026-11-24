namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.AdulteracaoDeEnvelopeDeTeste;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.VersoesDoEnvelopeDeTeste;

/// <summary>
/// A gramática do envelope é fechada em todos os níveis: chave desconhecida, chave duplicada, bloco ausente
/// ou com a forma de um stub extinto e identificador legível fora da regra do cadastro são recusados — um
/// leitor tolerante perderia configuração em silêncio.
/// </summary>
public sealed class EnvelopeCodecRecusaGramaticaFechadaTests
{
    // ── A gramática é FECHADA em todos os níveis ──

    /// <summary>
    /// O caso crítico é o <c>fada</c>: um leitor que só olhasse <c>presente</c>
    /// leria <c>{"presente":false,"fator":"1.2000",…}</c> como “sem bônus” e
    /// <b>descartaria o bônus regional do certame</b> (Fator de Atenuação das Desigualdades de Acesso, UNI-REQ-0015) sem deixar rastro. É o modo
    /// de falha exato que esta story existe para tornar impossível.
    /// </summary>
    [Theory(DisplayName = "Chave desconhecida em QUALQUER nível é recusada — um leitor tolerante perde configuração em silêncio")]
    [InlineData("$")]
    [InlineData("fada")]
    [InlineData("periodo")]
    [InlineData("hashesEdital")]
    [InlineData("atendimento")]
    [InlineData("classificacao")]
    [InlineData("etapas.0")]
    [InlineData("distribuicao.0")]
    [InlineData("distribuicao.0.regraDistribuicao")]
    [InlineData("distribuicao.0.referenciaDemografica")]
    [InlineData("modalidades.0")]
    [InlineData("criteriosDesempate.0")]
    [InlineData("criteriosDesempate.0.parametros")]
    [InlineData("criteriosDesempate.0.regra")]
    [InlineData("criteriosDesempate.5.parametros")]
    [InlineData("classificacao.regrasEliminacao.0")]
    [InlineData("classificacao.regrasEliminacao.0.parametros")]
    [InlineData("classificacao.regrasEliminacao.0.regra")]
    [InlineData("classificacao.regraCalculo")]
    [InlineData("classificacao.quadroPesoAreaEnem.0")]
    [InlineData("classificacao.quadroPesoAreaEnem.0.grupoAreaEnem")]
    [InlineData("classificacao.quadroPesoAreaEnem.0.areas.0")]
    [InlineData("atendimento.condicoes.0")]
    [InlineData("atendimento.recursos.0")]
    [InlineData("atendimento.tiposDeficiencia.0")]
    [InlineData("fada.regra")]
    [InlineData("vagas.0")]
    [InlineData("cascataRemanejamento")]
    [InlineData("cascataRemanejamento.regra")]
    [InlineData("cascataRemanejamento.ordens.0")]
    [InlineData("identidadesUnidade")]
    [InlineData("identidadesUnidade.administradora")]
    [InlineData("divulgacao")]
    // Os dois objetos de regra OPCIONAIS e o bloco da convenção de contagem. Os obrigatórios
    // acima já cobriam o leitor compartilhado; estes três não passavam por caso nenhum, e um
    // fechamento de chaves esquecido neles não acusaria em lugar algum da suíte.
    [InlineData("classificacao.regraArredondamento")]
    [InlineData("distribuicao.0.regraAjuste")]
    [InlineData("algoritmoContagemPrazo")]
    public void ChaveDesconhecida_Recusa(string caminho)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonObject alvo = caminho == "$"
                ? envelope
                : Navegar(envelope, caminho);
            alvo["chaveIntrusa"] = "x";
        });

        resultado.IsFailure.Should().BeTrue(
            $"uma chave desconhecida em '{caminho}' significa que o envelope tem uma forma que este codec não " +
            "conhece — ignorá-la é a definição de perder dado em silêncio");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "O caso crítico: {presente:false} com parâmetros de bônus é RECUSADO, não lido como 'sem bônus'")]
    public void BonusPresenteFalsoComParametros_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(DeclararBonusAusenteMantendoOFator);

        resultado.IsFailure.Should().BeTrue(
            "um bônus 'ausente' que ainda carrega parâmetros é envelope contraditório. Lê-lo como 'sem bônus' descartaria " +
            "o bônus regional (Fator de Atenuação das Desigualdades de Acesso, UNI-REQ-0015) de um certame publicado, e ninguém veria.");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    /// <summary>
    /// Mesmo raciocínio de <see cref="BonusPresenteFalsoComParametros_Recusa"/>, agora para a
    /// cascata (Story #575): um leitor que só olhasse <c>presente</c> leria
    /// <c>{"presente":false,"regra":{...},...}</c> como "sem cascata" e descartaria a
    /// sequência legal de remanejamento (cascata de remanejamento de cotas, UNI-REQ-0056) de um certame publicado, sem
    /// deixar rastro — exatamente o modo de falha que <c>ExigirChaves</c> torna impossível.
    /// </summary>
    [Fact(DisplayName = "O caso crítico: {presente:false} com parâmetros de cascata é RECUSADO, não lido como 'sem cascata'")]
    public void CascataPresenteFalsoComParametros_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonObject cascata = envelope["cascataRemanejamento"]!.AsObject();
            cascata["presente"] = false;
            // Sobra `regra`, `fallbackCodigo` e `ordens` — a forma "ausente" só admite `presente`.
        });

        resultado.IsFailure.Should().BeTrue(
            "uma cascata 'ausente' que ainda carrega regra/fallback/ordens é envelope contraditório. Lê-la como " +
            "'sem cascata' descartaria a sequência legal de remanejamento (cascata de remanejamento de cotas, UNI-REQ-0056) de um certame publicado, " +
            "e ninguém veria.");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Chave desconhecida DENTRO do 18º bloco (retificacao) é recusada")]
    public void ChaveDesconhecidaNaRetificacao_Recusa()
    {
        JsonObject envelope = EnvelopeDosBytes(BytesComRetificacaoDe(ExemploDeEnvelope.AtoAbertura, "motivo"));
        envelope["retificacao"]!.AsObject()["chaveIntrusa"] = "x";

        byte[] retificada = PerfilCanonicoV1.Instancia.Serializar(envelope);
        Result<EnvelopeReidratado> resultado = ReidratarComoSucessora(BytesDoExemplo(), retificada);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Theory(DisplayName = "Bloco real com chaves de um formato de stub extinto é forma intrusa — recusado, não tolerado")]
    [InlineData("vagas")]
    [InlineData("documentosExigidos")]
    [InlineData("cascataRemanejamento")]
    public void BlocoRealComFormaDeStubExtinto_Recusa(string bloco)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope[bloco] = new JsonObject { ["status"] = "construido", ["itens"] = new JsonArray() });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Bloco obrigatório ausente é recusado")]
    public void BlocoAusente_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope => envelope.Remove("atendimento"));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    // ── Identificador legível congelado: o decoder aplica a mesma regra do cadastro. Um valor que
    // o value object recusaria não sai do encoder, então achá-lo é sinal de bytes que não vieram
    // desse caminho. ──

    [Fact(DisplayName = "Identificador legível ausente do envelope é recusado")]
    public void IdentificadorLegivelAusente_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope => envelope.Remove("identificadorLegivel"));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("identificadorLegivel");
    }

    [Fact(DisplayName = "Identificador legível que não é texto é recusado")]
    public void IdentificadorLegivelNaoTexto_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope => envelope["identificadorLegivel"] = 2026);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("identificadorLegivel");
    }

    [Theory(DisplayName = "Identificador legível fora da regra do cadastro é recusado com a causa do cadastro")]
    [InlineData("PSIQ 2026", ProcessoSeletivoErrorCodes.IdentificadorLegivelFormatoInvalido)]
    [InlineData("ps", ProcessoSeletivoErrorCodes.IdentificadorLegivelTamanho)]
    [InlineData("a1b2c3d4-e5f6-4789-8abc-def012345678", ProcessoSeletivoErrorCodes.IdentificadorLegivelComFormatoDeGuid)]
    public void IdentificadorLegivelInvalido_Recusa(string valor, string codigoEsperado)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope => envelope["identificadorLegivel"] = valor);

        resultado.IsFailure.Should().BeTrue(
            "o endereço público congelado tem de obedecer à mesma regra que o cadastro impõe");
        resultado.Error!.Code.Should().Be(codigoEsperado);
    }

    [Theory(DisplayName = "O código estável da base legal do bônus fora da gramática do cadastro é recusado no envelope")]
    [InlineData("minuscula")]
    [InlineData("COM PONTUACAO!")]
    [InlineData("1COMECA_POR_DIGITO")]
    [InlineData("COM-HIFEN")]
    [InlineData("A")]
    [InlineData("TERMINA_EM_QUEBRA\n")]
    public void Bonus_CodigoDaBaseLegalForaDaGramatica_RecusaOEnvelope(string codigo)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["fada"]!["baseLegalCodigo"] = codigo);

        resultado.IsFailure.Should().BeTrue("o código congelado tem de obedecer à gramática que o cadastro impõe");
    }

    [Theory(DisplayName = "O código estável da base legal do bônus é obrigatório e não vazio no envelope")]
    [InlineData(true)]
    [InlineData(false)]
    public void Bonus_CodigoDaBaseLegalAusenteOuVazio_RecusaOEnvelope(bool removerChave)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonObject bonus = envelope["fada"]!.AsObject();
            if (removerChave)
            {
                bonus.Remove("baseLegalCodigo");
            }
            else
            {
                bonus["baseLegalCodigo"] = string.Empty;
            }
        });

        resultado.IsFailure.Should().BeTrue("o snapshot do bônus sem código estável deixaria o certame sem a referência que o documento resolve");
    }

    [Theory(DisplayName = "O código do termo e o número da versão ausentes, vazios ou fora do limite são recusados no envelope")]
    [InlineData("termoCodigo", true, null)]
    [InlineData("termoCodigo", false, "")]
    [InlineData("termoCodigo", false, "UM_CODIGO_QUE_PASSA_DO_LIMITE_DO_CATALOGO_PORQUE_TEM_MUITO_MAIS_DE_CEM_CARACTERES_DE_COMPRIMENTO_TOTAL_DO_VALOR")]
    [InlineData("versaoNumero", true, null)]
    [InlineData("versaoNumero", false, 0)]
    [InlineData("versaoNumero", false, -1)]
    [InlineData("versaoNumero", false, "1")]
    public void Termo_CodigoENumeroDaVersaoInvalidos_RecusaOEnvelope(string chave, bool removerChave, object? valor)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonObject termo = envelope["formularios"]!.AsArray().First(f => f!["termos"]!.AsArray().Count > 0)!["termos"]![0]!.AsObject();
            if (removerChave)
            {
                termo.Remove(chave);
            }
            else
            {
                termo[chave] = JsonValue.Create(valor);
            }
        });

        resultado.IsFailure.Should().BeTrue("o termo congelado sem código estável ou com número inválido não é citável pelo documento");
    }

    private static readonly string[] ParametrosDoBonusAlemDoFator =
    [
        "regra", "teto", "baseLegalId", "baseLegalCodigo", "tipoInstrumento", "identificacao", "descricao", "municipios",
    ];

    /// <summary>Sobra `fator` — a forma "ausente" só admite `presente`.</summary>
    private static void DeclararBonusAusenteMantendoOFator(JsonObject envelope)
    {
        JsonObject bonus = envelope["fada"]!.AsObject();
        bonus["presente"] = false;
        foreach (string chave in ParametrosDoBonusAlemDoFator)
        {
            bonus.Remove(chave);
        }
    }
}
