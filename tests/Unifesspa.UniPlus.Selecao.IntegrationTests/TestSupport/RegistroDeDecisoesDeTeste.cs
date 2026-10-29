namespace Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using System.Collections.Concurrent;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;

/// <summary>
/// Guarda em memória as decisões de acesso que o ponto de decisão registra, para o teste conferir o que a trilha recebeu:
/// quantas decisões, de qual permissão, com qual resultado e qual sensibilidade.
/// </summary>
internal sealed class RegistroDeDecisoesDeTeste : IRegistroOperacionalRestrito
{
    private readonly ConcurrentQueue<RegistroDecisaoAcesso> _registrados = new();

    public IReadOnlyList<RegistroDecisaoAcesso> Registrados => [.. _registrados];

    public void Registrar(RegistroDecisaoAcesso registro) => _registrados.Enqueue(registro);
}
