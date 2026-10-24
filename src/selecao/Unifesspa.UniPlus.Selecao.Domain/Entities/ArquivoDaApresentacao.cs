namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Errors;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

using ValueObjects;

/// <summary>
/// O arquivo de uma apresentação de documento da inscrição. Vai ao armazenamento privado por envio
/// direto do cliente (URL pré-assinada de PUT); a API nunca recebe nem lê os bytes. Nasce
/// <see cref="SituacaoDoArquivoDoCandidato.Pendente"/>; a confirmação do envio o põe em verificação,
/// e o verificador, fora da API, o conclui como verificado, recusado ou a reenviar. O formato e o
/// tamanho máximo são os da exigência congelada, copiados no início do envio.
/// </summary>
/// <remarks>
/// O nome original do arquivo não é guardado: pode carregar dado pessoal ("laudo_maria_F84.pdf"). As
/// chaves do armazenamento levam só o id do arquivo (ADR-0019).
/// </remarks>
public sealed class ArquivoDaApresentacao : EntityBase
{
    /// <summary>O teto do sistema, vigente só quando a exigência não define limite.</summary>
    public const long TetoDoSistemaBytes = 10 * 1024 * 1024;

    public const long TetoAbsolutoBytes = 20 * 1024 * 1024;

    public Guid ApresentacaoId { get; private set; }

    /// <summary>A inscrição, repetida aqui para a autorização e a limpeza não dependerem de junção.</summary>
    public Guid InscricaoId { get; private set; }

    public FormatoPermitido Formato { get; private set; }

    /// <summary>O maior tamanho que a exigência admite para este formato; o teto do sistema quando ela não define limite.</summary>
    public long TamanhoMaximoBytes { get; private set; }

    public string ObjectKey { get; private set; } = string.Empty;
    public SituacaoDoArquivoDoCandidato Situacao { get; private set; }
    public DateTimeOffset ExpiraEm { get; private set; }

    public long? TamanhoBytes { get; private set; }

    public string? HashSha256 { get; private set; }

    public DateTimeOffset? ConfirmadoEm { get; private set; }

    /// <summary>O ETag que o PUT devolveu ao candidato: fixa a versão do objeto que o verificador confere.</summary>
    public string? ETag { get; private set; }

    /// <summary>Quantas vezes o pedido de verificação foi publicado, a confirmação incluída.</summary>
    public int Publicacoes { get; private set; }

    /// <summary>
    /// Quando a reconciliação pode republicar o pedido. Conta da última publicação, e não da confirmação, para não
    /// republicar a cada execução o pedido que ainda está nas novas tentativas, e cresce a cada publicação.
    /// </summary>
    public DateTimeOffset? ProximaPublicacaoEm { get; private set; }

    public string? ChaveDefinitiva { get; private set; }

    public MotivoDeRecusaDoArquivo? MotivoDaRecusa { get; private set; }

    /// <summary>
    /// O arquivo já constou de um envio da inscrição (UNI-REQ-0168): a prova do que o candidato
    /// apresentou, que a limpeza dos envios seguintes nunca apaga.
    /// </summary>
    public bool ConstouDeEnvio { get; private set; }

    public string ChaveDefinitivaPrevista => ChaveDefinitivaDe(Id);

    public static string ChaveDefinitivaDe(Guid id) => $"{PrefixoDasChaves}/{id:D}/definitivo";

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
            Situacao = SituacaoDoArquivoDoCandidato.Pendente,
            ExpiraEm = clock.GetUtcNow().Add(ttl),
        };
        arquivo.ObjectKey = $"{PrefixoDasChaves}/{arquivo.Id:D}/envio";
        return Result<ArquivoDaApresentacao>.Success(arquivo);
    }

    /// <summary>Recusa o ETag ausente ou em branco: sem ele a confirmação não tem o que conferir no armazenamento.</summary>
    public static Result ValidarETag(string? etag) =>
        string.IsNullOrWhiteSpace(etag) ? Result.Failure(ETagObrigatorio) : Result.Success();

    /// <summary>Põe o arquivo pendente em verificação, com o ETag que o PUT devolveu.</summary>
    public Result Confirmar(string etag, TimeProvider clock, PoliticaDeRepublicacao? politica = null)
    {
        ArgumentNullException.ThrowIfNull(clock);

        Result etagValido = ValidarETag(etag);
        if (etagValido.IsFailure)
        {
            return etagValido;
        }

        if (Situacao != SituacaoDoArquivoDoCandidato.Pendente)
        {
            return Result.Failure(StatusInvalidoParaConfirmacao);
        }

        DateTimeOffset agora = clock.GetUtcNow();
        ETag = etag.Trim();
        Situacao = SituacaoDoArquivoDoCandidato.EmVerificacao;
        ConfirmadoEm = agora;
        Publicacoes = 1;
        ProximaPublicacaoEm = agora + (politica ?? PoliticaDeRepublicacao.Padrao).PrazoApos(Publicacoes);
        return Result.Success();
    }

    public void RegistrarPublicacao(TimeProvider clock, PoliticaDeRepublicacao? politica = null)
    {
        ArgumentNullException.ThrowIfNull(clock);

        if (Situacao == SituacaoDoArquivoDoCandidato.EmVerificacao)
        {
            Publicacoes++;
            ProximaPublicacaoEm = clock.GetUtcNow() + (politica ?? PoliticaDeRepublicacao.Padrao).PrazoApos(Publicacoes);
        }
    }

    /// <summary>
    /// O que difere entre o resultado "verificado" e o declarado — formato, tamanho acima do máximo, SHA-256 que não é
    /// hexadecimal de 64 dígitos —, ou nulo quando confere.
    /// </summary>
    public string? DivergenciaDoVerificado(long tamanhoBytes, string hashSha256, FormatoPermitido formato)
    {
        ArgumentNullException.ThrowIfNull(hashSha256);

        if (formato != Formato)
        {
            return $"formato {formato} no lugar do declarado {Formato}";
        }

        if (tamanhoBytes <= 0 || tamanhoBytes > TamanhoMaximoBytes)
        {
            return $"{tamanhoBytes} bytes fora de 1 a {TamanhoMaximoBytes}";
        }

        return hashSha256.Length == 64 && hashSha256.All(char.IsAsciiHexDigit) ? null : "SHA-256 que não tem 64 dígitos hexadecimais";
    }

    /// <summary>
    /// Só o primeiro resultado vale — o verificado, o recusado e o reenvio devolvem <see langword="false"/> se o
    /// arquivo já saiu de "em verificação" —, porque a fila repete a entrega.
    /// </summary>
    public bool RegistrarVerificado(long tamanhoBytes, string hashSha256, FormatoPermitido formato)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hashSha256);

        if (Situacao != SituacaoDoArquivoDoCandidato.EmVerificacao)
        {
            return false;
        }

        TamanhoBytes = tamanhoBytes;
        HashSha256 = hashSha256;
        ChaveDefinitiva = ChaveDefinitivaPrevista;
        Situacao = SituacaoDoArquivoDoCandidato.Verificado;
        return true;
    }

    public bool RegistrarRecusa(MotivoDeRecusaDoArquivo motivo)
    {
        if (Situacao != SituacaoDoArquivoDoCandidato.EmVerificacao)
        {
            return false;
        }

        MotivoDaRecusa = motivo;
        Situacao = SituacaoDoArquivoDoCandidato.Recusado;
        return true;
    }

    public bool PedirReenvio()
    {
        if (Situacao != SituacaoDoArquivoDoCandidato.EmVerificacao)
        {
            return false;
        }

        Situacao = SituacaoDoArquivoDoCandidato.ReenvioNecessario;
        return true;
    }

    public void MarcarComoConstanteDeEnvio()
    {
        if (Situacao == SituacaoDoArquivoDoCandidato.Verificado)
        {
            ConstouDeEnvio = true;
        }
    }

    /// <summary>A mensagem ao candidato (UNI-REQ-0027); a de recusa nunca nomeia a ameaça.</summary>
    public string? MensagemAoCandidato => Situacao switch
    {
        SituacaoDoArquivoDoCandidato.EmVerificacao => "Arquivo em verificação. Isso costuma levar poucos segundos.",
        SituacaoDoArquivoDoCandidato.ReenvioNecessario => "Não recebemos este arquivo. Envie-o novamente.",
        SituacaoDoArquivoDoCandidato.Recusado => MensagemDaRecusa(MotivoDaRecusa),
        _ => null,
    };

    private string MensagemDaRecusa(MotivoDeRecusaDoArquivo? motivo) => motivo switch
    {
        MotivoDeRecusaDoArquivo.TamanhoExcedido =>
            $"Este arquivo passa do tamanho máximo de {MegabytesDe(TamanhoMaximoBytes)} MB para este documento. Envie um arquivo menor.",
        MotivoDeRecusaDoArquivo.FormatoNaoCorresponde =>
            $"O conteúdo deste arquivo não corresponde ao formato {Formato.ToCodigo()}. Envie o arquivo original.",
        MotivoDeRecusaDoArquivo.PdfComSenha => "Este PDF está protegido por senha. Envie o arquivo sem senha.",
        _ => "Este arquivo não passou na verificação de segurança (antivírus) e não foi aceito. Envie outro arquivo.",
    };

    private static string MegabytesDe(long bytes) =>
        (bytes / (1024 * 1024.0)).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

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

    public static DomainError NaoVerificado { get; } = new(
        DocumentoDaInscricaoErrorCodes.ArquivoNaoVerificado,
        "O arquivo ainda não foi verificado. Aguarde a verificação para abri-lo.");

    public static DomainError ObjetoNaoEncontrado { get; } = new(
        DocumentoDaInscricaoErrorCodes.ObjetoNaoEncontrado,
        "O arquivo ainda não foi enviado ao armazenamento ou expirou antes da confirmação.");

    public static DomainError StatusInvalidoParaConfirmacao { get; } = new(
        DocumentoDaInscricaoErrorCodes.StatusInvalidoParaConfirmacao, "Somente um arquivo pendente pode ser confirmado.");

    public static DomainError ETagObrigatorio { get; } = new(
        DocumentoDaInscricaoErrorCodes.ETagObrigatorio, "Informe o ETag que a resposta do envio devolveu.");

    public static DomainError TamanhoExcedidoAte(long limiteBytes) => new(
        DocumentoDaInscricaoErrorCodes.TamanhoExcedido,
        $"O arquivo excede o tamanho máximo de {MegabytesDe(limiteBytes)} MB que esta exigência admite.");

    private const string PrefixoDasChaves = "selecao/documentos-da-inscricao";

    /// <summary>Os formatos que a exigência aceita; "qualquer" são os três que o sistema recebe.</summary>
    public static IReadOnlyList<FormatoPermitido> FormatosAceitosPor(DocumentoExigido exigencia)
    {
        ArgumentNullException.ThrowIfNull(exigencia);
        return exigencia.FormatosPermitidos.Qualquer
            ? [FormatoPermitido.Pdf, FormatoPermitido.Jpeg, FormatoPermitido.Png]
            : [.. exigencia.FormatosPermitidos.Lista!.Select(static e => e.Formato)];
    }

    /// <summary>
    /// O tamanho máximo que o portal pode anunciar para a exigência sem saber o formato: o menor limite
    /// efetivo entre os formatos aceitos. O servidor confere o do formato escolhido.
    /// </summary>
    public static long LimiteAnunciadoDe(DocumentoExigido exigencia)
    {
        ArgumentNullException.ThrowIfNull(exigencia);
        return FormatosAceitosPor(exigencia).Min(formato => LimiteEfetivo(
            exigencia, exigencia.FormatosPermitidos.Lista?.FirstOrDefault(e => e.Formato == formato)));
    }

    /// <summary>O limite da exigência (o menor entre o do formato e o global), nunca acima de 20 MB; o teto do sistema só quando ela não define nenhum.</summary>
    private static long LimiteEfetivo(DocumentoExigido exigencia, FormatoPermitidoEntry? entrada)
    {
        long? daExigencia = (exigencia.TamanhoMaximoBytes, entrada?.TamanhoMaximoBytesMax) switch
        {
            (int global, int porFormato) => Math.Min(global, porFormato),
            (int global, null) => global,
            (null, int porFormato) => porFormato,
            _ => null,
        };
        return Math.Min(daExigencia ?? TetoDoSistemaBytes, TetoAbsolutoBytes);
    }

    public static string TipoDeMidiaDe(FormatoPermitido formato) => formato switch
    {
        FormatoPermitido.Pdf => "application/pdf",
        FormatoPermitido.Jpeg => "image/jpeg",
        FormatoPermitido.Png => "image/png",
        _ => throw new ArgumentOutOfRangeException(nameof(formato), formato, "Formato sem tipo de mídia."),
    };
}
