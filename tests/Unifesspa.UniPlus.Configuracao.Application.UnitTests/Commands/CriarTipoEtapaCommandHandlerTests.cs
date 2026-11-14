namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposEtapa;
using Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CriarTipoEtapaCommandHandlerTests
{
    private readonly ITipoEtapaRepository _repository = Substitute.For<ITipoEtapaRepository>();
    private readonly ITipoConceitoRepository _conceitos = Substitute.For<ITipoConceitoRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static CriarTipoEtapaCommand ComandoValido() =>
        new("TIPO_ETAPA_TESTE", "Tipo de etapa de teste", AdmitePontuacao: true, AdmiteEliminacao: true, ConceitosAdmitidos: []);

    private Task<Result<Guid>> Criar(CriarTipoEtapaCommand comando) =>
        CriarTipoEtapaCommandHandler.Handle(comando, _repository, _conceitos, _unitOfWork, CancellationToken.None);

    private void CatalogoTem(params TipoConceito[] tipos) =>
        _conceitos.ListarPorCodigosAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(tipos);

    [Fact(DisplayName = "Código livre cria o tipo, persiste e retorna o Id")]
    public async Task Handle_CodigoLivre_CriaEPersiste()
    {
        _repository.CodigoExisteAsync("TIPO_ETAPA_TESTE", Arg.Any<CancellationToken>()).Returns(false);

        Result<Guid> resultado = await Criar(ComandoValido());

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().NotBe(Guid.Empty);
        await _repository.Received(1).AdicionarAsync(Arg.Any<TipoEtapa>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código já existente retorna conflito (CodigoJaExiste) sem persistir")]
    public async Task Handle_CodigoDuplicado_RetornaConflito()
    {
        _repository.CodigoExisteAsync("TIPO_ETAPA_TESTE", Arg.Any<CancellationToken>()).Returns(true);

        Result<Guid> resultado = await Criar(ComandoValido());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoEtapaErrorCodes.CodigoJaExiste);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<TipoEtapa>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Nome ausente propaga o erro de domínio sem consultar unicidade nem persistir")]
    public async Task Handle_NomeAusente_RetornaErroSemConsultarUnicidadeNemPersistir()
    {
        CriarTipoEtapaCommand comando = ComandoValido() with { Nome = "" };

        Result<Guid> resultado = await Criar(comando);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoEtapaErrorCodes.NomeObrigatorio);
        await _repository.DidNotReceive().CodigoExisteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código ausente e nome ausente no mesmo payload acumulam as duas violações")]
    public async Task Handle_CodigoENomeAusentes_AcumulaAsDuasViolacoes()
    {
        CriarTipoEtapaCommand comando = ComandoValido() with { Codigo = "", Nome = "" };

        Result<Guid> resultado = await Criar(comando);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors[0].Field.Should().Be("codigo");
        resultado.Errors[1].Field.Should().Be("nome");
    }

    [Fact(DisplayName = "Conceitos ativos do catálogo ficam admitidos no tipo criado")]
    public async Task Handle_ConceitosAtivos_AdmiteNoTipoCriado()
    {
        TipoConceito nota = TiposConceitoDeTeste.Numerico();
        CatalogoTem(nota);
        TipoEtapa? gravado = null;
        await _repository.AdicionarAsync(Arg.Do<TipoEtapa>(tipo => gravado = tipo), Arg.Any<CancellationToken>());

        Result<Guid> resultado = await Criar(ComandoValido() with { ConceitosAdmitidos = [nota.Codigo] });

        resultado.IsSuccess.Should().BeTrue();
        gravado!.ConceitosAdmitidos.Should().ContainSingle().Which.Should().BeSameAs(nota);
    }

    [Fact(DisplayName = "Lista vazia cria o tipo sem consultar o catálogo de conceitos")]
    public async Task Handle_ListaVazia_CriaSemConsultarOCatalogo()
    {
        Result<Guid> resultado = await Criar(ComandoValido());

        resultado.IsSuccess.Should().BeTrue();
        await _conceitos.DidNotReceive().ListarPorCodigosAsync(
            Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Lista ausente é recusada como campo obrigatório, sem consultar o catálogo nem persistir")]
    public async Task Handle_ListaAusente_RecusaSemConsultarNemPersistir()
    {
        Result<Guid> resultado = await Criar(ComandoValido() with { ConceitosAdmitidos = null });

        resultado.Errors.Select(erro => (erro.Field, erro.Error.Code)).Should().Equal(
            ("conceitosAdmitidos", TipoEtapaErrorCodes.ConceitosAdmitidosObrigatorio));
        await _conceitos.DidNotReceive().ListarPorCodigosAsync(
            Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Conceito inexistente acumula com a violação de campo no mesmo lote e não persiste")]
    public async Task Handle_ConceitoInexistenteENomeAusente_AcumulaEmOrdemDeCampo()
    {
        CatalogoTem();

        Result<Guid> resultado = await Criar(ComandoValido() with { Nome = "", ConceitosAdmitidos = ["FANTASMA"] });

        resultado.Errors.Select(erro => (erro.Field, erro.Error.Code)).Should().Equal(
            ("nome", TipoEtapaErrorCodes.NomeObrigatorio),
            ("conceitosAdmitidos", TipoEtapaErrorCodes.ConceitoInexistente));
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<TipoEtapa>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Conceito desativado e conceito repetido são recusados no mesmo lote")]
    public async Task Handle_ConceitoInativoEDuplicado_AcumulaAsDuasRecusas()
    {
        TipoConceito antigo = TiposConceitoDeTeste.Categorico();
        antigo.Desativar();
        TipoConceito nota = TiposConceitoDeTeste.Numerico();
        CatalogoTem(antigo, nota);

        Result<Guid> resultado = await Criar(
            ComandoValido() with { ConceitosAdmitidos = [antigo.Codigo, nota.Codigo, nota.Codigo] });

        resultado.Errors.Select(erro => erro.Error.Code).Should().Equal(
            TipoEtapaErrorCodes.ConceitoInativo, TipoEtapaErrorCodes.ConceitoDuplicado);
    }
}
