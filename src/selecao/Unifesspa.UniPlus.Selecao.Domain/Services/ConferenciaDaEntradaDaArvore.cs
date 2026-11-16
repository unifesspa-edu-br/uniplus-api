namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Entities;

using Enums;

using Kernel.Results;

using ValueObjects;

/// <summary>
/// A defesa em profundidade do resolvedor da árvore de satisfação: a árvore congelada reidratada e as
/// ocorrências do chamador são conferidas antes de desreferenciar, e a recusa é nomeada, nunca exceção
/// (ADR-0076).
/// </summary>
internal static class ConferenciaDaEntradaDaArvore
{
    private const string ArvoreInvalida = "ResolvedorArvoreSatisfacao.ArvoreEstruturalmenteInvalida";
    private const string InstanciaInvalida = "ResolvedorArvoreSatisfacao.InstanciaEntidadeInvalida";

    internal static DomainError? Pendencia(
        ArvoreExigenciasCongelada arvore, IReadOnlyDictionary<string, IReadOnlyList<InstanciaEntidade>> instanciasPorGrupo) =>
        PendenciaDaArvore(arvore) ?? PendenciaDasInstancias(instanciasPorGrupo);

    private static DomainError? PendenciaDaArvore(ArvoreExigenciasCongelada arvore)
    {
        List<NoExigencia> folhas = [.. arvore.Raizes
            .SelectMany(static raiz => raiz.AchatarComDescendentes())
            .Where(static no => no.Tipo == TipoNo.Folha)];

        return PendenciaDasFolhas(folhas) ?? PendenciaDaRepeticaoAninhada(arvore);
    }

    // Folha sem DocumentoExigido só alcançaria o resolvedor via Reidratar mal-formado (nunca via
    // NoExigencia.CriarFolha, que exige o documento); a invariante de outra camada não é presumida.
    private static DomainError? PendenciaDasFolhas(List<NoExigencia> folhas)
    {
        if (folhas.Any(static no => no.DocumentoExigidoId is null || no.DocumentoExigido is null))
        {
            return new DomainError(ArvoreInvalida, "A árvore congelada tem folha sem DocumentoExigido — toda folha precisa envolver uma exigência.");
        }

        List<Guid> idsDeFolhas = [.. folhas.Select(static no => no.DocumentoExigidoId!.Value)];
        return idsDeFolhas.Distinct().Count() != idsDeFolhas.Count
            ? new DomainError(ArvoreInvalida, "A árvore congelada tem exigenciaId repetido entre folhas — cada exigência precisa de identidade única (CA-09).")
            : null;
    }

    // NoExigencia.CriarGrupo recusa aninhamento na escrita, mas Reidratar confia no dado congelado sem
    // revalidar — um envelope corrompido não pode suprimir em silêncio as consequências de uma repetição
    // aninhada.
    private static DomainError? PendenciaDaRepeticaoAninhada(ArvoreExigenciasCongelada arvore) =>
        arvore.Raizes.Any(ContemRepeticaoAninhada)
            ? new DomainError(ArvoreInvalida, "A árvore congelada tem repetição por entidade aninhada — uma subárvore repetePorEntidade não pode conter outra.")
            : null;

    // Duas ocorrências do mesmo grupo com o mesmo entidade_id (ou um entidade_id em branco) tornariam
    // ambígua a correlação (exigência, grupo, entidade_id): apresentações e estados de uma ocorrência
    // vazariam para outra. O insumo é do chamador, não da árvore congelada.
    private static DomainError? PendenciaDasInstancias(IReadOnlyDictionary<string, IReadOnlyList<InstanciaEntidade>> instanciasPorGrupo) =>
        instanciasPorGrupo
            .Select(static par => PendenciaDoGrupo(par.Key, par.Value))
            .FirstOrDefault(static pendencia => pendencia is not null);

    private static DomainError? PendenciaDoGrupo(string grupo, IReadOnlyList<InstanciaEntidade> instancias)
    {
        List<string> ids = [.. instancias.Select(static i => i.EntidadeId)];
        if (ids.Any(static id => string.IsNullOrWhiteSpace(id)))
        {
            return new DomainError(InstanciaInvalida, $"Uma ocorrência do grupo {grupo} tem EntidadeId vazio ou em branco.");
        }

        return ids.Distinct(StringComparer.Ordinal).Count() != ids.Count
            ? new DomainError(InstanciaInvalida, $"Duas ou mais ocorrências do grupo {grupo} declaram o mesmo EntidadeId — cada ocorrência precisa de identidade única.")
            : null;
    }

    private static bool ContemRepeticaoAninhada(NoExigencia no) =>
        (no.RepetePorEntidade is not null && no.Filhos.Any(ContemRepeticaoOuDescendente))
        || no.Filhos.Any(ContemRepeticaoAninhada);

    private static bool ContemRepeticaoOuDescendente(NoExigencia no) =>
        no.RepetePorEntidade is not null || no.Filhos.Any(ContemRepeticaoOuDescendente);
}
