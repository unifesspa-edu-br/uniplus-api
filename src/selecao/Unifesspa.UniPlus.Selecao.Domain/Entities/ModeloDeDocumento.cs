namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Errors;

using Interfaces;

using Services;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O modelo de documento que uma exigência oferece ao candidato — a autodeclaração ou a declaração
/// de pertencimento que ele baixa, preenche, assina e devolve (UNI-REQ-0016). Arquivo editável
/// (DOCX ou ODT) guardado no MinIO por envio direto do cliente (URL pré-assinada
/// de PUT); a API nunca recebe os bytes. Vinculado ao processo, mas não é filho do agregado: nasce
/// <see cref="StatusArquivoEnviado.Pendente"/> e só vira dado de negócio ao ser
/// <see cref="StatusArquivoEnviado.Confirmado"/> — conteúdo lido, validado e hasheado no servidor.
/// O confirmado é imutável; um novo envio cria outro registro.
/// </summary>
public sealed class ModeloDeDocumento : EntityBase, IArquivoEnviado
{
    /// <summary>Tamanho máximo do arquivo do modelo.</summary>
    public const long TamanhoMaximoBytes = 10 * 1024 * 1024;

    /// <summary>Tamanho máximo do nome do arquivo, com a extensão.</summary>
    public const int NomeArquivoMaxLength = NomeDeArquivoEnviado.TamanhoMaximo;

    public Guid ProcessoSeletivoId { get; private set; }
    public string NomeArquivo { get; private set; } = string.Empty;
    public FormatoDeDocumento Formato { get; private set; }
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
    public string Extensao => FormatosDeDocumento.Extensao(Formato);

    /// <summary>O content-type do arquivo no formato.</summary>
    public string ContentType => FormatosDeDocumento.TipoDeMidia(Formato);

    string IArquivoEnviado.TipoDeMidia => ContentType;

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

        FormatoDeDocumento? formatoDoModelo = FormatoDoToken(formato);
        List<FieldError> erros = [];
        if (formatoDoModelo is null)
        {
            erros.Add(new FieldError("formato", new DomainError(
                ModeloDeDocumentoErrorCodes.FormatoNaoEditavel,
                "O modelo é um documento editável: DOCX ou ODT. PDF e imagem não são editáveis.")));
        }

        string? nome = NomeDeArquivoEnviado.Normalizar(
            nomeArquivo, formatoDoModelo is { } f ? FormatosDeDocumento.Extensao(f) : string.Empty);
        if (nome is null)
        {
            erros.Add(new FieldError("nomeArquivo", new DomainError(
                ModeloDeDocumentoErrorCodes.NomeArquivoInvalido,
                $"O nome do arquivo é obrigatório, tem até {NomeArquivoMaxLength} caracteres com a extensão e não tem separador de caminho, caractere de controle nem {NomeDeArquivoEnviado.CaracteresProibidos}.")));
        }

        if (erros.Count > 0)
        {
            return Result<ModeloDeDocumento>.ValidationFailure(erros);
        }

        ModeloDeDocumento modelo = new()
        {
            ProcessoSeletivoId = processoSeletivoId,
            NomeArquivo = nome!,
            Formato = formatoDoModelo!.Value,
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
    /// estrutura do pacote (<see cref="PacoteDeDocumentoDeTexto"/>). O pacote com macro é recusado:
    /// o modelo vai a público com a chancela da instituição. Regra pura, sem I/O.
    /// </summary>
    public Result ValidarConteudo(long tamanhoBytes, string contentType, byte[] conteudo)
    {
        ArgumentNullException.ThrowIfNull(conteudo);

        if (tamanhoBytes > TamanhoMaximoBytes)
        {
            return Result.Failure(TamanhoExcedido);
        }

        if (!string.Equals(contentType, ContentType, StringComparison.OrdinalIgnoreCase))
        {
            return ConteudoDiverge();
        }

        return (Formato == FormatoDeDocumento.Docx
            ? PacoteDeDocumentoDeTexto.ValidarDocx(conteudo)
            : PacoteDeDocumentoDeTexto.ValidarOdt(conteudo)) switch
        {
            VeredictoDoPacote.Valido => Result.Success(),
            VeredictoDoPacote.ContemMacro => ComMacro(),
            _ => ConteudoDiverge(),
        };
    }

    /// <summary>O formato do token do contrato; nulo quando não é editável (DOCX ou ODT).</summary>
    public static FormatoDeDocumento? FormatoDoToken(string? token) => token switch
    {
        "DOCX" => FormatoDeDocumento.Docx,
        "ODT" => FormatoDeDocumento.Odt,
        _ => null,
    };

    /// <summary>O token do formato no contrato.</summary>
    public static string TokenDe(FormatoDeDocumento formato) => formato switch
    {
        FormatoDeDocumento.Docx => "DOCX",
        FormatoDeDocumento.Odt => "ODT",
        _ => throw new ArgumentOutOfRangeException(nameof(formato), formato, "Formato de modelo sem token."),
    };

    private Result ConteudoDiverge() => Result.Failure(new DomainError(
        ModeloDeDocumentoErrorCodes.ConteudoDivergeDoFormato,
        $"O conteúdo do arquivo não é um documento de texto {TokenDe(Formato)}."));

    private static Result ComMacro() => Result.Failure(new DomainError(
        ModeloDeDocumentoErrorCodes.ContemMacro, "O modelo não pode conter macro: ele é distribuído ao candidato."));
}
