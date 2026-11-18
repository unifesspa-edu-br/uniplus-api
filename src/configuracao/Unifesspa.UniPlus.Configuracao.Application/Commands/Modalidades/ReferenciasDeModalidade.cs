namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Modalidades;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Coleta os códigos de outras modalidades que uma <see cref="Modalidade"/> cita —
/// <c>ComposicaoOrigem</c> e os parâmetros de remanejamento (destino/par/fallback)
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
        Adicionar(referencias, modalidade.RemanejamentoParametros.Destino);
        Adicionar(referencias, modalidade.RemanejamentoParametros.Par);
        Adicionar(referencias, modalidade.RemanejamentoParametros.Fallback);

        return referencias;
    }

    /// <summary>
    /// Confere que todo código citado existe como modalidade viva (invariante 7); sem nenhuma citação, nada a
    /// conferir.
    /// </summary>
    public static async Task<bool> TodasExistemAsync(
        Modalidade modalidade, IModalidadeRepository repository, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<string> referencias = Coletar(modalidade);
        return referencias.Count == 0
            || await repository.CodigosVivosExistemAsync(referencias, cancellationToken).ConfigureAwait(false);
    }

    public static DomainError RecusaDaReferenciaInexistente() =>
        new(ModalidadeErrorCodes.ReferenciaInexistenteOuInativa,
            "Um ou mais códigos de modalidade referenciados (origem ou remanejamento) "
            + "não correspondem a modalidades vivas.");

    private static void Adicionar(HashSet<string> destino, string? codigo)
    {
        if (!string.IsNullOrWhiteSpace(codigo))
        {
            destino.Add(codigo);
        }
    }
}
