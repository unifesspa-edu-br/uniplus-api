namespace Unifesspa.UniPlus.Selecao.Application.Abstractions;

/// <summary>
/// Port de storage de objeto para o documento do Edital (Story #759, T3
/// #784). Não expõe conceitos de vendor (bucket, cliente MinIO) — só
/// operações por <c>objectKey</c>, resolvido pela implementação em
/// <c>Selecao.Infrastructure</c> (que é quem conhece o bucket via
/// <c>StorageOptions</c> e envolve o <c>IStorageService</c> compartilhado de
/// <c>Infrastructure.Core</c>). Application não referencia
/// <c>Infrastructure.Core</c> diretamente (ADR-0042 — Application depende
/// só de Domain/SharedKernel; a fitness test R3 do módulo protege a
/// direção de dependência).
/// </summary>
public interface IDocumentoEditalStorage
{
    /// <summary>Gera a URL pre-assinada de PUT para upload direto do cliente.</summary>
    Task<string> GerarUrlUploadAsync(string objectKey, TimeSpan expiracao, CancellationToken cancellationToken = default);

    /// <summary>
    /// Metadados do objeto (tamanho + content-type) sem baixar o conteúdo.
    /// Retorna <see langword="null"/> quando o objeto ainda não foi enviado.
    /// </summary>
    Task<InfoObjetoArmazenado?> ObterInfoAsync(string objectKey, CancellationToken cancellationToken = default);

    /// <summary>Abre o conteúdo do objeto para leitura (hash + validação de assinatura).</summary>
    Task<Stream> AbrirLeituraAsync(string objectKey, CancellationToken cancellationToken = default);
}

/// <summary>Metadados de um objeto de storage, sem o conteúdo.</summary>
public sealed record InfoObjetoArmazenado(long TamanhoBytes, string ContentType);
