namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Cobertura de <see cref="DocumentoExigidoBaseLegal"/> (Story #554, PR-c, issue #549,
/// ADR-0074): fábrica — CA-01 (referência não vazia, abrangência/status no domínio).
/// </summary>
public sealed class DocumentoExigidoBaseLegalTests
{
    [Fact(DisplayName = "CA-01: Criar aceita referência, abrangência e status coerentes")]
    public void Criar_DadosCoerentes_Aceita()
    {
        Result<DocumentoExigidoBaseLegal> resultado = DocumentoExigidoBaseLegal.Criar(
            "Lei 12.711/2012, art. 3º", TipoAbrangencia.Federal, StatusBaseLegal.Resolvido, "Observação livre");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Referencia.Should().Be("Lei 12.711/2012, art. 3º");
        resultado.Value.Abrangencia.Should().Be(TipoAbrangencia.Federal);
        resultado.Value.Status.Should().Be(StatusBaseLegal.Resolvido);
        resultado.Value.Observacao.Should().Be("Observação livre");
    }

    [Fact(DisplayName = "CA-01: referência vazia é recusada")]
    public void Criar_ReferenciaVazia_Recusa()
    {
        Result<DocumentoExigidoBaseLegal> resultado = DocumentoExigidoBaseLegal.Criar(
            "", TipoAbrangencia.Federal, StatusBaseLegal.Resolvido, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigidoBaseLegal.ReferenciaObrigatoria");
    }

    [Fact(DisplayName = "CA-01: referência só de espaços é recusada")]
    public void Criar_ReferenciaSoDeEspacos_Recusa()
    {
        Result<DocumentoExigidoBaseLegal> resultado = DocumentoExigidoBaseLegal.Criar(
            "   ", TipoAbrangencia.Federal, StatusBaseLegal.Resolvido, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigidoBaseLegal.ReferenciaObrigatoria");
    }

    [Fact(DisplayName = "Abrangência Nenhuma é recusada")]
    public void Criar_AbrangenciaNenhuma_Recusa()
    {
        Result<DocumentoExigidoBaseLegal> resultado = DocumentoExigidoBaseLegal.Criar(
            "Lei X", TipoAbrangencia.Nenhuma, StatusBaseLegal.Resolvido, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigidoBaseLegal.AbrangenciaObrigatoria");
    }

    [Fact(DisplayName = "Status Nenhuma é recusado")]
    public void Criar_StatusNenhuma_Recusa()
    {
        Result<DocumentoExigidoBaseLegal> resultado = DocumentoExigidoBaseLegal.Criar(
            "Lei X", TipoAbrangencia.Federal, StatusBaseLegal.Nenhuma, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigidoBaseLegal.StatusObrigatorio");
    }

    [Theory(DisplayName = "Todas as abrangências e status válidos são aceitos")]
    [InlineData(TipoAbrangencia.Federal, StatusBaseLegal.Pendente)]
    [InlineData(TipoAbrangencia.Estadual, StatusBaseLegal.Resolvido)]
    [InlineData(TipoAbrangencia.Municipal, StatusBaseLegal.Pendente)]
    [InlineData(TipoAbrangencia.InternaNorma, StatusBaseLegal.Resolvido)]
    [InlineData(TipoAbrangencia.InternaEdital, StatusBaseLegal.Resolvido)]
    public void Criar_TodasCombinacoesValidas_Aceita(TipoAbrangencia abrangencia, StatusBaseLegal status) =>
        DocumentoExigidoBaseLegal.Criar("Referência", abrangencia, status, null).IsSuccess.Should().BeTrue();

    [Fact(DisplayName = "Observação em branco é normalizada para null")]
    public void Criar_ObservacaoEmBranco_NormalizaParaNull()
    {
        DocumentoExigidoBaseLegal baseLegal = DocumentoExigidoBaseLegal.Criar(
            "Lei X", TipoAbrangencia.Federal, StatusBaseLegal.Resolvido, "   ").Value!;

        baseLegal.Observacao.Should().BeNull();
    }
}
