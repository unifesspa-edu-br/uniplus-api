namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>A finalidade ocupa os bits superiores, seguida do indicador de termo, da ordem e da posição após a seção.</summary>
internal static class PosicaoNaColeta
{
    internal static long DoItem(FinalidadeFormulario finalidade, int ordem) => DaOrdem(finalidade, ordem) | 1;
    internal static long DoTermo(FinalidadeFormulario finalidade, int ordem) => DaOrdem(finalidade, ordem) | (1L << 34) | 1;

    internal static long DaSecao(FormularioProcesso formulario, PaginaDoFormulario secao,
        IEnumerable<FatoColetado> itens, IEnumerable<GrupoColetado> grupos)
    {
        long ordem = GrafoDoFormulario.PosicaoDaPagina(secao.ParaGrafo(), formulario.Paginas.Select(static e => e.ParaGrafo()),
            itens.Where(i => i.Finalidade == formulario.Finalidade).Select(static i => i.ParaGrafo()),
            grupos.Where(g => g.Finalidade == formulario.Finalidade).Select(static g => g.ParaGrafo()));
        return DaOrdem(formulario.Finalidade, ordem == DependenciasDoFormulario.PosicaoDosTermos ? int.MaxValue : (int)ordem);
    }

    private static long DaOrdem(FinalidadeFormulario finalidade, int ordem) => ((long)finalidade << 35) | ((long)(uint)ordem << 1);
}
