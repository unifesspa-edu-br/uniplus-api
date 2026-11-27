namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Ordenacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Lê as declarações de todas as etapas, acumulando as recusas, e adota as que têm tipo de etapa no catálogo. A ordem da etapa é a
/// posição no documento mais 1, preservando lacunas: a etapa recusada sai da lista, e as seguintes conservam a posição enviada, que
/// as causas apontam. A origem liga as posições canônicas às posições de entrada.
/// </summary>
internal static class LeituraDasEtapas
{
    internal static EtapasLidas Ler(IReadOnlyList<EtapaNoDocumento> etapas, FasesLidas fases, CadastrosCitadosNoDocumento cadastros)
    {
        ArgumentNullException.ThrowIfNull(etapas);
        ArgumentNullException.ThrowIfNull(fases);
        ArgumentNullException.ThrowIfNull(cadastros);

        return NaOrdemDaConfiguracao([.. etapas.Select(etapa => LerEtapa(etapa, fases, cadastros))]);
    }

    private static EtapaLida LerEtapa(EtapaNoDocumento etapa, FasesLidas fases, CadastrosCitadosNoDocumento cadastros)
    {
        TipoEtapaSnapshot? tipo = cadastros.TipoDeEtapa(etapa.Identificacao.TipoEtapaCodigo);
        DeclaracaoDaEtapaLida declaracao = LerDeclaracao(etapa, fases, cadastros);
        RecusaDaParte[] recusas = [.. RecusaDoTipo(etapa, tipo), .. declaracao.Recusas];

        return tipo is null
            ? new(null, etapa.PosicaoNoDocumento, etapa.Chave, recusas, ItensAdotadosDaEtapa.Nenhum)
            : new(Adotar(etapa, tipo, declaracao), etapa.PosicaoNoDocumento, etapa.Chave, recusas, declaracao.Itens);
    }

    private static DeclaracaoDaEtapaLida LerDeclaracao(EtapaNoDocumento etapa, FasesLidas fases, CadastrosCitadosNoDocumento cadastros)
    {
        ValorLido<string?> fase = LeituraDaFaseDaEtapa.Ler(etapa, fases);
        AvaliacaoLidaDaEtapa avaliacao = LeituraDaAvaliacaoDaEtapa.Ler(etapa, cadastros);
        ValorLido<IReadOnlyList<(ProdutoDaEtapa Item, int PosicaoNoDocumento)>> produtos = LeituraDosProdutosDaEtapa.Ler(etapa, cadastros);
        ValorLido<IReadOnlyList<(BancaDaEtapa Item, int PosicaoNoDocumento)>> bancas = LeituraDasBancasDaEtapa.Ler(etapa, cadastros);
        ProdutoDaEtapa[] produtosAceitos = [.. produtos.Valor.Select(static par => par.Item)];
        ValorLido<IReadOnlyList<RecursoLido>> recursos = LeituraDosRecursosDaEtapa.Ler(etapa, produtosAceitos, avaliacao.ItensPelaChave, cadastros);

        return new(
            fase.Valor,
            avaliacao,
            new(produtosAceitos, etapa.EmiteParecerIndividual, [.. recursos.Valor.Select(static recurso => recurso.Recurso)], etapa.TiposDeDecisao),
            [.. bancas.Valor.Select(static par => par.Item)],
            new(produtos.Valor, bancas.Valor, recursos.Valor),
            [.. fase.Recusas, .. avaliacao.Recusas, .. produtos.Recusas, .. bancas.Recusas, .. recursos.Recusas]);
    }

    private static EtapaProcesso Adotar(EtapaNoDocumento etapa, TipoEtapaSnapshot tipo, DeclaracaoDaEtapaLida declaracao) => EtapaProcesso.Adotar(
        ChaveLocal.Criar(etapa.Chave).Value,
        new IdentificacaoDaEtapa(etapa.Identificacao.Nome ?? string.Empty, etapa.Identificacao.Carater ?? default, tipo, etapa.PosicaoNoDocumento + 1, declaracao.FaseCodigo),
        declaracao.Avaliacao.Declaracao,
        declaracao.Avaliacao.Itens,
        etapa.Janela,
        declaracao.Publicacoes,
        declaracao.Bancas);

    private static IEnumerable<RecusaDaParte> RecusaDoTipo(EtapaNoDocumento etapa, TipoEtapaSnapshot? tipo) =>
        tipo is null
            ? [new RecusaDaParte(null, "tipoEtapaOrigemId", etapa.PosicaoNoDocumento, etapa.Chave, RecusaDoTipoDeEtapa.NaoEncontradoOuInativo("indicado"))]
            : [];

    private static EtapasLidas NaOrdemDaConfiguracao(IReadOnlyList<EtapaLida> lidas)
    {
        (EtapaProcesso Etapa, int Posicao)[] adotadas = [.. lidas.Where(static lida => lida.Etapa is not null).Select(static lida => (lida.Etapa!, lida.PosicaoNoDocumento))];
        OrigemNoDocumento posicoes = OrigemNoDocumento.DaLista(ListasDasEtapas.ListaDasEtapas, adotadas, OrdemDaConfiguracao.Etapas, []);
        OrigemNoDocumento partesRecusadas = OrigemNoDocumento.SoComPartesRecusadas(PartesRecusadasDasEtapas.De(lidas, posicoes));

        return new(
            [.. OrdemDaConfiguracao.Etapas(adotadas.Select(static par => par.Etapa))],
            ChavesDasPartesLidas.IdPelaChave(adotadas.Select(static par => (par.Etapa.Chave.Valor, par.Etapa.Id))),
            ChavesDasPartesLidas.Recusadas(lidas.Where(static lida => lida.Etapa is null).Select(static lida => lida.Chave)),
            [.. lidas.SelectMany(static lida => lida.Recusas)],
            [posicoes, partesRecusadas, .. ListasDasEtapas.Origens(lidas, posicoes)]);
    }
}
