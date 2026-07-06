namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

public sealed class ObterConformidadeProcessoSeletivoQueryHandlerTests
{
    [Fact(DisplayName = "Handle com processo inexistente retorna null (mapeado a 404 pelo controller)")]
    public async Task Handle_ProcessoInexistente_RetornaNull()
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ProcessoSeletivo?)null);

        ConformidadeProcessoSeletivoDto? result = await ObterConformidadeProcessoSeletivoQueryHandler.Handle(
            new ObterConformidadeProcessoSeletivoQuery(Guid.CreateVersion7()), repository, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact(DisplayName = "Handle com etapas mas sem atendimento devolve Etapas ok e Atendimento pendente")]
    public async Task Handle_EtapasSemAtendimento_ChecklistParcial()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU);
        processo.DefinirEtapas([EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 3m, ordem: 1)]);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        ConformidadeProcessoSeletivoDto? result = await ObterConformidadeProcessoSeletivoQueryHandler.Handle(
            new ObterConformidadeProcessoSeletivoQuery(processo.Id), repository, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Itens.Should().Contain(i => i.Item == "Etapas" && i.Ok);
        result.Itens.Should().Contain(i => i.Item == "Atendimento especializado" && !i.Ok);
    }

    [Fact(DisplayName = "Handle com todos os itens obrigatórios configurados devolve checklist sem pendências")]
    public async Task Handle_TodosOsItens_SemPendencia()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU);
        processo.DefinirEtapas([EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 3m, ordem: 1)]);
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        ConformidadeProcessoSeletivoDto? result = await ObterConformidadeProcessoSeletivoQueryHandler.Handle(
            new ObterConformidadeProcessoSeletivoQuery(processo.Id), repository, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Itens.Should().OnlyContain(i => i.Ok);
    }
}
