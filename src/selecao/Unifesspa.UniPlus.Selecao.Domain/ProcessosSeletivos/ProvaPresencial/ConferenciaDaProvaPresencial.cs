namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que a publicação exige da declaração de prova presencial (UNI-REQ-0407, ADR-0090): a declaração
/// existe, o processo com prova presencial tem município, e todo município foi conferido no Geo. O que o
/// Geo não confirmou nunca vale como conferido; a indisponibilidade é tratada antes desta conferência. As
/// recusas se acumulam (ADR-0125).
/// </summary>
public static class ConferenciaDaProvaPresencial
{
    private const string CampoDosMunicipios = "provaPresencial.municipios";

    public static IReadOnlyList<FieldError> Conferir(DeclaracaoDaProvaPresencial? declaracao, MunicipiosConferidosNoGeo conferidos, int limiteDeMunicipios)
    {
        ArgumentNullException.ThrowIfNull(conferidos);

        return declaracao switch
        {
            null => [Recusa("provaPresencial", ProvaPresencialErrorCodes.PorDeclarar, "O processo ainda não declara se tem prova presencial.")],
            { TemProvaPresencial: false } => [],
            { Municipios.Count: 0 } => [Recusa(
                CampoDosMunicipios, ProvaPresencialErrorCodes.SemMunicipio,
                "O processo declara prova presencial e ainda não declara nenhum município de prova.")],
            _ => ConferirMunicipios(declaracao.Municipios, conferidos, limiteDeMunicipios),
        };
    }

    private static List<FieldError> ConferirMunicipios(
        IReadOnlyList<MunicipioDeProva> municipios, MunicipiosConferidosNoGeo conferidos, int limiteDeMunicipios)
    {
        if (municipios.Count > limiteDeMunicipios)
        {
            return [Recusa(CampoDosMunicipios, ProvaPresencialErrorCodes.MunicipiosAcimaDoLimite,
                "O processo declara mais municípios de prova do que o limite admite.")];
        }

        return RecusasDoGeo(municipios, conferidos);
    }

    private static List<FieldError> RecusasDoGeo(IReadOnlyList<MunicipioDeProva> municipios, MunicipiosConferidosNoGeo conferidos)
    {
        List<FieldError> recusas = [];
        for (int indice = 0; indice < municipios.Count; indice++)
        {
            string campo = $"{CampoDosMunicipios}[{indice}].codigoIbge";
            DomainError? erro = ErroDoGeo(conferidos.Resultado(municipios[indice].CodigoIbge));
            if (erro is not null)
            {
                recusas.Add(new FieldError(campo, erro));
            }
        }

        return recusas;
    }

    private static DomainError? ErroDoGeo(ResultadoDoMunicipioNoGeo resultado) => resultado switch
    {
        ResultadoDoMunicipioNoGeo.NaoConferidoNoGeo => new DomainError(
            ProvaPresencialErrorCodes.MunicipioNaoConferidoNoGeo,
            "O Geo não confirmou o município de prova: confira o código do IBGE e a UF informados."),
        _ => null,
    };

    private static FieldError Recusa(string campo, string codigo, string mensagem) => new(campo, new DomainError(codigo, mensagem));
}
