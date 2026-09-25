namespace Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Identificador legível válido e distinto a cada chamada, para os processos de teste que chegam
/// a gerar versão publicada — a publicação o exige, e a unicidade no banco impede reusar um valor
/// fixo entre testes que compartilham o mesmo banco.
/// </summary>
internal static class IdentificadoresDeTeste
{
    public static IdentificadorLegivel Novo() =>
        IdentificadorLegivel.Criar(NovoValor()).Value;

    public static string NovoValor() => $"certame-{Guid.NewGuid():N}"[..40];
}
