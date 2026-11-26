namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>A posição do formulário na ordem dos códigos ocupa os bits superiores, seguida do indicador de termo, da ordem e da posição após a seção.</summary>
internal static class PosicaoNaColeta
{
    internal static long DoItem(int posicaoDoFormulario, int ordem) => DaOrdem(posicaoDoFormulario, ordem) | 1;
    internal static long DoTermo(int posicaoDoFormulario, int ordem) => DaOrdem(posicaoDoFormulario, ordem) | (1L << 34) | 1;

    internal static long DaSecao(int posicaoDoFormulario, FormularioProcesso formulario, PaginaDoFormulario secao,
        IEnumerable<FatoColetado> itens, IEnumerable<GrupoColetado> grupos)
    {
        long ordem = GrafoDoFormulario.PosicaoDaPagina(secao.ParaGrafo(), formulario.Paginas.Select(static e => e.ParaGrafo()),
            itens.Where(i => i.FormularioId == formulario.Id).Select(static i => i.ParaGrafo()),
            grupos.Where(g => g.FormularioId == formulario.Id).Select(static g => g.ParaGrafo()));
        return DaOrdem(posicaoDoFormulario, ordem == DependenciasDoFormulario.PosicaoDosTermos ? int.MaxValue : (int)ordem);
    }

    private static long DaOrdem(int posicaoDoFormulario, int ordem) => ((long)posicaoDoFormulario << 35) | ((long)(uint)ordem << 1);
}
