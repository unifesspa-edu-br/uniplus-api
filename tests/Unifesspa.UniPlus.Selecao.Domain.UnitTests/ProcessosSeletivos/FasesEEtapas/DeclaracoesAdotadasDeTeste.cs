namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A fase e a etapa que um documento lido entrega, adotadas sem conferência, e a configuração declarada que as reúne com a
/// origem que a leitura montou.
/// </summary>
internal static class DeclaracoesAdotadasDeTeste
{
    internal static FaseCronograma Adotada(this ModeloDeFase modelo) =>
        FaseCronograma.Adotar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!, modelo.Origem(), modelo.Decisoes(), modelo.Publicacoes(), modelo.Bancas);

    internal static EtapaProcesso Adotada(this ModeloDeEtapa modelo) => EtapaProcesso.Adotar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
        modelo.Identificacao(),
        new AvaliacaoDeclaradaDaEtapa(modelo.Avaliacao(), modelo.Itens, modelo.Conceito),
        new JanelaDeTempo(modelo.Inicio, modelo.Fim),
        new PublicacoesDaEtapa(modelo.Produtos, modelo.EmiteParecerIndividual, modelo.Recursos, modelo.TiposDeDecisao),
        modelo.Bancas);

    internal static FieldError Causa(string ponteiro) => new(ponteiro, new DomainError("Leitura.ParteRecusada", "A parte foi recusada na leitura."));

    internal static ParteRecusada Recusada(string colecao, int posicaoDeOrigem) =>
        new(colecao, null, posicaoDeOrigem, Causa($"/{colecao.Replace("[", "/", StringComparison.Ordinal).Replace("]", string.Empty, StringComparison.Ordinal).Replace('.', '/')}/{posicaoDeOrigem}"));

    internal static OrigemNoDocumento ComPartesRecusadas(params string[] colecoes) =>
        new(new Dictionary<string, IReadOnlyList<int>>(), [.. colecoes.Select(static (colecao, posicao) => Recusada(colecao, posicao))]);

    internal static ConfiguracaoDeclarada Lida(
        IReadOnlyList<FaseCronograma> fases, IReadOnlyList<EtapaProcesso> etapas, OrigemNoDocumento origem, ConfiguracaoDeclarada? daRaiz = null)
    {
        ConfiguracaoDeclarada raiz = daRaiz ?? ProcessoConformeFactory.Criar().Declaracao();

        return new ConfiguracaoDeclarada(
            raiz.Identidade,
            new CronogramaDeclarado(raiz.Cronograma.Localidade, raiz.Cronograma.AlgoritmoContagemPrazo, fases),
            new AvaliacaoDeclarada(etapas, raiz.Avaliacao.CriteriosDesempate, raiz.Avaliacao.Classificacao),
            raiz.Vagas,
            raiz.Exigencias,
            raiz.Coleta,
            raiz.DivulgacaoETaxa)
        {
            Origem = origem,
        };
    }
}
