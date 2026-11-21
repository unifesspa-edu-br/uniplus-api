namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// A regra de homologação das inscrições é declaração da fase: entra pelas publicações e é reposta ao atualizar.
/// </summary>
public sealed class FaseCronogramaRegraDeHomologacaoTests
{
    private const string Homologacao = TipoMotivoDeDecisaoCodigo.HomologacaoDasInscricoes;

    private static readonly RegraDeHomologacaoDasInscricoes Regra =
        RegraDeHomologacaoDasInscricoes.Declarar("RESULTADO_HOMOLOGACAO", []);

    private static FaseCronograma FaseCom(RegraDeHomologacaoDasInscricoes? regra, params string?[] tipos) => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), "HOMOLOGACAO_INSCRICOES", OrigemDataFase.NaExecucao,
            AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(1, null, null, UnidadesDeTeste.Ceps),
        new PublicacoesDaFase([ProdutoDaFase.Criar("RESULTADO_HOMOLOGACAO", PapelProdutoFase.Definitivo)], false, null, tipos, regra),
        []).Value!;

    [Fact(DisplayName = "A fase guarda a regra declarada nas publicações e, sem declarar, fica sem regra")]
    public void Criar_GuardaARegraDeclarada()
    {
        FaseCom(Regra, Homologacao).RegraDeHomologacao.Should().BeSameAs(Regra);
        FaseCom(regra: null, Homologacao).RegraDeHomologacao.Should().BeNull();
    }

    [Fact(DisplayName = "Atualizar a fase repõe a regra da fase de origem, inclusive a ausência dela")]
    public void AtualizarDe_ReporARegraDaOrigem()
    {
        FaseCronograma viva = FaseCom(regra: null);

        viva.AtualizarDe(FaseCom(Regra, Homologacao), []);
        viva.RegraDeHomologacao.Should().BeSameAs(Regra);

        viva.AtualizarDe(FaseCom(regra: null), []);
        viva.RegraDeHomologacao.Should().BeNull();
    }
}
