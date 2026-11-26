namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Os fatos derivados do catálogo que a publicação congela no envelope: o nome, a descrição, o
/// componente de exibição, o domínio, a cardinalidade e a proteção de cada um (ADR-0154).
/// </summary>
/// <remarks>
/// O derivado não é coletado, então nenhum item do processo o congela. Na retificação, o derivado que a
/// versão vigente já congelou mantém a proteção, o domínio e a cardinalidade que ela congelou, e adota a
/// descrição e o componente de exibição de hoje, que são apresentação: o catálogo só vale por inteiro para
/// o derivado que a versão ainda não tem e cuja proteção a proteção de dados aprovou.
/// </remarks>
internal static class CongelamentoDosFatosDerivados
{
    public static IReadOnlyDictionary<string, FatoDerivadoCongelado> Resolver(
        IReadOnlyList<FatoCandidatoView> catalogoDeFatos,
        IReadOnlyDictionary<string, FatoDerivadoCongelado>? daVersaoVigente)
    {
        ArgumentNullException.ThrowIfNull(catalogoDeFatos);

        Dictionary<string, FatoDerivadoCongelado> congelados = MantidosDaVersaoVigente(catalogoDeFatos, daVersaoVigente);
        foreach (FatoCandidatoView fato in catalogoDeFatos.Where(static f => f.ProtecaoAprovada && f.OrigemValor == OrigemFato.Calculado))
        {
            congelados.TryAdd(fato.Codigo, Congelar(fato));
        }

        return congelados;
    }

    // O derivado que a versão vigente congelou continua no bloco ainda que o catálogo de hoje não o
    // traga mais como derivado (desativado, por exemplo): uma exigência publicada pode comprová-lo.
    private static Dictionary<string, FatoDerivadoCongelado> MantidosDaVersaoVigente(
        IReadOnlyList<FatoCandidatoView> catalogoDeFatos, IReadOnlyDictionary<string, FatoDerivadoCongelado>? daVersaoVigente)
    {
        Dictionary<string, FatoCandidatoView> vivos = catalogoDeFatos.ToDictionary(static f => f.Codigo, StringComparer.Ordinal);
        return (daVersaoVigente ?? new Dictionary<string, FatoDerivadoCongelado>()).ToDictionary(
            static par => par.Key,
            par => vivos.TryGetValue(par.Key, out FatoCandidatoView? vivo)
                ? par.Value with { Descricao = vivo.Descricao, ComponenteExibicao = vivo.ComponenteExibicao?.Codigo }
                : par.Value,
            StringComparer.Ordinal);
    }

    // O derivado sem a proteção aprovada não entra: congelaria uma classificação que a proteção de dados nunca
    // aprovou, e a versão seguinte, que passasse a usá-lo já aprovado, preservaria essa entrada em vez de congelar
    // a proteção aprovada.
    private static FatoDerivadoCongelado Congelar(FatoCandidatoView fato) =>
        new(
            fato.Codigo, fato.Nome, fato.Descricao, fato.ComponenteExibicao?.Codigo, fato.DominioValor, fato.CardinalidadeValor,
            fato.ClassificacaoProtecao.Codigo, fato.HipoteseLegal.Codigo, fato.FinalidadeTratamento);
}
