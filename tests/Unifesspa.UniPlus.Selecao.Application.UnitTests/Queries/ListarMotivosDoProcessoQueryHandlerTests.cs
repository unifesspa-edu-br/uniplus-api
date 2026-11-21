namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.MotivosDoProcesso;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.MotivoDeTeste;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.PublicacaoDeTeste;

/// <summary>A leitura dos motivos escolhidos pelo processo: a escolha atual, ou a que valia numa versão publicada do edital.</summary>
public sealed class ListarMotivosDoProcessoQueryHandlerTests
{
    private readonly ProcessoSeletivo _processo = NovoProcessoConforme();
    private readonly IMotivoDoProcessoRepository _escolhidos = Substitute.For<IMotivoDoProcessoRepository>();

    [Fact(DisplayName = "Sem versão, a leitura devolve a escolha atual ordenada por tipo e código, com o lugar e a vigência")]
    public async Task SemVersao_DevolveAEscolhaAtualOrdenada()
    {
        LugarDaDecisao lugar = LugarDaPrimeiraFase(_processo);
        PrepararEscolhaAtualForaDeOrdem(lugar);

        Result<IReadOnlyList<MotivoDoProcessoDto>> lidos = await ListarAsync(numeroDaVersao: null);

        lidos.Value!.Select(m => m.Codigo).Should().Equal("RENDA_A", "RENDA_B", "NOTA_ZERADA");
        lidos.Value!.Select(m => m.Tipo).Should().Equal(
            CodigoComRotulo<TipoMotivoDeDecisao>.De(TipoMotivoDeDecisao.IsencaoDaTaxa),
            CodigoComRotulo<TipoMotivoDeDecisao>.De(TipoMotivoDeDecisao.IsencaoDaTaxa),
            CodigoComRotulo<TipoMotivoDeDecisao>.De(TipoMotivoDeDecisao.Classificacao));
        lidos.Value![0].Lugar.FaseCanonicaOrigemId.Should().Be(lugar.FaseCanonicaOrigemId);
        lidos.Value![0].Vigencia.Should().Be(new VigenciaDoMotivoDto(1, null));
    }

    [Fact(DisplayName = "Com versão publicada, a leitura devolve a escolha que valia nela")]
    public async Task ComVersaoPublicada_DevolveAEscolhaDaVersao()
    {
        MotivoDoProcesso escolhido = Escolhido(_processo, LugarDaPrimeiraFase(_processo), Motivo());
        _escolhidos.ListarDaVersaoPublicadaAsync(_processo.Id, 1, Arg.Any<CancellationToken>()).Returns([escolhido]);

        Result<IReadOnlyList<MotivoDoProcessoDto>> lidos = await ListarAsync(numeroDaVersao: 1);

        lidos.Value.Should().ContainSingle().Which.MotivoDeDecisaoId.Should().Be(escolhido.MotivoDeDecisaoOrigemId);
        await _escolhidos.DidNotReceive().ListarEscolhaAtualAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A versão que o processo não publicou devolve versão não publicada")]
    public async Task VersaoNaoPublicada_Recusa()
    {
        _escolhidos.ListarDaVersaoPublicadaAsync(_processo.Id, 7, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<MotivoDoProcesso>?)null);

        Result<IReadOnlyList<MotivoDoProcessoDto>> lidos = await ListarAsync(numeroDaVersao: 7);

        lidos.Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.VersaoNaoPublicada);
    }

    [Theory(DisplayName = "A versão anterior à primeira nunca existiu e a leitura dispensa o banco")]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task VersaoAnteriorAPrimeira_RecusaSemConsultar(int numeroDaVersao)
    {
        Result<IReadOnlyList<MotivoDoProcessoDto>> lidos = await ListarAsync(numeroDaVersao);

        lidos.Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.VersaoNaoPublicada);
        _escolhidos.ReceivedCalls().Should().BeEmpty();
    }

    private void PrepararEscolhaAtualForaDeOrdem(LugarDaDecisao lugar)
    {
        MotivoDoProcesso classificacao = Escolhido(_processo, lugar, Motivo(TipoMotivoDeDecisao.Classificacao, "NOTA_ZERADA", null));
        MotivoDoProcesso isencaoB = Escolhido(_processo, lugar, Motivo(codigo: "RENDA_B"));
        MotivoDoProcesso isencaoA = Escolhido(_processo, lugar, Motivo(codigo: "RENDA_A"));
        _escolhidos.ListarEscolhaAtualAsync(_processo.Id, Arg.Any<CancellationToken>()).Returns([classificacao, isencaoB, isencaoA]);
    }

    private Task<Result<IReadOnlyList<MotivoDoProcessoDto>>> ListarAsync(int? numeroDaVersao) =>
        ListarMotivosDoProcessoQueryHandler.Handle(new ListarMotivosDoProcessoQuery(_processo.Id, numeroDaVersao), _escolhidos, CancellationToken.None);
}
