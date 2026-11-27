namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries.ProcessosDoPainelDeConformidade;

/// <summary>
/// O painel de conformidade julga a precedência do cadastro como a publicação: lida na primeira publicação e quando o cronograma
/// mudou desde a versão publicada, e sem leitura quando o cronograma é o publicado.
/// </summary>
public sealed class ConformidadeDaPrecedenciaNoPainelTests
{
    private const string ItemDaPrecedencia = "cronograma_precedencia_entre_fases";

    private static ProcessoSeletivo ProcessoComDuasFasesNoMesmoMomento()
    {
        ProcessoSeletivo processo = ComTodosOsItensObrigatorios();
        FaseCronograma habilitacao = FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            OrigensDoTipoDeFaseDeTeste.Da(Guid.CreateVersion7(), "HABILITACAO", OrigemDataFase.NaExecucao, agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(2, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps) { NoMesmoMomentoDaAnterior = true },
            new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
            []).Value!;
        processo.DefinirCronogramaFases([.. processo.CronogramaFases, habilitacao], []).IsSuccess.Should().BeTrue();

        return processo;
    }

    private static async Task<bool> ItemOkAsync(ServicosDoCongelamentoDeTeste servicos, ProcessoSeletivo processo)
    {
        servicos.Repositorio.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        ConformidadeProcessoSeletivoDto? painel = (await servicos.ConformidadeAsync(new ObterConformidadeProcessoSeletivoQuery(processo.Id))).Value;

        return painel!.Itens.Single(static item => item.Codigo == ItemDaPrecedencia).Ok;
    }

    [Fact(DisplayName = "Sem versão publicada, o painel lê a precedência e mostra pendentes as fases ligadas no mesmo momento")]
    public async Task SemVersaoPublicada_PainelLeAPrecedencia()
    {
        ProcessoSeletivo processo = ProcessoComDuasFasesNoMesmoMomento();
        IPrecedenciaFaseReader cadastro = PrecedenciasDeTeste.Com((processo.CronogramaFases.Single(static fase => fase.Ordem == 1).Codigo, "HABILITACAO", true));

        bool ok = await ItemOkAsync(ServicosDoCongelamentoDeTeste.Para(processo).ComPrecedencias(cadastro), processo);

        ok.Should().BeFalse();
        await cadastro.Received(1).ListarVivasAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sem relação no cadastro, o item da precedência fica conforme")]
    public async Task SemRelacaoNoCadastro_ItemConforme()
    {
        ProcessoSeletivo processo = ProcessoComDuasFasesNoMesmoMomento();

        bool ok = await ItemOkAsync(ServicosDoCongelamentoDeTeste.Para(processo), processo);

        ok.Should().BeTrue();
    }

    [Fact(DisplayName = "Com o cronograma igual ao da versão publicada, o painel não lê a precedência e o item fica conforme")]
    public async Task CronogramaIgualAoPublicado_PainelNaoLeAPrecedencia()
    {
        ProcessoSeletivo processo = ProcessoComDuasFasesNoMesmoMomento();
        IPrecedenciaFaseReader cadastro = PrecedenciasDeTeste.Com((processo.CronogramaFases.Single(static fase => fase.Ordem == 1).Codigo, "HABILITACAO", true));
        ServicosDoCongelamentoDeTeste servicos = ServicosDoCongelamentoDeTeste.Para(processo).ComPrecedencias(cadastro);
        VersaoPublicadaDeTeste.Publicar(processo, servicos.Repositorio, servicos.Codecs);

        bool ok = await ItemOkAsync(servicos, processo);

        ok.Should().BeTrue();
        await cadastro.DidNotReceive().ListarVivasAsync(Arg.Any<CancellationToken>());
    }
}
