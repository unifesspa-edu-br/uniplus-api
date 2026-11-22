namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Enderecos;
using Unifesspa.UniPlus.Configuracao.Application.Commands.LocaisOferta;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class LocalOfertaEnderecoInvalidoHandlerTests
{
    private static readonly EnderecoGeoInput EnderecoMalformado =
        new("123", null, null, null, null, null, null, null, null, null, null);

    private readonly ILocalOfertaRepository _repository = Substitute.For<ILocalOfertaRepository>();
    private readonly ICampusRepository _campusRepository = Substitute.For<ICampusRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact(DisplayName = "Criar com endereço malformado e tipo inválido acumula o tipo e depois o endereço")]
    public async Task Criar_EnderecoMalformadoETipoInvalido_AcumulaTipoDepoisEndereco()
    {
        var comando = new CriarLocalOfertaCommand(
            "INEXISTENTE", null, "1504208", "Marabá", "PA", EnderecoMalformado, null);

        Result<Guid> resultado = await CriarLocalOfertaCommandHandler.Handle(
            comando, _repository, _campusRepository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.Errors.Select(erro => erro.Field).Should().Equal("tipo", "endereco");
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Criar com endereço malformado e demais campos válidos recusa só o endereço")]
    public async Task Criar_SoEnderecoMalformado_RecusaSoOEndereco()
    {
        var comando = new CriarLocalOfertaCommand(
            "POLO_DE_EDUCACAO_A_DISTANCIA", null, "1504208", "Marabá", "PA", EnderecoMalformado, null);

        Result<Guid> resultado = await CriarLocalOfertaCommandHandler.Handle(
            comando, _repository, _campusRepository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.Errors.Select(erro => erro.Field).Should().Equal("endereco");
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<LocalOferta>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Atualizar com endereço malformado e tipo inválido acumula o tipo e depois o endereço sem buscar o local")]
    public async Task Atualizar_EnderecoMalformadoETipoInvalido_AcumulaSemBuscarOLocal()
    {
        var comando = new AtualizarLocalOfertaCommand(
            Guid.CreateVersion7(), "INEXISTENTE", null, "1504208", "Marabá", "PA", EnderecoMalformado, null);

        Result resultado = await AtualizarLocalOfertaCommandHandler.Handle(
            comando, _repository, _campusRepository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.Errors.Select(erro => erro.Field).Should().Equal("tipo", "endereco");
        await _repository.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
