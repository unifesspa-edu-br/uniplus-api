namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DefinirOpcoesDeclaradasCommandHandlerTests
{
    [Theory(DisplayName = "Declarar opções para fato cuja fonte não é o processo, ou fora do catálogo, é recusado")]
    [InlineData("GLOBAL")]
    [InlineData(null)]
    public async Task Handle_FonteQueNaoEhDoProcesso_Recusa(string? fonte)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Opções", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        IProcessoSeletivoRepository repositorio = Substitute.For<IProcessoSeletivoRepository>();
        repositorio.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        IFatoCandidatoReader reader = Substitute.For<IFatoCandidatoReader>();
        reader.ObterPorCodigoAsync("COR_RACA", Arg.Any<CancellationToken>()).Returns(fonte is null
            ? null
            : new FatoCandidatoView(Guid.CreateVersion7(), "COR_RACA", "Cor ou raça", null, "CATEGORICO", "DECLARADO",
                "ESCALAR", ["PRETA"], "INSCRICAO", "CAMPO_INSCRICAO:COR_RACA", null, fonte, Ativo: true));

        Result<MutacaoAceita> resultado = await DefinirOpcoesDeclaradasCommandHandler.Handle(
            new DefinirOpcoesDeclaradasCommand(processo.Id, "COR_RACA", [new OpcaoDeclaradaInput("PRETA", "Preta")], PrecondicaoIfMatch.Ausente),
            repositorio, reader, Substitute.For<ISelecaoUnitOfWork>(), CancellationToken.None);

        resultado.Error!.Code.Should().Be("OpcaoDeclaradaFato.FonteNaoEhDoProcesso");
        processo.OpcoesDeclaradas.Should().BeEmpty();
    }
}
