namespace Unifesspa.UniPlus.Selecao.Infrastructure.ExternalServices;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Storage;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;

/// <summary>
/// <see cref="IArquivoDoCandidatoStorage"/> sobre o <see cref="IStorageService"/> compartilhado.
/// </summary>
public sealed class ArquivoDoCandidatoStorageService : IArquivoDoCandidatoStorage
{
    public const string ChaveDoArmazenamento = "arquivos-do-candidato";

    private readonly IStorageService _storageService;
    private readonly ArquivosDoCandidatoOptions _opcoes;

    public ArquivoDoCandidatoStorageService(
        [FromKeyedServices(ChaveDoArmazenamento)] IStorageService storageService, IOptions<ArquivosDoCandidatoOptions> opcoes)
    {
        ArgumentNullException.ThrowIfNull(storageService);
        ArgumentNullException.ThrowIfNull(opcoes);

        _storageService = storageService;
        _opcoes = opcoes.Value;
    }

    public Task<string> GerarUrlUploadAsync(string chaveDeEnvio, string contentType, TimeSpan expiracao, CancellationToken cancellationToken = default) =>
        _storageService.GerarUrlUploadTemporariaAsync(_opcoes.BucketDeEnvio, chaveDeEnvio, expiracao, contentType, cancellationToken);

    public Task<string> GerarUrlLeituraAsync(string chaveDefinitiva, string nomeDeApresentacao, TimeSpan expiracao, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nomeDeApresentacao);

        // filename* (RFC 5987): o nome percent-encoded não injeta parâmetros no cabeçalho.
        string disposicao = $"inline; filename*=UTF-8''{Uri.EscapeDataString(nomeDeApresentacao)}";
        return _storageService.GerarUrlTemporariaComDisposicaoAsync(_opcoes.BucketDefinitivo, chaveDefinitiva, expiracao, disposicao, cancellationToken);
    }

    public Task RemoverDefinitivoAsync(string chaveDefinitiva, CancellationToken cancellationToken = default) =>
        _storageService.RemoverAsync(_opcoes.BucketDefinitivo, chaveDefinitiva, cancellationToken);
}
