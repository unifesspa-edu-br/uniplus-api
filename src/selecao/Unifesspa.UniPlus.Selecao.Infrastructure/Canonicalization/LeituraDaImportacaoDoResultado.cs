namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;

internal static class LeituraDaImportacaoDoResultado
{
    private const string Bloco = "importacaoDoResultado";

    internal static DeclaracaoDaImportacaoDoResultado? Ler(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject? bloco = leitor.ObjetoOpcional(payload, Bloco, "$");
        if (leitor.Falhou || bloco is null)
        {
            return null;
        }

        leitor.ExigirChaves(bloco, Bloco, "relatorios", "primeiraFaseDepoisDaImportacaoId");
        JsonArray lista = leitor.Array(bloco, "relatorios", Bloco);
        Guid? faseId = leitor.IdentificadorOpcional(bloco, "primeiraFaseDepoisDaImportacaoId", Bloco);
        return leitor.Falhou ? null : LerDeclaracao(leitor, lista, faseId);
    }

    private static DeclaracaoDaImportacaoDoResultado? LerDeclaracao(LeitorEnvelope leitor, JsonArray lista, Guid? faseId)
    {
        List<RelatorioInformado> relatorios = [];
        for (int i = 0; i < lista.Count && !leitor.Falhou; i++)
        {
            relatorios.Add(LerRelatorio(leitor, lista, i));
        }

        if (leitor.Falhou)
        {
            return null;
        }

        Result<DeclaracaoDaImportacaoDoResultado> resultado = DeclaracaoDaImportacaoDoResultado.Declarar(relatorios, faseId);
        return resultado.IsFailure ? leitor.Propagar<DeclaracaoDaImportacaoDoResultado>(resultado.Errors, "") : resultado.Value;
    }

    private static RelatorioInformado LerRelatorio(LeitorEnvelope leitor, JsonArray lista, int indice)
    {
        string caminho = $"{Bloco}.relatorios[{indice}]";
        JsonObject relatorio = leitor.ItemObjeto(lista, indice, $"{Bloco}.relatorios");
        leitor.ExigirChaves(relatorio, caminho, "leiauteCodigo", "leiauteVersao");
        string codigo = leitor.TextoNaoVazio(relatorio, "leiauteCodigo", caminho, RelatorioDeclarado.LeiauteCodigoComprimentoMaximo);
        string versao = leitor.TextoNaoVazio(relatorio, "leiauteVersao", caminho, RelatorioDeclarado.LeiauteVersaoComprimentoMaximo);
        return new(codigo, versao);
    }
}
