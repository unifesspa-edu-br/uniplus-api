namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Documento em PDF anexado ao pedido de concessão: sobe por URL assinada, o verificador o confere
/// e o promove, e só o anexo verificado é lido. Nunca guarda o conteúdo, só o resumo (SHA-256).
/// </summary>
public sealed class AnexoDoPedido : EntityBase
{
    public const long TamanhoMaximoBytes = 10L * 1024 * 1024;

    private const int ETagMaxLength = 200;
    private const string PrefixoDasChaves = "autorizacao/pedido";

    public Guid PedidoId { get; private set; }

    public SituacaoDoAnexo Situacao { get; private set; }

    /// <summary>Tamanho declarado do PDF, em bytes; é o limite do pedido ao verificador.</summary>
    public long TamanhoDeclarado { get; private set; }

    /// <summary>ETag que o PUT devolveu; fixa a versão do PDF que o verificador confere.</summary>
    public string? ETag { get; private set; }

    public string? Sha256 { get; private set; }

    public DateTimeOffset? EnviadoEm { get; private set; }

    public DateTimeOffset? VerificadoEm { get; private set; }

    /// <summary>Instante em que o verificador recusou o documento, ou pediu o reenvio.</summary>
    public DateTimeOffset? RecusadoEm { get; private set; }

    /// <summary>Chave do PDF no bucket de envio, onde o cliente o sobe pela URL assinada.</summary>
    public string ChaveDeEnvio => $"{PrefixoDasChaves}/{PedidoId:D}/anexo/{Id:D}/envio";

    /// <summary>Chave em que o verificador promove o PDF limpo.</summary>
    public string ChaveDefinitiva => $"{PrefixoDasChaves}/{PedidoId:D}/anexo/{Id:D}/definitivo";

    // Construtor de materialização do EF Core.
    private AnexoDoPedido()
    {
    }

    internal static Result<AnexoDoPedido> Criar(Guid pedidoId, long tamanhoDeclarado)
    {
        if (tamanhoDeclarado is < 1 or > TamanhoMaximoBytes)
        {
            return Result<AnexoDoPedido>.ValidationFailure([new FieldError("tamanhoDoPdf", new DomainError(
                PedidoErrorCodes.AnexoInvalido,
                "O PDF do anexo deve ter de 1 byte a 10 MB."))]);
        }

        return Result<AnexoDoPedido>.Success(new AnexoDoPedido
        {
            PedidoId = pedidoId,
            Situacao = SituacaoDoAnexo.AguardandoEnvio,
            TamanhoDeclarado = tamanhoDeclarado,
        });
    }

    /// <summary>Põe o PDF em verificação, com o ETag que o PUT devolveu.</summary>
    internal Result ConfirmarEnvio(string? etag, DateTimeOffset instante)
    {
        if (Situacao != SituacaoDoAnexo.AguardandoEnvio)
        {
            return Result.Failure(new DomainError(
                PedidoErrorCodes.SituacaoInvalida,
                "O anexo não está aguardando o envio do PDF."));
        }

        if (string.IsNullOrWhiteSpace(etag) || etag.Length > ETagMaxLength || etag.Contains('\0', StringComparison.Ordinal))
        {
            return Result.ValidationFailure([new FieldError("etag", new DomainError(
                PedidoErrorCodes.AnexoInvalido,
                "O ETag que o armazenamento devolveu ao enviar o PDF é obrigatório."))]);
        }

        ETag = etag.Trim();
        EnviadoEm = instante;
        Situacao = SituacaoDoAnexo.EmVerificacao;
        return Result.Success();
    }

    /// <summary>
    /// Registra o veredicto "verificado". Só vale com o anexo em verificação; o resultado que chega
    /// repetido ou fora de hora não muda nada e devolve falso.
    /// </summary>
    public bool RegistrarVerificado(string sha256, DateTimeOffset instante)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);

        if (Situacao != SituacaoDoAnexo.EmVerificacao)
        {
            return false;
        }

        Sha256 = sha256;
        VerificadoEm = instante;
        Situacao = SituacaoDoAnexo.Verificado;
        return true;
    }

    /// <summary>Registra a recusa, ou o pedido de reenvio, do verificador; só vale com o anexo em verificação.</summary>
    public bool RegistrarRecusado(DateTimeOffset instante)
    {
        if (Situacao != SituacaoDoAnexo.EmVerificacao)
        {
            return false;
        }

        RecusadoEm = instante;
        Situacao = SituacaoDoAnexo.Recusado;
        return true;
    }
}
