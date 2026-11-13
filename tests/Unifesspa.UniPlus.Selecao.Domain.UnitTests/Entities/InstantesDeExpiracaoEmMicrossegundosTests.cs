namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O instante de expiração que a entidade deriva de um prazo recebido por parâmetro fica em microssegundos inteiros,
/// a precisão do banco, ainda que o relógio e o prazo tragam ticks abaixo do microssegundo.
/// </summary>
public sealed class InstantesDeExpiracaoEmMicrossegundosTests
{
    private static readonly DateTimeOffset Meio = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset AgoraComSetimaCasa = Meio.AddTicks(1_234_567);
    private static readonly TimeSpan PrazoComResto = TimeSpan.FromTicks((15 * TimeSpan.TicksPerMinute) + 7);
    private static readonly DateTimeOffset VencimentoEsperado = Meio + TimeSpan.FromMinutes(15) + TimeSpan.FromTicks(1_234_570);

    private static RelogioFixo Relogio => new(AgoraComSetimaCasa);

    private static DocumentoExigido Exigencia() => DocumentoExigido.Criar(
        Guid.CreateVersion7(), Guid.CreateVersion7(), "LAUDO", "Laudo médico", "SAUDE", ProtecaoDoDocumentoDeTeste.Pessoal,
        Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null,
        [], [], null, FormatosPermitidos.Criar(true, null).Value!, null).Value!;

    private static ArquivoDaApresentacao ArquivoPendente() => ArquivoDaApresentacao.IniciarPendente(
        Guid.CreateVersion7(), Guid.CreateVersion7(), Exigencia(), "application/pdf", 1024, Relogio, PrazoComResto).Value!;

    [Fact(DisplayName = "O arquivo do candidato pendente vence em microssegundos inteiros")]
    public void ArquivoDaApresentacao_IniciarPendente_VenceEmMicrossegundos() =>
        ArquivoPendente().ExpiraEm.Should().Be(VencimentoEsperado);

    [Fact(DisplayName = "O modelo de documento pendente vence em microssegundos inteiros")]
    public void ModeloDeDocumento_IniciarPendente_VenceEmMicrossegundos()
    {
        ModeloDeDocumento modelo = ModeloDeDocumento.IniciarPendente(
            Guid.CreateVersion7(), "modelo.docx", "DOCX", Relogio, PrazoComResto).Value!;

        modelo.ExpiraEm.Should().Be(VencimentoEsperado);
    }

    [Fact(DisplayName = "O documento a publicar pendente vence em microssegundos inteiros")]
    public void DocumentoAPublicar_IniciarPendente_VenceEmMicrossegundos()
    {
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(
            Guid.CreateVersion7(), FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, Relogio, PrazoComResto).Value!;

        documento.ExpiraEm.Should().Be(VencimentoEsperado);
    }

    [Fact(DisplayName = "O rascunho da publicação vence em microssegundos inteiros ao ser criado")]
    public void RascunhoDePublicacao_Criar_VenceEmMicrossegundos()
    {
        RascunhoDePublicacao rascunho = RascunhoDePublicacao.Criar(
            Guid.CreateVersion7(), "sub-123", "{}", 1, AgoraComSetimaCasa, PrazoComResto).Value!;

        rascunho.ExpiraEm.Should().Be(VencimentoEsperado);
    }

    [Fact(DisplayName = "O rascunho da publicação regravado renova o vencimento em microssegundos inteiros")]
    public void RascunhoDePublicacao_Substituir_VenceEmMicrossegundos()
    {
        RascunhoDePublicacao rascunho = RascunhoDePublicacao.Criar(
            Guid.CreateVersion7(), "sub-123", "{}", 1, Meio, TimeSpan.FromDays(1)).Value!;

        rascunho.Substituir("{}", 1, AgoraComSetimaCasa, PrazoComResto).IsSuccess.Should().BeTrue();

        rascunho.ExpiraEm.Should().Be(VencimentoEsperado);
    }

    [Fact(DisplayName = "A próxima publicação do arquivo confirmado sai em microssegundos inteiros com a política de prazo com resto")]
    public void ArquivoDaApresentacao_Confirmar_ProximaPublicacaoEmMicrossegundos()
    {
        ArquivoDaApresentacao arquivo = ArquivoPendente();
        PoliticaDeRepublicacao politica = new(TimeSpan.FromTicks(6_000_000_003), TimeSpan.FromHours(6));

        arquivo.Confirmar("\"etag\"", new RelogioFixo(Meio), politica).IsSuccess.Should().BeTrue();

        arquivo.ProximaPublicacaoEm.Should().Be(Meio + TimeSpan.FromMinutes(10));
    }

    [Fact(DisplayName = "A republicação do arquivo em verificação agenda a próxima em microssegundos inteiros")]
    public void ArquivoDaApresentacao_RegistrarPublicacao_ProximaPublicacaoEmMicrossegundos()
    {
        ArquivoDaApresentacao arquivo = ArquivoPendente();
        PoliticaDeRepublicacao politica = new(TimeSpan.FromTicks(6_000_000_003), TimeSpan.FromHours(6));
        arquivo.Confirmar("\"etag\"", new RelogioFixo(Meio), politica).IsSuccess.Should().BeTrue();

        arquivo.RegistrarPublicacao(new RelogioFixo(Meio), politica);

        arquivo.ProximaPublicacaoEm.Should().Be(Meio + TimeSpan.FromMinutes(20));
    }
}
