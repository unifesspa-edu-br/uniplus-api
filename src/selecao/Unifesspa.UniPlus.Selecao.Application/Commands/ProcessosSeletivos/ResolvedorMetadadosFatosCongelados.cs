namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Referencias;

/// <summary>
/// Resolve o metadado congelável (Story #919, congelamento dos parâmetros na publicação, UNI-REQ-0019) de cada fato do candidato citado em
/// alguma <see cref="CondicaoGatilho"/> de alguma <see cref="DocumentoExigido"/> viva do
/// processo — mapeando <see cref="FatoCandidatoView"/> (Configuração) para
/// <see cref="MetadadoFatoCongelado"/> (Application.Abstractions), nunca reaproveitando
/// <see cref="Regras.ValueObjects.DescritorFatoCandidato"/> (VO mínimo do validador de
/// predicado, propósito distinto: validação de forma, não congelamento de evidência).
/// </summary>
/// <remarks>
/// Espelha <see cref="ConferenciaDeConformidadeLegal"/>/<see cref="ConferenciaDoTipoDeAto"/>
/// em estilo: helper estático compartilhado pelos dois handlers que congelam
/// (<c>Publicar</c> e <c>Retificar</c>) — os dois precisam do
/// mesmo metadado congelado, pela mesma razão (qualquer um deles pode congelar uma versão
/// com gatilho de documento vivo). Não faz I/O próprio: recebe o catálogo já lido pelo
/// handler (uma leitura só por congelamento, compartilhada com a conferência de
/// coletabilidade e o resolvedor de valores selecionáveis) — duas leituras abririam janela para
/// a conferência aprovar sobre um catálogo e este resolvedor congelar sobre outro.
/// </remarks>
internal static class ResolvedorMetadadosFatosCongelados
{
    private static readonly IReadOnlySet<string> NenhumFatoExcluido = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// <see langword="null"/> quando o processo não tem nenhuma condição de gatilho. Quando
    /// existe ao menos uma, todo código referenciado tem de resolver contra
    /// <paramref name="catalogo"/>: o vocabulário de fatos é fechado e append-only (ADR-0111),
    /// então um código que não resolve é defesa em profundidade — nunca deveria acontecer, mas
    /// aborta o congelamento com um erro nomeado por fato ausente, na ordem das condições, em vez
    /// de deixar o canonicalizador congelar um metadado incompleto em silêncio.
    /// </summary>
    public static Result<IReadOnlyDictionary<string, MetadadoFatoCongelado>?> Resolver(
        ProcessoSeletivo processo,
        IReadOnlyDictionary<string, FatoCandidatoView> catalogo) =>
        Resolver(processo, catalogo, NenhumFatoExcluido);

    /// <summary>
    /// Resolve o metadado dos fatos citados, menos os dos fatos excluídos (os que o catálogo deixou de aceitar, recusados
    /// pela conferência de coletabilidade): o fato excluído que o catálogo não tem não repete a causa nem interrompe a
    /// resolução dos demais.
    /// </summary>
    internal static Result<IReadOnlyDictionary<string, MetadadoFatoCongelado>?> Resolver(
        ProcessoSeletivo processo,
        IReadOnlyDictionary<string, FatoCandidatoView> catalogo,
        IReadOnlySet<string> fatosExcluidos)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(fatosExcluidos);

        IReadOnlyList<string> codigos = [.. processo.DocumentosExigidos
            .SelectMany(static d => d.Condicoes)
            .Select(static c => c.Fato)
            .Where(c => !fatosExcluidos.Contains(c))
            .Distinct(StringComparer.Ordinal)];

        return codigos.Count == 0
            ? Result<IReadOnlyDictionary<string, MetadadoFatoCongelado>?>.Success(null)
            : ResolverOsCitados(processo, catalogo, codigos);
    }

    private static Result<IReadOnlyDictionary<string, MetadadoFatoCongelado>?> ResolverOsCitados(
        ProcessoSeletivo processo, IReadOnlyDictionary<string, FatoCandidatoView> catalogo, IReadOnlyList<string> codigos)
    {
        IReadOnlyList<FieldError> ausentes = [.. codigos.Where(codigo => !catalogo.ContainsKey(codigo)).Select(static codigo => new FieldError(null, FatoNaoEncontrado(codigo)))];

        return ausentes.Count > 0
            ? Result<IReadOnlyDictionary<string, MetadadoFatoCongelado>?>.ValidationFailure(ausentes)
            : Result<IReadOnlyDictionary<string, MetadadoFatoCongelado>?>.Success(MetadadosDosFatos(processo, catalogo, codigos));
    }

    private static Dictionary<string, MetadadoFatoCongelado> MetadadosDosFatos(
        ProcessoSeletivo processo, IReadOnlyDictionary<string, FatoCandidatoView> catalogo, IReadOnlyList<string> codigos)
    {
        IReadOnlySet<(string Fato, string Valor)> valoresCitados = ReferenciasVivas.Vinculos(processo.Declaracao()).Valores;

        return codigos.ToDictionary(
            static codigo => codigo,
            codigo => Metadado(catalogo[codigo], VocabularioDeFatos.ValoresVigentes(catalogo[codigo], valoresCitados)),
            StringComparer.Ordinal);
    }

    private static DomainError FatoNaoEncontrado(string codigo) =>
        new(
            "ProcessoSeletivo.FatoCongeladoNaoEncontrado",
            $"O fato '{codigo}', citado numa condição de gatilho de documento exigido, não foi encontrado " +
            "no catálogo de fatos do candidato — o congelamento não persiste um metadado incompleto.");

    private static MetadadoFatoCongelado Metadado(FatoCandidatoView fato, IReadOnlyList<FatoValorDominioViewItem> vigentes) =>
        new(
            fato.Codigo,
            fato.DominioValor,
            fato.OrigemValor,
            fato.CardinalidadeValor,
            fato.PontoResolucao,
            fato.Binding,
            vigentes.Count > 0 ? [.. vigentes.Select(static v => v.Codigo)] : fato.ValoresDominio,
            vigentes.Count > 0
                ? [.. vigentes.Select(static v => new ValorDominioDeclaradoCongelado(v.Codigo, v.Descricao, v.Ordem))]
                : null);
}
