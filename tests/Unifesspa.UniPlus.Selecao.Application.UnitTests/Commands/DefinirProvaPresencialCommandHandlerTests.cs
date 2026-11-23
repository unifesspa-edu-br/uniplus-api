namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Microsoft.Extensions.Options;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;

public sealed class DefinirProvaPresencialCommandHandlerTests
{
    [Fact]
    public async Task Handle_ComProva_GravaNaOrdemInformada()
    {
        ProcessoSeletivo processo = PublicacaoDeTeste.NovoProcessoConforme();
        Contexto contexto = new(processo);
        Result resultado = await contexto.Definir(true, [new("1508159", "PA"), new("1504208", "PA")]);
        resultado.IsSuccess.Should().BeTrue();
        processo.ProvaPresencial!.Municipios.Select(m => (m.CodigoIbge, m.Posicao))
            .Should().Equal(("1508159", 1), ("1504208", 2));
        await contexto.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SemProva_SubstituiDeclaracao()
    {
        ProcessoSeletivo processo = PublicacaoDeTeste.NovoProcessoConforme();
        processo.DefinirProvaPresencial(true, [new("1504208", "PA")], 100);
        Contexto contexto = new(processo);
        Result resultado = await contexto.Definir(false, []);
        resultado.IsSuccess.Should().BeTrue();
        processo.ProvaPresencial!.TemProvaPresencial.Should().BeFalse();
        processo.ProvaPresencial.Municipios.Should().BeEmpty();
        await contexto.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AcimaDoLimite_RecusaSemSalvar()
    {
        Contexto contexto = new(PublicacaoDeTeste.NovoProcessoConforme(), 1);
        Result resultado = await contexto.Definir(true, [new("1504208", "PA"), new("1508159", "PA")]);
        resultado.Errors.Should().Contain(e => e.Error.Code == ProvaPresencialErrorCodes.MunicipiosAcimaDoLimite);
        await contexto.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("150420", "PA", "CidadeReferencia.CodigoIbgeFormatoInvalido")]
    [InlineData("1504208", "SP", "CidadeReferencia.UfIncoerente")]
    public async Task Handle_MunicipioInvalido_PropagaErroDoDominio(string codigo, string uf, string erro)
    {
        Contexto contexto = new(PublicacaoDeTeste.NovoProcessoConforme());
        Result resultado = await contexto.Definir(true, [new(codigo, uf)]);
        resultado.Errors.Should().Contain(e => e.Error.Code == erro);
        await contexto.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ItemNulo_RecusaPeloDominioSemSalvar()
    {
        Contexto contexto = new(PublicacaoDeTeste.NovoProcessoConforme());
        Result resultado = await contexto.Definir(true, [null!]);
        resultado.Errors.Should().Contain(e => e.Field == "provaPresencial.municipios[0].codigoIbge" && e.Error.Code == "CidadeReferencia.CodigoIbgeObrigatorio");
        resultado.Errors.Should().Contain(e => e.Field == "provaPresencial.municipios[0].uf" && e.Error.Code == "CidadeReferencia.UfObrigatoria");
        await contexto.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        Contexto contexto = new(null);
        Result resultado = await contexto.Definir(false, []);
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        await contexto.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ProcessoPublicado_RecusaSemSalvar()
    {
        Contexto contexto = new(ApoioDaCascataDeRemanejamento.NovoProcessoPublicado());
        Result resultado = await contexto.Definir(false, []);
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
        await contexto.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    private sealed class Contexto
    {
        private readonly Guid _id;
        private readonly IProcessoSeletivoRepository _repositorio = Substitute.For<IProcessoSeletivoRepository>();
        private readonly IOptions<ProvaPresencialOptions> _options;
        public Contexto(ProcessoSeletivo? processo, int limite = 100)
        {
            _id = processo?.Id ?? Guid.NewGuid();
            _repositorio.ObterParaMutacaoAsync(_id, Arg.Any<CancellationToken>()).Returns(processo);
            _options = Options.Create(new ProvaPresencialOptions { LimiteDeMunicipios = limite });
        }

        public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();
        public Task<Result> Definir(bool tem, IReadOnlyList<MunicipioDeProvaInformado> municipios) =>
            DefinirProvaPresencialCommandHandler.Handle(new(_id, tem, municipios), _repositorio, UnitOfWork, _options, CancellationToken.None);
    }
}
