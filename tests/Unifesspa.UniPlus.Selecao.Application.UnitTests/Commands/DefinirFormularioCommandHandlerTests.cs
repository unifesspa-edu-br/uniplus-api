namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

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
            repository, Substitute.For<IFatoCandidatoReader>(), Substitute.For<ISelecaoUnitOfWork>(), CancellationToken.None);

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
            repository, Substitute.For<IFatoCandidatoReader>(), Substitute.For<ISelecaoUnitOfWork>(), CancellationToken.None);

        result.Errors.Select(static e => e.Field).Should().BeEquivalentTo(["etapas[0].titulo", "titulo", "faseId"]);
    }

    [Fact(DisplayName = "Exibição de seção que cita fato fora do catálogo é recusada na etapa, junto das recusas de estrutura e de título")]
    public async Task Handle_ExibicaoComFatoDesconhecido_RecusaNaEtapa()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Formulário", out Guid faseDeInscricao);
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        IFatoCandidatoReader reader = Substitute.For<IFatoCandidatoReader>();
        reader.ListarAsync(Arg.Any<CancellationToken>()).Returns(CatalogoDoConjuntoBasico.Com([]));

        Result<MutacaoAceita> result = await DefinirFormularioCommandHandler.Handle(
            new DefinirFormularioCommand(
                processo.Id, FinalidadeFormulario.Inscricao, faseDeInscricao, new string('a', 301),
                [
                    new EtapaFormularioInput("DADOS", 0, "SECAO", null, "Dados", null, null,
                        [[new CondicaoPrecondicaoInput("FATO_INEXISTENTE", "IGUAL", System.Text.Json.JsonSerializer.SerializeToElement(true))]]),
                ],
                PrecondicaoIfMatch.Ausente),
            repository, reader, Substitute.For<ISelecaoUnitOfWork>(), CancellationToken.None);

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("etapas[0].exibicao", "PredicadoDnf.FatoDesconhecido"),
            ("titulo", EstruturaFormularioErrorCodes.TituloTamanho),
            ("etapas", "EstruturaFormulario.BlocoExigidoAusente"),
        ]);
    }

    [Fact(DisplayName = "Criar o formulário de inscrição põe a seção dos dados básicos em primeiro, com os itens dela")]
    public async Task Handle_CriarInscricao_TrazASecaoDosDadosBasicos()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Formulário", out Guid faseDeInscricao);
        processo.RemoverFormulario(FinalidadeFormulario.Inscricao, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        IFatoCandidatoReader reader = Substitute.For<IFatoCandidatoReader>();
        reader.ListarAsync(Arg.Any<CancellationToken>()).Returns(CatalogoDoConjuntoBasico.Fatos);

        Result<MutacaoAceita> result = await DefinirFormularioCommandHandler.Handle(
            new DefinirFormularioCommand(
                processo.Id, FinalidadeFormulario.Inscricao, faseDeInscricao, null,
                [
                    new EtapaFormularioInput("DADOS", 0, "SECAO", null, "Dados", null, null),
                    new EtapaFormularioInput("REVISAO", 1, "BLOCO", "REVISAO_E_ACEITE", "Revisão e aceite", null, null),
                ],
                PrecondicaoIfMatch.Ausente),
            repository, reader, Substitute.For<ISelecaoUnitOfWork>(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.FormularioDe(FinalidadeFormulario.Inscricao)!.Etapas.OrderBy(static e => e.Ordem).Select(static e => e.Codigo)
            .Should().Equal(ConjuntoBasicoDaInscricao.CodigoDaSecao, "DADOS", "REVISAO");
        processo.FatosColetados.Select(static f => f.FatoCodigo).Should().BeEquivalentTo(ConjuntoBasicoDaInscricao.Fatos);
    }
}
