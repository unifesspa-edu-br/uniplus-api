namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Leitura do snapshot de tipo aninhado em cada item de <c>etapas</c>.
/// </summary>
/// <remarks>
/// Tem o mesmo formato do bloco de topo <c>tipoProcesso</c>, mas, diferente daquele (só forma, nunca
/// reidratado: o tipo do processo não muda ao restaurar uma versão), o valor lido aqui é efetivamente usado
/// para reconstruir a identidade de tipo da etapa. Um envelope adulterado com <c>origemId</c>, <c>codigo</c>
/// ou <c>nome</c> vazio ou acima do limite é recusado como malformado pela própria factory do VO, nunca
/// reidratado.
/// </remarks>
internal static class LeituraDoTipoDaEtapa
{
    internal static TipoEtapaSnapshot? Ler(LeitorEnvelope leitor, JsonObject item, string path)
    {
        if (leitor.Falhou)
        {
            return null;
        }

        JsonObject tipo = leitor.Objeto(item, "tipoEtapa", path);
        return leitor.Falhou ? null : LerSnapshot(leitor, tipo, $"{path}.tipoEtapa");
    }

    private static TipoEtapaSnapshot? LerSnapshot(LeitorEnvelope leitor, JsonObject tipo, string tipoPath)
    {
        leitor.ExigirChaves(
            tipo, tipoPath, "origemId", "codigo", "nome", "admitePontuacao", "admiteEliminacao", "notaDeOrigemNoEnem");
        Guid origemId = leitor.Identificador(tipo, "origemId", tipoPath);
        string codigo = leitor.TextoNaoVazio(tipo, "codigo", tipoPath, LimitesDoEnvelope.TipoEtapaCodigo);
        string nome = leitor.TextoNaoVazio(tipo, "nome", tipoPath, LimitesDoEnvelope.TipoEtapaNome);
        bool admitePontuacao = leitor.Booleano(tipo, "admitePontuacao", tipoPath);
        bool admiteEliminacao = leitor.Booleano(tipo, "admiteEliminacao", tipoPath);
        bool notaDeOrigemNoEnem = leitor.Booleano(tipo, "notaDeOrigemNoEnem", tipoPath);
        return leitor.Falhou ? null : Criar(leitor, origemId, codigo, nome, admitePontuacao, admiteEliminacao, notaDeOrigemNoEnem);
    }

    private static TipoEtapaSnapshot? Criar(
        LeitorEnvelope leitor, Guid origemId, string codigo, string nome, bool admitePontuacao, bool admiteEliminacao, bool notaDeOrigemNoEnem)
    {
        Result<TipoEtapaSnapshot> resultado = TipoEtapaSnapshot.Criar(
            origemId, codigo, nome, admitePontuacao, admiteEliminacao, notaDeOrigemNoEnem);
        return resultado.IsFailure ? leitor.Propagar<TipoEtapaSnapshot>(resultado.Error!) : resultado.Value;
    }
}
