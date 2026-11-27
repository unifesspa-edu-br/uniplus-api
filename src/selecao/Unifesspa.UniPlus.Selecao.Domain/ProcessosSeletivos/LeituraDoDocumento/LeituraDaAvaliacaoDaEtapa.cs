namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Como a nota da etapa é lançada e de que itens e níveis ela se compõe. O item e o nível conservam a chave que o documento
/// declarou, e a ordem de cada um é a posição mais 1. A regra de consolidação que o catálogo não tem é recusa da regra, e a etapa
/// segue sem ela; a conferência da pontuação, do rótulo e da coerência da avaliação é da declaração da etapa.
/// </summary>
internal static class LeituraDaAvaliacaoDaEtapa
{
    internal const string CampoDaRegra = "regraConsolidacao";

    internal static AvaliacaoLidaDaEtapa Ler(EtapaNoDocumento etapa, CadastrosCitadosNoDocumento cadastros)
    {
        AvaliacaoDaEtapaNoDocumento avaliacao = etapa.Avaliacao;
        ValorLido<ReferenciaRegra?> regra = Regra(etapa, cadastros);
        ItemDeAvaliacao[] itens = [.. avaliacao.Itens.Select(Item)];
        NivelDeAtendimento?[] niveis = [.. avaliacao.Niveis.Select(Nivel)];

        return new(
            new(etapa.Identificacao.Peso, etapa.Identificacao.NotaMinima, avaliacao.NotaMaxima, avaliacao.PassoDoLancamento, avaliacao.Forma, regra.Valor, niveis),
            itens,
            ItensPelaChave(itens),
            regra.Recusas);
    }

    private static ItemDeAvaliacao Item(ItemDeAvaliacaoNoDocumento item) =>
        ItemDeAvaliacao.Criar(ChaveLocal.Criar(item.Chave).Value, new ItemDeAvaliacaoDeclarado(null, item.Rotulo ?? string.Empty, item.PontuacaoMaxima ?? 0m));

    private static NivelDeAtendimento Nivel(NivelDeAtendimentoNoDocumento nivel) =>
        NivelDeAtendimento.Criar(ChaveLocal.Criar(nivel.Chave).Value, nivel.Rotulo ?? string.Empty, nivel.Fracao ?? 0m);

    private static Dictionary<string, Guid> ItensPelaChave(IEnumerable<ItemDeAvaliacao> itens)
    {
        Dictionary<string, Guid> pelaChave = new(StringComparer.Ordinal);
        foreach (ItemDeAvaliacao item in itens)
        {
            pelaChave.TryAdd(item.Chave.Valor, item.Id);
        }

        return pelaChave;
    }

    private static ValorLido<ReferenciaRegra?> Regra(EtapaNoDocumento etapa, CadastrosCitadosNoDocumento cadastros)
    {
        if (etapa.Avaliacao.RegraDeApuracao is null or { Codigo: null, Versao: null })
        {
            return new(null, []);
        }

        RegraDoCatalogoCitada? citada = cadastros.Regra(etapa.Avaliacao.RegraDeApuracao.Codigo, etapa.Avaliacao.RegraDeApuracao.Versao);

        return citada is { Tipo: TipoRegra.RegraApuracaoAvaliacao } ? new(citada.Referencia, []) : new(null, [RecusaDaRegra(etapa)]);
    }

    private static RecusaDaParte RecusaDaRegra(EtapaNoDocumento etapa) => new(
        CampoDaRegra,
        CampoDaRegra,
        etapa.PosicaoNoDocumento,
        etapa.Chave,
        new DomainError(EtapaProcessoErrorCodes.RegraDeConsolidacaoInvalida, "A regra declarada não é uma regra de consolidação do catálogo."));
}
