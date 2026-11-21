namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que a banca da fase e a da etapa guardam em comum no envelope: a identidade, a posição no tipo, a origem copiada do tipo
/// e a regra.
/// </summary>
internal sealed record BancaLida(Guid Id, int PosicaoNoTipo, Guid TipoBancaOrigemId, string Codigo, string Nome, RegraDeDeliberacao? Regra)
{
    /// <summary>
    /// Lê a banca. A posição no tipo é lida como está no envelope e conferida contra a que a fase ou a etapa atribui, depois
    /// que as bancas chegam a ela (<see cref="BancasLidas{TBanca}.ExigirQueSeguemARaiz"/>): quem numera é a raiz, na ordem de
    /// chegada, e a leitura sozinha só enxerga que a posição começa em 1.
    /// </summary>
    internal static BancaLida? Ler(LeitorEnvelope leitor, JsonObject item, string path)
    {
        Guid id = leitor.Identificador(item, "id", path);
        int posicaoNoTipo = LerPosicaoNoTipo(leitor, item, path);
        Guid tipoBancaOrigemId = leitor.Identificador(item, "tipoBancaOrigemId", path);
        string codigo = leitor.TextoNaoVazio(item, "codigo", path, LimitesDoEnvelope.TipoBancaCodigo);
        string nome = leitor.TextoNaoVazio(item, "nome", path, LimitesDoEnvelope.TipoBancaNome);
        RegraDeDeliberacao? regra = RegraDeDeliberacaoNoEnvelope.Ler(leitor, item, path);

        return leitor.Falhou ? null : new BancaLida(id, posicaoNoTipo, tipoBancaOrigemId, codigo, nome, regra);
    }

    private static int LerPosicaoNoTipo(LeitorEnvelope leitor, JsonObject item, string path)
    {
        int posicao = leitor.Inteiro(item, "posicaoNoTipo", path);

        return !leitor.Falhou && posicao < 1
            ? leitor.Malformado<int>($"{path}.posicaoNoTipo", "a posição no tipo começa em 1.")
            : posicao;
    }
}
