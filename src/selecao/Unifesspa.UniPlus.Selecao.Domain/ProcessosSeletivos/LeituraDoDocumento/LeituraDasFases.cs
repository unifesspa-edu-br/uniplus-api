namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Ordenacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Lê as declarações de todas as fases, acumulando suas recusas, e adota as que têm fase canônica.
/// A ordem é a posição no documento mais 1, preservando lacunas, e a origem liga as posições canônicas às posições de entrada.
/// </summary>
internal static class LeituraDasFases
{
    internal static FasesLidas Ler(IReadOnlyList<FaseNoDocumento> fases, CadastrosCitadosNoDocumento cadastros) =>
        NaOrdemDaConfiguracao([.. fases.Select(fase => LerFase(fase, cadastros))]);

    private static FaseLida LerFase(FaseNoDocumento fase, CadastrosCitadosNoDocumento cadastros)
    {
        DeclaracaoDaFaseLida declaracao = LerDeclaracao(fase, cadastros);
        if (cadastros.FaseCanonica(fase.Referencias.FaseCanonicaCodigo) is not { } canonica)
        {
            return new(null, fase.PosicaoNoDocumento, fase.Chave, [FaseCanonicaNaoEncontrada(fase), .. declaracao.Recusas], ItensAdotadosDaFase.Nenhum);
        }

        FaseCronograma adotada = FaseCronograma.Adotar(ChaveLocal.Criar(fase.Chave).Value, canonica, declaracao.Cronograma, declaracao.Publicacoes, declaracao.Bancas);
        return new(adotada, fase.PosicaoNoDocumento, fase.Chave, declaracao.Recusas, declaracao.Itens);
    }

    private static DeclaracaoDaFaseLida LerDeclaracao(FaseNoDocumento fase, CadastrosCitadosNoDocumento cadastros)
    {
        ValorLido<UnidadeResponsavelSnapshot?> unidade = LeituraDaUnidadeResponsavel.Ler(fase, cadastros);
        ValorLido<IReadOnlyList<(ProdutoDaFase Item, int PosicaoNoDocumento)>> produtos = LeituraDosProdutosDaFase.Ler(fase, cadastros);
        ValorLido<IReadOnlyList<(BancaRequerida Item, int PosicaoNoDocumento)>> bancas = LeituraDasBancasDaFase.Ler(fase, cadastros);
        ValorLido<RegraDeRecursoLida> regra = LeituraDaRegraDeRecursoDaFase.Ler(fase, [.. produtos.Valor.Select(static par => par.Item)], cadastros);
        ValorLido<RegraDeHomologacaoDasInscricoes?> homologacao = LeituraDaRegraDeHomologacaoDaFase.Ler(fase);

        return new(
            new(fase.PosicaoNoDocumento + 1, fase.Janela.Inicio, fase.Janela.Fim, unidade.Valor) { NoMesmoMomentoDaAnterior = fase.NoMesmoMomentoDaAnterior },
            new([.. produtos.Valor.Select(static par => par.Item)], fase.Publicacoes.EmiteParecerIndividual, regra.Valor.Regra, fase.Publicacoes.TiposDeDecisao, homologacao.Valor),
            [.. bancas.Valor.Select(static par => par.Item)],
            new(produtos.Valor, bancas.Valor, regra.Valor.Procedimentos),
            [.. unidade.Recusas, .. produtos.Recusas, .. bancas.Recusas, .. regra.Recusas, .. homologacao.Recusas]);
    }

    private static RecusaDaParte FaseCanonicaNaoEncontrada(FaseNoDocumento fase) => new(
        null,
        "faseCanonicaId",
        fase.PosicaoNoDocumento,
        null,
        new DomainError(
            FaseCronogramaErrorCodes.FaseCanonicaNaoEncontrada,
            "A fase canônica citada pela fase não foi encontrada ou não está mais viva."));

    private static FasesLidas NaOrdemDaConfiguracao(IReadOnlyList<FaseLida> lidas)
    {
        (FaseCronograma Fase, int Posicao)[] adotadas = [.. lidas.Where(static lida => lida.Fase is not null).Select(static lida => (lida.Fase!, lida.PosicaoNoDocumento))];
        OrigemNoDocumento posicoes = OrigemNoDocumento.DaLista(CronogramaDoProcesso.ListaDasFases, adotadas, OrdemDaConfiguracao.Fases, []);
        OrigemNoDocumento origem = OrigemNoDocumento.Reunir(
            [posicoes, OrigemNoDocumento.SoComPartesRecusadas(PartesRecusadasDasFases.De(lidas, posicoes)), .. ListasDasFases.Origens(lidas, posicoes)]);

        return new([.. OrdemDaConfiguracao.Fases(adotadas.Select(static par => par.Fase))], origem);
    }
}
