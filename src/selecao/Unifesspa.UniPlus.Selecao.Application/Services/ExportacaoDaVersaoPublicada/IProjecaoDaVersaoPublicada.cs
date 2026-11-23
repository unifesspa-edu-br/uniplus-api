namespace Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;

using System.Text.Json.Nodes;

public sealed record VersaoPublicada(Guid ProcessoSeletivoId, int Numero, DateTimeOffset PublicadaEm, string Conteudo);

public interface IProjecaoDaVersaoPublicada
{
    JsonObject Projetar(VersaoPublicada versao);
}
