namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;

using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os itens do painel sobre os motivos de decisão escolhidos, um por regra e na ordem em que a conferência da configuração
/// as confere: a mesma consulta sobre os mesmos lugares, de modo que o painel e a conferência concordam. Com parte recusada
/// no que declara os tipos de decisão, os itens que dependem dele ficam pendentes em vez de aprovados por omissão.
/// </summary>
internal static class ItensDosMotivosDeDecisao
{
    internal static IReadOnlyList<ItemConformidade> Avaliar(ConfiguracaoDeclarada configuracao, MotivosEscolhidos motivos)
    {
        LugaresDaDecisao lugares = LugaresDaDecisao.Da(configuracao);

        return
        [
            Item("motivos_escolhidos_ativos_no_cadastro", "Motivos de decisão: nenhum motivo escolhido foi desativado no cadastro antes de entrar na versão", !motivos.Desativados(lugares).Any()),
            Item("motivos_escolhidos_de_tipo_declarado_pelo_lugar", "Motivos de decisão: cada motivo escolhido é de um tipo que a fase ou a etapa declara", !lugares.DeclaracaoDosTiposComParteRecusada && !motivos.DeTipoNaoDeclarado(lugares).Any()),
            Item("motivos_cobrem_as_decisoes_do_processo", "Motivos de decisão: cada fase ou etapa tem os motivos que as decisões dela exigem", !lugares.DeclaracaoDosTiposComParteRecusada && !CoberturaDosMotivos.Faltas(configuracao, lugares, motivos.QueCompoemAVersao(lugares)).Any()),
        ];
    }

    private static ItemConformidade Item(string codigo, string mensagem, bool conforme) =>
        new(codigo, DimensaoConformidade.Cronograma, mensagem, conforme);
}
