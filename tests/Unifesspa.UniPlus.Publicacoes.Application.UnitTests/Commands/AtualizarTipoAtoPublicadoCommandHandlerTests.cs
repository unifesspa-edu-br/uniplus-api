namespace Unifesspa.UniPlus.Publicacoes.Application.UnitTests.Commands;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Application.Commands.TiposAtoPublicado;
using Unifesspa.UniPlus.Publicacoes.Application.Persistence;
using Unifesspa.UniPlus.Publicacoes.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;
using Unifesspa.UniPlus.Testes.Compartilhado;

[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class AtualizarTipoAtoPublicadoCommandHandlerTests
{
    private static readonly DateOnly Inicio = new(2026, 1, 1);

    private readonly ITipoAtoPublicadoRepository _repository = Substitute.For<ITipoAtoPublicadoRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact(DisplayName = "Recusa quando o tipo de ato não existe")]
    public async Task Handle_NaoEncontrado_Falha()
    {
        _repository.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((TipoAtoPublicado?)null);

        Result resultado = await AtualizarTipoAtoPublicadoCommandHandler.Handle(
            Comando(Guid.NewGuid()), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoAtoPublicadoErrorCodes.NaoEncontrado);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    /// <remarks>A janela não mudou, logo nenhuma sobreposição pode ter surgido: a consulta seria um acesso ao banco inútil a cada atualização.</remarks>
    [Fact(DisplayName = "Não consulta sobreposição quando código e janela não mudam")]
    public async Task Handle_SoMudaNome_NaoConsultaSobreposicao()
    {
        TipoAtoPublicado existente = ExistenteNoRepositorio();

        Result resultado = await AtualizarTipoAtoPublicadoCommandHandler.Handle(
            Comando(existente.Id) with { Nome = "Outro nome" }, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        await _repository.DidNotReceive().ExisteSobreposicaoDeVigenciaAsync(
            Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    public static TheoryData<DateOnly, DateOnly?> JanelasQueMudam => new()
    {
        { new DateOnly(2026, 3, 1), null },
        { new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1) },
    };

    /// <remarks>A própria versão é excluída da checagem: senão ela colidiria consigo mesma.</remarks>
    [Theory(DisplayName = "Consulta sobreposição quando a janela muda")]
    [MemberData(nameof(JanelasQueMudam))]
    public async Task Handle_MudaJanela_ConsultaSobreposicao(DateOnly inicio, DateOnly? fim)
    {
        TipoAtoPublicado existente = ExistenteNoRepositorio();
        ResponderSobreposicaoDeVigencia(false);
        AtualizarTipoAtoPublicadoCommand comando = Comando(existente.Id) with { VigenciaInicio = inicio, VigenciaFim = fim };

        await AtualizarTipoAtoPublicadoCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        await _repository.Received(1).ExisteSobreposicaoDeVigenciaAsync(
            "EDITAL_ABERTURA", inicio, fim, existente.Id, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Recusa código alterado sem consultar sobreposição — o código é a identidade do tipo")]
    public async Task Handle_ComCodigoAlterado_RecusaSemConsultarSobreposicao()
    {
        TipoAtoPublicado existente = ExistenteNoRepositorio();

        Result resultado = await AtualizarTipoAtoPublicadoCommandHandler.Handle(
            Comando(existente.Id) with { Codigo = "EDITAL_RETIFICACAO" },
            _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoAtoPublicadoErrorCodes.CodigoImutavel);
        await _repository.DidNotReceive().ExisteSobreposicaoDeVigenciaAsync(
            Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Recusa quando a nova janela intercepta outra versão viva")]
    public async Task Handle_ComSobreposicao_Falha()
    {
        TipoAtoPublicado existente = ExistenteNoRepositorio();
        ResponderSobreposicaoDeVigencia(true);

        Result resultado = await AtualizarTipoAtoPublicadoCommandHandler.Handle(
            Comando(existente.Id) with { VigenciaInicio = Inicio.AddMonths(2) },
            _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoAtoPublicadoErrorCodes.VigenciaSobreposta);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Propaga o erro do agregado sem buscar por Id — validação vence 404")]
    public async Task Handle_ComPayloadInvalido_RetornaErroSemBuscarPorId()
    {
        Result resultado = await AtualizarTipoAtoPublicadoCommandHandler.Handle(
            Comando(Guid.NewGuid()) with { Nome = "E" }, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoAtoPublicadoErrorCodes.NomeTamanho);
        await _repository.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Atributo ausente é recusado como obrigatório sem buscar por Id, acumulado com as demais recusas")]
    public async Task Handle_ComAtributoAusente_AcumulaSemBuscarPorId()
    {
        Result resultado = await AtualizarTipoAtoPublicadoCommandHandler.Handle(
            Comando(Guid.NewGuid()) with { Nome = "E", ExigeAtoCorrigido = null },
            _repository, _unitOfWork, CancellationToken.None);

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ("nome", TipoAtoPublicadoErrorCodes.NomeTamanho),
            ("exigeAtoCorrigido", CodigosDeValidacao.Obrigatorio));
        await _repository.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Início da vigência ausente é recusado e o tipo existente não muda")]
    public async Task Handle_ComInicioDaVigenciaAusente_NaoAlteraOTipo()
    {
        TipoAtoPublicado existente = ExistenteNoRepositorio();

        Result resultado = await AtualizarTipoAtoPublicadoCommandHandler.Handle(
            Comando(existente.Id) with { VigenciaInicio = null }, _repository, _unitOfWork, CancellationToken.None);

        resultado.Errors.Should().ContainSingle(e => e.Field == "vigenciaInicio" && e.Error.Code == CodigosDeValidacao.Obrigatorio);
        existente.VigenciaInicio.Should().Be(Inicio);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Violação da exclusion constraint de vigência descarta o rastreamento antes de devolver o conflito")]
    public async Task Handle_ColisaoNaExclusionConstraint_DescartaRastreamentoERetornaConflito()
    {
        TipoAtoPublicado existente = ExistenteNoRepositorio();
        ResponderSobreposicaoDeVigencia(false);
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateException(
                "conflito sintético de teste",
                PostgresExceptionFactory.Create("23P01", RestricoesDoBanco.TipoDeAtoPublicadoCodigoVigencia)));

        Result resultado = await AtualizarTipoAtoPublicadoCommandHandler.Handle(
            Comando(existente.Id) with { VigenciaInicio = Inicio.AddMonths(2) },
            _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoAtoPublicadoErrorCodes.VigenciaSobreposta);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    private static TipoAtoPublicado Existente() =>
        new TipoAtoPublicadoDeTeste { CongelaConfiguracao = true, UnicoPorObjeto = true }.Criar().Value!;

    private TipoAtoPublicado ExistenteNoRepositorio()
    {
        TipoAtoPublicado existente = Existente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        return existente;
    }

    private void ResponderSobreposicaoDeVigencia(bool existe) =>
        _repository.ExisteSobreposicaoDeVigenciaAsync(
            Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(existe);

    private static AtualizarTipoAtoPublicadoCommand Comando(Guid id) =>
        new(
            Id: id,
            Codigo: "EDITAL_ABERTURA",
            Nome: "Edital de abertura",
            CongelaConfiguracao: true,
            UnicoPorObjeto: true,
            EfeitoIrreversivel: false,
            EhResultado: false,
            PublicavelComoAvulso: false,
            ExigeAtoCorrigido: false,
            VigenciaInicio: Inicio);
}
