namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O arquivo que o candidato envia para uma exigência: o formato e o tamanho vêm da exigência congelada,
/// o conteúdo é conferido pela assinatura dos primeiros bytes e as chaves do armazenamento levam só o id.
/// </summary>
public sealed class ArquivoDaApresentacaoTests
{
    private const long MegaByte = 1024 * 1024;

    private static readonly byte[] Pdf = [.. "%PDF-1.7 laudo"u8];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];

    private static DocumentoExigido Exigencia(
        IReadOnlyList<(string Formato, int? Teto)>? formatos = null, int? tamanhoMaximoBytes = null) => DocumentoExigido.Criar(
        Guid.CreateVersion7(), Guid.CreateVersion7(), "LAUDO", "Laudo médico", "SAUDE",
        Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null,
        [], [], null, FormatosPermitidos.Criar(formatos is null, formatos).Value!, tamanhoMaximoBytes).Value!;

    private static Result<ArquivoDaApresentacao> Iniciar(DocumentoExigido exigencia, string? tipoDeMidia, long tamanhoBytes) =>
        ArquivoDaApresentacao.IniciarPendente(
            Guid.CreateVersion7(), Guid.CreateVersion7(), exigencia, tipoDeMidia, tamanhoBytes, TimeProvider.System, TimeSpan.FromMinutes(15));

    [Fact(DisplayName = "Formato que a exigência não aceita é recusado já no início do envio")]
    public void IniciarPendente_FormatoForaDaExigencia_Recusa()
    {
        Result<ArquivoDaApresentacao> resultado = Iniciar(Exigencia([("PDF", null)]), "image/png", 1024);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle(e => e.Error.Code == DocumentoDaInscricaoErrorCodes.FormatoNaoAceitoPelaExigencia);
    }

    [Theory(DisplayName = "A exigência com formato Qualquer aceita exatamente PDF, JPEG e PNG")]
    [InlineData("application/pdf", true)]
    [InlineData("image/jpeg", true)]
    [InlineData("image/png", true)]
    [InlineData("image/gif", false)]
    [InlineData("application/zip", false)]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document", false)]
    public void IniciarPendente_QualquerFormato_AceitaSoPdfJpegEPng(string tipoDeMidia, bool aceito) =>
        Iniciar(Exigencia(), tipoDeMidia, 1024).IsSuccess.Should().Be(aceito);

    [Theory(DisplayName = "O limite é o da exigência (o menor entre o do formato e o global); o teto de 10 MB só vale quando ela não define nenhum")]
    [InlineData(null, null, 10 * MegaByte, 10 * MegaByte + 1)]
    [InlineData(2 * (int)MegaByte, null, 2 * MegaByte, 2 * MegaByte + 1)]
    [InlineData(20 * (int)MegaByte, null, 20 * MegaByte, 20 * MegaByte + 1)]
    [InlineData(5 * (int)MegaByte, 1 * (int)MegaByte, 1 * MegaByte, 1 * MegaByte + 1)]
    public void IniciarPendente_Tamanho_RespeitaOMenorLimite(int? tetoDaExigencia, int? tetoDoFormato, long cabe, long naoCabe)
    {
        DocumentoExigido exigencia = Exigencia([("PDF", tetoDoFormato)], tetoDaExigencia);

        Iniciar(exigencia, "application/pdf", cabe).IsSuccess.Should().BeTrue();
        Iniciar(exigencia, "application/pdf", naoCabe).Errors
            .Should().ContainSingle(e => e.Error.Code == DocumentoDaInscricaoErrorCodes.TamanhoExcedido);
    }

    [Fact(DisplayName = "As chaves do armazenamento levam só o id do arquivo")]
    public void Chaves_LevamSoOIdDoArquivo()
    {
        ArquivoDaApresentacao arquivo = Iniciar(Exigencia(), "application/pdf", 1024).Value!;
        arquivo.Confirmar(1024, new string('a', 64), TimeProvider.System).IsSuccess.Should().BeTrue();

        arquivo.ObjectKey.Should().Be($"selecao/documentos-da-inscricao/{arquivo.Id:D}/envio");
        arquivo.ObjectKeyConfirmado.Should().Be($"selecao/documentos-da-inscricao/{arquivo.Id:D}/selado");
    }

    [Theory(DisplayName = "O conteúdo tem de começar pela assinatura do formato declarado, não só declarar o tipo de mídia")]
    [InlineData("application/pdf", "application/pdf", true)]
    [InlineData("image/jpeg", "image/jpeg", true)]
    [InlineData("image/png", "image/png", true)]
    [InlineData("application/pdf", "image/jpeg", false)]
    [InlineData("image/png", "application/pdf", false)]
    [InlineData("image/jpeg", "image/png", false)]
    public void ValidarConteudo_ConfereAAssinaturaDoFormatoDeclarado(string declarado, string conteudoDe, bool aceito)
    {
        byte[] conteudo = conteudoDe switch { "application/pdf" => Pdf, "image/jpeg" => Jpeg, _ => Png };
        ArquivoDaApresentacao arquivo = Iniciar(Exigencia(), declarado, conteudo.Length).Value!;

        Result resultado = arquivo.ValidarConteudo(conteudo.Length, declarado, conteudo);

        resultado.IsSuccess.Should().Be(aceito);
        if (!aceito)
        {
            resultado.Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.ConteudoNaoConfereComFormato);
        }
    }

    [Fact(DisplayName = "Arquivo que passou do limite entre o início e a confirmação é recusado pelo tamanho lido")]
    public void ValidarConteudo_TamanhoLidoAcimaDoLimite_Recusa()
    {
        ArquivoDaApresentacao arquivo = Iniciar(Exigencia(tamanhoMaximoBytes: (int)MegaByte), "application/pdf", 1024).Value!;

        arquivo.ValidarConteudo(MegaByte + 1, "application/pdf", Pdf).Error!.Code
            .Should().Be(DocumentoDaInscricaoErrorCodes.TamanhoExcedido);
    }
}
