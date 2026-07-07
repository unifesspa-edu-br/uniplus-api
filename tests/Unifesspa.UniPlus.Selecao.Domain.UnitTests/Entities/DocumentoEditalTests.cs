namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Cobertura das invariantes de <see cref="DocumentoEdital"/> (Story #759, T3
/// #784): ciclo de vida pendente → confirmado e validação de conteúdo
/// (content-type, tamanho, assinatura PDF) na confirmação.
/// </summary>
public sealed class DocumentoEditalTests
{
    private static readonly Guid ProcessoSeletivoId = Guid.CreateVersion7();
    private static readonly byte[] ConteudoPdfValido = [.. "%PDF-1.7 conteúdo qualquer"u8];

    [Fact(DisplayName = "IniciarPendente cria o registro em Pendente com ExpiraEm = agora + ttl")]
    public void IniciarPendente_CriaRegistroPendente()
    {
        TimeProvider clock = TimeProvider.System;
        TimeSpan ttl = TimeSpan.FromMinutes(15);

        DocumentoEdital documento = DocumentoEdital.IniciarPendente(
            ProcessoSeletivoId, clock, ttl);

        documento.ProcessoSeletivoId.Should().Be(ProcessoSeletivoId);
        documento.Status.Should().Be(StatusDocumentoEdital.Pendente);
        documento.ExpiraEm.Should().BeCloseTo(clock.GetUtcNow().Add(ttl), TimeSpan.FromSeconds(5));
        documento.HashSha256.Should().BeNull();
        documento.TamanhoBytes.Should().BeNull();
        documento.ConfirmadoEm.Should().BeNull();
    }

    [Fact(DisplayName = "Confirmar a partir de Pendente finaliza o documento como imutável (CA)")]
    public void Confirmar_APartirDePendente_Finaliza()
    {
        TimeProvider clock = TimeProvider.System;
        DocumentoEdital documento = DocumentoEdital.IniciarPendente(
            ProcessoSeletivoId, clock, TimeSpan.FromMinutes(15));

        Result resultado = documento.Confirmar(1024, "hash-sha256-fake", clock);

        resultado.IsSuccess.Should().BeTrue();
        documento.Status.Should().Be(StatusDocumentoEdital.Confirmado);
        documento.TamanhoBytes.Should().Be(1024);
        documento.HashSha256.Should().Be("hash-sha256-fake");
        documento.ConfirmadoEm.Should().BeCloseTo(clock.GetUtcNow(), TimeSpan.FromSeconds(5));
    }

    [Fact(DisplayName = "Confirmar um documento já confirmado é recusado (imutabilidade)")]
    public void Confirmar_DocumentoJaConfirmado_Recusa()
    {
        TimeProvider clock = TimeProvider.System;
        DocumentoEdital documento = DocumentoEdital.IniciarPendente(
            ProcessoSeletivoId, clock, TimeSpan.FromMinutes(15));
        documento.Confirmar(1024, "hash-original", clock);

        Result resultado = documento.Confirmar(2048, "hash-novo", clock);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoEdital.StatusInvalidoParaConfirmacao");
        // Confirmação recusada não deve mutar o registro já confirmado.
        documento.TamanhoBytes.Should().Be(1024);
        documento.HashSha256.Should().Be("hash-original");
    }

    [Fact(DisplayName = "ValidarConteudo aceita PDF dentro do limite com assinatura correta")]
    public void ValidarConteudo_PdfValido_Aceita()
    {
        Result resultado = DocumentoEdital.ValidarConteudo(ConteudoPdfValido.Length, "application/pdf", ConteudoPdfValido);

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "ValidarConteudo recusa tamanho acima do limite máximo (422)")]
    public void ValidarConteudo_TamanhoExcedido_Recusa()
    {
        Result resultado = DocumentoEdital.ValidarConteudo(
            DocumentoEdital.TamanhoMaximoBytes + 1, "application/pdf", ConteudoPdfValido);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoEdital.TamanhoExcedido");
    }

    [Fact(DisplayName = "ValidarConteudo recusa content-type diferente de application/pdf (422)")]
    public void ValidarConteudo_ContentTypeInvalido_Recusa()
    {
        Result resultado = DocumentoEdital.ValidarConteudo(
            ConteudoPdfValido.Length, "image/png", ConteudoPdfValido);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoEdital.ContentTypeInvalido");
    }

    [Fact(DisplayName = "ValidarConteudo recusa assinatura (magic bytes) que não é %PDF- (422)")]
    public void ValidarConteudo_AssinaturaInvalida_Recusa()
    {
        byte[] conteudoFalso = [.. "não é um pdf de verdade"u8];

        Result resultado = DocumentoEdital.ValidarConteudo(
            conteudoFalso.Length, "application/pdf", conteudoFalso);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoEdital.AssinaturaInvalida");
    }

    [Fact(DisplayName = "ValidarConteudo recusa conteúdo vazio (assinatura ausente)")]
    public void ValidarConteudo_ConteudoVazio_Recusa()
    {
        Result resultado = DocumentoEdital.ValidarConteudo(0, "application/pdf", ReadOnlySpan<byte>.Empty);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoEdital.AssinaturaInvalida");
    }
}
