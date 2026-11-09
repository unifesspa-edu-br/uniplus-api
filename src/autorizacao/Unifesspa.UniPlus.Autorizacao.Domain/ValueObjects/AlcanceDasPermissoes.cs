namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

/// <summary>O alcance que um conjunto de permissões do catálogo dá ao perfil que o reúne.</summary>
internal static class AlcanceDasPermissoes
{
    /// <summary>
    /// Unidade quando todas as permissões são de unidade, instituição quando nenhuma é. Nulo quando mistura os dois.
    /// Permissão que saiu do catálogo não concede nada e não decide o alcance das demais.
    /// </summary>
    public static TipoDeEscopo? De(IEnumerable<string> codigos, ICatalogoDePermissoesConsultavel catalogo)
    {
        bool[] deUnidade = [.. codigos.Where(catalogo.Existe).Select(catalogo.DeUnidade)];

        if (deUnidade.Length == 0 || deUnidade.All(static u => !u))
        {
            return TipoDeEscopo.Instituicao;
        }

        return deUnidade.All(static u => u) ? TipoDeEscopo.Unidade : null;
    }
}
