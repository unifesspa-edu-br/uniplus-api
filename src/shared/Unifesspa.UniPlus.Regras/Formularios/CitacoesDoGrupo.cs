namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Results;

internal sealed class CitacoesDoGrupo(GrupoDoGrafo grupo, DependenciasDoFormulario dependencias)
{
    internal DomainError? PrimeiraRecusa()
    {
        RegraQueCita regra = new($"do grupo '{grupo.Codigo}'", grupo.FatosCitados, null);
        DomainError? recusa = regra.CitacaoInvalidaNaPosicao(grupo.Ordem, dependencias);
        if (recusa is not null)
        {
            return recusa;
        }
        Dictionary<string, int> ordens = grupo.Subitens.ToDictionary(static item => item.FatoCodigo, static item => item.Ordem, StringComparer.Ordinal);
        return grupo.Subitens.Select(item => CitacaoInvalidaDoSubitem(item, ordens)).FirstOrDefault(static erro => erro is not null);
    }

    private DomainError? CitacaoInvalidaDoSubitem(ItemDoGrafo subitem, Dictionary<string, int> ordens)
    {
        string? posterior = subitem.FatosCitados.FirstOrDefault(citado => ordens.TryGetValue(citado, out int ordem) && ordem >= subitem.Ordem);
        if (posterior is not null)
        {
            return new DomainError(GrafoFormularioErrorCodes.CitaFatoPosterior,
                $"Uma regra do fato '{subitem.FatoCodigo}' cita '{posterior}', campo do grupo '{grupo.Codigo}' que vem nele ou depois dele — "
                + "o campo dependeria de uma resposta ainda não dada na ocorrência.");
        }
        RegraQueCita regra = new($"do fato '{subitem.FatoCodigo}'", [.. subitem.FatosCitados.Where(citado => !ordens.ContainsKey(citado))], grupo.Codigo);
        return regra.CitacaoInvalidaNaPosicao(grupo.Ordem, dependencias);
    }
}
