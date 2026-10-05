namespace Unifesspa.UniPlus.Selecao.Infrastructure.ExternalServices;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Storage;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;

/// <summary>
/// Implementação de <see cref="IAcervoPublico"/> sobre o <see cref="IStorageService"/> compartilhado:
/// conhece os dois buckets — o privado dos arquivos enviados e o do acervo — e os nomes dos
/// metadados de procedência gravados no objeto.
/// </summary>
public sealed class AcervoPublicoService : IAcervoPublico
{
    private readonly Func<IStorageService> _storageService;
    private readonly IOptions<StorageOptions> _storageOptions;
    private readonly IOptions<AcervoPublicoOptions> _acervoOptions;

    /// <param name="storageService">
    /// O storage, resolvido no uso: a materialização de todo certame depende deste port, e só a do
    /// certame com documento a publicar precisa do armazenamento — e das credenciais dele.
    /// </param>
    public AcervoPublicoService(
        Func<IStorageService> storageService,
        IOptions<StorageOptions> storageOptions,
        IOptions<AcervoPublicoOptions> acervoOptions)
    {
        ArgumentNullException.ThrowIfNull(storageService);
        ArgumentNullException.ThrowIfNull(storageOptions);
        ArgumentNullException.ThrowIfNull(acervoOptions);

        _storageService = storageService;
        _storageOptions = storageOptions;
        _acervoOptions = acervoOptions;
    }

    public async Task CopiarAsync(
        string chavePrivada,
        string chaveNoAcervo,
        DocumentoNoAcervo documento,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documento);

        try
        {
            await CopiarParaOAcervoAsync(chavePrivada, chaveNoAcervo, documento, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception falha) when (falha is not OperationCanceledException)
        {
            // Nomeada para a política de reentrega da divulgação: indisponibilidade do armazenamento
            // e bucket ainda não provisionado se resolvem em minutos, não em segundos.
            throw new AcervoPublicoIndisponivelException(
                $"A cópia para o acervo público em {chaveNoAcervo} falhou.", falha);
        }
    }

    private async Task CopiarParaOAcervoAsync(
        string chavePrivada,
        string chaveNoAcervo,
        DocumentoNoAcervo documento,
        CancellationToken cancellationToken)
    {
        // Os buckets são lidos no uso, e não na construção, pelo mesmo motivo do storage.
        string bucketDoAcervo = _acervoOptions.Value.Bucket is { Length: > 0 } bucket
            ? bucket
            : throw new InvalidOperationException("AcervoPublico:Bucket não configurado — obrigatório para publicar documento no acervo.");

        // Identificadores e hash, nunca dado pessoal: o objeto é lido por qualquer um.
        Dictionary<string, string> procedencia = new(StringComparer.Ordinal)
        {
            ["ato-normativo-id"] = documento.AtoId.ToString("D"),
            ["processo-seletivo-id"] = documento.ProcessoSeletivoId.ToString("D"),
            ["sha256"] = documento.HashSha256,
        };

        await _storageService()
            .CopiarComoObjetoPublicoAsync(
                BucketDosArquivosEnviados.De(_storageOptions.Value),
                chavePrivada,
                bucketDoAcervo,
                chaveNoAcervo,
                new ApresentacaoDoObjetoPublico(documento.ContentType, documento.NomeDeApresentacao, procedencia),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
