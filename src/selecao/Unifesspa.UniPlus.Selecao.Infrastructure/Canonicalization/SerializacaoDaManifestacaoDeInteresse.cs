namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Manifestacao;

internal static class SerializacaoDaManifestacaoDeInteresse
{
    internal static JsonNode? Serializar(ManifestacaoDeInteresse? manifestacao) =>
        manifestacao?.Codigo();
}
