namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Documento (PDF) do Edital de um <see cref="ProcessoSeletivo"/> (Story
/// #759, T3 #784) — armazenado no MinIO via upload direto do cliente (URL
/// pre-assinada de PUT); a API nunca recebe os bytes. Vinculado ao processo
/// por <see cref="ProcessoSeletivoId"/>, mas não é entidade filha do
/// agregado: tem ciclo de vida e repositório próprios, pois nasce
/// <see cref="StatusDocumentoEdital.Pendente"/> (URL gerada) e só se torna
/// dado de negócio real ao ser <see cref="StatusDocumentoEdital.Confirmado"/>
/// (conteúdo lido do MinIO, validado e hasheado server-side). Um documento
/// confirmado é imutável — um novo envio sempre cria um novo registro com
/// nova <see cref="ObjectKey"/>, nunca sobrescreve.
/// </summary>
public sealed class DocumentoEdital : EntityBase
{
    /// <summary>Tamanho máximo aceito para o PDF do Edital. Valor fixo — sem
    /// critério de aceite pedindo configurabilidade.</summary>
    public const long TamanhoMaximoBytes = 20 * 1024 * 1024;

    private const string ContentTypeEsperado = "application/pdf";
    private static readonly byte[] AssinaturaPdf = "%PDF-"u8.ToArray();

    public Guid ProcessoSeletivoId { get; private set; }
    public string ObjectKey { get; private set; } = string.Empty;
    public StatusDocumentoEdital Status { get; private set; }
    public DateTimeOffset ExpiraEm { get; private set; }
    public long? TamanhoBytes { get; private set; }
    public string? HashSha256 { get; private set; }
    public DateTimeOffset? ConfirmadoEm { get; private set; }

    /// <summary>
    /// Object key selado, gravado apenas na confirmação — <see langword="null"/>
    /// enquanto pendente. Nunca é alvo de nenhuma URL pre-assinada de PUT
    /// (essas só apontam para <see cref="ObjectKey"/>): a cópia do conteúdo
    /// já validado para esta chave, feita pelo handler no mesmo passo em que
    /// chama <see cref="Confirmar"/>, é o que torna o documento confirmado
    /// realmente imutável — sem isso, o titular da URL de <see cref="ObjectKey"/>
    /// poderia sobrescrever o conteúdo até o TTL expirar, mesmo depois do
    /// registro já marcado como confirmado.
    /// </summary>
    public string? ObjectKeyConfirmado { get; private set; }

    private DocumentoEdital() { }

    /// <summary>
    /// Cria o registro pendente que acompanha a URL pre-assinada de upload
    /// devolvida ao cliente (passo 1 do fluxo). A <see cref="ObjectKey"/> é
    /// composta aqui a partir do próprio <see cref="EntityBase.Id"/> gerado
    /// (Guid v7) — a entidade é dona da própria convenção de path, o caller
    /// não decide o layout do storage. Qual bucket físico guarda o objeto é
    /// decisão de configuração de infraestrutura (<c>StorageOptions</c>),
    /// fora do alcance do domínio — por isso não é um campo aqui.
    /// <paramref name="ttl"/> é o mesmo prazo assinado na URL — usado aqui só
    /// para registrar quando o pendente se torna elegível a limpeza futura,
    /// não para revalidar a assinatura (isso é responsabilidade do MinIO).
    /// </summary>
    public static DocumentoEdital IniciarPendente(
        Guid processoSeletivoId,
        TimeProvider clock,
        TimeSpan ttl)
    {
        ArgumentNullException.ThrowIfNull(clock);

        DocumentoEdital documento = new()
        {
            ProcessoSeletivoId = processoSeletivoId,
            Status = StatusDocumentoEdital.Pendente,
            ExpiraEm = clock.GetUtcNow().Add(ttl),
        };
        documento.ObjectKey = $"selecao/documentos-edital/{processoSeletivoId:D}/{documento.Id:D}.pdf";
        return documento;
    }

    /// <summary>
    /// Finaliza o documento como confirmado e imutável (passo 3 do fluxo) e
    /// determina a <see cref="ObjectKeyConfirmado"/> selada. Só é permitido a
    /// partir de <see cref="StatusDocumentoEdital.Pendente"/> — a validação
    /// do conteúdo (<see cref="ValidarConteudo"/>) já deve ter passado antes
    /// desta chamada. O caller (handler) ainda precisa copiar o conteúdo já
    /// validado para <see cref="ObjectKeyConfirmado"/> no storage — este
    /// método só decide o estado e o nome da chave selada, sem I/O.
    /// </summary>
    public Result Confirmar(long tamanhoBytes, string hashSha256, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashSha256);

        if (Status != StatusDocumentoEdital.Pendente)
        {
            return Result.Failure(new DomainError(
                "DocumentoEdital.StatusInvalidoParaConfirmacao",
                "Somente um documento pendente pode ser confirmado."));
        }

        TamanhoBytes = tamanhoBytes;
        HashSha256 = hashSha256;
        Status = StatusDocumentoEdital.Confirmado;
        ConfirmadoEm = clock.GetUtcNow();
        ObjectKeyConfirmado = $"selecao/documentos-edital/{ProcessoSeletivoId:D}/{Id:D}/confirmado.pdf";
        return Result.Success();
    }

    /// <summary>
    /// Valida o conteúdo lido do MinIO na confirmação: content-type
    /// declarado pelo objeto, tamanho máximo e assinatura de arquivo (magic
    /// bytes <c>%PDF-</c>). Regra de negócio pura — sem I/O; o handler já
    /// buscou <paramref name="tamanhoBytes"/>/<paramref name="contentType"/>
    /// via stat e os primeiros bytes via download antes de chamar aqui.
    /// </summary>
    public static Result ValidarConteudo(long tamanhoBytes, string contentType, ReadOnlySpan<byte> conteudo)
    {
        if (tamanhoBytes > TamanhoMaximoBytes)
        {
            return Result.Failure(new DomainError(
                "DocumentoEdital.TamanhoExcedido",
                $"O documento excede o tamanho máximo permitido de {TamanhoMaximoBytes / (1024 * 1024)} MB."));
        }

        if (!string.Equals(contentType, ContentTypeEsperado, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(new DomainError(
                "DocumentoEdital.ContentTypeInvalido",
                "O documento do Edital deve ser do tipo application/pdf."));
        }

        if (conteudo.Length < AssinaturaPdf.Length || !conteudo[..AssinaturaPdf.Length].SequenceEqual(AssinaturaPdf))
        {
            return Result.Failure(new DomainError(
                "DocumentoEdital.AssinaturaInvalida",
                "O conteúdo do arquivo não corresponde a um PDF válido."));
        }

        return Result.Success();
    }
}
