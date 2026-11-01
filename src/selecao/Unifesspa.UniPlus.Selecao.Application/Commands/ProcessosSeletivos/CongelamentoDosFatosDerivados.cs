namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Os fatos derivados do catálogo que a publicação congela no envelope: o nome, o domínio, a
/// cardinalidade e a proteção de cada um (ADR-0154).
/// </summary>
/// <remarks>
/// O derivado não é coletado, então nenhum item do processo o congela. Na retificação, o derivado que a
/// versão vigente já congelou mantém o que ela congelou: o catálogo só vale para o derivado que a versão
/// ainda não tem e cuja proteção a proteção de dados aprovou.
/// </remarks>
internal static class CongelamentoDosFatosDerivados
{
    public static IReadOnlyDictionary<string, FatoDerivadoCongelado> Resolver(
        IReadOnlyList<FatoCandidatoView> catalogoDeFatos,
        IReadOnlyDictionary<string, FatoDerivadoCongelado>? daVersaoVigente)
    {
        ArgumentNullException.ThrowIfNull(catalogoDeFatos);

        // O derivado que a versão vigente congelou continua no bloco ainda que o catálogo de hoje não o
        // traga mais como derivado (desativado, por exemplo): uma exigência publicada pode comprová-lo.
        Dictionary<string, FatoDerivadoCongelado> congelados = new(daVersaoVigente ?? new Dictionary<string, FatoDerivadoCongelado>(), StringComparer.Ordinal);
        // O derivado sem a proteção aprovada não entra: congelaria uma classificação que a proteção de dados nunca
        // aprovou, e a versão seguinte, que passasse a usá-lo já aprovado, preservaria essa entrada em vez de congelar
        // a proteção aprovada.
        foreach (FatoCandidatoView fato in catalogoDeFatos.Where(static f =>
            f.ProtecaoAprovada && string.Equals(f.Origem, DominioDoCatalogo.OrigemDerivada, StringComparison.Ordinal)))
        {
            congelados.TryAdd(fato.Codigo, new FatoDerivadoCongelado(
                fato.Codigo, fato.Nome, fato.Dominio, fato.Cardinalidade, fato.ClassificacaoProtecao, fato.HipoteseLegal, fato.FinalidadeTratamento));
        }

        return congelados;
    }
}
