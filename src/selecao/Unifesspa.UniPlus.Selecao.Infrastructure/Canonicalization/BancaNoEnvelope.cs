namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>O que a banca da fase e a da etapa guardam em comum no envelope: a origem copiada do tipo e a regra.</summary>
internal sealed record BancaLida(Guid TipoBancaOrigemId, string Codigo, string Nome, RegraDeDeliberacao? Regra)
{
    internal static BancaLida? Ler(LeitorEnvelope leitor, JsonObject item, string path)
    {
        Guid tipoBancaOrigemId = leitor.Identificador(item, "tipoBancaOrigemId", path);
        string codigo = leitor.TextoNaoVazio(item, "codigo", path, LimitesDoEnvelope.TipoBancaCodigo);
        string nome = leitor.TextoNaoVazio(item, "nome", path, LimitesDoEnvelope.TipoBancaNome);
        RegraDeDeliberacao? regra = RegraDeDeliberacaoNoEnvelope.Ler(leitor, item, path);

        return leitor.Falhou ? null : new BancaLida(tipoBancaOrigemId, codigo, nome, regra);
    }
}
