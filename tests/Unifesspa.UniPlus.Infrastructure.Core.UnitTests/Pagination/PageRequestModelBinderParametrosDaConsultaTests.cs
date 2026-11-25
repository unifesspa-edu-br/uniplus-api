namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Pagination;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;

using static CursorBinderTestSupport;

public sealed class PageRequestModelBinderParametrosDaConsultaTests
{
    [Fact]
    public async Task BindModelAsync_SemCursorSemLimit_RetornaPageRequestComDefaults()
    {
        ModelBindingContext context = await BindAsync("");

        context.Result.IsModelSet.Should().BeTrue();
        PageRequest page = context.Result.Model.Should().BeOfType<PageRequest>().Subject;
        page.AfterId.Should().BeNull();
        page.Limit.Should().Be(20);
    }

    [Fact]
    public async Task BindModelAsync_CursorWhitespaceOnly_TratadoComoCursorAusente()
    {
        ModelBindingContext context = await BindAsync("?cursor=%20%20%20");

        context.Result.IsModelSet.Should().BeTrue();
        PageRequest page = (PageRequest)context.Result.Model!;
        page.AfterId.Should().BeNull();
        page.Limit.Should().Be(20);
    }

    [Fact]
    public async Task BindModelAsync_LimitNaoNumerico_RetornaLimitInvalido()
    {
        ModelBindingContext context = await BindAsync("?limit=abc");

        context.Result.IsModelSet.Should().BeFalse();
        context.HttpContext.Items[CursorBindingErrorCodes.HttpContextItemKey]
            .Should().Be(CursorBindingErrorCodes.LimitInvalido);
    }

    [Fact]
    public async Task BindModelAsync_LimitNegativo_RetornaLimitInvalido()
    {
        ModelBindingContext context = await BindAsync("?limit=-5");

        context.Result.IsModelSet.Should().BeFalse();
        context.HttpContext.Items[CursorBindingErrorCodes.HttpContextItemKey]
            .Should().Be(CursorBindingErrorCodes.LimitInvalido);
    }

    [Fact]
    public async Task BindModelAsync_LimitZero_RetornaLimitInvalido()
    {
        ModelBindingContext context = await BindAsync("?limit=0");

        context.Result.IsModelSet.Should().BeFalse();
        context.HttpContext.Items[CursorBindingErrorCodes.HttpContextItemKey]
            .Should().Be(CursorBindingErrorCodes.LimitInvalido);
    }

    [Fact]
    public async Task BindModelAsync_LimitAcimaDoMaximo_RetornaLimitInvalido()
    {
        ModelBindingContext context = await BindAsync("?limit=999");

        context.Result.IsModelSet.Should().BeFalse();
        context.HttpContext.Items[CursorBindingErrorCodes.HttpContextItemKey]
            .Should().Be(CursorBindingErrorCodes.LimitInvalido);
    }

    [Fact]
    public async Task BindModelAsync_SemDirection_DefaultEhNext()
    {
        ModelBindingContext context = await BindAsync("");

        PageRequest page = context.Result.Model.Should().BeOfType<PageRequest>().Subject;
        page.Direction.Should().Be(PaginationDirection.Next);
    }

    [Fact]
    public async Task BindModelAsync_DirectionInvalida_RetornaDirecaoInvalida()
    {
        ModelBindingContext context = await BindAsync("?direction=foo");

        context.Result.IsModelSet.Should().BeFalse();
        context.HttpContext.Items[CursorBindingErrorCodes.HttpContextItemKey]
            .Should().Be(CursorBindingErrorCodes.DirecaoInvalida);
    }

}
