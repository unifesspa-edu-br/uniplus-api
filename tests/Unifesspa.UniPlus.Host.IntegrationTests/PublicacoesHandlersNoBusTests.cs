namespace Unifesspa.UniPlus.Host.IntegrationTests;

using System.Diagnostics.CodeAnalysis;

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
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
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

        var comando = new CriarTipoAtoPublicadoCommand(
            Codigo: "BUS_TESTE",
            Nome: "Tipo de ato exercitado pelo bus",
            CongelaConfiguracao: true,
            UnicoPorObjeto: false,
            EfeitoIrreversivel: false,
            VigenciaInicio: Inicio);

        Result<Guid> criado = await commandBus.Send(comando, CancellationToken.None);

        criado.IsSuccess.Should().BeTrue(
            "o assembly Publicacoes.Application precisa estar no Discovery do Wolverine "
            + "e a IPublicacoesUnitOfWork declarada no opt-in de codegen");

        TipoAtoPublicadoDto? lido = await queryBus.Send(
            new ObterTipoAtoPublicadoPorIdQuery(criado.Value), CancellationToken.None);

        lido.Should().NotBeNull();
        lido!.Codigo.Should().Be("BUS_TESTE");
        lido.CongelaConfiguracao.Should().BeTrue();
    }

    [Fact(DisplayName = "A query de tipo vigente atravessa o bus e resolve a data de referência")]
    public async Task QueryVigente_AtravessaOBus()
    {
        using IServiceScope scope = _fixture.Factory.Services.CreateScope();
        ICommandBus commandBus = scope.ServiceProvider.GetRequiredService<ICommandBus>();
        IQueryBus queryBus = scope.ServiceProvider.GetRequiredService<IQueryBus>();

        Result<Guid> criado = await commandBus.Send(
            new CriarTipoAtoPublicadoCommand(
                "BUS_VIGENTE", "Tipo vigente", false, false, false, Inicio, Inicio.AddYears(1)),
            CancellationToken.None);
        criado.IsSuccess.Should().BeTrue();

        TipoAtoPublicadoDto? dentro = await queryBus.Send(
            new ObterTipoAtoPublicadoVigenteQuery("BUS_VIGENTE", Inicio.AddMonths(6)),
            CancellationToken.None);
        dentro.Should().NotBeNull();

        // O fim é exclusivo: no dia em que a janela fecha, nenhuma versão vale.
        TipoAtoPublicadoDto? noFim = await queryBus.Send(
            new ObterTipoAtoPublicadoVigenteQuery("BUS_VIGENTE", Inicio.AddYears(1)),
            CancellationToken.None);
        noFim.Should().BeNull();
    }

    [Fact(DisplayName = "O registro de ato atravessa o bus, resolve o tipo vigente e copia por valor")]
    public async Task RegistrarAto_AtravessaOBus()
    {
        using IServiceScope scope = _fixture.Factory.Services.CreateScope();
        ICommandBus commandBus = scope.ServiceProvider.GetRequiredService<ICommandBus>();
        IQueryBus queryBus = scope.ServiceProvider.GetRequiredService<IQueryBus>();

        Result<Guid> tipo = await commandBus.Send(
            new CriarTipoAtoPublicadoCommand(
                "BUS_ATO_TIPO", "Tipo para ato exercitado pelo bus",
                CongelaConfiguracao: true, UnicoPorObjeto: false, EfeitoIrreversivel: false,
                VigenciaInicio: Inicio),
            CancellationToken.None);
        tipo.IsSuccess.Should().BeTrue();

        Result<RegistrarAtoNormativoResult> registrado = await commandBus.Send(
            new RegistrarAtoNormativoCommand(
                Orgao: "CEPS",
                Serie: "BUS_EDITAL",
                Ano: 2026,
                Numero: "1",
                TipoCodigo: "BUS_ATO_TIPO",
                DataPublicacao: Inicio.AddMonths(2),
                DocumentoHash: "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                Assinante: "Jairo Belchior"),
            CancellationToken.None);

        registrado.IsSuccess.Should().BeTrue(
            "o handler de registro precisa resolver ITipoAtoPublicadoRepository, "
            + "IAtoNormativoRepository e a IPublicacoesUnitOfWork encaminhada pelo codegen (ADR-0098)");

        AtoNormativoDto? lido = await queryBus.Send(
            new ObterAtoNormativoPorIdQuery(registrado.Value!.AtoId), CancellationToken.None);

        lido.Should().NotBeNull();
        // O tipo vigente congela configuração — copiado por valor no ato (AC5).
        lido!.CongelaConfiguracao.Should().BeTrue();
    }
}
