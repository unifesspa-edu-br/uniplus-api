namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;

internal static class SerializacaoDoCadastroDeReserva
{
    internal static JsonArray Serializar(CadastroDeReservaPorChamada cadastro) =>
        new([.. cadastro.Chamadas.Select(static chamada => (JsonNode)SerializarChamada(chamada))]);

    private static JsonObject SerializarChamada(CadastroDeReservaDaChamada chamada)
    {
        JsonObject objeto = new()
        {
            ["numeroDaChamada"] = chamada.Chamada.Valor,
            ["pessoasDaReservaPorVaga"] = chamada.PessoasDaReservaPorVaga,
            ["participacaoDaReserva"] = chamada.Participacao?.Codigo(),
        };
        ContextoDosRotulos.Acrescentar(objeto, "participacaoDaReserva", chamada.Participacao);
        return objeto;
    }
}
