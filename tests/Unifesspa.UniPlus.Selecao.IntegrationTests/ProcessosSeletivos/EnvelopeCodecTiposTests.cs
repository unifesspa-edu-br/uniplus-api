namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ApoioDoEnvelopeCodecTipos;

/// <summary>
/// A leitura é <b>total</b>: todo valor é recusado antes de ser usado, nunca depois
/// (Story #859).
/// </summary>
/// <remarks>
/// <para>
/// Um envelope pode ter <b>todas</b> as chaves certas e ainda assim ser lixo: um array
/// onde deveria haver objeto, um número onde deveria haver texto, um inteiro que não cabe
/// em <c>Int32</c>, uma string vazia num campo que a factory do domínio exige não-branco.
/// </para>
/// <para>
/// A string vazia é o caso mais traiçoeiro. <c>EtapaProcesso.Reidratar</c>,
/// <c>CondicaoOfertada.Criar</c> e as irmãs validam com
/// <c>ArgumentException.ThrowIfNullOrWhiteSpace</c> — elas <b>lançam</b>, não devolvem
/// <c>Result</c>. Sem a guarda de não-vazio no leitor, um <c>"nome": ""</c> atravessaria a
/// leitura e explodiria dentro da factory: <b>exceção não tratada no meio de um
/// descarte</b>, que sai como 500 em vez de recusa nomeada.
/// </para>
/// </remarks>
public sealed class EnvelopeCodecTiposTests
{
    [Theory(DisplayName = "Tipo JSON errado é recusado — nunca lido como se fosse o tipo certo")]
    [InlineData("etapas", "objeto onde deveria haver array")]
    [InlineData("criteriosDesempate", "objeto onde deveria haver array")]
    [InlineData("atendimento", "array onde deveria haver objeto")]
    [InlineData("bonusRegional", "array onde deveria haver objeto")]
    public void TipoErrado_Recusa(string bloco, string _)
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            envelope[bloco] = envelope[bloco] is JsonArray ? new JsonObject() : new JsonArray());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Número onde deveria haver texto é recusado")]
    public void NumeroNoLugarDeTexto_Recusa()
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            envelope["etapas"]!.AsArray()[0]!["nome"] = 42);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Texto onde deveria haver inteiro é recusado")]
    public void TextoNoLugarDeInteiro_Recusa()
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            envelope["distribuicao"]!.AsArray()[0]!["vagasOfertadasBase"] = "sessenta");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Inteiro fora de Int32 é recusado — não silenciosamente truncado")]
    public void InteiroForaDeFaixa_Recusa()
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            envelope["distribuicao"]!.AsArray()[0]!["vagasOfertadasBase"] = 9_999_999_999L);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Theory(DisplayName = "Número de opções de curso fora de 1 e 2 é recusado, como o agregado o recusa ao declarar")]
    [InlineData(0)]
    [InlineData(3)]
    public void NumeroDeOpcoesDeCursoForaDeUmEDois_Recusa(int numero)
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            envelope["opcoesDeCurso"]!["numero"] = numero);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Número fracionário onde deveria haver inteiro é recusado")]
    public void FracionarioNoLugarDeInteiro_Recusa()
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            envelope["opcoesDeCurso"]!["numero"] = 1.5);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    /// <summary>
    /// O decimal é escrito como <b>string com escala declarada</b> (ADR-0100 item 2).
    /// Aceitar <c>"1.0"</c> onde o encoder escreve <c>"1.0000"</c> deixaria a escala do
    /// envelope à mercê de quem o escrevesse por fora — e a nota de corte de um certame é
    /// uma dessas escalas.
    /// </summary>
    [Theory(DisplayName = "Decimal fora da escala canônica é recusado")]
    [InlineData("3.5")]
    [InlineData("3.50000")]
    [InlineData("3,5000")]
    public void DecimalForaDaEscala_Recusa(string valor)
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            envelope["etapas"]!.AsArray()[0]!["peso"] = valor);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    /// <summary>
    /// Nota máxima e passo não positivos são limite, e só limite: o envelope que os traz não
    /// descreve escala nenhuma. A coerência entre eles não é conferida na leitura.
    /// </summary>
    [Theory(DisplayName = "Nota máxima ou passo do lançamento não positivos no envelope são recusados")]
    [InlineData("notaMaxima", "0.0000")]
    [InlineData("notaMaxima", "-1.0000")]
    [InlineData("passoDoLancamento", "0.0000")]
    [InlineData("passoDoLancamento", "-0.2500")]
    public void EscalaNaoPositivaNoEnvelope_Recusa(string chave, string valor)
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            envelope["etapas"]!.AsArray()[0]![chave] = valor);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "Decimal como NÚMERO JSON (e não string) é recusado")]
    public void DecimalComoNumero_Recusa()
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope =>
            envelope["etapas"]!.AsArray()[0]!["peso"] = 3.5);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    /// <summary>
    /// Sem a guarda de não-vazio, isto <b>lançaria</b> dentro de
    /// <c>EtapaProcesso.Reidratar</c> — 500 no meio de um descarte, em vez de recusa.
    /// </summary>
    [Theory(DisplayName = "Texto vazio onde o caminho de escrita exige não-branco é RECUSADO")]
    [InlineData("etapas.0.nome")]
    [InlineData("atendimento.condicoes.0.condicaoCodigo")]
    [InlineData("atendimento.condicoes.0.condicaoNome")]
    [InlineData("atendimento.recursos.0.recursoCodigo")]
    [InlineData("atendimento.recursos.0.recursoNome")]
    [InlineData("atendimento.tiposDeficiencia.0.tipoDeficienciaNome")]
    [InlineData("modalidades.0.codigo")]
    [InlineData("modalidades.0.baseLegal")]
    [InlineData("distribuicao.0.referenciaDemografica.censoReferencia")]
    [InlineData("distribuicao.0.referenciaDemografica.baseLegal")]
    [InlineData("distribuicao.0.regraDistribuicao.versao")]
    // As duas primeiras do predicado de fato (fato/operador) passam por leitor.TextoNaoVazio,
    // que recusa em branco ANTES de chegar a CondicaoDnf.Criar — mesma família de erro
    // (EnvelopeMalformado) dos demais campos deste teorema.
    [InlineData("criteriosDesempate.3.parametros.fato")]
    [InlineData("criteriosDesempate.3.parametros.operador")]
    public void TextoVazio_Recusa(string caminho)
    {
        Result<EnvelopeReidratado> resultado = Reidratar(envelope => Definir(envelope, caminho, "   "));

        resultado.IsFailure.Should().BeTrue(
            $"'{caminho}' é exigido não-branco pelo caminho de escrita, mas o domínio não o valida — e o encoder " +
            "reemite a string vazia tal qual, de modo que o round-trip PASSA. Sem a guarda no leitor, o descarte " +
            "restauraria configuração inválida (ou explodiria numa factory como 500).");
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }
}
