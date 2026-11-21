namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A precedência entre fases lida do cadastro como a conferência do cronograma a recebe: lida na primeira publicação e quando o
/// cronograma mudou desde a versão publicada, e não lida quando não mudou.
/// </summary>
public sealed class LeituraDasPrecedenciasDosCadastrosTests
{
    private static FaseCronograma Fase(Guid origem, string codigo, int ordem) =>
        FaseCronograma.Criar(
            new OrigemCanonicaDaFase(origem, codigo, OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(ordem, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;

    [Fact(DisplayName = "As relações vivas do cadastro viram arestas com antecessora, sucessora e permissão de sobreposição")]
    public async Task ListarVivasAsync_TraduzAsRelacoes()
    {
        IPrecedenciaFaseReader leitor = PrecedenciasDeTeste.Com(("A", "B", true), ("B", "C", false));

        IReadOnlyList<ArestaPrecedencia> arestas = await new LeituraDasPrecedenciasDosCadastros(leitor).ListarVivasAsync(CancellationToken.None);

        arestas.Should().Equal(new ArestaPrecedencia("A", "B", true), new ArestaPrecedencia("B", "C", false));
    }

    [Fact(DisplayName = "Na primeira publicação, sem versão publicada, a precedência é lida")]
    public async Task LerAsync_SemVersaoPublicada_Le()
    {
        IPrecedenciaFaseReader leitor = PrecedenciasDeTeste.Com(("A", "B", true));
        FaseCronograma[] atuais = [Fase(Guid.CreateVersion7(), "A", 1), Fase(Guid.CreateVersion7(), "B", 2)];

        PrecedenciasDosCadastros precedencias = await new LeituraDasPrecedenciasDosCadastros(leitor).LerAsync(null, atuais, CancellationToken.None);

        precedencias.FoiLida.Should().BeTrue();
        precedencias.Arestas.Should().ContainSingle();
    }

    [Fact(DisplayName = "O cronograma que mudou desde a versão publicada tem a precedência lida")]
    public async Task LerAsync_CronogramaMudou_Le()
    {
        Guid origemA = Guid.CreateVersion7();
        Guid origemB = Guid.CreateVersion7();
        IPrecedenciaFaseReader leitor = PrecedenciasDeTeste.SemArestas();

        PrecedenciasDosCadastros precedencias = await new LeituraDasPrecedenciasDosCadastros(leitor).LerAsync(
            [Fase(origemA, "A", 1), Fase(origemB, "B", 2)], [Fase(origemA, "A", 1), Fase(origemB, "B", 3)], CancellationToken.None);

        precedencias.FoiLida.Should().BeTrue();
        precedencias.Arestas.Should().BeEmpty();
    }

    [Fact(DisplayName = "O cronograma igual ao publicado não lê o cadastro e devolve a precedência não lida")]
    public async Task LerAsync_CronogramaIgual_NaoLeOCadastro()
    {
        Guid origemA = Guid.CreateVersion7();
        IPrecedenciaFaseReader leitor = PrecedenciasDeTeste.Com(("A", "B", true));

        PrecedenciasDosCadastros precedencias = await new LeituraDasPrecedenciasDosCadastros(leitor).LerAsync(
            [Fase(origemA, "A", 1)], [Fase(origemA, "A", 1)], CancellationToken.None);

        precedencias.FoiLida.Should().BeFalse();
        await leitor.DidNotReceive().ListarVivasAsync(Arg.Any<CancellationToken>());
    }
}
