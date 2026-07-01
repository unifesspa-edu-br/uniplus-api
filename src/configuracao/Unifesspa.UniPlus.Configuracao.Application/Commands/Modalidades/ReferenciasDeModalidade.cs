namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Modalidades;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;

/// <summary>
/// Coleta os códigos de outras modalidades que uma <see cref="Modalidade"/> cita —
/// <c>ComposicaoOrigem</c> e os argumentos de remanejamento (destino/par/fallback)
/// — para a checagem de integridade referencial no handler (invariante 7). Lê os
/// valores já normalizados pelo agregado.
/// </summary>
internal static class ReferenciasDeModalidade
{
    public static IReadOnlyCollection<string> Coletar(Modalidade modalidade)
    {
        ArgumentNullException.ThrowIfNull(modalidade);

        var referencias = new HashSet<string>(StringComparer.Ordinal);

        Adicionar(referencias, modalidade.ComposicaoOrigem);
        Adicionar(referencias, modalidade.RemanejamentoArgs.Destino);
        Adicionar(referencias, modalidade.RemanejamentoArgs.Par);
        Adicionar(referencias, modalidade.RemanejamentoArgs.Fallback);

        return referencias;
    }

    private static void Adicionar(HashSet<string> destino, string? codigo)
    {
        if (!string.IsNullOrWhiteSpace(codigo))
        {
            destino.Add(codigo);
        }
    }
}
