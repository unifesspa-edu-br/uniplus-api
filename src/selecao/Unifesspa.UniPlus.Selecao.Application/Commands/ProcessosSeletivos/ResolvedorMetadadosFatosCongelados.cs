namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;
using Domain.Entities;
using Kernel.Results;
using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Resolve o metadado congelável (Story #919, RN08) de cada fato do candidato citado em
/// alguma <see cref="CondicaoGatilho"/> de alguma <see cref="DocumentoExigido"/> viva do
/// processo — mapeando <see cref="FatoCandidatoView"/> (Configuração) para
/// <see cref="MetadadoFatoCongelado"/> (Application.Abstractions), nunca reaproveitando
/// <see cref="Domain.ValueObjects.DescritorFatoCandidato"/> (VO mínimo do validador de
/// predicado, propósito distinto: validação de forma, não congelamento de evidência).
/// </summary>
/// <remarks>
/// Espelha <see cref="ConferenciaDeConformidadeLegal"/>/<see cref="ConferenciaDoTipoDeAto"/>
/// em estilo: helper estático compartilhado pelos três handlers que congelam
/// (<c>Publicar</c>, <c>Retificar</c>, <c>FecharRetificacao</c>) — os três precisam do
/// mesmo metadado congelado, pela mesma razão (qualquer um deles pode congelar uma versão
/// com gatilho de documento vivo).
/// </remarks>
internal static class ResolvedorMetadadosFatosCongelados
{
    /// <summary>
    /// <see langword="null"/> quando o processo não tem nenhuma condição de gatilho — nenhum
    /// I/O é disparado nesse caso. Quando existe ao menos uma, todo código referenciado tem
    /// de resolver: o vocabulário de fatos é fechado e append-only (ADR-0111), então um
    /// código que não resolve é defesa em profundidade — nunca deveria acontecer, mas aborta
    /// o congelamento com um erro nomeado em vez de deixar o canonicalizador congelar um
    /// metadado incompleto em silêncio.
    /// </summary>
    public static async Task<Result<IReadOnlyDictionary<string, MetadadoFatoCongelado>?>> ResolverAsync(
        ProcessoSeletivo processo,
        IFatoCandidatoReader fatoCandidatoReader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(fatoCandidatoReader);

        IReadOnlyList<string> codigos = [.. processo.DocumentosExigidos
            .SelectMany(static d => d.Condicoes)
            .Select(static c => c.Fato)
            .Distinct(StringComparer.Ordinal)];

        if (codigos.Count == 0)
        {
            return Result<IReadOnlyDictionary<string, MetadadoFatoCongelado>?>.Success(null);
        }

        Dictionary<string, MetadadoFatoCongelado> metadados = new(StringComparer.Ordinal);
        foreach (string codigo in codigos)
        {
            FatoCandidatoView? fato = await fatoCandidatoReader
                .ObterPorCodigoAsync(codigo, cancellationToken)
                .ConfigureAwait(false);
            if (fato is null)
            {
                return Result<IReadOnlyDictionary<string, MetadadoFatoCongelado>?>.Failure(new DomainError(
                    "ProcessoSeletivo.FatoCongeladoNaoEncontrado",
                    $"O fato '{codigo}', citado numa condição de gatilho de documento exigido, não foi encontrado " +
                    "no catálogo de fatos do candidato — o congelamento não persiste um metadado incompleto."));
            }

            metadados[codigo] = new MetadadoFatoCongelado(
                fato.Codigo,
                fato.Dominio,
                fato.Origem,
                fato.Cardinalidade,
                fato.PontoResolucao,
                fato.Binding,
                fato.ValoresDominio,
                fato.ValoresDominioDeclarados?.Count > 0
                    ? [.. fato.ValoresDominioDeclarados.Select(static v => new ValorDominioDeclaradoCongelado(v.Codigo, v.Descricao))]
                    : null);
        }

        return Result<IReadOnlyDictionary<string, MetadadoFatoCongelado>?>.Success(metadados);
    }
}
