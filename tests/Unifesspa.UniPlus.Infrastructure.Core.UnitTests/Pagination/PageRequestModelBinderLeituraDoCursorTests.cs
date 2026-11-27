namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Pagination;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Apoio.ControllersDeTeste;
using Unifesspa.UniPlus.Kernel.Pagination;

using static CursorBinderTestSupport;

public sealed class PageRequestModelBinderLeituraDoCursorTests
{
    [Fact]
    public async Task BindModelAsync_CursorPrevComDirectionPrev_RetornaPageRequestPrev()
    {
        Guid expectedAfter = Guid.NewGuid();
        ServiceProvider services = CreateCursorServices().BuildServiceProvider();
        string cursor = await EncodeAsync(services, ValidPayload() with { After = expectedAfter.ToString(), Limit = 20, Direction = PaginationDirection.Prev });

        ModelBindingContext context = await BindAsync(QueryWithCursor(cursor, "&direction=prev"), services: services);

        PageRequest page = context.Result.Model.Should().BeOfType<PageRequest>().Subject;
        page.AfterId.Should().Be(expectedAfter);
        page.Direction.Should().Be(PaginationDirection.Prev);
    }

    [Fact]
    public async Task BindModelAsync_CursorComResourceTagDeOutroRecurso_RetornaCursorInvalido()
    {
        ServiceProvider services = CreateCursorServices().BuildServiceProvider();
        string foreignCursor = await EncodeAsync(services, ValidPayload("inscricoes"));

        ModelBindingContext context = await BindAsync(QueryWithCursor(foreignCursor), services: services);

        context.Result.IsModelSet.Should().BeFalse();
        context.HttpContext.Items[CursorBindingErrorCodes.HttpContextItemKey]
            .Should().Be(CursorBindingErrorCodes.Invalido);
    }

    [Fact]
    public async Task BindModelAsync_CursorAfterNaoEhGuidValido_RetornaCursorInvalido()
    {
        ServiceProvider services = CreateCursorServices().BuildServiceProvider();
        string malformedAfter = await EncodeAsync(services, ValidPayload() with { After = "not-a-guid" });

        ModelBindingContext context = await BindAsync(QueryWithCursor(malformedAfter), services: services);

        context.Result.IsModelSet.Should().BeFalse();
        context.HttpContext.Items[CursorBindingErrorCodes.HttpContextItemKey]
            .Should().Be(CursorBindingErrorCodes.Invalido);
    }

    [Fact]
    public async Task BindModelAsync_CursorValido_RetornaPageRequestComAfterIdEDLimitDoCursor()
    {
        Guid expectedAfter = Guid.NewGuid();
        ServiceProvider services = CreateCursorServices().BuildServiceProvider();
        string cursor = await EncodeAsync(services, ValidPayload() with { After = expectedAfter.ToString(), Limit = 50 });

        ModelBindingContext context = await BindAsync(QueryWithCursor(cursor), services: services);

        context.Result.IsModelSet.Should().BeTrue();
        PageRequest page = (PageRequest)context.Result.Model!;
        page.AfterId.Should().Be(expectedAfter);
        page.Limit.Should().Be(50);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BindModelAsync_CursorSemChaveDeContinuacao_RetornaCursorInvalido(string? chave)
    {
        ServiceProvider services = CreateCursorServices().BuildServiceProvider();
        string cursor = await EncodeAsync(services, ValidPayload("editais") with { SortKey = chave });

        ModelBindingContext context = await BindAsync(QueryWithCursor(cursor), services: services);

        AssertInvalidCursor(context);
    }

    [Fact]
    public async Task BindModelAsync_CursorComChaveDeContinuacao_DevolveAChaveNaPagina()
    {
        Guid expectedAfter = Guid.NewGuid();
        ServiceProvider services = CreateCursorServices().BuildServiceProvider();
        string cursor = await EncodeAsync(services, ValidPayload("editais") with { After = expectedAfter.ToString(), Limit = 20, SortKey = "chave-x" });

        ModelBindingContext context = await BindAsync(QueryWithCursor(cursor), services: services);

        PageRequest page = AssertPage(context);
        page.AfterId.Should().Be(expectedAfter);
        page.AfterSortKey.Should().Be("chave-x");
    }

    [Fact]
    public async Task BindModelAsync_QueryStringLimitVenceCursorLimit()
    {
        Guid expectedAfter = Guid.NewGuid();
        ServiceProvider services = CreateCursorServices().BuildServiceProvider();
        string cursor = await EncodeAsync(services, ValidPayload() with { After = expectedAfter.ToString() });

        ModelBindingContext context = await BindAsync(QueryWithCursor(cursor, "&limit=50"), services: services);

        PageRequest page = (PageRequest)context.Result.Model!;
        page.Limit.Should().Be(50, "limit do query string vence sobre o do cursor");
    }

    [Fact]
    public async Task BindModelAsync_CursorExpirado_RetornaCursorExpirado()
    {
        Guid afterId = Guid.NewGuid();
        ServiceProvider services = CreateCursorServices().BuildServiceProvider();
        string cursor = await EncodeAsync(services, ValidPayload() with { After = afterId.ToString(), ExpiresAt = InstanteDaConsulta.AddMinutes(-1) });

        ModelBindingContext context = await BindAsync(QueryWithCursor(cursor), services: services);

        context.Result.IsModelSet.Should().BeFalse();
        context.HttpContext.Items[CursorBindingErrorCodes.HttpContextItemKey]
            .Should().Be(CursorBindingErrorCodes.Expirado);
    }
}
