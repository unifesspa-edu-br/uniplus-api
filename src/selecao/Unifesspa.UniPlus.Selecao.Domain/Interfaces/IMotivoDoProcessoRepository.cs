namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>Os motivos que cada processo escolheu do cadastro (UNI-REQ-0285).</summary>
public interface IMotivoDoProcessoRepository
{
    /// <summary>Os motivos escolhidos pelo processo, rastreados para mutação.</summary>
    Task<IReadOnlyList<MotivoDoProcesso>> ListarDoProcessoAsync(Guid processoSeletivoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Os motivos escolhidos, rastreados, cada um com o motivo de origem no cadastro (nulo se a
    /// linha de origem não existe mais): é o que a publicação confere e recopia.
    /// </summary>
    Task<IReadOnlyList<MotivoEscolhidoComOrigem>> ListarComOrigemAsync(Guid processoSeletivoId, CancellationToken cancellationToken = default);

    Task AdicionarAsync(MotivoDoProcesso motivo, CancellationToken cancellationToken = default);

    /// <summary>Remove a escolha ainda não congelada; a congelada nunca chega aqui.</summary>
    void Remover(MotivoDoProcesso motivo);
}

/// <summary>O motivo escolhido pelo processo e o motivo do cadastro de onde veio.</summary>
public sealed record MotivoEscolhidoComOrigem(MotivoDoProcesso Escolhido, MotivoDeDecisao? Origem);
