namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Entities;

/// <summary>
/// A projeção de <c>documentosExigidos.arvore</c> de UMA <see cref="Entities.VersaoConfiguracao"/>
/// já reidratada (Story #920) — o insumo de <see cref="Services.ResolvedorArvoreSatisfacao"/>.
/// Substitui <c>BlocoExigenciasCongelado</c> (grupo plano) pela floresta de raízes da árvore de
/// satisfação. Tipo próprio (não <c>IReadOnlyList&lt;NoExigencia&gt;</c> cru) pela mesma razão do
/// antecessor: marca no sistema de tipos a fronteira entre evidência CONGELADA de uma versão
/// publicada e a configuração viva do agregado.
/// </summary>
public sealed record ArvoreExigenciasCongelada
{
    private ArvoreExigenciasCongelada(IReadOnlyList<NoExigencia> raizes)
    {
        Raizes = raizes;
    }

    /// <summary>Nós raiz da floresta (cada raiz é uma árvore de 1..* nós — a folha "solteira" é a árvore degenerada de 1 nó).</summary>
    public IReadOnlyList<NoExigencia> Raizes { get; }

    /// <summary>
    /// Constrói a floresta a partir do grafo já reidratado de uma versão publicada —
    /// <c>EnvelopeReidratado.Grafo.NosExigencia</c> (só as raízes, <c>NoPaiId == null</c>),
    /// nunca <c>ProcessoSeletivo.NosExigencia</c> (a coleção viva plana).
    /// </summary>
    public static ArvoreExigenciasCongelada DeGrafoReidratado(IReadOnlyList<NoExigencia> raizes)
    {
        ArgumentNullException.ThrowIfNull(raizes);
        return new ArvoreExigenciasCongelada([.. raizes]);
    }
}
