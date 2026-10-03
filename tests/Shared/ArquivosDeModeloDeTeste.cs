namespace Unifesspa.UniPlus.Testes.Compartilhado;

using System.IO.Compression;
using System.Text;

/// <summary>
/// Pacotes DOCX e ODT mínimos, montados em memória, para os testes do modelo de documento: o de
/// texto válido e as variações que a validação recusa.
/// </summary>
public static class ArquivosDeModeloDeTeste
{
    public const string ContentTypeDocx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    public const string ContentTypeOdt = "application/vnd.oasis.opendocument.text";

    private static string ManifestoDocx(string parte, string contentType) => $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="{parte}" ContentType="{contentType}"/>
        </Types>
        """;

    private const string DocumentoPrincipalDoWord = "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";

    private static string Relacoes(string alvo) => $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="{alvo}"/>
        </Relationships>
        """;

    /// <summary>
    /// Um DOCX de texto: o documento principal, apontado por <c>_rels/.rels</c>, declarado em
    /// <c>[Content_Types].xml</c> com o tipo do Word. O nome da parte varia entre geradores.
    /// </summary>
    public static byte[] Docx(string parte = "word/document.xml", bool comAParte = true) =>
        Pacote(
            [
                ("[Content_Types].xml", ManifestoDocx("/" + parte, DocumentoPrincipalDoWord), CompressionLevel.Optimal),
                ("_rels/.rels", Relacoes(parte), CompressionLevel.Optimal),
                .. comAParte ? [(parte, "<w:document/>", CompressionLevel.Optimal)] : Array.Empty<(string, string, CompressionLevel)>(),
            ]);

    /// <summary>Uma planilha renomeada: pacote OOXML válido, mas sem documento de texto.</summary>
    public static byte[] PlanilhaComoDocx() =>
        Pacote(
            ("[Content_Types].xml", ManifestoDocx("/xl/workbook.xml",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"), CompressionLevel.Optimal),
            ("_rels/.rels", Relacoes("xl/workbook.xml"), CompressionLevel.Optimal),
            ("xl/workbook.xml", "<workbook/>", CompressionLevel.Optimal));

    /// <summary>Um DOCX de texto com macro (<c>vbaProject.bin</c>).</summary>
    public static byte[] DocxComMacro() =>
        Pacote(
            ("[Content_Types].xml", ManifestoDocx("/word/document.xml", DocumentoPrincipalDoWord), CompressionLevel.Optimal),
            ("_rels/.rels", Relacoes("word/document.xml"), CompressionLevel.Optimal),
            ("word/document.xml", "<w:document/>", CompressionLevel.Optimal),
            ("word/vbaProject.bin", "macro", CompressionLevel.Optimal));

    /// <summary>
    /// Um DOCX cujo <c>[Content_Types].xml</c> declara o documento de texto numa parte que a
    /// relação <c>officeDocument</c> não aponta — o principal é outro.
    /// </summary>
    public static byte[] DocxComPrincipalNaoDeclarado() =>
        Pacote(
            ("[Content_Types].xml", ManifestoDocx("/word/document.xml", DocumentoPrincipalDoWord), CompressionLevel.Optimal),
            ("_rels/.rels", Relacoes("xl/workbook.xml"), CompressionLevel.Optimal),
            ("xl/workbook.xml", "<workbook/>", CompressionLevel.Optimal),
            ("word/document.xml", "<w:document/>", CompressionLevel.Optimal));

    /// <summary>
    /// Um ODT de texto: a entrada <c>mimetype</c> primeiro, sem compressão, o conteúdo e o
    /// manifesto do pacote.
    /// </summary>
    public static byte[] Odt(
        string mimetype = ContentTypeOdt,
        CompressionLevel compressaoDoMimetype = CompressionLevel.NoCompression,
        string? entradaExtra = null,
        bool comConteudo = true,
        bool comManifesto = true) =>
        Pacote(
            [
                ("mimetype", mimetype, compressaoDoMimetype),
                .. comConteudo ? [("content.xml", "<office:document-content/>", CompressionLevel.Optimal)] : Array.Empty<(string, string, CompressionLevel)>(),
                .. comManifesto ? [("META-INF/manifest.xml", "<manifest:manifest/>", CompressionLevel.Optimal)] : Array.Empty<(string, string, CompressionLevel)>(),
                .. entradaExtra is null ? Array.Empty<(string, string, CompressionLevel)>() : [(entradaExtra, "macro", CompressionLevel.Optimal)],
            ]);

    /// <summary>Um PDF mínimo: assinatura <c>%PDF-</c>.</summary>
    public static byte[] Pdf() => "%PDF-1.7\n%%EOF"u8.ToArray();

    private static byte[] Pacote(params (string Nome, string Conteudo, CompressionLevel Compressao)[] entradas)
    {
        using MemoryStream saida = new();
        using (ZipArchive pacote = new(saida, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string nome, string conteudo, CompressionLevel compressao) in entradas)
            {
                using Stream entrada = pacote.CreateEntry(nome, compressao).Open();
                entrada.Write(Encoding.UTF8.GetBytes(conteudo));
            }
        }

        return saida.ToArray();
    }
}
