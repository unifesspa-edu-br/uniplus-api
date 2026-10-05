namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Xml;

using Enums;

using Errors;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O modelo de documento que uma exigência oferece ao candidato — a autodeclaração ou a declaração
/// de pertencimento que ele baixa, preenche, assina e devolve (UNI-REQ-0016). Arquivo editável
/// (<see cref="FormatoDeModelo"/>) guardado no MinIO por envio direto do cliente (URL pré-assinada
/// de PUT); a API nunca recebe os bytes. Vinculado ao processo, mas não é filho do agregado: nasce
/// <see cref="StatusArquivoEnviado.Pendente"/> e só vira dado de negócio ao ser
/// <see cref="StatusArquivoEnviado.Confirmado"/> — conteúdo lido, validado e hasheado no servidor.
/// O confirmado é imutável; um novo envio cria outro registro.
/// </summary>
public sealed class ModeloDeDocumento : EntityBase
{
    /// <summary>Tamanho máximo do arquivo do modelo.</summary>
    public const long TamanhoMaximoBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Tamanho máximo do nome do arquivo, com a extensão. Cabe no <c>Content-Disposition</c> do
    /// download mesmo com os caracteres acentuados codificados.
    /// </summary>
    public const int NomeArquivoMaxLength = 120;

    /// <summary>Teto de entradas no pacote: um documento de texto não chega perto disso.</summary>
    private const int MaximoDeEntradas = 5_000;

    /// <summary>Teto de cada XML lido do DOCX (<c>[Content_Types].xml</c> e <c>_rels/.rels</c>).</summary>
    private const int TamanhoMaximoDoXmlLido = 1024 * 1024;

    private const string ContentTypeDocx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string ContentTypeOdt = "application/vnd.oasis.opendocument.text";
    private const string ContentTypeDoDocumentoPrincipalDocx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";
    private const string CaracteresProibidosNoNome = "/\\<>:\"|?*;";

    private static readonly byte[] CabecalhoZip = [0x50, 0x4B, 0x03, 0x04];

    public Guid ProcessoSeletivoId { get; private set; }
    public string NomeArquivo { get; private set; } = string.Empty;
    public FormatoDeModelo Formato { get; private set; }
    public string ObjectKey { get; private set; } = string.Empty;
    public StatusArquivoEnviado Status { get; private set; }
    public DateTimeOffset ExpiraEm { get; private set; }
    public long? TamanhoBytes { get; private set; }
    public string? HashSha256 { get; private set; }
    public DateTimeOffset? ConfirmadoEm { get; private set; }

    /// <summary>
    /// Chave selada, gravada só na confirmação — nunca alvo de URL pré-assinada de PUT. A cópia do
    /// conteúdo validado para ela é o que torna o modelo confirmado imutável de fato: a chave de
    /// envio segue sobrescrevível até o TTL da URL expirar.
    /// </summary>
    public string? ObjectKeyConfirmado { get; private set; }

    /// <summary>A recusa do arquivo maior que <see cref="TamanhoMaximoBytes"/>.</summary>
    public static DomainError TamanhoExcedido { get; } = new(
        ModeloDeDocumentoErrorCodes.TamanhoExcedido,
        $"O modelo excede o tamanho máximo permitido de {TamanhoMaximoBytes / (1024 * 1024)} MB.");

    /// <summary>A extensão do arquivo no formato, sem o ponto.</summary>
    public string Extensao => ExtensaoDe(Formato);

    /// <summary>O content-type do arquivo no formato.</summary>
    public string ContentType => ContentTypeDe(Formato);

    private ModeloDeDocumento() { }

    /// <summary>
    /// Cria o registro pendente que acompanha a URL de envio. O formato vem do token do contrato
    /// (<c>DOCX</c> ou <c>ODT</c>); o nome é o que o candidato recebe no download, e a extensão é
    /// a do formato. As recusas do formato e do nome acumulam.
    /// </summary>
    public static Result<ModeloDeDocumento> IniciarPendente(
        Guid processoSeletivoId, string? nomeArquivo, string? formato, TimeProvider clock, TimeSpan ttl)
    {
        ArgumentNullException.ThrowIfNull(clock);

        FormatoDeModelo formatoDoModelo = FormatoDoToken(formato);
        List<FieldError> erros = [];
        if (formatoDoModelo == FormatoDeModelo.Nenhum)
        {
            erros.Add(new FieldError("formato", new DomainError(
                ModeloDeDocumentoErrorCodes.FormatoNaoEditavel,
                "O modelo é um documento editável: DOCX ou ODT. PDF e imagem não são editáveis.")));
        }

        string? nome = NomeNormalizado(nomeArquivo, formatoDoModelo);
        if (nome is null)
        {
            erros.Add(new FieldError("nomeArquivo", new DomainError(
                ModeloDeDocumentoErrorCodes.NomeArquivoInvalido,
                $"O nome do arquivo é obrigatório, tem até {NomeArquivoMaxLength} caracteres com a extensão e não tem separador de caminho, caractere de controle nem {CaracteresProibidosNoNome}.")));
        }

        if (erros.Count > 0)
        {
            return Result<ModeloDeDocumento>.ValidationFailure(erros);
        }

        ModeloDeDocumento modelo = new()
        {
            ProcessoSeletivoId = processoSeletivoId,
            NomeArquivo = nome!,
            Formato = formatoDoModelo,
            Status = StatusArquivoEnviado.Pendente,
            ExpiraEm = clock.GetUtcNow().Add(ttl),
        };
        modelo.ObjectKey = $"selecao/modelos-de-documento/{processoSeletivoId:D}/{modelo.Id:D}.{modelo.Extensao}";
        return Result<ModeloDeDocumento>.Success(modelo);
    }

    /// <summary>
    /// Finaliza o modelo como confirmado e imutável e determina a chave selada. Só a partir do
    /// pendente; a validação do conteúdo (<see cref="ValidarConteudo"/>) já passou. Quem chama
    /// ainda copia o conteúdo para <see cref="ObjectKeyConfirmado"/> no storage.
    /// </summary>
    public Result Confirmar(long tamanhoBytes, string hashSha256, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashSha256);

        if (Status != StatusArquivoEnviado.Pendente)
        {
            return Result.Failure(new DomainError(
                ModeloDeDocumentoErrorCodes.StatusInvalidoParaConfirmacao, "Somente um modelo pendente pode ser confirmado."));
        }

        TamanhoBytes = tamanhoBytes;
        HashSha256 = hashSha256;
        Status = StatusArquivoEnviado.Confirmado;
        ConfirmadoEm = clock.GetUtcNow();
        ObjectKeyConfirmado = $"selecao/modelos-de-documento/{ProcessoSeletivoId:D}/{Id:D}/confirmado.{Extensao}";
        return Result.Success();
    }

    /// <summary>
    /// Valida o conteúdo lido na confirmação contra o formato declarado no início. O content-type
    /// que o objeto declara é só uma triagem — o cliente o escolhe no envio —; o que decide é a
    /// estrutura do pacote: no DOCX, o documento principal de texto declarado em
    /// <c>[Content_Types].xml</c>; no ODT, a entrada <c>mimetype</c> gravada sem compressão no
    /// início do pacote. Planilha ou apresentação renomeadas são recusadas, e o pacote com macro
    /// também — o modelo vai a público com a chancela da instituição. Regra pura, sem I/O.
    /// </summary>
    public Result ValidarConteudo(long tamanhoBytes, string contentType, byte[] conteudo)
    {
        ArgumentNullException.ThrowIfNull(conteudo);

        if (tamanhoBytes > TamanhoMaximoBytes)
        {
            return Result.Failure(TamanhoExcedido);
        }

        if (!string.Equals(contentType, ContentType, StringComparison.OrdinalIgnoreCase)
            || conteudo.Length < CabecalhoZip.Length
            || !conteudo.AsSpan(0, CabecalhoZip.Length).SequenceEqual(CabecalhoZip))
        {
            return ConteudoDiverge();
        }

        try
        {
            using ZipArchive pacote = new(new MemoryStream(conteudo, writable: false), ZipArchiveMode.Read);
            if (pacote.Entries.Count > MaximoDeEntradas)
            {
                return ConteudoDiverge();
            }

            return Formato == FormatoDeModelo.Docx ? ValidarDocx(pacote) : ValidarOdt(pacote, conteudo);
        }
        catch (InvalidDataException)
        {
            return ConteudoDiverge();
        }
        catch (XmlException)
        {
            return ConteudoDiverge();
        }
    }

    /// <summary>O formato do token do contrato; <see cref="FormatoDeModelo.Nenhum"/> quando não é editável.</summary>
    public static FormatoDeModelo FormatoDoToken(string? token) => token switch
    {
        "DOCX" => FormatoDeModelo.Docx,
        "ODT" => FormatoDeModelo.Odt,
        _ => FormatoDeModelo.Nenhum,
    };

    /// <summary>O token do formato no contrato.</summary>
    public static string TokenDe(FormatoDeModelo formato) => formato switch
    {
        FormatoDeModelo.Docx => "DOCX",
        FormatoDeModelo.Odt => "ODT",
        _ => throw new ArgumentOutOfRangeException(nameof(formato), formato, "Formato de modelo sem token."),
    };

    /// <summary>A extensão do arquivo no formato, sem o ponto.</summary>
    public static string ExtensaoDe(FormatoDeModelo formato) => formato switch
    {
        FormatoDeModelo.Docx => "docx",
        FormatoDeModelo.Odt => "odt",
        _ => throw new ArgumentOutOfRangeException(nameof(formato), formato, "Formato de modelo sem extensão."),
    };

    /// <summary>O content-type do arquivo no formato.</summary>
    public static string ContentTypeDe(FormatoDeModelo formato) => formato switch
    {
        FormatoDeModelo.Docx => ContentTypeDocx,
        FormatoDeModelo.Odt => ContentTypeOdt,
        _ => throw new ArgumentOutOfRangeException(nameof(formato), formato, "Formato de modelo sem content-type."),
    };

    /// <summary>
    /// O nome aparado e normalizado em NFC — a forma que o envelope canônico emite —, com a
    /// extensão do formato; nulo quando vazio, longo ou com caractere proibido. Sem formato válido,
    /// o nome ainda é conferido, sem extensão, para a recusa dele acumular com a do formato.
    /// </summary>
    private static string? NomeNormalizado(string? nomeArquivo, FormatoDeModelo formato)
    {
        string extensao = formato == FormatoDeModelo.Nenhum ? string.Empty : "." + ExtensaoDe(formato);
        string nome = (nomeArquivo ?? string.Empty).Trim().Normalize(NormalizationForm.FormC);
        if (extensao.Length > 0 && nome.EndsWith(extensao, StringComparison.OrdinalIgnoreCase))
        {
            nome = nome[..^extensao.Length].TrimEnd();
        }

        if (nome.Length == 0 || nome.Any(static c => char.IsControl(c) || CaracteresProibidosNoNome.Contains(c, StringComparison.Ordinal)))
        {
            return null;
        }

        string comExtensao = nome + extensao;
        return comExtensao.Length <= NomeArquivoMaxLength ? comExtensao : null;
    }

    /// <summary>
    /// O documento principal do pacote é a parte que <c>_rels/.rels</c> aponta como
    /// <c>officeDocument</c> — o nome dela varia (<c>/word/document.xml</c>,
    /// <c>/word/document2.xml</c>) e não diferencia caixa —, e é de texto quando o
    /// <c>[Content_Types].xml</c> a declara com o tipo do documento principal do Word.
    /// </summary>
    private Result ValidarDocx(ZipArchive pacote)
    {
        if (pacote.Entries.Any(static e => e.FullName.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase)))
        {
            return ComMacro();
        }

        string? principal = ParteDoDocumentoPrincipal(pacote);
        ZipArchiveEntry? manifesto = pacote.GetEntry("[Content_Types].xml");
        if (manifesto is null || manifesto.Length > TamanhoMaximoDoXmlLido || !TemEntrada(pacote, principal?.TrimStart('/')))
        {
            return ConteudoDiverge();
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
                return ComMacro();
            }

            principalEhTexto |= leitor.LocalName == "Override"
                && string.Equals(leitor.GetAttribute("PartName"), principal, StringComparison.OrdinalIgnoreCase)
                && string.Equals(tipo, ContentTypeDoDocumentoPrincipalDocx, StringComparison.Ordinal);
        }

        return principalEhTexto ? Result.Success() : ConteudoDiverge();
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
    private Result ValidarOdt(ZipArchive pacote, byte[] conteudo)
    {
        if (pacote.Entries.Any(static e => e.FullName.StartsWith("Basic/", StringComparison.Ordinal) || e.FullName.StartsWith("Scripts/", StringComparison.Ordinal)))
        {
            return ComMacro();
        }

        if (!TemEntrada(pacote, "content.xml") || !TemEntrada(pacote, "META-INF/manifest.xml"))
        {
            return ConteudoDiverge();
        }

        // Cabeçalho local da primeira entrada: tamanho gravado em 18, tamanho do nome em 26, do
        // campo extra em 28, e o nome a partir de 30. Comparar os bytes gravados com o tipo já
        // exige a entrada sem compressão: comprimida, ela não teria o texto do tipo.
        const int InicioDoNome = 30;
        byte[] nomeEsperado = "mimetype"u8.ToArray();
        byte[] tipoEsperado = Encoding.ASCII.GetBytes(ContentTypeOdt);
        if (conteudo.Length < InicioDoNome)
        {
            return ConteudoDiverge();
        }

        uint tamanho = BinaryPrimitives.ReadUInt32LittleEndian(conteudo.AsSpan(18));
        int tamanhoDoNome = BinaryPrimitives.ReadUInt16LittleEndian(conteudo.AsSpan(26));
        int tamanhoDoExtra = BinaryPrimitives.ReadUInt16LittleEndian(conteudo.AsSpan(28));
        int inicioDosDados = InicioDoNome + tamanhoDoNome + tamanhoDoExtra;
        bool primeiraEhOMimetype = tamanho == tipoEsperado.Length
            && conteudo.Length >= inicioDosDados + tipoEsperado.Length
            && conteudo.AsSpan(InicioDoNome, tamanhoDoNome).SequenceEqual(nomeEsperado)
            && conteudo.AsSpan(inicioDosDados, tipoEsperado.Length).SequenceEqual(tipoEsperado);
        return primeiraEhOMimetype ? Result.Success() : ConteudoDiverge();
    }

    private Result ConteudoDiverge() => Result.Failure(new DomainError(
        ModeloDeDocumentoErrorCodes.ConteudoDivergeDoFormato,
        $"O conteúdo do arquivo não é um documento de texto {TokenDe(Formato)}."));

    private static Result ComMacro() => Result.Failure(new DomainError(
        ModeloDeDocumentoErrorCodes.ContemMacro, "O modelo não pode conter macro: ele é distribuído ao candidato."));
}
