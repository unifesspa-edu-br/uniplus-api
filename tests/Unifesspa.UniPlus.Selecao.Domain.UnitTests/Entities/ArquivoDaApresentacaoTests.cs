namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O arquivo que o candidato envia para uma exigência: o formato e o tamanho vêm da exigência congelada, a
/// confirmação o põe em verificação, só o primeiro resultado do verificador vale e as chaves do armazenamento
/// levam só o id.
/// </summary>
public sealed class ArquivoDaApresentacaoTests
{
    private const long MegaByte = 1024 * 1024;

    private static DocumentoExigido Exigencia(
        IReadOnlyList<(string Formato, int? Teto)>? formatos = null, int? tamanhoMaximoBytes = null) => DocumentoExigido.Criar(
        Guid.CreateVersion7(), Guid.CreateVersion7(), "LAUDO", "Laudo médico", "SAUDE", ProtecaoDoDocumentoDeTeste.Pessoal,
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

    [Theory(DisplayName = "O limite é o da exigência (o menor entre o do formato e o global); o teto de 10 MB só vale quando ela não define nenhum, e nada passa de 20 MB")]
    [InlineData(null, null, 10 * MegaByte, 10 * MegaByte + 1)]
    [InlineData(2 * (int)MegaByte, null, 2 * MegaByte, 2 * MegaByte + 1)]
    [InlineData(20 * (int)MegaByte, null, 20 * MegaByte, 20 * MegaByte + 1)]
    [InlineData(50 * (int)MegaByte, null, 20 * MegaByte, 20 * MegaByte + 1)]
    [InlineData(5 * (int)MegaByte, 1 * (int)MegaByte, 1 * MegaByte, 1 * MegaByte + 1)]
    public void IniciarPendente_Tamanho_RespeitaOMenorLimite(int? tetoDaExigencia, int? tetoDoFormato, long cabe, long naoCabe)
    {
        DocumentoExigido exigencia = Exigencia([("PDF", tetoDoFormato)], tetoDaExigencia);

        Iniciar(exigencia, "application/pdf", cabe).IsSuccess.Should().BeTrue();
        Iniciar(exigencia, "application/pdf", naoCabe).Errors
            .Should().ContainSingle(e => e.Error.Code == DocumentoDaInscricaoErrorCodes.TamanhoExcedido);
    }

    private static ArquivoDaApresentacao EmVerificacao()
    {
        ArquivoDaApresentacao arquivo = Iniciar(Exigencia(), "application/pdf", 1024).Value!;
        arquivo.Confirmar("\"etag-do-put\"", TimeProvider.System).IsSuccess.Should().BeTrue();
        return arquivo;
    }

    [Fact(DisplayName = "As chaves do armazenamento levam só o id do arquivo")]
    public void Chaves_LevamSoOIdDoArquivo()
    {
        ArquivoDaApresentacao arquivo = Iniciar(Exigencia(), "application/pdf", 1024).Value!;

        arquivo.ObjectKey.Should().Be($"selecao/documentos-da-inscricao/{arquivo.Id:D}/envio");
        arquivo.ChaveDefinitivaPrevista.Should().Be($"selecao/documentos-da-inscricao/{arquivo.Id:D}/definitivo");
    }

    [Fact(DisplayName = "Confirmar põe o arquivo em verificação com o ETag, e só o pendente pode ser confirmado")]
    public void Confirmar_PoeEmVerificacao_ESoUmaVez()
    {
        ArquivoDaApresentacao arquivo = EmVerificacao();

        arquivo.Situacao.Should().Be(SituacaoDoArquivoDoCandidato.EmVerificacao);
        arquivo.ETag.Should().Be("\"etag-do-put\"");
        arquivo.Publicacoes.Should().Be(1);
        arquivo.ProximaPublicacaoEm.Should().NotBeNull();
        arquivo.Confirmar("outro", TimeProvider.System).Error!.Code
            .Should().Be(DocumentoDaInscricaoErrorCodes.StatusInvalidoParaConfirmacao);
    }

    [Theory(DisplayName = "Confirmar recusa o ETag ausente ou em branco e deixa o arquivo pendente")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Confirmar_ETagEmBranco_Recusa(string? etag)
    {
        ArquivoDaApresentacao arquivo = Iniciar(Exigencia(), "application/pdf", 1024).Value!;

        arquivo.Confirmar(etag!, TimeProvider.System).Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.ETagObrigatorio);
        arquivo.Situacao.Should().Be(SituacaoDoArquivoDoCandidato.Pendente);
    }

    [Fact(DisplayName = "O prazo da próxima publicação dobra a cada uma, até o teto")]
    public void RegistrarPublicacao_PrazoCresce_ComTeto()
    {
        PoliticaDeRepublicacao politica = new(TimeSpan.FromMinutes(10), TimeSpan.FromHours(1));
        DateTimeOffset agora = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
        ArquivoDaApresentacao arquivo = Iniciar(Exigencia(), "application/pdf", 1024).Value!;
        arquivo.Confirmar("etag", new RelogioFixo(agora), politica).IsSuccess.Should().BeTrue();

        List<TimeSpan> prazos = [arquivo.ProximaPublicacaoEm!.Value - agora];
        for (int i = 0; i < 4; i++)
        {
            arquivo.RegistrarPublicacao(new RelogioFixo(agora), politica);
            prazos.Add(arquivo.ProximaPublicacaoEm!.Value - agora);
        }

        prazos.Should().Equal(
            TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(40), TimeSpan.FromHours(1), TimeSpan.FromHours(1));
        arquivo.Publicacoes.Should().Be(5);
    }

    [Theory(DisplayName = "O verificado diverge do declarado no formato, no tamanho e no SHA-256 que não tem 64 hexadecimais")]
    [InlineData(FormatoPermitido.Png, 1024, 'a', true)]
    [InlineData(FormatoPermitido.Pdf, 0, 'a', true)]
    [InlineData(FormatoPermitido.Pdf, 21 * MegaByte, 'a', true)]
    [InlineData(FormatoPermitido.Pdf, 1024, 'g', true)]
    [InlineData(FormatoPermitido.Pdf, 1024, 'a', false)]
    public void DivergenciaDoVerificado_ConfereFormatoTamanhoEHash(FormatoPermitido formato, long tamanho, char digito, bool diverge)
    {
        ArquivoDaApresentacao arquivo = EmVerificacao();

        string? divergencia = arquivo.DivergenciaDoVerificado(tamanho, new string(digito, 64), formato);

        (divergencia is not null).Should().Be(diverge);
    }

    [Fact(DisplayName = "Só o primeiro resultado vale: o segundo, ainda que de outro veredicto, não altera o arquivo")]
    public void Resultado_SoOPrimeiroPrevalece()
    {
        ArquivoDaApresentacao arquivo = EmVerificacao();

        arquivo.RegistrarVerificado(2048, new string('a', 64), FormatoPermitido.Pdf).Should().BeTrue();
        arquivo.RegistrarRecusa(MotivoDeRecusaDoArquivo.NaoPassouNaVerificacaoDeSeguranca).Should().BeFalse();
        arquivo.PedirReenvio().Should().BeFalse();

        arquivo.Situacao.Should().Be(SituacaoDoArquivoDoCandidato.Verificado);
        arquivo.TamanhoBytes.Should().Be(2048);
        arquivo.ChaveDefinitiva.Should().Be(arquivo.ChaveDefinitivaPrevista);
        arquivo.MensagemAoCandidato.Should().BeNull();
    }

    [Theory(DisplayName = "A mensagem de recusa diz o que corrigir e nunca nomeia a ameaça")]
    [InlineData(MotivoDeRecusaDoArquivo.TamanhoExcedido, "Este arquivo passa do tamanho máximo de 10 MB para este documento. Envie um arquivo menor.")]
    [InlineData(MotivoDeRecusaDoArquivo.FormatoNaoCorresponde, "O conteúdo deste arquivo não corresponde ao formato PDF. Envie o arquivo original.")]
    [InlineData(MotivoDeRecusaDoArquivo.PdfComSenha, "Este PDF está protegido por senha. Envie o arquivo sem senha.")]
    [InlineData(MotivoDeRecusaDoArquivo.NaoPassouNaVerificacaoDeSeguranca, "Este arquivo não passou na verificação de segurança (antivírus) e não foi aceito. Envie outro arquivo.")]
    public void Recusa_MensagemDaUniReq0027(MotivoDeRecusaDoArquivo motivo, string mensagem)
    {
        ArquivoDaApresentacao arquivo = EmVerificacao();

        arquivo.RegistrarRecusa(motivo).Should().BeTrue();

        arquivo.MensagemAoCandidato.Should().Be(mensagem);
    }

    [Fact(DisplayName = "Em verificação e a reenviar têm a mensagem própria da tela")]
    public void Mensagens_EmVerificacaoEReenvio()
    {
        ArquivoDaApresentacao arquivo = EmVerificacao();
        arquivo.MensagemAoCandidato.Should().Be("Arquivo em verificação. Isso costuma levar poucos segundos.");

        arquivo.PedirReenvio().Should().BeTrue();

        arquivo.MensagemAoCandidato.Should().Be("Não recebemos este arquivo. Envie-o novamente.");
    }
}
