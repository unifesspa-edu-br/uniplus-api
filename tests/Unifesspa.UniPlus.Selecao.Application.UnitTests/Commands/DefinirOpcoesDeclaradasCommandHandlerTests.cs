namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class DefinirOpcoesDeclaradasCommandHandlerTests
{
    [Theory(DisplayName = "Declarar opções para fato cuja fonte não é o processo, fora do catálogo ou agregado de grupo é recusado")]
    [InlineData("GLOBAL", "CAMPO_FORMULARIO:COR_RACA")]
    [InlineData(null, "CAMPO_FORMULARIO:COR_RACA")]
    [InlineData("PROCESSO", "AGREGACAO_GRUPO:COR_RACA_MEMBRO")]
    public async Task Handle_FonteQueNaoEhDoProcesso_Recusa(string? fonte, string binding)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Opções", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        IProcessoSeletivoRepository repositorio = Substitute.For<IProcessoSeletivoRepository>();
        repositorio.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        IFatoCandidatoReader reader = Substitute.For<IFatoCandidatoReader>();
        reader.ObterPorCodigoAsync("COR_RACA", Arg.Any<CancellationToken>()).Returns(fonte is null
            ? null
            : new FatoCandidatoView(Guid.CreateVersion7(), "COR_RACA", "Cor ou raça", null, "CATEGORICO", "DECLARADO",
                "ESCALAR", ["PRETA"], "INSCRICAO", binding, null, fonte, Ativo: true));

        Result<MutacaoAceita> resultado = await DefinirOpcoesDeclaradasCommandHandler.Handle(
            new DefinirOpcoesDeclaradasCommand(processo.Id, "COR_RACA", [new OpcaoDeclaradaInput("PRETA", "Preta")], PrecondicaoIfMatch.Ausente),
            repositorio, reader, Substitute.For<ISelecaoUnitOfWork>(), CancellationToken.None);

        resultado.Error!.Code.Should().Be("OpcaoDeclaradaFato.FonteNaoEhDoProcesso");
        processo.OpcoesDeclaradas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Retirar do fato de membro uma opção que a condição de um agregado cita é recusado")]
    public async Task Handle_OpcaoCitadaPeloAgregado_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Opções", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        processo.DefinirOpcoesDeclaradas(
            "CATEGORIA_RENDA", [OpcaoDeclaradaFato.Criar("CATEGORIA_RENDA", "RURAL", "Rural", 0).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirRegrasDerivacao(
        [
            ConfiguracaoDerivacaoFato.Criar("RENDA_RURAL",
            [
                RegraDerivacaoConfigurada.Criar(0, "SIM",
                    [CondicaoRegraDerivacao.Criar(0, "CATEGORIAS_RENDA_FAMILIA", Operador.Em, JsonSerializer.SerializeToElement(new[] { "RURAL" })).Value!]).Value!,
            ]).Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        IProcessoSeletivoRepository repositorio = Substitute.For<IProcessoSeletivoRepository>();
        repositorio.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        FatoCandidatoView membro = new(Guid.CreateVersion7(), "CATEGORIA_RENDA", "Categoria de renda", null, "CATEGORICO", "DECLARADO",
            "ESCALAR", null, "HABILITACAO", "CAMPO_FORMULARIO:CATEGORIA_RENDA", null, "PROCESSO", Ativo: true, Escopo: "MEMBRO_GRUPO");
        IFatoCandidatoReader reader = Substitute.For<IFatoCandidatoReader>();
        reader.ObterPorCodigoAsync("CATEGORIA_RENDA", Arg.Any<CancellationToken>()).Returns(membro);
        reader.ListarAsync(Arg.Any<CancellationToken>()).Returns(CatalogoDoConjuntoBasico.Com(
        [
            membro,
            new FatoCandidatoView(Guid.CreateVersion7(), "CATEGORIAS_RENDA_FAMILIA", "Categorias de renda da família", null, "CATEGORICO",
                "DERIVADO", "MULTIVALORADO", null, "HABILITACAO", "AGREGACAO_GRUPO:CATEGORIA_RENDA", null, "PROCESSO", Ativo: true),
        ]));

        Result<MutacaoAceita> resultado = await DefinirOpcoesDeclaradasCommandHandler.Handle(
            new DefinirOpcoesDeclaradasCommand(processo.Id, "CATEGORIA_RENDA", [new OpcaoDeclaradaInput("URBANA", "Urbana")], PrecondicaoIfMatch.Ausente),
            repositorio, reader, Substitute.For<ISelecaoUnitOfWork>(), CancellationToken.None);

        resultado.Error!.Code.Should().Be(OpcaoDeclaradaFatoErrorCodes.ReferenciadaPorExigenciaViva);
    }
}
