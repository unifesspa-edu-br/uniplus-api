namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Errors;

using Interfaces;

using Services;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Documento que o administrador envia para o processo publicar — o PDF do edital, o anexo do
/// edital, o documento de um ato avulso ou de um resultado. Fica no armazenamento privado por envio
/// direto do cliente (URL pré-assinada de PUT); a API nunca recebe os bytes. Vinculado ao processo
/// por <see cref="ProcessoSeletivoId"/>, mas não é filho do agregado: tem ciclo de vida e
/// repositório próprios. Nasce <see cref="StatusArquivoEnviado.Pendente"/> (URL gerada) e só vira
/// dado de negócio ao ser <see cref="StatusArquivoEnviado.Confirmado"/> (conteúdo lido, validado e
/// hasheado no servidor). O confirmado é imutável: um novo envio cria outro registro, com outra
/// chave, e nunca sobrescreve.
/// </summary>
/// <remarks>
/// O documento não sabe a que ele serve. Quem o referencia — o edital, o anexo, o ato — decide o
/// que exige dele (o PDF do edital, por exemplo, tem de ser <see cref="FormatoDeDocumento.Pdf"/>).
/// </remarks>
public sealed class DocumentoAPublicar : EntityBase, IArquivoEnviado
{
    /// <summary>Tamanho máximo do arquivo. Valor fixo — sem critério de aceite pedindo configurabilidade.</summary>
    public const long TamanhoMaximoBytes = 20 * 1024 * 1024;

    /// <summary>Tamanho máximo do nome do arquivo, com a extensão.</summary>
    public const int NomeArquivoMaxLength = NomeDeArquivoEnviado.TamanhoMaximo;

    private static readonly byte[] AssinaturaPdf = "%PDF-"u8.ToArray();

    public Guid ProcessoSeletivoId { get; private set; }

    /// <summary>
    /// Para que o documento foi enviado. Declarada no início do envio e imutável: é o que separa o
    /// PDF do edital do anexo que também é PDF.
    /// </summary>
    public FinalidadeDoDocumento Finalidade { get; private set; }

    public string NomeArquivo { get; private set; } = string.Empty;
    public FormatoDeDocumento Formato { get; private set; }
    public string ObjectKey { get; private set; } = string.Empty;
    public StatusArquivoEnviado Status { get; private set; }
    public DateTimeOffset ExpiraEm { get; private set; }
    public long? TamanhoBytes { get; private set; }
    public string? HashSha256 { get; private set; }
    public DateTimeOffset? ConfirmadoEm { get; private set; }

    /// <summary>
    /// Chave selada, gravada só na confirmação — <see langword="null"/> enquanto pendente. Nunca é
    /// alvo de URL pré-assinada de PUT (essas só apontam para <see cref="ObjectKey"/>): a cópia do
    /// conteúdo já validado para esta chave, feita no mesmo passo de <see cref="Confirmar"/>, é o
    /// que torna o documento confirmado imutável de fato — sem ela, o titular da URL de envio
    /// poderia sobrescrever o conteúdo até o TTL expirar, mesmo com o registro já confirmado.
    /// </summary>
    public string? ObjectKeyConfirmado { get; private set; }

    /// <summary>A recusa do arquivo maior que <see cref="TamanhoMaximoBytes"/>.</summary>
    public static DomainError TamanhoExcedido { get; } = new(
        DocumentoAPublicarErrorCodes.TamanhoExcedido,
        $"O documento excede o tamanho máximo permitido de {TamanhoMaximoBytes / (1024 * 1024)} MB.");

    /// <summary>A recusa do arquivo sem nenhum byte.</summary>
    public static DomainError ArquivoVazio { get; } = new(
        DocumentoAPublicarErrorCodes.ArquivoVazio, "O arquivo enviado está vazio.");

    /// <summary>A extensão do arquivo no formato, sem o ponto.</summary>
    public string Extensao => FormatosDeDocumento.Extensao(Formato);

    /// <summary>O tipo de mídia do arquivo no formato.</summary>
    public string TipoDeMidia => FormatosDeDocumento.TipoDeMidia(Formato);

    private DocumentoAPublicar() { }

    /// <summary>
    /// Cria o registro pendente que acompanha a URL de envio devolvida ao cliente (passo 1 do
    /// fluxo). A finalidade é obrigatória e define o formato aceito (<see cref="FinalidadesDeDocumento"/>).
    /// O formato vem do tipo de mídia declarado; o nome é o que a pessoa recebe no download,
    /// e a extensão é a do formato. As recusas do tipo de mídia e do nome acumulam. A
    /// <see cref="ObjectKey"/> é composta aqui a partir do <see cref="EntityBase.Id"/> gerado
    /// (Guid v7): a entidade é dona da convenção de caminho, e o chamador não decide o layout do
    /// armazenamento. <paramref name="ttl"/> é o mesmo prazo assinado na URL — serve só para
    /// registrar quando o pendente vencido passa a ser removido pela rotina periódica.
    /// </summary>
    public static Result<DocumentoAPublicar> IniciarPendente(
        Guid processoSeletivoId,
        FinalidadeDoDocumento? finalidade,
        string? nomeArquivo,
        string? tipoDeMidia,
        TimeProvider clock,
        TimeSpan ttl)
    {
        ArgumentNullException.ThrowIfNull(clock);

        FormatoDeDocumento? formato = FormatosDeDocumento.DoTipoDeMidia(tipoDeMidia);
        List<FieldError> erros = [];
        if (formato is null)
        {
            erros.Add(new FieldError("tipoDeMidia", new DomainError(
                DocumentoAPublicarErrorCodes.TipoDeMidiaNaoAceito,
                $"O tipo de mídia deve ser {TiposDeMidiaDeDocumento.Pdf}, {TiposDeMidiaDeDocumento.Docx} ou {TiposDeMidiaDeDocumento.Odt}.")));
        }

        if (finalidade is not { } finalidadeDeclarada || !FinalidadesDeDocumento.Conhecida(finalidadeDeclarada))
        {
            erros.Add(new FieldError("finalidade", new DomainError(
                DocumentoAPublicarErrorCodes.FinalidadeInvalida,
                "A finalidade do documento é obrigatória: edital, anexo do edital ou documento de ato.")));
        }
        else if (formato is { } formatoDeclarado && !FinalidadesDeDocumento.AceitaFormato(finalidadeDeclarada, formatoDeclarado))
        {
            erros.Add(new FieldError("tipoDeMidia", new DomainError(
                DocumentoAPublicarErrorCodes.FormatoNaoAceitoParaAFinalidade,
                finalidadeDeclarada == FinalidadeDoDocumento.DocumentoDoEdital
                    ? $"O documento do edital deve ser um PDF ({TiposDeMidiaDeDocumento.Pdf})."
                    : "O formato do documento não é aceito para a finalidade declarada.")));
        }

        string? nome = NomeDeArquivoEnviado.Normalizar(nomeArquivo, formato is { } f ? FormatosDeDocumento.Extensao(f) : string.Empty);
        if (nome is null)
        {
            erros.Add(new FieldError("nomeArquivo", new DomainError(
                DocumentoAPublicarErrorCodes.NomeArquivoInvalido,
                $"O nome do arquivo é obrigatório, tem até {NomeArquivoMaxLength} caracteres com a extensão e não tem separador de caminho, caractere de controle nem {NomeDeArquivoEnviado.CaracteresProibidos}.")));
        }

        if (erros.Count > 0)
        {
            return Result<DocumentoAPublicar>.ValidationFailure(erros);
        }

        DocumentoAPublicar documento = new()
        {
            ProcessoSeletivoId = processoSeletivoId,
            Finalidade = finalidade!.Value,
            NomeArquivo = nome!,
            Formato = formato!.Value,
            Status = StatusArquivoEnviado.Pendente,
            ExpiraEm = clock.GetUtcNow().Add(ttl),
        };
        documento.ObjectKey = $"selecao/documentos-a-publicar/{processoSeletivoId:D}/{documento.Id:D}.{documento.Extensao}";
        return Result<DocumentoAPublicar>.Success(documento);
    }

    /// <summary>
    /// Finaliza o documento como confirmado e imutável (passo 3 do fluxo) e determina a
    /// <see cref="ObjectKeyConfirmado"/> selada. Só a partir de <see cref="StatusArquivoEnviado.Pendente"/>;
    /// a validação do conteúdo (<see cref="ValidarConteudo"/>) já passou. Quem chama ainda copia o
    /// conteúdo validado para a chave selada no armazenamento — este método só decide o estado e o
    /// nome da chave, sem I/O.
    /// </summary>
    public Result Confirmar(long tamanhoBytes, string hashSha256, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashSha256);

        if (Status != StatusArquivoEnviado.Pendente)
        {
            return Result.Failure(new DomainError(
                DocumentoAPublicarErrorCodes.StatusInvalidoParaConfirmacao, "Somente um documento pendente pode ser confirmado."));
        }

        TamanhoBytes = tamanhoBytes;
        HashSha256 = hashSha256;
        Status = StatusArquivoEnviado.Confirmado;
        ConfirmadoEm = clock.GetUtcNow();
        ObjectKeyConfirmado = $"selecao/documentos-a-publicar/{ProcessoSeletivoId:D}/{Id:D}/confirmado.{Extensao}";
        return Result.Success();
    }

    /// <summary>
    /// Valida o conteúdo lido na confirmação contra o formato declarado no início: tamanho, tipo de
    /// mídia que o objeto declara e estrutura. O tipo de mídia que o objeto declara é só uma
    /// triagem — o cliente o escolhe no envio —; o que decide é o conteúdo: a assinatura
    /// <c>%PDF-</c> no PDF e a estrutura do pacote no DOCX e no ODT
    /// (<see cref="PacoteDeDocumentoDeTexto"/>). O pacote com macro é recusado: o documento vai a
    /// público com a chancela da instituição. Regra pura, sem I/O.
    /// </summary>
    public Result ValidarConteudo(long tamanhoBytes, string contentType, byte[] conteudo)
    {
        ArgumentNullException.ThrowIfNull(conteudo);

        if (tamanhoBytes > TamanhoMaximoBytes)
        {
            return Result.Failure(TamanhoExcedido);
        }

        if (tamanhoBytes == 0)
        {
            return Result.Failure(ArquivoVazio);
        }

        if (!string.Equals(contentType, TipoDeMidia, StringComparison.OrdinalIgnoreCase))
        {
            return ConteudoDiverge();
        }

        VeredictoDoPacote veredito = Formato switch
        {
            FormatoDeDocumento.Pdf => conteudo.AsSpan().StartsWith(AssinaturaPdf) ? VeredictoDoPacote.Valido : VeredictoDoPacote.Diverge,
            FormatoDeDocumento.Docx => PacoteDeDocumentoDeTexto.ValidarDocx(conteudo),
            _ => PacoteDeDocumentoDeTexto.ValidarOdt(conteudo),
        };

        return veredito switch
        {
            VeredictoDoPacote.Valido => Result.Success(),
            VeredictoDoPacote.ContemMacro => Result.Failure(new DomainError(
                DocumentoAPublicarErrorCodes.ContemMacro, "O documento não pode conter macro: ele é publicado com a chancela da instituição.")),
            _ => ConteudoDiverge(),
        };
    }

    private Result ConteudoDiverge() => Result.Failure(new DomainError(
        DocumentoAPublicarErrorCodes.ConteudoDivergeDoFormato,
        $"O conteúdo do arquivo não corresponde a um documento {TipoDeMidia}."));
}
