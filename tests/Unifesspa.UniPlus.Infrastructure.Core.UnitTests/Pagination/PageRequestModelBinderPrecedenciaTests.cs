namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Pagination;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Apoio.ControllersDeTeste;
using Unifesspa.UniPlus.Kernel.Pagination;

using static CursorBinderTestSupport;

public sealed class PageRequestModelBinderPrecedenciaTests
{
    private const string InvalidLimitMessage = "O parâmetro 'limit' deve estar entre 1 e 100.";
    private const string InvalidDirectionMessage = "O parâmetro 'direction' deve ser 'next' ou 'prev'.";

    [Fact]
    public async Task BindModelAsync_LimiteInvalidoComCursorInvalido_RecusaOLimite()
    {
        ModelBindingContext context = await BindAsync("?limit=0&cursor=@@@@");

        AssertRejection(context, CursorBindingErrorCodes.LimitInvalido, InvalidLimitMessage);
    }

    [Fact]
    public async Task BindModelAsync_LimiteInvalidoComDirecaoInvalida_RecusaOLimite()
    {
        ModelBindingContext context = await BindAsync("?limit=101&direction=foo");

        AssertRejection(context, CursorBindingErrorCodes.LimitInvalido, InvalidLimitMessage);
    }

    [Fact]
    public async Task BindModelAsync_DirecaoInvalidaComCursorInvalido_RecusaADirecao()
    {
        ModelBindingContext context = await BindAsync("?direction=foo&cursor=@@@@");

        AssertRejection(context, CursorBindingErrorCodes.DirecaoInvalida, InvalidDirectionMessage);
    }

    [Fact]
    public async Task BindModelAsync_LimiteForaDoIntervaloComCursorValido_RecusaOLimite()
    {
        ServiceProvider services = CreateCursorServices().BuildServiceProvider();
        string cursor = await EncodeAsync(services, ValidPayload());

        ModelBindingContext context = await BindAsync(QueryWithCursor(cursor, "&limit=500"), services: services);

        AssertRejection(context, CursorBindingErrorCodes.LimitInvalido, InvalidLimitMessage);
    }

    [Fact]
    public async Task BindModelAsync_CursorInvalido_RecusaComAMensagemDeCursorInvalido()
    {
        ModelBindingContext context = await BindAsync("?cursor=@@@@");

        AssertInvalidCursor(context);
    }

    [Fact]
    public async Task BindModelAsync_CursorExpiradoComEtiquetaDeOutroRecurso_RecusaComoExpirado()
    {
        ServiceProvider services = CreateCursorServices().BuildServiceProvider();
        CursorPayload payload = ValidPayload("inscricoes") with { ExpiresAt = InstanteDaConsulta.AddMinutes(-1) };
        string cursor = await EncodeAsync(services, payload);

        ModelBindingContext context = await BindAsync(QueryWithCursor(cursor), services: services);

        AssertRejection(context, CursorBindingErrorCodes.Expirado, "O cursor informado expirou.");
    }

    [Fact]
    public async Task BindModelAsync_CursorEmitidoParaProxima_NavegadoComoAnterior_RecusaComoInvalido()
    {
        ServiceProvider services = CreateCursorServices().BuildServiceProvider();
        string cursor = await EncodeAsync(services, ValidPayload());

        ModelBindingContext context = await BindAsync(QueryWithCursor(cursor, "&direction=prev"), services: services);

        AssertInvalidCursor(context);
    }

    [Fact]
    public async Task BindModelAsync_DirecaoEmCaixaAlta_AceitaAnterior()
    {
        ServiceProvider services = CreateCursorServices().BuildServiceProvider();
        string cursor = await EncodeAsync(services, ValidPayload() with { Direction = PaginationDirection.Prev });

        ModelBindingContext context = await BindAsync(QueryWithCursor(cursor, "&direction=PREV"), services: services);

        AssertPage(context).Direction.Should().Be(PaginationDirection.Prev);
    }

    [Fact]
    public async Task BindModelAsync_PrevSemCursor_CoageParaNext()
    {
        ModelBindingContext context = await BindAsync("?direction=prev");

        PageRequest page = context.Result.Model.Should().BeOfType<PageRequest>().Subject;
        page.AfterId.Should().BeNull();
        page.Direction.Should().Be(PaginationDirection.Next);
    }

    [Fact]
    public async Task BindModelAsync_CursorEmBrancoComDirecaoAnterior_ComecaParaAFrente()
    {
        ModelBindingContext context = await BindAsync("?cursor=%20&direction=prev");

        PageRequest page = AssertPage(context);

        page.AfterId.Should().BeNull();
        page.Direction.Should().Be(PaginationDirection.Next);
    }

    [Fact]
    public async Task BindModelAsync_SemCursorComLimiteNaConsulta_UsaOLimiteDaConsulta()
    {
        ModelBindingContext context = await BindAsync("?limit=35");

        AssertPage(context).Limit.Should().Be(35);
    }

    [Fact]
    public async Task BindModelAsync_LimiteMalformadoComMinimoInteiro_AceitaValorSentinela()
    {
        ServiceCollection services = CreateCursorServices();
        services.AddSingleton(Options.Create(new CursorPaginationOptions { LimitMin = int.MinValue }));

        ModelBindingContext context = await BindAsync("?limit=abc", services: services.BuildServiceProvider());

        AssertPage(context).Limit.Should().Be(int.MinValue);
    }

    [Fact]
    public async Task BindModelAsync_CursorComLimiteAcimaDoMaximo_AjustaAoMaximo()
    {
        ModelBindingContext context = await BindWithCursorLimitAsync(500);

        AssertPage(context).Limit.Should().Be(100);
    }

    [Fact]
    public async Task BindModelAsync_CursorComLimiteAbaixoDoMinimo_AjustaAoMinimo()
    {
        ModelBindingContext context = await BindWithCursorLimitAsync(-3);

        AssertPage(context).Limit.Should().Be(1);
    }

    private static async Task<ModelBindingContext> BindWithCursorLimitAsync(int cursorLimit)
    {
        ServiceProvider services = CreateCursorServices().BuildServiceProvider();
        string cursor = await EncodeAsync(services, ValidPayload() with { Limit = cursorLimit });

        return await BindAsync(QueryWithCursor(cursor), services: services);
    }
}
