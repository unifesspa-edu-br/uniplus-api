namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;

internal static class LeituraDoCadastroDeReserva
{
    private const string Bloco = "cadastroDeReserva";

    internal static CadastroDeReservaPorChamada? Ler(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonArray itens = leitor.Array(payload, Bloco, "$");
        List<CadastroDeReservaInformado>? informados = leitor.Falhou ? null : LeituraDeItens.Ler(leitor, itens, Bloco, LerItem);
        return informados is null ? null : Declarar(leitor, informados);
    }

    private static CadastroDeReservaPorChamada? Declarar(LeitorEnvelope leitor, List<CadastroDeReservaInformado> informados)
    {
        Result<CadastroDeReservaPorChamada> resultado = CadastroDeReservaPorChamada.Declarar(informados);
        return resultado.IsFailure ? leitor.Propagar<CadastroDeReservaPorChamada>(resultado.Errors, "") : resultado.Value;
    }

    private static CadastroDeReservaInformado? LerItem(LeitorEnvelope leitor, JsonObject item, string caminho)
    {
        leitor.ExigirChaves(item, caminho, "numeroDaChamada", "pessoasDaReservaPorVaga", "participacaoDaReserva");
        int numero = leitor.Inteiro(item, "numeroDaChamada", caminho);
        int pessoas = leitor.Inteiro(item, "pessoasDaReservaPorVaga", caminho);
        string? participacao = leitor.TextoOpcional(item, "participacaoDaReserva", caminho, LimitesDoEnvelope.Token);
        return leitor.Falhou ? null : new CadastroDeReservaInformado(numero, pessoas, participacao);
    }
}
