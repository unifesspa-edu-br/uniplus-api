namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que a publicação exige da declaração de prova presencial (UNI-REQ-0407, ADR-0090): a declaração
/// existe, o processo com prova presencial tem município, e todo município foi conferido no Geo. O que o
/// Geo não confirmou nunca vale como conferido; o Geo fora do ar é uma só causa para toda a lista. As
/// recusas se acumulam (ADR-0125).
/// </summary>
public static class ConferenciaDaProvaPresencial
{
    private const string CampoDosMunicipios = "provaPresencial.municipios";

    public static IReadOnlyList<FieldError> Conferir(DeclaracaoDaProvaPresencial? declaracao, MunicipiosConferidosNoGeo conferidos)
    {
        ArgumentNullException.ThrowIfNull(conferidos);

        return declaracao switch
        {
            null => [Recusa("provaPresencial", ProvaPresencialErrorCodes.PorDeclarar, "O processo ainda não declara se tem prova presencial.")],
            { TemProvaPresencial: false } => [],
            { Municipios.Count: 0 } => [Recusa(
                CampoDosMunicipios, ProvaPresencialErrorCodes.SemMunicipio,
                "O processo declara prova presencial e ainda não declara nenhum município de aplicação.")],
            _ => RecusasDoGeo(declaracao.Municipios, conferidos),
        };
    }

    private static List<FieldError> RecusasDoGeo(IReadOnlyList<MunicipioDeAplicacao> municipios, MunicipiosConferidosNoGeo conferidos)
    {
        List<FieldError> recusas = [.. RecusasDeCadaMunicipio(municipios, conferidos)];
        if (municipios.Any(municipio => conferidos.Resultado(municipio.CodigoIbge) is ResultadoDoMunicipioNoGeo.NaoConferidoNoGeo))
        {
            recusas.Add(Recusa(
                CampoDosMunicipios, ProvaPresencialErrorCodes.MunicipiosNaoConferidosNoGeo,
                "O Geo não respondeu, e os municípios de aplicação ainda não foram conferidos; a publicação os confere de novo."));
        }

        return recusas;
    }

    private static IEnumerable<FieldError> RecusasDeCadaMunicipio(IReadOnlyList<MunicipioDeAplicacao> municipios, MunicipiosConferidosNoGeo conferidos)
    {
        for (int indice = 0; indice < municipios.Count; indice++)
        {
            if (conferidos.Resultado(municipios[indice].CodigoIbge) is ResultadoDoMunicipioNoGeo.RecusadoNoGeo recusado)
            {
                yield return new FieldError($"{CampoDosMunicipios}[{indice}].codigoIbge", recusado.Erro);
            }
        }
    }

    private static FieldError Recusa(string campo, string codigo, string mensagem) => new(campo, new DomainError(codigo, mensagem));
}
