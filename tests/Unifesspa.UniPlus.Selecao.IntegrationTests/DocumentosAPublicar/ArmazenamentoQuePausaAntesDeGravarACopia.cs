namespace Unifesspa.UniPlus.Selecao.IntegrationTests.DocumentosAPublicar;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;

/// <summary>
/// Pausa antes de gravar a cópia do arquivo até o teste liberar: a confirmação fica no meio, com o bloqueio do dono retido, e o teste
/// põe outra operação concorrente para disputar com ela. Depois de liberada, grava de verdade.
/// </summary>
internal sealed class ArmazenamentoQuePausaAntesDeGravarACopia(IArquivoArmazenadoStorage armazenamento) : IArquivoArmazenadoStorage
{
    private readonly TaskCompletionSource _chegouAGravacaoDaCopia = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _liberada = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Conclui quando a confirmação chega à gravação da cópia do arquivo.</summary>
    public Task ChegouAGravacaoDaCopia => _chegouAGravacaoDaCopia.Task;

    public void Liberar() => _liberada.SetResult();

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
        _chegouAGravacaoDaCopia.SetResult();
        await _liberada.Task;
        await armazenamento.SalvarConteudoSeladoAsync(objectKey, conteudo, contentType, cancellationToken);
    }

    public Task RemoverAsync(string objectKey, CancellationToken cancellationToken = default) =>
        armazenamento.RemoverAsync(objectKey, cancellationToken);
}
