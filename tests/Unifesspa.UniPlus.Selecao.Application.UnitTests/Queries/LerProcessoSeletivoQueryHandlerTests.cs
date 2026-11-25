namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Reflection;
using System.Runtime.CompilerServices;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class LerProcessoSeletivoQueryHandlerTests
{
    private const string Localidade = """{"localidade":{"codigoIbge":"1504208","nome":"Marabá","uf":"PA","fusoHorario":"America/Belem"}}""";

    [Theory(DisplayName = "Sem a localidade publicada legível, quem não configura recebe a identificação com a localidade vazia, nunca erro nem 404")]
    [InlineData("""{"tipoProcesso":{}}""")]
    [InlineData("""{"localidade":{"codigoIbge":1,"nome":"x","uf":"PA"}}""")]
    [InlineData("""{"localidade":"Marabá"}""")]
    [InlineData("{ não é json")]
    [InlineData("[]")]
    public async Task LocalidadePublicadaIlegivel_DevolveIdentificacaoSemLocalidade(string envelope)
    {
        (ProcessoSeletivoDto? dto, _) = await LerSemConfigurarAsync(envelope);

        dto.Should().NotBeNull();
        dto!.Localidade.Should().Be(new LocalidadeRegenteDto(string.Empty, string.Empty, string.Empty));
    }

    [Fact(DisplayName = "Quem não configura recebe a localidade da versão publicada, e não a da configuração viva")]
    public async Task LocalidadePublicada_VemDoSnapshot()
    {
        (ProcessoSeletivoDto? dto, ProcessoSeletivo processo) = await LerSemConfigurarAsync(Localidade.Replace("1504208", "1500107", StringComparison.Ordinal));

        dto!.Localidade.CodigoIbge.Should().Be("1500107").And.NotBe(processo.Localidade.CodigoIbge);
        processo.ProvaPresencial.Should().NotBeNull();
        dto.ProvaPresencial.Should().BeNull();
    }

    private static async Task<(ProcessoSeletivoDto? Dto, ProcessoSeletivo Processo)> LerSemConfigurarAsync(string envelope)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Leitura", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.CreateVersion7(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        processo.DefinirProvaPresencial(true, [new("1504208", "PA")], 100).IsSuccess.Should().BeTrue();
        (IProcessoSeletivoRepository repositorio, ICertameDivulgadoRepository divulgacoes) = Repositorios(processo, envelope);
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();
        verificador.PodeAsync(default!, default, default).ReturnsForAnyArgs(ResultadoDoAcesso.IdentidadeIncompleta);

        ProcessoSeletivoDto? dto = await LerProcessoSeletivoQueryHandler.Handle(
            new LerProcessoSeletivoQuery(processo.Id), repositorio, divulgacoes, verificador, CancellationToken.None);
        return (dto, processo);
    }

    private static (IProcessoSeletivoRepository, ICertameDivulgadoRepository) Repositorios(ProcessoSeletivo processo, string envelope)
    {
        IProcessoSeletivoRepository repositorio = Substitute.For<IProcessoSeletivoRepository>();
        repositorio.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        repositorio.ObterEstadoAsync(processo.Id, Arg.Any<CancellationToken>())
            .Returns(new EstadoDoProcesso(processo.UnidadeAdministradoraOrigemId, SituacaoDoProcessoSeletivo.Publicado, JaPublicado: true));
        ICertameDivulgadoRepository divulgacoes = Divulgacao(repositorio, processo.Id, envelope);
        return (repositorio, divulgacoes);
    }

    private static ICertameDivulgadoRepository Divulgacao(IProcessoSeletivoRepository repositorio, Guid processoId, string envelope)
    {
        VersaoConfiguracao versao = (VersaoConfiguracao)RuntimeHelpers.GetUninitializedObject(typeof(VersaoConfiguracao));
        typeof(VersaoConfiguracao).GetProperty(nameof(VersaoConfiguracao.ConfiguracaoCongelada), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(versao, envelope);
        Guid ato = Guid.CreateVersion7();
        CertameDivulgado divulgado = (CertameDivulgado)RuntimeHelpers.GetUninitializedObject(typeof(CertameDivulgado));
        typeof(CertameDivulgado).GetProperty(nameof(CertameDivulgado.AtoCriadorId), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(divulgado, ato);
        ICertameDivulgadoRepository divulgacoes = Substitute.For<ICertameDivulgadoRepository>();
        divulgacoes.ObterParaLeituraAsync(processoId, Arg.Any<CancellationToken>()).Returns(divulgado);
        repositorio.ObterVersoesPorAtoCriadorAsync(Arg.Is<IReadOnlyCollection<Guid>>(a => a.Contains(ato)), Arg.Any<CancellationToken>())
            .Returns([versao]);
        return divulgacoes;
    }
}
