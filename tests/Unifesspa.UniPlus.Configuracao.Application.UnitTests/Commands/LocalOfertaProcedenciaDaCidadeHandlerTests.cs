namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Enderecos;
using Unifesspa.UniPlus.Configuracao.Application.Commands.LocaisOferta;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class LocalOfertaProcedenciaDaCidadeHandlerTests
{
    private static readonly DateTimeOffset Criacao = new(2026, 6, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Edicao = Criacao.AddDays(30);

    private readonly ILocalOfertaRepository _repository = Substitute.For<ILocalOfertaRepository>();
    private readonly ICampusRepository _campusRepository = Substitute.For<ICampusRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private LocalOferta Existente()
    {
        LocalOferta local = LocalOferta.Criar(
            new(TipoLocalOferta.PoloEad, null, "1504208", "Marabá", "PA", null, null), Criacao).ValorEsperado();
        _repository.ObterPorIdAsync(local.Id, Arg.Any<CancellationToken>()).Returns(local);

        return local;
    }

    private Task<Result> AtualizarAsync(AtualizarLocalOfertaCommand comando) =>
        AtualizarLocalOfertaCommandHandler.Handle(
            comando, _repository, _campusRepository, _unitOfWork, new RelogioFixo(Edicao), CancellationToken.None);

    [Fact(DisplayName = "Atualizar com outra cidade renova a procedência com o instante do relógio")]
    public async Task Handle_CidadeMudou_RenovaAProcedencia()
    {
        LocalOferta local = Existente();

        Result resultado = await AtualizarAsync(new(local.Id, TipoLocalOferta.PoloEad, null, "1501402", "Belém", "PA", null, null));

        resultado.IsSuccess.Should().BeTrue();
        local.CidadeOrigem.Should().Be(ReferenciaCidadeGeo.OrigemGeoApi);
        local.CidadeDisplayAtualizadoEm.Should().Be(Edicao);
    }

    [Fact(DisplayName = "Atualizar com a mesma cidade e outro tipo preserva o instante da procedência")]
    public async Task Handle_CidadeIgual_PreservaOInstante()
    {
        LocalOferta local = Existente();

        Result resultado = await AtualizarAsync(new(local.Id, TipoLocalOferta.CampusSede, null, "1504208", "Marabá", "PA", null, null));

        resultado.IsSuccess.Should().BeTrue();
        local.Tipo.Should().Be(TipoLocalOferta.CampusSede);
        local.CidadeDisplayAtualizadoEm.Should().Be(Criacao);
    }

    [Fact(DisplayName = "Criar define a procedência da cidade com o instante do relógio")]
    public async Task Handle_Criar_DefineAProcedencia()
    {
        LocalOferta? criado = null;
        await _repository.AdicionarAsync(Arg.Do<LocalOferta>(local => criado = local), Arg.Any<CancellationToken>());

        Result<Guid> resultado = await CriarLocalOfertaCommandHandler.Handle(
            new(TipoLocalOferta.PoloEad, null, "1504208", "Marabá", "PA", null, null),
            _repository, _campusRepository, _unitOfWork, new RelogioFixo(Edicao), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        criado.Should().NotBeNull();
        criado.CidadeOrigem.Should().Be(ReferenciaCidadeGeo.OrigemGeoApi);
        criado.CidadeDisplayAtualizadoEm.Should().Be(Edicao);
    }

    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }
}
