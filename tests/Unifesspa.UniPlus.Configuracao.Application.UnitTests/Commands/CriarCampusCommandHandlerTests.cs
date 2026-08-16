namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Campi;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Enderecos;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CriarCampusCommandHandlerTests
{
    private readonly ICampusRepository _repository = Substitute.For<ICampusRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static CriarCampusCommand ComandoValido() =>
        new("CAMar", "Campus Marabá", "1504208", "Marabá", "PA", null, null);

    private static EnderecoGeoInput EnderecoValido(string cidadeCodigoIbge = "1504208", string cidadeUf = "PA") =>
        new("68507590", "Folha 31", "s/n", null, "Nova Marabá", null,
            new CidadeReferenciaInput(cidadeCodigoIbge, "Marabá", cidadeUf),
            -5.3m, -49.1m, NivelResolucaoEndereco.Logradouro, "logradouro");

    [Fact(DisplayName = "Cria o campus, persiste e retorna o Id")]
    public async Task Handle_SiglaLivre_CriaEPersiste()
    {
        _repository.SiglaExisteEntreLivosAsync("CAMAR", null, Arg.Any<CancellationToken>())
            .Returns(false);

        Result<Guid> resultado = await CriarCampusCommandHandler.Handle(
            ComandoValido(), _repository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().NotBe(Guid.Empty);
        await _repository.Received(1).AdicionarAsync(Arg.Any<Campus>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sigla já existente entre vivos retorna conflito (SiglaJaExiste)")]
    public async Task Handle_SiglaDuplicada_RetornaConflito()
    {
        _repository.SiglaExisteEntreLivosAsync("CAMAR", null, Arg.Any<CancellationToken>())
            .Returns(true);

        Result<Guid> resultado = await CriarCampusCommandHandler.Handle(
            ComandoValido(), _repository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CampusErrorCodes.SiglaJaExiste);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<Campus>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Cidade malformada propaga o erro de domínio sem persistir")]
    public async Task Handle_CidadeMalformada_RetornaErroDeFormato()
    {
        CriarCampusCommand comando = ComandoValido() with { CidadeCodigoIbge = "150420" };

        Result<Guid> resultado = await CriarCampusCommandHandler.Handle(
            comando, _repository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());

        // Prova direta de que a unicidade só é consultada depois da validação de
        // campo: cidade malformada falha em Campus.Criar antes do handler chegar
        // a perguntar ao repositório se a sigla já existe.
        await _repository.DidNotReceive()
            .SiglaExisteEntreLivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Endereço estruturado é construído e persistido com o campus")]
    public async Task Handle_ComEndereco_PersisteEnderecoEstruturado()
    {
        _repository.SiglaExisteEntreLivosAsync("CAMAR", null, Arg.Any<CancellationToken>())
            .Returns(false);

        Campus? capturado = null;
        await _repository.AdicionarAsync(Arg.Do<Campus>(c => capturado = c), Arg.Any<CancellationToken>());

        CriarCampusCommand comando = ComandoValido() with { Endereco = EnderecoValido() };

        Result<Guid> resultado = await CriarCampusCommandHandler.Handle(
            comando, _repository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        capturado!.Endereco.Should().NotBeNull();
        capturado.Endereco!.Cep.Should().Be("68507590");
        capturado.Endereco.CidadeCodigoIbge.Should().Be("1504208");
    }

    [Fact(DisplayName = "Sigla vazia e CEP em formato inválido no mesmo payload acumulam as duas violações")]
    public async Task Handle_SiglaVaziaEEnderecoInvalido_AcumulaAsDuasViolacoes()
    {
        // Antes: o handler retornava cedo no erro de endereço, sem nunca chamar
        // Campus.Criar — a violação de Sigla nunca aparecia junto.
        CriarCampusCommand comando = ComandoValido() with
        {
            Sigla = "",
            Endereco = EnderecoValido() with { Cep = "123" },
        };

        Result<Guid> resultado = await CriarCampusCommandHandler.Handle(
            comando, _repository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors[0].Error.Code.Should().Be(CampusErrorCodes.SiglaObrigatoria);
        resultado.Errors[1].Field.Should().Be("endereco");
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Endereço com cidade incoerente com a cidade do campus propaga erro sem persistir")]
    public async Task Handle_EnderecoCidadeIncoerente_RetornaErro()
    {
        _repository.SiglaExisteEntreLivosAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>())
            .Returns(false);

        // Cidade do endereço (Belém/1501402) diverge da cidade do campus (Marabá/1504208).
        CriarCampusCommand comando = ComandoValido() with
        {
            Endereco = EnderecoValido(cidadeCodigoIbge: "1501402"),
        };

        Result<Guid> resultado = await CriarCampusCommandHandler.Handle(
            comando, _repository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(EnderecoReferenciaErrorCodes.CidadeIncoerente);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
