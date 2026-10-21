namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Errors;

using Interfaces;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

using ValueObjects;

/// <summary>
/// O arquivo de uma apresentação de documento da inscrição. Vai ao armazenamento privado por envio
/// direto do cliente (URL pré-assinada de PUT); a API nunca recebe os bytes. Nasce
/// <see cref="StatusArquivoEnviado.Pendente"/> e só vira dado de negócio na confirmação, quando o
/// conteúdo é lido, conferido contra o formato pela assinatura dos primeiros bytes, verificado pelo
/// antivírus e selado cifrado. O formato e o tamanho máximo são os da exigência congelada, copiados
/// no início do envio, e por isso <see cref="ValidarConteudo"/> é regra pura.
/// </summary>
/// <remarks>
/// O nome original do arquivo não é guardado: pode carregar dado pessoal ("laudo_maria_F84.pdf"). As
/// chaves do armazenamento levam só o id do arquivo (ADR-0019).
/// </remarks>
public sealed class ArquivoDaApresentacao : EntityBase, IArquivoEnviado
{
    /// <summary>O teto do sistema, vigente só quando a exigência não define limite.</summary>
    public const long TetoDoSistemaBytes = 10 * 1024 * 1024;

    private static readonly byte[] AssinaturaPdf = "%PDF-"u8.ToArray();
    private static readonly byte[] AssinaturaJpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] AssinaturaPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public Guid ApresentacaoId { get; private set; }

    /// <summary>A inscrição, repetida aqui para a autorização e a limpeza não dependerem de junção.</summary>
    public Guid InscricaoId { get; private set; }

    public FormatoPermitido Formato { get; private set; }

    /// <summary>O maior tamanho que a exigência admite para este formato; o teto do sistema quando ela não define limite.</summary>
    public long TamanhoMaximoBytes { get; private set; }

    public string ObjectKey { get; private set; } = string.Empty;
    public StatusArquivoEnviado Status { get; private set; }
    public DateTimeOffset ExpiraEm { get; private set; }
    public long? TamanhoBytes { get; private set; }

    /// <summary>O SHA-256 do conteúdo em claro, calculado no servidor.</summary>
    public string? HashSha256 { get; private set; }

    public DateTimeOffset? ConfirmadoEm { get; private set; }

    /// <summary>
    /// O arquivo já constou de um envio da inscrição (UNI-REQ-0168): a prova do que o candidato
    /// apresentou, que a limpeza dos envios seguintes nunca apaga.
    /// </summary>
    public bool ConstouDeEnvio { get; private set; }

    /// <summary>A chave do conteúdo selado cifrado, definida na confirmação; nunca alvo de URL pré-assinada.</summary>
    public string? ObjectKeyConfirmado { get; private set; }

    /// <summary>A chave em que o conteúdo selado cifrado é (ou será) guardado; só o id do arquivo a compõe.</summary>
    public string ChaveSelada => ChaveSeladaDe(Id);

    /// <summary>A chave do conteúdo selado do arquivo de <paramref name="id"/>.</summary>
    public static string ChaveSeladaDe(Guid id) => $"{PrefixoDasChaves}/{id:D}/selado";

    public string TipoDeMidia => TipoDeMidiaDe(Formato);

    private ArquivoDaApresentacao() { }

    /// <summary>
    /// Cria o registro pendente que acompanha a URL de envio. Recusa, acumulando, o tipo de mídia que
    /// a exigência não aceita e o tamanho declarado vazio ou acima do limite. Só PDF, JPEG e PNG
    /// valem, mesmo quando a exigência aceita qualquer formato.
    /// </summary>
    public static Result<ArquivoDaApresentacao> IniciarPendente(
        Guid apresentacaoId,
        Guid inscricaoId,
        DocumentoExigido exigencia,
        string? tipoDeMidia,
        long tamanhoDeclaradoBytes,
        TimeProvider clock,
        TimeSpan ttl)
    {
        ArgumentNullException.ThrowIfNull(exigencia);
        ArgumentNullException.ThrowIfNull(clock);

        List<FieldError> erros = [];
        FormatoPermitido? formato = FormatoDoTipoDeMidia(tipoDeMidia);
        FormatoPermitidoEntry? entrada = null;
        bool aceito = formato is { } declarado
            && (exigencia.FormatosPermitidos.Qualquer
                || (entrada = exigencia.FormatosPermitidos.Lista!.FirstOrDefault(e => e.Formato == declarado)) is not null);
        if (!aceito)
        {
            erros.Add(new FieldError("tipoDeMidia", new DomainError(
                DocumentoDaInscricaoErrorCodes.FormatoNaoAceitoPelaExigencia,
                "O formato do arquivo não é aceito por esta exigência. Envie um PDF, JPEG ou PNG que ela admita.")));
        }

        long limite = LimiteEfetivo(exigencia, entrada);
        if (tamanhoDeclaradoBytes <= 0)
        {
            erros.Add(new FieldError("tamanhoBytes", ArquivoVazio));
        }
        else if (tamanhoDeclaradoBytes > limite)
        {
            erros.Add(new FieldError("tamanhoBytes", TamanhoExcedidoAte(limite)));
        }

        if (erros.Count > 0)
        {
            return Result<ArquivoDaApresentacao>.ValidationFailure(erros);
        }

        ArquivoDaApresentacao arquivo = new()
        {
            ApresentacaoId = apresentacaoId,
            InscricaoId = inscricaoId,
            Formato = formato!.Value,
            TamanhoMaximoBytes = limite,
            Status = StatusArquivoEnviado.Pendente,
            ExpiraEm = clock.GetUtcNow().Add(ttl),
        };
        arquivo.ObjectKey = $"{PrefixoDasChaves}/{arquivo.Id:D}/envio";
        return Result<ArquivoDaApresentacao>.Success(arquivo);
    }

    /// <summary>
    /// Finaliza o arquivo como confirmado e determina a chave do conteúdo selado. Só a partir do
    /// pendente; quem chama grava o conteúdo cifrado nessa chave — este método só decide o estado.
    /// </summary>
    public Result Confirmar(long tamanhoBytes, string hashSha256, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashSha256);

        if (Status != StatusArquivoEnviado.Pendente)
        {
            return Result.Failure(StatusInvalidoParaConfirmacao);
        }

        TamanhoBytes = tamanhoBytes;
        HashSha256 = hashSha256;
        Status = StatusArquivoEnviado.Confirmado;
        ConfirmadoEm = clock.GetUtcNow();
        ObjectKeyConfirmado = ChaveSelada;
        return Result.Success();
    }

    /// <summary>Registra que o arquivo confirmado entrou num envio da inscrição; a partir daí ele não é mais apagado.</summary>
    public void MarcarComoConstanteDeEnvio()
    {
        if (Status == StatusArquivoEnviado.Confirmado)
        {
            ConstouDeEnvio = true;
        }
    }

    /// <summary>
    /// Valida o conteúdo lido na confirmação: o tamanho contra o limite da exigência e, mais que o
    /// tipo de mídia que o objeto declara (que o cliente escolhe), a assinatura dos primeiros bytes
    /// do formato declarado no início. Regra pura, sem I/O.
    /// </summary>
    public Result ValidarConteudo(long tamanhoBytes, string contentType, byte[] conteudo)
    {
        ArgumentNullException.ThrowIfNull(conteudo);

        if (tamanhoBytes > TamanhoMaximoBytes)
        {
            return Result.Failure(TamanhoExcedidoAte(TamanhoMaximoBytes));
        }

        if (tamanhoBytes == 0)
        {
            return Result.Failure(ArquivoVazio);
        }

        return string.Equals(contentType, TipoDeMidia, StringComparison.OrdinalIgnoreCase)
            && conteudo.AsSpan().StartsWith(AssinaturaDe(Formato))
                ? Result.Success()
                : Result.Failure(ConteudoNaoConfereComFormato);
    }

    /// <summary>O formato do tipo de mídia; nulo quando não é PDF, JPEG nem PNG.</summary>
    public static FormatoPermitido? FormatoDoTipoDeMidia(string? tipoDeMidia) => (tipoDeMidia ?? string.Empty).Trim().ToUpperInvariant() switch
    {
        "APPLICATION/PDF" => FormatoPermitido.Pdf,
        "IMAGE/JPEG" => FormatoPermitido.Jpeg,
        "IMAGE/PNG" => FormatoPermitido.Png,
        _ => null,
    };

    public static DomainError ArquivoVazio { get; } = new(
        DocumentoDaInscricaoErrorCodes.ArquivoVazio, "O arquivo enviado está vazio.");

    public static DomainError ConteudoNaoConfereComFormato { get; } = new(
        DocumentoDaInscricaoErrorCodes.ConteudoNaoConfereComFormato,
        "O conteúdo do arquivo não corresponde ao formato declarado. Envie um PDF, JPEG ou PNG válido.");

    public static DomainError ArquivoInfectado { get; } = new(
        DocumentoDaInscricaoErrorCodes.ArquivoInfectado,
        "O arquivo foi recusado pela verificação de segurança. Envie outro arquivo.");

    public static DomainError AntivirusIndisponivel { get; } = new(
        DocumentoDaInscricaoErrorCodes.AntivirusIndisponivel,
        "Não foi possível verificar o arquivo agora. Tente confirmar de novo em instantes.");

    public static DomainError ConteudoAdulterado { get; } = new(
        DocumentoDaInscricaoErrorCodes.ConteudoAdulterado,
        "O conteúdo guardado não confere com o código de verificação do arquivo.");

    public static DomainError StatusInvalidoParaConfirmacao { get; } = new(
        DocumentoDaInscricaoErrorCodes.StatusInvalidoParaConfirmacao, "Somente um arquivo pendente pode ser confirmado.");

    public static DomainError TamanhoExcedidoAte(long limiteBytes) => new(
        DocumentoDaInscricaoErrorCodes.TamanhoExcedido,
        $"O arquivo excede o tamanho máximo de {limiteBytes / (1024 * 1024.0):0.##} MB que esta exigência admite.");

    private const string PrefixoDasChaves = "selecao/documentos-da-inscricao";

    /// <summary>O limite da exigência (o menor entre o do formato e o global); o teto do sistema só quando ela não define nenhum.</summary>
    private static long LimiteEfetivo(DocumentoExigido exigencia, FormatoPermitidoEntry? entrada)
    {
        long? daExigencia = (exigencia.TamanhoMaximoBytes, entrada?.TamanhoMaximoBytesMax) switch
        {
            (int global, int porFormato) => Math.Min(global, porFormato),
            (int global, null) => global,
            (null, int porFormato) => porFormato,
            _ => null,
        };
        return daExigencia ?? TetoDoSistemaBytes;
    }

    private static string TipoDeMidiaDe(FormatoPermitido formato) => formato switch
    {
        FormatoPermitido.Pdf => "application/pdf",
        FormatoPermitido.Jpeg => "image/jpeg",
        FormatoPermitido.Png => "image/png",
        _ => throw new ArgumentOutOfRangeException(nameof(formato), formato, "Formato sem tipo de mídia."),
    };

    private static byte[] AssinaturaDe(FormatoPermitido formato) => formato switch
    {
        FormatoPermitido.Pdf => AssinaturaPdf,
        FormatoPermitido.Jpeg => AssinaturaJpeg,
        FormatoPermitido.Png => AssinaturaPng,
        _ => throw new ArgumentOutOfRangeException(nameof(formato), formato, "Formato sem assinatura."),
    };
}
