namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.ValueObjects;

using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Lê no catálogo de Publicações, para os tipos de ato que o cronograma declara, quais são
/// únicos por objeto (ADR-0056 — leitura cross-módulo).
/// </summary>
/// <remarks>
/// A leitura é I/O e por isso fica aqui, e não no agregado (ADR-0042). Pergunta-se ao catálogo
/// vigente, e não a uma lista de códigos: <c>unico_por_objeto</c> é editável, e o que o catálogo
/// disser no momento de publicar é o que o registro do ato aplicará. Tipo sem versão vigente
/// não entra: quem o recusa é a conferência do tipo declarado, não esta.
/// </remarks>
internal static class ResolucaoDosTiposDeAtoUnicos
{
    public static async Task<TiposDeAtoUnicosPorObjeto> ResolverAsync(
        ITipoAtoPublicadoReader reader,
        ProcessoSeletivo processo,
        DateOnly dataVigencia,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(processo);

        IEnumerable<string> declarados = processo.CronogramaFases
            .SelectMany(static fase => fase.Produtos.Select(static produto => produto.AtoCodigo))
            .Concat(processo.Etapas.SelectMany(static etapa => etapa.Produtos.Select(static produto => produto.AtoCodigo)))
            .Distinct(StringComparer.Ordinal);

        Dictionary<string, string> nomePorCodigo = new(StringComparer.Ordinal);
        foreach (string codigo in declarados)
        {
            TipoAtoPublicadoView? tipo = await reader
                .ObterVigenteAsync(codigo, dataVigencia, cancellationToken)
                .ConfigureAwait(false);

            if (tipo is { UnicoPorObjeto: true })
            {
                nomePorCodigo[codigo] = tipo.Nome;
            }
        }

        return new TiposDeAtoUnicosPorObjeto(nomePorCodigo);
    }
}
