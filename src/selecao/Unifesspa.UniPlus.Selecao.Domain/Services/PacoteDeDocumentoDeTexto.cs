namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Xml;

/// <summary>O desfecho da conferência da estrutura de um pacote de documento de texto.</summary>
public enum VeredictoDoPacote
{
    Valido,
    Diverge,
    ContemMacro,
}

/// <summary>
/// A conferência da estrutura dos pacotes DOCX e ODT, que o modelo de documento e o documento a
/// publicar compartilham. O content-type que o objeto declara é só uma triagem — o cliente o
/// escolhe no envio —; o que decide é a estrutura do pacote: no DOCX, o documento principal de
/// texto declarado em <c>[Content_Types].xml</c>; no ODT, a entrada <c>mimetype</c> gravada sem
/// compressão no início do pacote. Planilha ou apresentação renomeadas são recusadas, e o pacote
/// com macro também: o arquivo vai a público com a chancela da instituição. Regra pura, sem I/O.
/// </summary>
public static class PacoteDeDocumentoDeTexto
{
    /// <summary>Teto de entradas no pacote: um documento de texto não chega perto disso.</summary>
    private const int MaximoDeEntradas = 5_000;

    /// <summary>Teto de cada XML lido do DOCX (<c>[Content_Types].xml</c> e <c>_rels/.rels</c>).</summary>
    private const int TamanhoMaximoDoXmlLido = 1024 * 1024;

    private const string ContentTypeDoDocumentoPrincipalDocx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";

    private static readonly byte[] CabecalhoZip = [0x50, 0x4B, 0x03, 0x04];

    public static VeredictoDoPacote ValidarDocx(byte[] conteudo) => Validar(conteudo, ValidarDocx);

    public static VeredictoDoPacote ValidarOdt(byte[] conteudo) => Validar(conteudo, pacote => ValidarOdt(pacote, conteudo));

    private static VeredictoDoPacote Validar(byte[] conteudo, Func<ZipArchive, VeredictoDoPacote> validarEstrutura)
    {
        ArgumentNullException.ThrowIfNull(conteudo);

        if (conteudo.Length < CabecalhoZip.Length || !conteudo.AsSpan(0, CabecalhoZip.Length).SequenceEqual(CabecalhoZip))
        {
            return VeredictoDoPacote.Diverge;
        }

        try
        {
            using ZipArchive pacote = new(new MemoryStream(conteudo, writable: false), ZipArchiveMode.Read);
            return pacote.Entries.Count > MaximoDeEntradas ? VeredictoDoPacote.Diverge : validarEstrutura(pacote);
        }
        catch (InvalidDataException)
        {
            return VeredictoDoPacote.Diverge;
        }
        catch (XmlException)
        {
            return VeredictoDoPacote.Diverge;
        }
    }

    /// <summary>
    /// O documento principal do pacote é a parte que <c>_rels/.rels</c> aponta como
    /// <c>officeDocument</c> — o nome dela varia (<c>/word/document.xml</c>,
    /// <c>/word/document2.xml</c>) e não diferencia caixa —, e é de texto quando o
    /// <c>[Content_Types].xml</c> a declara com o tipo do documento principal do Word.
    /// </summary>
    private static VeredictoDoPacote ValidarDocx(ZipArchive pacote)
    {
        if (pacote.Entries.Any(static e => e.FullName.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase)))
        {
            return VeredictoDoPacote.ContemMacro;
        }

        string? principal = ParteDoDocumentoPrincipal(pacote);
        ZipArchiveEntry? manifesto = pacote.GetEntry("[Content_Types].xml");
        if (manifesto is null || manifesto.Length > TamanhoMaximoDoXmlLido || !TemEntrada(pacote, principal?.TrimStart('/')))
        {
            return VeredictoDoPacote.Diverge;
        }

        bool principalEhTexto = false;
        using Stream stream = manifesto.Open();
        using XmlReader leitor = LeitorSeguro(stream);
        while (leitor.Read())
        {
            if (leitor.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            string? tipo = leitor.GetAttribute("ContentType");
            if (tipo is not null && tipo.Contains("macroEnabled", StringComparison.OrdinalIgnoreCase))
            {
                return VeredictoDoPacote.ContemMacro;
            }

            principalEhTexto |= leitor.LocalName == "Override"
                && string.Equals(leitor.GetAttribute("PartName"), principal, StringComparison.OrdinalIgnoreCase)
                && string.Equals(tipo, ContentTypeDoDocumentoPrincipalDocx, StringComparison.Ordinal);
        }

        return principalEhTexto ? VeredictoDoPacote.Valido : VeredictoDoPacote.Diverge;
    }

    /// <summary>O nome da parte que a relação <c>officeDocument</c> do pacote aponta, com a barra inicial; nulo sem ela.</summary>
    private static string? ParteDoDocumentoPrincipal(ZipArchive pacote)
    {
        ZipArchiveEntry? relacoes = pacote.GetEntry("_rels/.rels");
        if (relacoes is null || relacoes.Length > TamanhoMaximoDoXmlLido)
        {
            return null;
        }

        using Stream stream = relacoes.Open();
        using XmlReader leitor = LeitorSeguro(stream);
        while (leitor.Read())
        {
            if (leitor.NodeType == XmlNodeType.Element
                && leitor.LocalName == "Relationship"
                && (leitor.GetAttribute("Type")?.EndsWith("/officeDocument", StringComparison.Ordinal) ?? false)
                && leitor.GetAttribute("Target") is { Length: > 0 } alvo)
            {
                return "/" + alvo.TrimStart('/');
            }
        }

        return null;
    }

    /// <summary>
    /// Se o pacote tem a entrada — as partes obrigatórias têm de existir, senão o pacote truncado
    /// ou montado só com os metadados passaria. O conteúdo delas não é lido inteiro: descomprimido,
    /// ele pode ser muito maior que o arquivo enviado.
    /// </summary>
    private static bool TemEntrada(ZipArchive pacote, string? nome) =>
        nome is not null && pacote.Entries.Any(e => string.Equals(e.FullName, nome, StringComparison.OrdinalIgnoreCase));

    private static XmlReader LeitorSeguro(Stream stream) =>
        XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });

    /// <summary>
    /// A especificação OpenDocument exige a entrada <c>mimetype</c> como a primeira do pacote, sem
    /// compressão, com o tipo do documento — é ela que distingue o texto da planilha —, e as
    /// entradas <c>content.xml</c> e <c>META-INF/manifest.xml</c>. Macros vivem nos diretórios
    /// <c>Basic/</c> e <c>Scripts/</c>.
    /// </summary>
    private static VeredictoDoPacote ValidarOdt(ZipArchive pacote, byte[] conteudo)
    {
        if (pacote.Entries.Any(static e => e.FullName.StartsWith("Basic/", StringComparison.Ordinal) || e.FullName.StartsWith("Scripts/", StringComparison.Ordinal)))
        {
            return VeredictoDoPacote.ContemMacro;
        }

        if (!TemEntrada(pacote, "content.xml") || !TemEntrada(pacote, "META-INF/manifest.xml"))
        {
            return VeredictoDoPacote.Diverge;
        }

        // Cabeçalho local da primeira entrada: tamanho gravado em 18, tamanho do nome em 26, do
        // campo extra em 28, e o nome a partir de 30. Comparar os bytes gravados com o tipo já
        // exige a entrada sem compressão: comprimida, ela não teria o texto do tipo.
        const int InicioDoNome = 30;
        byte[] nomeEsperado = "mimetype"u8.ToArray();
        byte[] tipoEsperado = Encoding.ASCII.GetBytes(TiposDeMidiaDeDocumento.Odt);
        if (conteudo.Length < InicioDoNome)
        {
            return VeredictoDoPacote.Diverge;
        }

        uint tamanho = BinaryPrimitives.ReadUInt32LittleEndian(conteudo.AsSpan(18));
        int tamanhoDoNome = BinaryPrimitives.ReadUInt16LittleEndian(conteudo.AsSpan(26));
        int tamanhoDoExtra = BinaryPrimitives.ReadUInt16LittleEndian(conteudo.AsSpan(28));
        int inicioDosDados = InicioDoNome + tamanhoDoNome + tamanhoDoExtra;
        bool primeiraEhOMimetype = tamanho == tipoEsperado.Length
            && conteudo.Length >= inicioDosDados + tipoEsperado.Length
            && conteudo.AsSpan(InicioDoNome, tamanhoDoNome).SequenceEqual(nomeEsperado)
            && conteudo.AsSpan(inicioDosDados, tipoEsperado.Length).SequenceEqual(tipoEsperado);
        return primeiraEhOMimetype ? VeredictoDoPacote.Valido : VeredictoDoPacote.Diverge;
    }
}
