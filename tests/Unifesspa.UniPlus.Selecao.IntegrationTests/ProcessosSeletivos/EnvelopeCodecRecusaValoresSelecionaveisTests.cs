namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.AdulteracaoDeEnvelopeDeTeste;

/// <summary>
/// Os blocos de valores selecionáveis (UNI-REQ-0072) e a bicondicional de
/// <c>fatosColetados[].valoresSelecionaveis</c> com <c>tipoRenderizacao</c>, nos dois sentidos: o decoder
/// recusa o que o encoder nunca emite, nos fatos coletados e nos metadados dos fatos documentais.
/// </summary>
public sealed class EnvelopeCodecRecusaValoresSelecionaveisTests
{
    [Fact(DisplayName = "fatosColetados[].valoresSelecionaveis ausente é recusado — omitir a chave não é o mesmo que declará-la null")]
    public void ValoresSelecionaveis_ChaveAusente_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            FatoColetadoPorCodigo(envelope, "COR_RACA").Remove("valoresSelecionaveis"));

        resultado.IsFailure.Should().BeTrue(
            "a gramática de cada item de fatosColetados fecha em 'valoresSelecionaveis' — o encoder sempre a " +
            "emite, presente e explícita (array ou null), nunca omitida");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "fatosColetados[].valoresSelecionaveis de tipo errado (nem array nem null) é recusado")]
    public void ValoresSelecionaveis_TipoErrado_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            FatoColetadoPorCodigo(envelope, "COR_RACA")["valoresSelecionaveis"] = "BRANCA");

        resultado.IsFailure.Should().BeTrue(
            "um escalar no lugar do array (ou de null) é forma que o encoder nunca produz para este campo");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "fatosColetados[].valoresSelecionaveis[].valorCodigo duplicado é recusado")]
    public void ValoresSelecionaveis_ValorCodigoDuplicado_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonArray valores = FatoColetadoPorCodigo(envelope, "COR_RACA")["valoresSelecionaveis"]!.AsArray();
            valores[1] = valores[0]!.DeepClone();
        });

        resultado.IsFailure.Should().BeTrue(
            "o encoder nunca emite duas entradas para o mesmo valorCodigo dentro do mesmo fato — a " +
            "repetição só é alcançável por adulteração dos bytes");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "fatosColetados[].valoresSelecionaveis[].ordem ausente é recusado")]
    public void ValoresSelecionaveis_OrdemAusente_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            FatoColetadoPorCodigo(envelope, "COR_RACA")["valoresSelecionaveis"]!.AsArray()[0]!.AsObject().Remove("ordem"));

        resultado.IsFailure.Should().BeTrue(
            "'ordem' fecha a gramática de cada item de valoresSelecionaveis — o encoder sempre a emite, " +
            "e é dela que o formulário público deriva a posição de exibição da opção");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "fatosColetados[].valoresSelecionaveis[].ordem negativa é recusada")]
    public void ValoresSelecionaveis_OrdemNegativa_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            FatoColetadoPorCodigo(envelope, "COR_RACA")["valoresSelecionaveis"]!.AsArray()[0]!["ordem"] = -1);

        resultado.IsFailure.Should().BeTrue(
            "uma ordem negativa não corresponde a posição alguma de exibição — o encoder só emite índices " +
            "canônicos, sempre a partir de zero");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    /// <summary>
    /// Bicondicional D1-bis: um fato de <b>seleção</b> (SELECAO_UNICA/SELECAO_MULTIPLA) sem
    /// array de valores é um seletor <b>mudo</b> — o candidato veria o campo, mas nenhuma opção
    /// para escolher. É o mesmo modo de falha que <see cref="ObterFormularioRenderizavelQueryHandlerTests"/>
    /// exercita no handler de leitura pública; aqui a guarda é a do próprio DECODER.
    /// </summary>
    [Fact(DisplayName = "Bicondicional: valoresSelecionaveis null num fato de SELEÇÃO é recusado — seletor mudo")]
    public void ValoresSelecionaveis_NuloEmFatoDeSelecao_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            FatoColetadoPorCodigo(envelope, "COR_RACA")["valoresSelecionaveis"] = null);

        resultado.IsFailure.Should().BeTrue(
            "COR_RACA é SELECAO_UNICA — a bicondicional exige array. Restaurar null aqui publicaria um " +
            "campo de seleção sem as opções que o candidato precisa escolher");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    /// <summary>issue #1077 — §4, CA-13: um envelope adulterado com array VAZIO para um fato de seleção é malformado, não um caso legítimo a reidratar.</summary>
    [Fact(DisplayName = "Bicondicional: valoresSelecionaveis com array VAZIO num fato de SELEÇÃO é recusado — cardinalidade mínima 1")]
    public void ValoresSelecionaveis_ArrayVazioEmFatoDeSelecao_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            FatoColetadoPorCodigo(envelope, "COR_RACA")["valoresSelecionaveis"] = new JsonArray());

        resultado.IsFailure.Should().BeTrue(
            "COR_RACA é SELECAO_UNICA — array vazio é um seletor sem opção nenhuma, tão mudo quanto null. " +
            "A publicação original nunca teria produzido isso: o gate de pré-canonicalização " +
            "(ProcessoSeletivo.PendenciaDeFatoColetadoSemValoresOfertados) recusa antes de canonicalizar, e " +
            "SerializarFatosColetados também lança — só um envelope adulterado chega aqui com [] persistido");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Bicondicional: valoresSelecionaveis com array num fato que NÃO é de seleção é recusado — vocabulário pendurado")]
    public void ValoresSelecionaveis_ArrayEmFatoNaoDeSelecao_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            FatoColetadoPorCodigo(envelope, "COR_RACA")["tipoRenderizacao"] = "BOOLEANO");

        resultado.IsFailure.Should().BeTrue(
            "trocar o tipo de renderização para BOOLEANO sem esvaziar 'valoresSelecionaveis' deixa um " +
            "vocabulário de seleção pendurado num campo que a bicondicional proíbe de tê-lo");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "documentosExigidos.metadadosFatos com item que não é objeto é recusado, nunca lança")]
    public void MetadadosFatos_ItemQueNaoEObjeto_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeDeReferenciaAdulterado(envelope =>
            envelope["documentosExigidos"]!["metadadosFatos"] = new JsonArray(42));

        resultado.IsFailure.Should().BeTrue("um item que não é objeto é forma que o encoder nunca produz");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "documentosExigidos.metadadosFatos[].valoresDominioDeclarados ausente é recusado")]
    public void ValoresDominioDeclarados_ChaveAusente_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeDeReferenciaAdulterado(envelope =>
            MetadadoFatoPorCodigo(envelope, "COR_RACA").Remove("valoresDominioDeclarados"));

        resultado.IsFailure.Should().BeTrue(
            "a gramática de cada item de metadadosFatos fecha em 'valoresDominioDeclarados' — o encoder " +
            "sempre a emite, presente e explícita, nunca omitida");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "documentosExigidos.metadadosFatos[].valoresDominioDeclarados de tipo errado (nem array nem null) é recusado")]
    public void ValoresDominioDeclarados_TipoErrado_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeDeReferenciaAdulterado(envelope =>
            MetadadoFatoPorCodigo(envelope, "COR_RACA")["valoresDominioDeclarados"] = "BRANCA");

        resultado.IsFailure.Should().BeTrue(
            "um escalar no lugar do array (ou de null) é forma que o encoder nunca produz para este campo");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "documentosExigidos.metadadosFatos[].valoresDominioDeclarados[].valorCodigo duplicado é recusado")]
    public void ValoresDominioDeclarados_ValorCodigoDuplicado_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeDeReferenciaAdulterado(envelope =>
        {
            JsonArray valores = MetadadoFatoPorCodigo(envelope, "COR_RACA")["valoresDominioDeclarados"]!.AsArray();
            valores[1] = valores[0]!.DeepClone();
        });

        resultado.IsFailure.Should().BeTrue(
            "o encoder nunca emite duas entradas para o mesmo valorCodigo dentro do metadado do mesmo fato");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "documentosExigidos.metadadosFatos[].valoresDominioDeclarados[].ordem ausente é recusado")]
    public void ValoresDominioDeclarados_OrdemAusente_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeDeReferenciaAdulterado(envelope =>
            MetadadoFatoPorCodigo(envelope, "COR_RACA")["valoresDominioDeclarados"]!.AsArray()[0]!.AsObject().Remove("ordem"));

        resultado.IsFailure.Should().BeTrue(
            "'ordem' fecha a gramática de cada item de valoresDominioDeclarados — o encoder sempre a emite");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "documentosExigidos.metadadosFatos[].valoresDominioDeclarados[].ordem negativa é recusada")]
    public void ValoresDominioDeclarados_OrdemNegativa_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeDeReferenciaAdulterado(envelope =>
            MetadadoFatoPorCodigo(envelope, "COR_RACA")["valoresDominioDeclarados"]!.AsArray()[0]!["ordem"] = -1);

        resultado.IsFailure.Should().BeTrue(
            "uma ordem negativa não corresponde a posição alguma de exibição — o encoder só emite índices " +
            "canônicos, sempre a partir de zero");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }
}
