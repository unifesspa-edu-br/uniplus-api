namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

internal static class LeituraDosMotivosDeDecisao
{
    internal static IReadOnlyList<MotivoDeDecisaoDeclarado> Ler(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonArray itens = leitor.Array(payload, "motivosDeDecisao", "$");
        List<MotivoDeDecisaoDeclarado> motivos = [];
        for (int i = 0; !leitor.Falhou && i < itens.Count; i++)
        {
            MotivoDeDecisaoDeclarado? motivo = LerMotivo(leitor, itens, i);
            if (motivo is not null)
            {
                motivos.Add(motivo);
            }
        }

        return motivos;
    }

    private static MotivoDeDecisaoDeclarado? LerMotivo(LeitorEnvelope leitor, JsonArray itens, int indice)
    {
        const string bloco = "motivosDeDecisao";
        string caminho = $"{bloco}[{indice}]";
        JsonObject item = leitor.ItemObjeto(itens, indice, bloco);
        if (leitor.Falhou)
        {
            return null;
        }

        leitor.ExigirChaves(item, caminho, "tipoDeDecisaoCodigo", "codigo", "lugar", "descricao", "subclassificacao", "sentido", "versaoDoEditalDeEntrada", "justificativaDaInclusao");
        return Campos(leitor, item, caminho);
    }

    private static MotivoDeDecisaoDeclarado Campos(LeitorEnvelope leitor, JsonObject item, string caminho) => new()
    {
        Tipo = leitor.CodigoDeContrato<TipoMotivoDeDecisao>(item, "tipoDeDecisaoCodigo", caminho),
        Codigo = leitor.TextoNaoVazio(item, "codigo", caminho),
        Lugar = LerLugar(leitor, item, caminho),
        Descricao = leitor.TextoNaoVazio(item, "descricao", caminho),
        Subclassificacao = leitor.TextoOpcional(item, "subclassificacao", caminho),
        Sentido = LerSentido(leitor, item, caminho),
        VersaoDeEntrada = leitor.Inteiro(item, "versaoDoEditalDeEntrada", caminho),
        JustificativaDaInclusao = leitor.TextoOpcional(item, "justificativaDaInclusao", caminho),
    };

    private static LugarDaDecisao LerLugar(LeitorEnvelope leitor, JsonObject item, string caminho)
    {
        JsonObject lugar = leitor.Objeto(item, "lugar", caminho);
        if (leitor.Falhou)
        {
            return default!;
        }

        leitor.ExigirChaves(lugar, $"{caminho}.lugar", "tipo", "id");
        string tipo = leitor.TextoNaoVazio(lugar, "tipo", $"{caminho}.lugar");
        Guid id = leitor.Identificador(lugar, "id", $"{caminho}.lugar");
        return leitor.Falhou ? default! : Lugar(leitor, tipo, id, caminho);
    }

    private static LugarDaDecisao Lugar(LeitorEnvelope leitor, string tipo, Guid id, string caminho) => tipo switch
    {
        "FASE" => LugarDaDecisao.DaFase(id),
        "ETAPA" => LugarDaDecisao.DaEtapa(id),
        _ => leitor.Malformado<LugarDaDecisao>($"{caminho}.lugar.tipo", "esperado FASE ou ETAPA."),
    };

    private static SentidoDoMotivo LerSentido(LeitorEnvelope leitor, JsonObject item, string caminho)
    {
        string codigo = leitor.TextoNaoVazio(item, "sentido", caminho);
        SentidoDoMotivo sentido = SentidoDoMotivoCodigo.FromCodigo(codigo);
        return leitor.Falhou || sentido != SentidoDoMotivo.Nenhum
            ? sentido
            : leitor.Malformado<SentidoDoMotivo>($"{caminho}.sentido", "sentido desconhecido.");
    }
}
