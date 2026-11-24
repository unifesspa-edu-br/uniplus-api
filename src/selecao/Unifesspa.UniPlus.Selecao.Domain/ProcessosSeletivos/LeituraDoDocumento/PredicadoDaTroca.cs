namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;

internal static class PredicadoDaTroca
{
    internal static Result<PredicadoDnf> Ler(JsonElement documento) => PredicadoDnfJson.DeJson(documento);
}
