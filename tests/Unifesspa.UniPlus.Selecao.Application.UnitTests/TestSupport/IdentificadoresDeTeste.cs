namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Identificador legível válido e distinto a cada chamada, para os processos de teste que chegam
/// a gerar versão publicada — a publicação o exige.
/// </summary>
internal static class IdentificadoresDeTeste
{
    public static IdentificadorLegivel Novo() =>
        IdentificadorLegivel.Criar($"certame-{Guid.NewGuid():N}"[..40]).Value;
}
