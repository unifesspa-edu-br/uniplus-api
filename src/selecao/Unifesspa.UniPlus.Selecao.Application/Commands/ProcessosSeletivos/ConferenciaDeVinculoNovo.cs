namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Desativar um fato ou um valor no catálogo recusa só vínculo novo (ADR-0136): a configuração
/// que já usava o fato, ou já citava o valor, continua com ele. Compara o que o comando propõe
/// com o que o processo já vincula e recusa o que é novo e está desativado.
/// </summary>
internal static class ConferenciaDeVinculoNovo
{
    public static Result Conferir(
        IReadOnlyDictionary<string, FatoCandidatoView> catalogo,
        VinculosDeFatos existentes,
        VinculosDeFatos propostos)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        return ConferenciaNoCatalogo.VinculoNovo(VocabularioDeFatos.ParaRegras(catalogo.Values), existentes, propostos);
    }

    /// <summary>A mesma conferência, com cada recusa apontando o campo do pedido que cita o dado ou o valor.</summary>
    public static IReadOnlyList<FieldError> ConferirNosCampos(
        IReadOnlyDictionary<string, FatoCandidatoView> catalogo,
        VinculosDeFatos existentes,
        IReadOnlyList<VinculosDoCampo> campos)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        return ConferenciaNoCatalogo.VinculosNovosNosCampos(VocabularioDeFatos.ParaRegras(catalogo.Values), existentes, campos);
    }
}
