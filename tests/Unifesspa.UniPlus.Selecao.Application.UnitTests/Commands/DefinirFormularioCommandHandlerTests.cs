namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DefinirFormularioCommandHandlerTests
{
    [Fact(DisplayName = "Processo publicado sem sessão recusa pela precondição antes de conferir as etapas")]
    public async Task Handle_PublicadoSemSessao_EtapaInvalida_RecusaPelaPrecondicao()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS 2026", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        typeof(ProcessoSeletivo).GetProperty(nameof(ProcessoSeletivo.Status))!.SetValue(processo, StatusProcesso.Publicado);
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        Result<MutacaoAceita> result = await DefinirFormularioCommandHandler.Handle(
            new DefinirFormularioCommand(
                processo.Id, FinalidadeFormulario.Inscricao, null, null,
                [new EtapaFormularioInput("DADOS", 0, "SECAO", null, "", null, null)],
                PrecondicaoIfMatch.Ausente),
            repository, Substitute.For<ISelecaoUnitOfWork>(), CancellationToken.None);

        result.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada",
            "o gate de estado e concorrência sai antes da validação das etapas");
    }

    [Fact(DisplayName = "Título, fase e etapa inválidos saem juntos na mesma recusa")]
    public async Task Handle_TituloFaseEEtapaInvalidos_AcumulaAsRecusas()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Formulário", out Guid faseDeInscricao);
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        Result<MutacaoAceita> result = await DefinirFormularioCommandHandler.Handle(
            new DefinirFormularioCommand(
                processo.Id, FinalidadeFormulario.Habilitacao, faseDeInscricao, new string('a', 301),
                [new EtapaFormularioInput("DADOS", 0, "SECAO", null, "", null, null)],
                PrecondicaoIfMatch.Ausente),
            repository, Substitute.For<ISelecaoUnitOfWork>(), CancellationToken.None);

        result.Errors.Select(static e => e.Field).Should().BeEquivalentTo(["etapas[0].titulo", "titulo", "faseId"]);
    }
}
