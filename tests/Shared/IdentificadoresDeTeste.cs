namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O identificador legível que o processo seletivo exige desde a criação, e a publicação
/// também, para fixtures.
/// </summary>
internal static class IdentificadoresDeTeste
{
    /// <summary>
    /// Um identificador válido e distinto a cada chamada: o índice único do banco recusa o mesmo
    /// valor em dois processos, e os testes de integração criam vários na mesma base.
    /// </summary>
    public static IdentificadorLegivel Novo() =>
        IdentificadorLegivel.Criar(NovoValor()).Value!;

    /// <summary>O mesmo identificador, como texto, para quem monta o corpo da requisição.</summary>
    public static string NovoValor() => $"certame-{Guid.NewGuid():N}"[..40];

    /// <summary>
    /// Deixa o processo sem identificador, como estaria o gravado antes de a criação exigi-lo: o
    /// estado não é alcançável pelo domínio, e o EF o hidrata direto da coluna.
    /// </summary>
    public static void Retirar(ProcessoSeletivo processo)
    {
        ArgumentNullException.ThrowIfNull(processo);
        typeof(ProcessoSeletivo)
            .GetProperty(nameof(ProcessoSeletivo.IdentificadorLegivel))!
            .SetValue(processo, null);
    }
}
