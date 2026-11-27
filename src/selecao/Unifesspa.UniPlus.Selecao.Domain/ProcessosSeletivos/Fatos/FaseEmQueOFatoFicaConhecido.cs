namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A fase em que o fato fica conhecido no processo e a recusa de citá-lo antes dela (UNI-REQ-0144, UNI-REQ-0077): o único
/// lugar que decide se o fato é resolvido em fase posterior à da exigência. O fato fica conhecido na mais tardia, pela ordem
/// do cronograma, entre a fase em que o catálogo o situa, as fases dos formulários que o coletam e as fases das dependências;
/// a fase do catálogo que o cronograma não tem é recusada, também quando é a de uma dependência.
/// </summary>
internal static class FaseEmQueOFatoFicaConhecido
{
    internal static FaseConhecida Resolver(IOrigensDoFato origens, OrdemDoCronograma ordem, string fato) =>
        Resolver(origens, ordem, fato, []);

    /// <summary>A recusa de a fase em que o fato fica conhecido ser a de uma fase posterior à da exigência, ou a recusa da resolução.</summary>
    internal static DomainError? RecusaDeFasePosterior(string fato, FaseConhecida conhecida, FaseCronograma faseDaExigencia, OrdemDoCronograma ordem) =>
        conhecida.Recusa ?? (conhecida.Fase is { } fase && ordem.Posterior(fase, faseDaExigencia)
            ? new DomainError(
                DocumentoExigidoErrorCodes.FatoResolvidoEmFasePosterior,
                $"O fato '{fato}' só é conhecido na fase {fase.Citacao} (ordem {fase.Ordem}), posterior à fase em que o documento é exigido (ordem {faseDaExigencia.Ordem}).")
            : null);

    private static FaseConhecida Resolver(IOrigensDoFato origens, OrdemDoCronograma ordem, string fato, HashSet<string> emAvaliacao)
    {
        FaseConhecida doCatalogo = FaseDoCatalogo(origens, fato);
        if (doCatalogo.Recusa is not null)
        {
            return doCatalogo;
        }

        FaseConhecida dasDependencias = emAvaliacao.Add(fato) ? FaseDasDependencias(origens, ordem, fato, emAvaliacao) : FaseConhecida.Nenhuma;

        return dasDependencias.Recusa is not null
            ? dasDependencias
            : new FaseConhecida(ordem.MaisTardia([doCatalogo.Fase, .. origens.FasesDosFormulariosQueColetam(fato), dasDependencias.Fase]), null);
    }

    private static FaseConhecida FaseDoCatalogo(IOrigensDoFato origens, string fato)
    {
        if (origens.CodigoDaFaseDoCatalogo(fato) is not { } codigo)
        {
            return FaseConhecida.Nenhuma;
        }

        return origens.Cronograma.FirstOrDefault(fase => string.Equals(fase.Codigo, codigo, StringComparison.Ordinal)) is { } fase
            ? new FaseConhecida(fase, null)
            : new FaseConhecida(null, new DomainError(
                DocumentoExigidoErrorCodes.PontoResolucaoForaDoCronograma,
                $"O fato '{fato}' resolve na fase '{codigo}', que não pertence ao cronograma deste processo."));
    }

    // As dependências seguem a ordem declarada, e a primeira recusa encerra o percurso.
    private static FaseConhecida FaseDasDependencias(IOrigensDoFato origens, OrdemDoCronograma ordem, string fato, HashSet<string> emAvaliacao)
    {
        List<FaseCronograma?> fases = [];
        foreach (string dependencia in origens.Dependencias(fato))
        {
            FaseConhecida daDependencia = Resolver(origens, ordem, dependencia, emAvaliacao);
            if (daDependencia.Recusa is not null)
            {
                return daDependencia;
            }

            fases.Add(daDependencia.Fase);
        }

        return new FaseConhecida(ordem.MaisTardia(fases), null);
    }
}

/// <summary>A fase em que o fato fica conhecido, ou a recusa da resolução; sem fase nem recusa, nenhuma fase o situa.</summary>
internal sealed record FaseConhecida(FaseCronograma? Fase, DomainError? Recusa)
{
    internal static FaseConhecida Nenhuma { get; } = new(null, null);
}
