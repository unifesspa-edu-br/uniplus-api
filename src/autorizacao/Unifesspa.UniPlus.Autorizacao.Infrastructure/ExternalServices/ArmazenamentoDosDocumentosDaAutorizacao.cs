namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.ExternalServices;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Infrastructure.Core.Storage;

/// <summary>
/// O <see cref="IStorageService"/> que opera com a credencial dos arquivos enviados à verificação. É um tipo
/// próprio, e não um segundo registro de <see cref="IStorageService"/>, para que nenhum consumidor por tipo
/// resolva a credencial restrita no lugar da geral.
/// </summary>
public sealed class ArmazenamentoDosDocumentosDaAutorizacao : IArmazenamentoDosDocumentosDaAutorizacao
{
    private readonly IStorageService _storage;
    private readonly ArquivosDaAutorizacaoOptions _opcoes;

    public ArmazenamentoDosDocumentosDaAutorizacao(IStorageService storage, IOptions<ArquivosDaAutorizacaoOptions> opcoes)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(opcoes);

        _storage = storage;
        _opcoes = opcoes.Value;
    }

    public Task<string> GerarUrlDeEnvioAsync(string chaveDeEnvio, string contentType, TimeSpan expiracao, CancellationToken cancellationToken = default) =>
        _storage.GerarUrlUploadTemporariaAsync(_opcoes.BucketDeEnvio, chaveDeEnvio, expiracao, contentType, cancellationToken);

    public Task<string> GerarUrlDeLeituraAsync(string chaveDefinitiva, string nomeDoArquivo, TimeSpan expiracao, CancellationToken cancellationToken = default) =>
        _storage.GerarUrlTemporariaComDisposicaoAsync(
            _opcoes.BucketDefinitivo, chaveDefinitiva, expiracao, $"attachment; filename=\"{nomeDoArquivo}\"", cancellationToken);
}
