namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;

internal static class SerializacaoDoCadastroDeReserva
{
    internal static JsonArray Serializar(CadastroDeReservaPorChamada cadastro) =>
        new([.. cadastro.Chamadas.Select(static chamada => (JsonNode)new JsonObject
        {
            ["numeroDaChamada"] = chamada.Chamada.Valor,
            ["pessoasDaReservaPorVaga"] = chamada.PessoasDaReservaPorVaga,
        })]);
}
