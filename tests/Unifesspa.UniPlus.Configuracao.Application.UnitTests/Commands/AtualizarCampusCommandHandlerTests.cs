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

public sealed class AtualizarCampusCommandHandlerTests
{
    private static readonly DateTimeOffset CidadeCarimbadaEm = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly ICampusRepository _repository = Substitute.For<ICampusRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static Campus CampusExistente(ReferenciaEnderecoGeo? endereco = null) =>
        Campus.Criar(
            "CAMar", "Campus Marabá", "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, CidadeCarimbadaEm, endereco, null).Value!;

    private static EnderecoGeoInput EnderecoInput() =>
        new("68507590", "Folha 31", "s/n", null, "Nova Marabá", null,
            new CidadeReferenciaInput("1504208", "Marabá", "PA"),
            -5.3m, -49.1m, NivelResolucaoEndereco.Logradouro, "logradouro");

    [Fact(DisplayName = "PUT para Id inexistente com campos válidos devolve NaoEncontrado")]
    public async Task Handle_IdInexistente_RetornaNaoEncontrado()
    {
        Guid idInexistente = Guid.NewGuid();
        AtualizarCampusCommand comando = new(
            idInexistente, "CAMar", "Campus Marabá", "1504208", "Marabá", "PA", null, null);

        Result resultado = await AtualizarCampusCommandHandler.Handle(
            comando, _repository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CampusErrorCodes.NaoEncontrado);
    }

    /// <summary>
    /// Antes da ADR-0125, o validator FluentValidation rodava como middleware
    /// antes do handler, então um payload mal formado nunca chegava a
    /// ObterPorIdAsync — validação sempre vencia sobre "não encontrado". Sem o
    /// validator, o handler precisa preservar essa prioridade explicitamente,
    /// validando antes de consultar o repositório.
    /// </summary>
    [Fact(DisplayName = "PUT para Id inexistente com Sigla vazia devolve a violação de campo, não NaoEncontrado")]
    public async Task Handle_IdInexistenteComSiglaVazia_RetornaViolacaoDeCampoSemConsultarRepositorio()
    {
        Guid idInexistente = Guid.NewGuid();
        AtualizarCampusCommand comando = new(
            idInexistente, "", "Campus Marabá", "1504208", "Marabá", "PA", null, null);

        Result resultado = await AtualizarCampusCommandHandler.Handle(
            comando, _repository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CampusErrorCodes.SiglaObrigatoria);
        await _repository.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "S4: PUT sem mudar a cidade preserva cidade_display_atualizado_em")]
    public async Task Handle_CidadeInalterada_PreservaCarimboDeCidade()
    {
        Campus campus = CampusExistente();
        _repository.ObterPorIdAsync(campus.Id, Arg.Any<CancellationToken>()).Returns(campus);

        // Muda só o nome do campus; o trio de cidade permanece igual.
        AtualizarCampusCommand comando = new(
            campus.Id, "CAMar", "Campus Marabá Renomeado", "1504208", "Marabá", "PA",
            null, null);

        Result resultado = await AtualizarCampusCommandHandler.Handle(
            comando, _repository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        campus.Nome.Should().Be("Campus Marabá Renomeado");
        campus.CidadeDisplayAtualizadoEm.Should().Be(CidadeCarimbadaEm,
            "a cidade não mudou, então o carimbo de frescura do display cache é preservado");
    }

    [Fact(DisplayName = "S4: PUT trocando a cidade recarimba cidade_display_atualizado_em")]
    public async Task Handle_CidadeAlterada_RecarimbaCidade()
    {
        Campus campus = CampusExistente();
        _repository.ObterPorIdAsync(campus.Id, Arg.Any<CancellationToken>()).Returns(campus);

        AtualizarCampusCommand comando = new(
            campus.Id, "CAMar", "Campus Marabá", "1501402", "Belém", "PA",
            null, null);

        Result resultado = await AtualizarCampusCommandHandler.Handle(
            comando, _repository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        campus.CidadeNome.Should().Be("Belém");
        campus.CidadeDisplayAtualizadoEm.Should().NotBe(CidadeCarimbadaEm,
            "a cidade mudou, então o carimbo é renovado a partir do TimeProvider");
        campus.CidadeOrigem.Should().Be(ReferenciaCidadeGeo.OrigemGeoApi);
    }

    /// <summary>
    /// Sem validator FluentValidation garantindo não-nulo a montante (ADR-0125,
    /// campos de <see cref="AtualizarCampusCommand"/> são <c>string?</c>), o
    /// handler precisa comparar sigla/cidade com segurança antes de
    /// <see cref="Campus.Atualizar"/> validar — nunca desreferenciar direto.
    /// </summary>
    [Fact(DisplayName = "ADR-0125: PUT com Sigla nula não lança — devolve a violação de domínio")]
    public async Task Handle_SiglaNula_NaoLancaEDevolveViolacaoDeDominio()
    {
        Campus campus = CampusExistente();
        _repository.ObterPorIdAsync(campus.Id, Arg.Any<CancellationToken>()).Returns(campus);

        AtualizarCampusCommand comando = new(
            campus.Id, null, "Campus Marabá", "1504208", "Marabá", "PA",
            null, null);

        Result resultado = await AtualizarCampusCommandHandler.Handle(
            comando, _repository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CampusErrorCodes.SiglaObrigatoria);
    }

    /// <summary>Mesma garantia para o trio de cidade — comparação null-safe antes de validar.</summary>
    [Fact(DisplayName = "ADR-0125: PUT com trio de cidade nulo não lança — devolve a violação de domínio")]
    public async Task Handle_CidadeNula_NaoLancaEDevolveViolacaoDeDominio()
    {
        Campus campus = CampusExistente();
        _repository.ObterPorIdAsync(campus.Id, Arg.Any<CancellationToken>()).Returns(campus);

        AtualizarCampusCommand comando = new(
            campus.Id, "CAMar", "Campus Marabá", null, null, null,
            null, null);

        Result resultado = await AtualizarCampusCommandHandler.Handle(
            comando, _repository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
    }

    /// <summary>
    /// Antes: a checagem de unicidade rodava antes de <see cref="Campus.Atualizar"/>
    /// validar, então uma sigla já usada mascarava qualquer outra violação de
    /// campo (ex.: Nome inválido) atrás de um SiglaJaExiste — e ainda gastava uma
    /// consulta ao repositório com um comando que já se sabe inválido por outro
    /// motivo.
    /// </summary>
    [Fact(DisplayName = "PUT com sigla já usada E nome vazio reporta a violação de Nome, não SiglaJaExiste")]
    public async Task Handle_SiglaDuplicadaENomeVazio_ReportaViolacaoDeCampoAntesDeConsultarUnicidade()
    {
        Campus campus = CampusExistente();
        _repository.ObterPorIdAsync(campus.Id, Arg.Any<CancellationToken>()).Returns(campus);
        _repository.SiglaExisteEntreLivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        AtualizarCampusCommand comando = new(
            campus.Id, "CABel", "", "1504208", "Marabá", "PA",
            null, null);

        Result resultado = await AtualizarCampusCommandHandler.Handle(
            comando, _repository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CampusErrorCodes.NomeObrigatorio);
        await _repository.DidNotReceive()
            .SiglaExisteEntreLivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Nome vazio e CEP em formato inválido no mesmo payload acumulam as duas violações")]
    public async Task Handle_NomeVazioEEnderecoInvalido_AcumulaAsDuasViolacoes()
    {
        Campus campus = CampusExistente();
        _repository.ObterPorIdAsync(campus.Id, Arg.Any<CancellationToken>()).Returns(campus);

        AtualizarCampusCommand comando = new(
            campus.Id, "CAMar", "", "1504208", "Marabá", "PA",
            EnderecoInput() with { Cep = "123" }, null);

        Result resultado = await AtualizarCampusCommandHandler.Handle(
            comando, _repository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors[0].Error.Code.Should().Be(CampusErrorCodes.NomeObrigatorio);
        resultado.Errors[1].Field.Should().Be("endereco");
    }

    /// <summary>
    /// Campus.Atualizar valida-e-muta atomicamente por dentro, mas o endereço é
    /// resolvido FORA dela — se o handler chamasse a mutação antes de saber que o
    /// endereço também falhou, o agregado rastreado pelo EF ficaria mutado com
    /// sigla/nome novos (e endereço nulo) na memória mesmo o handler retornando
    /// falha, e o Wolverine roda SaveChangesAsync depois do handler mesmo em
    /// falha — persistindo dado que o cliente nunca confirmou como válido.
    /// </summary>
    [Fact(DisplayName = "Endereço inválido com os demais campos válidos não muta o agregado rastreado antes de falhar")]
    public async Task Handle_EnderecoInvalidoComDemaisCamposValidos_NaoMutaAgregadoAntesDeFalhar()
    {
        Campus campus = CampusExistente();
        _repository.ObterPorIdAsync(campus.Id, Arg.Any<CancellationToken>()).Returns(campus);

        AtualizarCampusCommand comando = new(
            campus.Id, "CANova", "Campus Novo Nome", "1504208", "Marabá", "PA",
            EnderecoInput() with { Cep = "123" }, null);

        Result resultado = await AtualizarCampusCommandHandler.Handle(
            comando, _repository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        campus.Sigla.Should().Be("CAMAR", "o agregado rastreado não pode ser mutado antes de o handler confirmar sucesso");
        campus.Nome.Should().Be("Campus Marabá");
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Mesmo raciocínio do teste acima, para o outro caminho de falha depois da
    /// validação de campo: a checagem de unicidade. Se a mutação acontecesse
    /// antes dela, a sigla em conflito ficaria escrita no agregado rastreado
    /// mesmo o handler devolvendo SiglaJaExiste.
    /// </summary>
    [Fact(DisplayName = "Sigla conflitante com os demais campos válidos não muta o agregado rastreado antes de falhar")]
    public async Task Handle_SiglaConflitanteComDemaisCamposValidos_NaoMutaAgregadoAntesDeFalhar()
    {
        Campus campus = CampusExistente();
        _repository.ObterPorIdAsync(campus.Id, Arg.Any<CancellationToken>()).Returns(campus);
        _repository.SiglaExisteEntreLivosAsync("CABEL", campus.Id, Arg.Any<CancellationToken>())
            .Returns(true);

        AtualizarCampusCommand comando = new(
            campus.Id, "CABel", "Campus Marabá", "1504208", "Marabá", "PA",
            null, null);

        Result resultado = await AtualizarCampusCommandHandler.Handle(
            comando, _repository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CampusErrorCodes.SiglaJaExiste);
        campus.Sigla.Should().Be("CAMAR", "a sigla em conflito não pode ser escrita no agregado rastreado antes da confirmação de unicidade");
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Endereço inalterado preserva o instante do display cache do endereço")]
    public async Task Handle_EnderecoInalterado_PreservaCarimboDeEndereco()
    {
        DateTimeOffset enderecoCarimbadoEm = new(2026, 2, 2, 0, 0, 0, TimeSpan.Zero);
        ReferenciaEnderecoGeo enderecoExistente = EnderecoInput().ParaReferencia(enderecoCarimbadoEm).Value!;
        Campus campus = CampusExistente(enderecoExistente);
        _repository.ObterPorIdAsync(campus.Id, Arg.Any<CancellationToken>()).Returns(campus);

        // Mesmo conteúdo de endereço, só muda o nome do campus.
        AtualizarCampusCommand comando = new(
            campus.Id, "CAMar", "Campus Marabá Renomeado", "1504208", "Marabá", "PA",
            EnderecoInput(), null);

        Result resultado = await AtualizarCampusCommandHandler.Handle(
            comando, _repository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        campus.Endereco!.DisplayAtualizadoEm.Should().Be(enderecoCarimbadoEm,
            "o conteúdo do endereço não mudou, então o carimbo de frescura é preservado");
    }
}
