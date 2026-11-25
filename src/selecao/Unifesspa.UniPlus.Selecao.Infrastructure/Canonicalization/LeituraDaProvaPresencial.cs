namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;

internal static class LeituraDaProvaPresencial
{
    internal static DeclaracaoDaProvaPresencial? Ler(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject bloco = leitor.Objeto(payload, "provaPresencial", "$");
        if (leitor.Falhou)
        {
            return null;
        }

        leitor.ExigirChaves(bloco, "provaPresencial", "temProvaPresencial", "municipios");
        bool temProva = leitor.Booleano(bloco, "temProvaPresencial", "provaPresencial");
        JsonArray municipios = leitor.Array(bloco, "municipios", "provaPresencial");
        return leitor.Falhou ? null : LerDeclaracao(leitor, temProva, municipios);
    }

    private static DeclaracaoDaProvaPresencial? LerDeclaracao(LeitorEnvelope leitor, bool temProva, JsonArray municipios)
    {
        List<MunicipioDeProvaInformado> informados = [];
        Dictionary<string, ResultadoDoMunicipioNoGeo> nomes = new(StringComparer.Ordinal);
        for (int i = 0; i < municipios.Count && !leitor.Falhou; i++)
        {
            LerMunicipio(leitor, municipios, i, informados, nomes);
        }

        if (leitor.Falhou)
        {
            return null;
        }

        Result<DeclaracaoDaProvaPresencial> resultado = DeclaracaoDaProvaPresencial.Declarar(temProva, informados, int.MaxValue);
        return resultado.IsFailure ? leitor.Propagar<DeclaracaoDaProvaPresencial>(resultado.Errors, "") : resultado.Value!.ComNomesDoGeo(MunicipiosConferidosNoGeo.De(nomes));
    }

    private static void LerMunicipio(LeitorEnvelope leitor, JsonArray municipios, int indice,
        List<MunicipioDeProvaInformado> informados, Dictionary<string, ResultadoDoMunicipioNoGeo> nomes)
    {
        string caminho = $"provaPresencial.municipios[{indice}]";
        JsonObject municipio = leitor.ItemObjeto(municipios, indice, "provaPresencial.municipios");
        if (leitor.Falhou)
        {
            return;
        }

        LerCamposDoMunicipio(leitor, municipio, caminho, informados, nomes);
    }

    private static void LerCamposDoMunicipio(LeitorEnvelope leitor, JsonObject municipio, string caminho,
        List<MunicipioDeProvaInformado> informados, Dictionary<string, ResultadoDoMunicipioNoGeo> nomes)
    {
        leitor.ExigirChaves(municipio, caminho, "codigoIbge", "uf", "nome");
        string codigo = leitor.TextoNaoVazio(municipio, "codigoIbge", caminho);
        string uf = leitor.TextoNaoVazio(municipio, "uf", caminho);
        string nome = leitor.TextoNaoVazio(municipio, "nome", caminho);
        if (!leitor.Falhou)
        {
            informados.Add(new(codigo, uf));
            nomes.TryAdd(codigo, ResultadoDoMunicipioNoGeo.Conferido(nome));
        }
    }
}
