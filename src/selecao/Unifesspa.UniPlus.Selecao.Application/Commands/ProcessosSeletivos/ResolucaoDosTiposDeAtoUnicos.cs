namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.ValueObjects;

using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Lê no catálogo de Publicações, para os tipos de ato que o processo cita, quais são únicos
/// por objeto (ADR-0056 — leitura cross-módulo).
/// </summary>
/// <remarks>
/// A leitura é I/O e por isso fica aqui, e não no agregado (ADR-0042). Pergunta-se ao catálogo
/// vigente, e não a uma lista de códigos: <c>unico_por_objeto</c> é editável, e o que o catálogo
/// disser no momento de publicar é o que o registro do ato aplicará. Os códigos vêm do próprio
/// agregado (<see cref="ProcessoSeletivo.DeclaracoesDeAtos"/>) e vão numa só leitura. Tipo sem
/// versão vigente não entra: quem o recusa é a conferência do tipo declarado, não esta.
/// </remarks>
internal static class ResolucaoDosTiposDeAtoUnicos
{
    /// <summary>Para publicar ou retificar: o tipo do ato da operação também ocupa a vaga.</summary>
    public static async Task<TiposDeAtoUnicosPorObjeto> ParaOperacaoAsync(
        ITipoAtoPublicadoReader reader,
        ProcessoSeletivo processo,
        string atoDaOperacaoCodigo,
        DateOnly dataVigencia,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, string> nomePorCodigo = await LerAsync(reader, processo, atoDaOperacaoCodigo, dataVigencia, cancellationToken)
            .ConfigureAwait(false);

        return TiposDeAtoUnicosPorObjeto.ParaOperacao(nomePorCodigo, atoDaOperacaoCodigo);
    }

    /// <summary>Para o checklist, que avalia o processo antes de haver ato.</summary>
    public static async Task<TiposDeAtoUnicosPorObjeto> ParaChecklistAsync(
        ITipoAtoPublicadoReader reader,
        ProcessoSeletivo processo,
        DateOnly dataVigencia,
        CancellationToken cancellationToken) =>
        TiposDeAtoUnicosPorObjeto.ParaChecklist(
            await LerAsync(reader, processo, null, dataVigencia, cancellationToken).ConfigureAwait(false));

    private static async Task<IReadOnlyDictionary<string, string>> LerAsync(
        ITipoAtoPublicadoReader reader,
        ProcessoSeletivo processo,
        string? atoDaOperacaoCodigo,
        DateOnly dataVigencia,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(processo);

        string[] codigos =
        [
            .. processo.DeclaracoesDeAtos().Select(static declaracao => declaracao.AtoCodigo)
                .Concat(atoDaOperacaoCodigo is null ? [] : [atoDaOperacaoCodigo])
                .Distinct(StringComparer.Ordinal),
        ];

        if (codigos.Length == 0)
        {
            return new Dictionary<string, string>();
        }

        IReadOnlyList<TipoAtoPublicadoView> vigentes = await reader
            .ListarVigentesAsync(codigos, dataVigencia, cancellationToken)
            .ConfigureAwait(false);

        return vigentes
            .Where(static tipo => tipo.UnicoPorObjeto)
            .GroupBy(static tipo => tipo.Codigo, StringComparer.Ordinal)
            .ToDictionary(static grupo => grupo.Key, static grupo => grupo.First().Nome, StringComparer.Ordinal);
    }
}
