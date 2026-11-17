namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>Os motivos que cada processo escolheu do cadastro, em cada fase ou etapa (UNI-REQ-0285).</summary>
public interface IMotivoDoProcessoRepository
{
    /// <summary>A escolha atual do processo, rastreada para mutação: os motivos que ainda não saíram do edital.</summary>
    Task<IReadOnlyList<MotivoDoProcesso>> ListarEscolhaAtualAsync(Guid processoSeletivoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// A escolha atual, rastreada, cada motivo com o motivo de origem no cadastro (nulo se a linha de origem não existe
    /// mais): é o que a publicação confere e recopia.
    /// </summary>
    Task<IReadOnlyList<MotivoEscolhidoComOrigem>> ListarEscolhaAtualComOrigemAsync(Guid processoSeletivoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// A escolha que valia na versão do edital de número <paramref name="numeroDaVersao"/>; nulo quando o processo não tem
    /// essa versão publicada.
    /// </summary>
    Task<IReadOnlyList<MotivoDoProcesso>?> ListarDaVersaoPublicadaAsync(Guid processoSeletivoId, int numeroDaVersao, CancellationToken cancellationToken = default);

    Task AdicionarAsync(MotivoDoProcesso motivo, CancellationToken cancellationToken = default);

    /// <summary>Remove a escolha pendente; a que já consta de versão publicada nunca chega aqui.</summary>
    void Remover(MotivoDoProcesso motivo);
}

/// <summary>O motivo escolhido pelo processo e o motivo do cadastro de onde veio.</summary>
public sealed record MotivoEscolhidoComOrigem(MotivoDoProcesso Escolhido, MotivoDeDecisao? Origem);
