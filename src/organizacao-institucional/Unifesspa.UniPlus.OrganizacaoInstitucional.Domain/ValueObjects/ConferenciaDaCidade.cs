namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A conferência da referência de cidade do Geo (ADR-0090), a mesma para a cidade da Unidade e a da
/// Instituição: opcional all-or-nothing, em que ausente por completo é válido e qualquer fragmento
/// exige o trio (código IBGE, nome e UF).
/// </summary>
internal static class ConferenciaDaCidade
{
    /// <summary>
    /// Cada violação vai para o campo do payload que ela de fato descreve (camelCase, ADR-0023): sem
    /// isso, todo erro de cidade seria rotulado com o código IBGE, mesmo quando a causa é o nome ou a UF.
    /// </summary>
    internal static IReadOnlyList<FieldError> Conferir(string? codigoIbge, string? nome, string? uf)
    {
        bool algumPresente = !string.IsNullOrWhiteSpace(codigoIbge)
            || !string.IsNullOrWhiteSpace(nome)
            || !string.IsNullOrWhiteSpace(uf);
        if (!algumPresente)
        {
            return [];
        }

        return [.. ReferenciaCidadeGeo.Validar(codigoIbge, nome, uf).Errors
            .Select(erro => new FieldError(CampoDoErro(erro.Error.Code), erro.Error))];
    }

    private static string CampoDoErro(string codigoErro) => codigoErro switch
    {
        CidadeReferenciaErrorCodes.NomeObrigatorio
            or CidadeReferenciaErrorCodes.NomeCaractereInvalido
            or CidadeReferenciaErrorCodes.NomeTamanho => "cidadeNome",
        CidadeReferenciaErrorCodes.UfObrigatoria
            or CidadeReferenciaErrorCodes.UfIncoerente => "cidadeUf",
        _ => "cidadeCodigoIbge",
    };
}
