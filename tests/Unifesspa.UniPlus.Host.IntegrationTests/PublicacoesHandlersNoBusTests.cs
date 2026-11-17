namespace Unifesspa.UniPlus.Host.IntegrationTests;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;
using Unifesspa.UniPlus.Publicacoes.Application.Commands.TiposAtoPublicado;
using Unifesspa.UniPlus.Publicacoes.Application.DTOs;
using Unifesspa.UniPlus.Publicacoes.Application.Queries.AtosNormativos;
using Unifesspa.UniPlus.Publicacoes.Application.Queries.TiposAtoPublicado;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;

/// <summary>
/// Prova que os handlers do módulo Publicações são alcançáveis pelo bus no
/// composition root real.
/// </summary>
/// <remarks>
/// <para>O <c>Program.cs</c> inclui os assemblies do Wolverine um a um, e declara
/// separadamente os opt-ins de codegen de cada unit of work (ADR-0098). Esquecer
/// qualquer um dos dois **compila**, passa nos testes unitários com dublê e no
/// CI — e estoura no primeiro <c>Send</c> em produção. Só um teste que atravessa
/// o bus do host real expõe a falta.</para>
/// <para>Por isso o command percorre o caminho inteiro: validação FluentValidation
/// pelo middleware, handler, repositório, unit of work encaminhada para a mesma
/// instância de <c>DbContext</c>, e a leitura de volta pela query.</para>
/// </remarks>
[Collection(MonolitoHostCollection.Name)]
public sealed class PublicacoesHandlersNoBusTests
{
    private static readonly DateOnly Inicio = new(2026, 1, 1);

    private readonly MonolitoPostgresFixture _fixture;

    public PublicacoesHandlersNoBusTests(MonolitoPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "O command de criação atravessa o bus e a query devolve o tipo gravado")]
    public async Task CommandEQuery_AtravessamOBusDoHostReal()
    {
        using IServiceScope scope = _fixture.Factory.Services.CreateScope();
        ICommandBus commandBus = scope.ServiceProvider.GetRequiredService<ICommandBus>();
        IQueryBus queryBus = scope.ServiceProvider.GetRequiredService<IQueryBus>();

        Result<Guid> criado = await commandBus.Send(NovoTipoQueCongela("BUS_TESTE"), CancellationToken.None);

        criado.IsSuccess.Should().BeTrue(
            "o assembly Publicacoes.Application precisa estar no Discovery do Wolverine "
            + "e a IUnitOfWork declarada no opt-in de codegen");
        Result<TipoAtoPublicadoDto> lido = await queryBus.Send(
            new ObterTipoAtoPublicadoPorIdQuery(criado.Value), CancellationToken.None);
        lido.Value.Should().NotBeNull();
        lido.Value!.Codigo.Should().Be("BUS_TESTE");
        lido.Value.CongelaConfiguracao.Should().BeTrue();
    }

    [Fact(DisplayName = "A query por Id de tipo inexistente atravessa o bus e falha com tipo não encontrado")]
    public async Task QueryPorId_TipoInexistente_AtravessaOBusComNaoEncontrado()
    {
        using IServiceScope scope = _fixture.Factory.Services.CreateScope();
        IQueryBus queryBus = scope.ServiceProvider.GetRequiredService<IQueryBus>();

        Result<TipoAtoPublicadoDto> lido = await queryBus.Send(
            new ObterTipoAtoPublicadoPorIdQuery(Guid.NewGuid()), CancellationToken.None);

        lido.IsFailure.Should().BeTrue();
        lido.Error!.Code.Should().Be(TipoAtoPublicadoErrorCodes.NaoEncontrado);
    }

    [Fact(DisplayName = "A query de tipo vigente atravessa o bus e resolve a data de referência")]
    public async Task QueryVigente_TipoComJanelaFechada_ResolveSoDentroDaJanela()
    {
        using IServiceScope scope = _fixture.Factory.Services.CreateScope();
        ICommandBus commandBus = scope.ServiceProvider.GetRequiredService<ICommandBus>();
        IQueryBus queryBus = scope.ServiceProvider.GetRequiredService<IQueryBus>();
        await CriarTipoComJanelaDeUmAnoAsync(commandBus, "BUS_VIGENTE");

        Result<TipoAtoPublicadoDto> dentro = await ObterVigenteAsync(queryBus, "BUS_VIGENTE", Inicio.AddMonths(6));
        Result<TipoAtoPublicadoDto> noFim = await ObterVigenteAsync(queryBus, "BUS_VIGENTE", Inicio.AddYears(1));

        dentro.Value.Should().NotBeNull();
        noFim.Error!.Code.Should().Be(
            TipoAtoPublicadoErrorCodes.NaoEncontrado,
            "o fim é exclusivo: no dia em que a janela fecha, nenhuma versão vale");
    }

    [Fact(DisplayName = "O registro de ato atravessa o bus, resolve o tipo vigente e copia por valor")]
    public async Task RegistrarAto_TipoVigente_CopiaConsequenciaPorValor()
    {
        using IServiceScope scope = _fixture.Factory.Services.CreateScope();
        ICommandBus commandBus = scope.ServiceProvider.GetRequiredService<ICommandBus>();
        IQueryBus queryBus = scope.ServiceProvider.GetRequiredService<IQueryBus>();
        await CriarTipoQueCongelaAsync(commandBus, "BUS_ATO_TIPO");

        Result<RegistrarAtoNormativoResult> registrado = await RegistrarAtoAsync(commandBus, "BUS_ATO_TIPO");

        registrado.IsSuccess.Should().BeTrue(
            "o handler de registro precisa resolver ITipoAtoPublicadoRepository, "
            + "IAtoNormativoRepository e a IUnitOfWork encaminhada pelo codegen (ADR-0098)");
        Result<AtoNormativoDto> leitura = await queryBus.Send(
            new ObterAtoNormativoPorIdQuery(registrado.Value!.AtoId), CancellationToken.None);
        leitura.IsSuccess.Should().BeTrue();
        leitura.Value!.CongelaConfiguracao.Should().BeTrue();
    }

    private static CriarTipoAtoPublicadoCommand NovoTipoQueCongela(string codigo) =>
        new(
            Codigo: codigo,
            Nome: "Tipo de ato exercitado pelo bus",
            CongelaConfiguracao: true,
            UnicoPorObjeto: false,
            EfeitoIrreversivel: false,
            EhResultado: false,
            PublicavelComoAvulso: false,
            ExigeAtoCorrigido: false,
            VigenciaInicio: Inicio);

    private static async Task CriarTipoQueCongelaAsync(ICommandBus commandBus, string codigo)
    {
        Result<Guid> tipo = await commandBus.Send(NovoTipoQueCongela(codigo), CancellationToken.None);
        tipo.IsSuccess.Should().BeTrue();
    }

    private static async Task CriarTipoComJanelaDeUmAnoAsync(ICommandBus commandBus, string codigo)
    {
        Result<Guid> criado = await commandBus.Send(
            NovoTipoQueCongela(codigo) with { CongelaConfiguracao = false, VigenciaFim = Inicio.AddYears(1) },
            CancellationToken.None);
        criado.IsSuccess.Should().BeTrue();
    }

    private static Task<Result<TipoAtoPublicadoDto>> ObterVigenteAsync(IQueryBus queryBus, string codigo, DateOnly data) =>
        queryBus.Send(new ObterTipoAtoPublicadoVigenteQuery(codigo, data), CancellationToken.None);

    private static Task<Result<RegistrarAtoNormativoResult>> RegistrarAtoAsync(
        ICommandBus commandBus, string tipoCodigo) =>
        commandBus.Send(
            new RegistrarAtoNormativoCommand(
                Orgao: "CEPS",
                Serie: "BUS_EDITAL",
                Ano: 2026,
                Numero: "1",
                TipoCodigo: tipoCodigo,
                DataDoAto: Inicio.AddMonths(2),
                DocumentoHash: "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                Assinante: "Jairo Belchior"),
            CancellationToken.None);
}
