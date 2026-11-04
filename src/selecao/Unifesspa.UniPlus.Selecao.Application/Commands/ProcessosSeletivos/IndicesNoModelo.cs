namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Kernel.Results;

/// <summary>
/// A posição, no modelo, das partes que entraram na cópia. O agregado indexa a cópia sem o que saiu dela, e
/// cada recusa dele volta à posição da parte no modelo.
/// </summary>
internal sealed record IndicesNoModelo(List<int> Itens, List<int> Grupos, List<int> Termos)
{
    public FieldError RecusaDoAgregado(FieldError erro) =>
        PosicaoNoModelo.NoModelo(PosicaoNoModelo.NaPosicaoDoModelo(
            PosicaoNoModelo.NaPosicaoDoModelo(PosicaoNoModelo.NaPosicaoDoModelo(erro, "itens", Itens), "grupos", Grupos), "termos", Termos));
}
