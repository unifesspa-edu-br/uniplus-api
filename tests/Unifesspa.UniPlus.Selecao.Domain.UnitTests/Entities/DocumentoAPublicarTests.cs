namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.IO.Compression;
using System.Text;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O documento que o processo envia para publicar: PDF, DOCX ou ODT, conferido pelo conteúdo e não
/// pelo que o cliente declara, com o nome que a pessoa recebe no download. Confirmado, é imutável.
/// </summary>
public sealed class DocumentoAPublicarTests
{
    private static readonly Guid ProcessoSeletivoId = Guid.CreateVersion7();
    private static readonly TimeProvider Relogio = TimeProvider.System;
    private static readonly byte[] ConteudoPdfValido = [.. "%PDF-1.7 conteúdo qualquer"u8];

    [Fact(DisplayName = "IniciarPendente cria o registro em Pendente com ExpiraEm = agora + ttl, formato e nome")]
    public void IniciarPendente_CriaRegistroPendente()
    {
        TimeSpan ttl = TimeSpan.FromMinutes(15);

        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(
            ProcessoSeletivoId, FinalidadeDoDocumento.DocumentoDoEdital, "Edital 2027", TiposDeMidiaDeDocumento.Pdf, Relogio, ttl).Value!;

        documento.ProcessoSeletivoId.Should().Be(ProcessoSeletivoId);
        documento.Status.Should().Be(StatusArquivoEnviado.Pendente);
        documento.Formato.Should().Be(FormatoDeDocumento.Pdf);
        documento.NomeArquivo.Should().Be("Edital 2027.pdf");
        documento.ExpiraEm.Should().BeCloseTo(Relogio.GetUtcNow().Add(ttl), TimeSpan.FromSeconds(5));
        documento.HashSha256.Should().BeNull();
        documento.TamanhoBytes.Should().BeNull();
        documento.ConfirmadoEm.Should().BeNull();
        documento.ObjectKeyConfirmado.Should().BeNull();
    }

    [Theory(DisplayName = "O edital só admite PDF; o anexo e o documento de ato admitem PDF, DOCX e ODT")]
    [InlineData(FinalidadeDoDocumento.DocumentoDoEdital, TiposDeMidiaDeDocumento.Pdf, true)]
    [InlineData(FinalidadeDoDocumento.DocumentoDoEdital, TiposDeMidiaDeDocumento.Docx, false)]
    [InlineData(FinalidadeDoDocumento.DocumentoDoEdital, TiposDeMidiaDeDocumento.Odt, false)]
    [InlineData(FinalidadeDoDocumento.AnexoDoEdital, TiposDeMidiaDeDocumento.Pdf, true)]
    [InlineData(FinalidadeDoDocumento.AnexoDoEdital, TiposDeMidiaDeDocumento.Docx, true)]
    [InlineData(FinalidadeDoDocumento.AnexoDoEdital, TiposDeMidiaDeDocumento.Odt, true)]
    [InlineData(FinalidadeDoDocumento.DocumentoDeAto, TiposDeMidiaDeDocumento.Docx, true)]
    public void IniciarPendente_FormatoPorFinalidade(FinalidadeDoDocumento finalidade, string tipoDeMidia, bool aceito)
    {
        Result<DocumentoAPublicar> resultado = DocumentoAPublicar.IniciarPendente(
            ProcessoSeletivoId, finalidade, "documento", tipoDeMidia, Relogio, TimeSpan.FromMinutes(15));

        resultado.IsSuccess.Should().Be(aceito);
        if (aceito)
        {
            resultado.Value!.Finalidade.Should().Be(finalidade);
        }
        else
        {
            resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                Field = "tipoDeMidia",
                Error = new { Code = DocumentoAPublicarErrorCodes.FormatoNaoAceitoParaAFinalidade },
            });
        }
    }

    [Theory(DisplayName = "Finalidade ausente ou fora do catálogo é recusada")]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(99)]
    public void IniciarPendente_FinalidadeInvalida_Recusa(int? valor)
    {
        Result<DocumentoAPublicar> resultado = DocumentoAPublicar.IniciarPendente(
            ProcessoSeletivoId, (FinalidadeDoDocumento?)valor, "documento", TiposDeMidiaDeDocumento.Pdf, Relogio, TimeSpan.FromMinutes(15));

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "finalidade",
            Error = new { Code = DocumentoAPublicarErrorCodes.FinalidadeInvalida },
        });
    }

    [Theory(DisplayName = "O tipo de mídia define o formato e a extensão da chave de envio e da chave selada")]
    [InlineData(TiposDeMidiaDeDocumento.Pdf, FormatoDeDocumento.Pdf, "pdf")]
    [InlineData(TiposDeMidiaDeDocumento.Docx, FormatoDeDocumento.Docx, "docx")]
    [InlineData(TiposDeMidiaDeDocumento.Odt, FormatoDeDocumento.Odt, "odt")]
    public void IniciarPendente_FormatoPeloTipoDeMidia(string tipoDeMidia, FormatoDeDocumento formato, string extensao)
    {
        DocumentoAPublicar documento = Pendente(tipoDeMidia);
        documento.Confirmar(10, new string('a', 64), Relogio);

        documento.Formato.Should().Be(formato);
        documento.TipoDeMidia.Should().Be(tipoDeMidia);
        documento.ObjectKey.Should().EndWith($".{extensao}");
        documento.ObjectKeyConfirmado.Should().EndWith($"/confirmado.{extensao}");
        documento.NomeArquivo.Should().EndWith($".{extensao}");
    }

    [Theory(DisplayName = "Tipo de mídia fora da lista é recusado")]
    [InlineData("image/png")]
    [InlineData("application/msword")]
    [InlineData("application/pdf; charset=utf-8")]
    [InlineData("")]
    [InlineData(null)]
    public void IniciarPendente_TipoDeMidiaForaDaLista_Recusa(string? tipoDeMidia)
    {
        Result<DocumentoAPublicar> resultado = DocumentoAPublicar.IniciarPendente(
            ProcessoSeletivoId, FinalidadeDoDocumento.AnexoDoEdital, "anexo", tipoDeMidia, Relogio, TimeSpan.FromMinutes(15));

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "tipoDeMidia",
            Error = new { Code = DocumentoAPublicarErrorCodes.TipoDeMidiaNaoAceito },
        });
    }

    [Theory(DisplayName = "Nome vazio, com separador de caminho ou caractere proibido é recusado")]
    [InlineData(" ")]
    [InlineData("../anexo")]
    [InlineData("anexo:1")]
    [InlineData("anexo\u0001")]
    public void IniciarPendente_NomeInvalido_Recusa(string nome)
    {
        Result<DocumentoAPublicar> resultado = DocumentoAPublicar.IniciarPendente(
            ProcessoSeletivoId, FinalidadeDoDocumento.AnexoDoEdital, nome, TiposDeMidiaDeDocumento.Docx, Relogio, TimeSpan.FromMinutes(15));

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "nomeArquivo",
            Error = new { Code = DocumentoAPublicarErrorCodes.NomeArquivoInvalido },
        });
    }

    [Fact(DisplayName = "Tipo de mídia e nome inválidos são recusados juntos")]
    public void IniciarPendente_TipoDeMidiaENomeInvalidos_RecusasAcumulam()
    {
        Result<DocumentoAPublicar> resultado = DocumentoAPublicar.IniciarPendente(
            ProcessoSeletivoId, FinalidadeDoDocumento.AnexoDoEdital, "", "image/png", Relogio, TimeSpan.FromMinutes(15));

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("tipoDeMidia", DocumentoAPublicarErrorCodes.TipoDeMidiaNaoAceito),
            ("nomeArquivo", DocumentoAPublicarErrorCodes.NomeArquivoInvalido),
        ]);
    }

    [Fact(DisplayName = "Confirmar a partir de Pendente finaliza o documento como imutável")]
    public void Confirmar_APartirDePendente_Finaliza()
    {
        DocumentoAPublicar documento = Pendente(TiposDeMidiaDeDocumento.Pdf);

        Result resultado = documento.Confirmar(1024, "hash-sha256-fake", Relogio);

        resultado.IsSuccess.Should().BeTrue();
        documento.Status.Should().Be(StatusArquivoEnviado.Confirmado);
        documento.TamanhoBytes.Should().Be(1024);
        documento.HashSha256.Should().Be("hash-sha256-fake");
        documento.ConfirmadoEm.Should().BeCloseTo(Relogio.GetUtcNow(), TimeSpan.FromSeconds(5));
    }

    [Fact(DisplayName = "Confirmar um documento já confirmado é recusado (imutabilidade)")]
    public void Confirmar_DocumentoJaConfirmado_Recusa()
    {
        DocumentoAPublicar documento = Pendente(TiposDeMidiaDeDocumento.Pdf);
        documento.Confirmar(1024, "hash-original", Relogio);

        Result resultado = documento.Confirmar(2048, "hash-novo", Relogio);

        resultado.Error!.Code.Should().Be(DocumentoAPublicarErrorCodes.StatusInvalidoParaConfirmacao);
        documento.TamanhoBytes.Should().Be(1024);
        documento.HashSha256.Should().Be("hash-original");
    }

    [Theory(DisplayName = "PDF com a assinatura, DOCX e ODT de texto, cada um com o tipo de mídia do formato, são aceitos")]
    [InlineData(TiposDeMidiaDeDocumento.Pdf)]
    [InlineData(TiposDeMidiaDeDocumento.Docx)]
    [InlineData(TiposDeMidiaDeDocumento.Odt)]
    public void ValidarConteudo_ArquivoDoFormato_Aceita(string tipoDeMidia)
    {
        byte[] conteudo = ConteudoValido(tipoDeMidia);

        Pendente(tipoDeMidia).ValidarConteudo(conteudo.Length, tipoDeMidia, conteudo).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "O DOCX montado para os casos de XML malformado, com os dois XMLs válidos, é aceito")]
    public void ValidarConteudo_DocxMontadoComXmlsValidos_Aceita()
    {
        byte[] conteudo = Docx(ContentTypesValido, RelsValido);

        Pendente(TiposDeMidiaDeDocumento.Docx).ValidarConteudo(conteudo.Length, TiposDeMidiaDeDocumento.Docx, conteudo).IsSuccess.Should().BeTrue();
    }

    public static TheoryData<string, string, byte[], string> Recusados => new()
    {
        { TiposDeMidiaDeDocumento.Pdf, TiposDeMidiaDeDocumento.Pdf, [.. "não é um pdf de verdade"u8], DocumentoAPublicarErrorCodes.ConteudoDivergeDoFormato },
        { TiposDeMidiaDeDocumento.Pdf, "image/png", ConteudoPdfValido, DocumentoAPublicarErrorCodes.ConteudoDivergeDoFormato },
        { TiposDeMidiaDeDocumento.Docx, TiposDeMidiaDeDocumento.Docx, ArquivosDeModeloDeTeste.Pdf(), DocumentoAPublicarErrorCodes.ConteudoDivergeDoFormato },
        { TiposDeMidiaDeDocumento.Docx, TiposDeMidiaDeDocumento.Docx, ArquivosDeModeloDeTeste.PlanilhaComoDocx(), DocumentoAPublicarErrorCodes.ConteudoDivergeDoFormato },
        { TiposDeMidiaDeDocumento.Docx, TiposDeMidiaDeDocumento.Docx, DocxComContentTypesMalFormado(), DocumentoAPublicarErrorCodes.ConteudoDivergeDoFormato },
        { TiposDeMidiaDeDocumento.Docx, TiposDeMidiaDeDocumento.Docx, DocxComRelsMalFormado(), DocumentoAPublicarErrorCodes.ConteudoDivergeDoFormato },
        { TiposDeMidiaDeDocumento.Docx, TiposDeMidiaDeDocumento.Docx, ArquivosDeModeloDeTeste.DocxComMacro(), DocumentoAPublicarErrorCodes.ContemMacro },
        { TiposDeMidiaDeDocumento.Odt, TiposDeMidiaDeDocumento.Odt, ArquivosDeModeloDeTeste.Docx(), DocumentoAPublicarErrorCodes.ConteudoDivergeDoFormato },
        { TiposDeMidiaDeDocumento.Odt, TiposDeMidiaDeDocumento.Odt, ArquivosDeModeloDeTeste.Odt("application/vnd.oasis.opendocument.spreadsheet"), DocumentoAPublicarErrorCodes.ConteudoDivergeDoFormato },
        { TiposDeMidiaDeDocumento.Odt, TiposDeMidiaDeDocumento.Odt, ArquivosDeModeloDeTeste.Odt(entradaExtra: "Scripts/macro.py"), DocumentoAPublicarErrorCodes.ContemMacro },
    };

    [Theory(DisplayName = "PDF sem %PDF-, tipo de mídia que o objeto não declara, pacote que não é do formato, XML malformado e pacote com macro são recusados")]
    [MemberData(nameof(Recusados))]
    public void ValidarConteudo_ForaDoFormato_Recusa(string tipoDeMidiaDeclarado, string tipoDeMidiaDoObjeto, byte[] conteudo, string codigo)
    {
        ArgumentNullException.ThrowIfNull(conteudo);

        Pendente(tipoDeMidiaDeclarado).ValidarConteudo(conteudo.Length, tipoDeMidiaDoObjeto, conteudo).Error!.Code.Should().Be(codigo);
    }

    [Theory(DisplayName = "O arquivo vazio é recusado em qualquer formato")]
    [InlineData(TiposDeMidiaDeDocumento.Pdf)]
    [InlineData(TiposDeMidiaDeDocumento.Docx)]
    [InlineData(TiposDeMidiaDeDocumento.Odt)]
    public void ValidarConteudo_ArquivoVazio_Recusa(string tipoDeMidia)
    {
        Pendente(tipoDeMidia).ValidarConteudo(0, tipoDeMidia, []).Error!.Code
            .Should().Be(DocumentoAPublicarErrorCodes.ArquivoVazio);
    }

    [Fact(DisplayName = "O arquivo acima de 20 MB é recusado e o de exatamente 20 MB passa do tamanho")]
    public void ValidarConteudo_Tamanho_LimiteDeVinteMegabytes()
    {
        DocumentoAPublicar documento = Pendente(TiposDeMidiaDeDocumento.Pdf);

        documento.ValidarConteudo(DocumentoAPublicar.TamanhoMaximoBytes + 1, TiposDeMidiaDeDocumento.Pdf, ConteudoPdfValido).Error!.Code
            .Should().Be(DocumentoAPublicarErrorCodes.TamanhoExcedido);
        documento.ValidarConteudo(DocumentoAPublicar.TamanhoMaximoBytes, TiposDeMidiaDeDocumento.Pdf, ConteudoPdfValido).IsSuccess
            .Should().BeTrue();
    }

    private static DocumentoAPublicar Pendente(string tipoDeMidia) =>
        DocumentoAPublicar.IniciarPendente(ProcessoSeletivoId, FinalidadeDoDocumento.AnexoDoEdital, "anexo", tipoDeMidia, Relogio, TimeSpan.FromMinutes(15)).Value!;

    private static byte[] ConteudoValido(string tipoDeMidia) => tipoDeMidia switch
    {
        TiposDeMidiaDeDocumento.Docx => ArquivosDeModeloDeTeste.Docx(),
        TiposDeMidiaDeDocumento.Odt => ArquivosDeModeloDeTeste.Odt(),
        _ => ConteudoPdfValido,
    };

    /// <summary>
    /// Um DOCX completo — <c>_rels/.rels</c> aponta para <c>word/document.xml</c>, que existe — em
    /// que só o <c>[Content_Types].xml</c> não é XML bem formado: a recusa só pode vir da leitura
    /// do XML, e não da falta de uma parte.
    /// </summary>
    private static byte[] DocxComContentTypesMalFormado() => Docx(contentTypes: "<Types><Override", rels: RelsValido);

    /// <summary>O caso simétrico: o <c>[Content_Types].xml</c> é válido e o <c>_rels/.rels</c> não.</summary>
    private static byte[] DocxComRelsMalFormado() => Docx(contentTypes: ContentTypesValido, rels: "<Relationships><Relationship");

    private const string RelsValido =
        """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>""";

    private const string ContentTypesValido =
        """<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>""";

    private static byte[] Docx(string contentTypes, string rels)
    {
        using MemoryStream saida = new();
        using (ZipArchive pacote = new(saida, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string nome, string conteudo) in new[] { ("[Content_Types].xml", contentTypes), ("_rels/.rels", rels), ("word/document.xml", "<document/>") })
            {
                using StreamWriter escritor = new(pacote.CreateEntry(nome).Open(), Encoding.UTF8);
                escritor.Write(conteudo);
            }
        }

        return saida.ToArray();
    }
}
