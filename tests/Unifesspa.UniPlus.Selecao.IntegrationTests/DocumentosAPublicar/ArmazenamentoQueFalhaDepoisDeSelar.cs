namespace Unifesspa.UniPlus.Selecao.IntegrationTests.DocumentosAPublicar;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;

/// <summary>Grava a cópia selada de verdade e então falha, como o armazenamento que cai entre a gravação e o fim da confirmação.</summary>
internal sealed class ArmazenamentoQueFalhaDepoisDeSelar(IArquivoArmazenadoStorage armazenamento) : IArquivoArmazenadoStorage
{
    public Task<string> GerarUrlUploadAsync(string objectKey, string contentType, TimeSpan expiracao, CancellationToken cancellationToken = default) =>
        armazenamento.GerarUrlUploadAsync(objectKey, contentType, expiracao, cancellationToken);

    public Task<string> GerarUrlLeituraAsync(string objectKey, TimeSpan expiracao, CancellationToken cancellationToken = default) =>
        armazenamento.GerarUrlLeituraAsync(objectKey, expiracao, cancellationToken);

    public Task<InfoObjetoArmazenado?> ObterInfoAsync(string objectKey, CancellationToken cancellationToken = default) =>
        armazenamento.ObterInfoAsync(objectKey, cancellationToken);

    public Task<Stream> AbrirLeituraAsync(string objectKey, long limiteBytes, CancellationToken cancellationToken = default) =>
        armazenamento.AbrirLeituraAsync(objectKey, limiteBytes, cancellationToken);

    public async Task SalvarConteudoSeladoAsync(string objectKey, byte[] conteudo, string contentType, CancellationToken cancellationToken = default)
    {
        await armazenamento.SalvarConteudoSeladoAsync(objectKey, conteudo, contentType, cancellationToken);
        throw new InvalidOperationException("Falha simulada depois de salvar a cópia selada.");
    }

    public Task RemoverAsync(string objectKey, CancellationToken cancellationToken = default) =>
        armazenamento.RemoverAsync(objectKey, cancellationToken);
}
