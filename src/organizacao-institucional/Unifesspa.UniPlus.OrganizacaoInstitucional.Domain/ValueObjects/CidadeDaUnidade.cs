namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Referência de cidade do Geo (ADR-0090) de uma Unidade: código IBGE mais o nome e a UF
/// guardados para exibição. Opcional all-or-nothing: ausente por completo é válido, e
/// qualquer fragmento exige o trio.
/// </summary>
public readonly record struct CidadeDaUnidade(string? CodigoIbge, string? Nome, string? Uf)
{
    public static CidadeDaUnidade Nenhuma => default;

    internal bool Informada => !string.IsNullOrWhiteSpace(CodigoIbge);

    internal IReadOnlyList<FieldError> Validar()
    {
        bool algumPresente = Informada
            || !string.IsNullOrWhiteSpace(Nome)
            || !string.IsNullOrWhiteSpace(Uf);
        if (!algumPresente)
        {
            return [];
        }

        // Cada violação vai para o campo do payload que ela de fato descreve.
        return [.. ReferenciaCidadeGeo.Validar(CodigoIbge, Nome, Uf).Errors
            .Select(erro => new FieldError(CampoDoErro(erro.Error.Code), erro.Error))];
    }

    private static string CampoDoErro(string codigoErro) => codigoErro switch
    {
        CidadeReferenciaErrorCodes.NomeObrigatorio
            or CidadeReferenciaErrorCodes.NomeCaractereNulo
            or CidadeReferenciaErrorCodes.NomeTamanho => "cidadeNome",
        CidadeReferenciaErrorCodes.UfObrigatoria
            or CidadeReferenciaErrorCodes.UfIncoerente => "cidadeUf",
        _ => "cidadeCodigoIbge",
    };
}
