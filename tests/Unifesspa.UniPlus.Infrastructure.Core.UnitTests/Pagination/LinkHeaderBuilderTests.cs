namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Pagination;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;

public sealed class LinkHeaderBuilderTests
{
    [Fact]
    public void Build_ApenasSelf_RetornaApenasRelSelf()
    {
        PageLinks links = new(Self: "https://api.uniplus/editais", Next: null, Prev: null);

        string header = LinkHeaderBuilder.Build(links);

        header.Should().Be("<https://api.uniplus/editais>; rel=\"self\"");
    }

    [Fact]
    public void Build_SelfNext_OmitePrev()
    {
        PageLinks links = new(
            Self: "https://api.uniplus/editais",
            Next: "https://api.uniplus/editais?cursor=abc",
            Prev: null);

        string header = LinkHeaderBuilder.Build(links);

        header.Should().Be(
            "<https://api.uniplus/editais?cursor=abc>; rel=\"next\", " +
            "<https://api.uniplus/editais>; rel=\"self\"");
    }

    [Fact]
    public void Build_SelfPrev_OmiteNext()
    {
        PageLinks links = new(
            Self: "https://api.uniplus/editais",
            Next: null,
            Prev: "https://api.uniplus/editais?cursor=xyz");

        string header = LinkHeaderBuilder.Build(links);

        header.Should().Be(
            "<https://api.uniplus/editais?cursor=xyz>; rel=\"prev\", " +
            "<https://api.uniplus/editais>; rel=\"self\"");
    }

    [Fact]
    public void Build_TodasAsRels_RetornaPrevNextSelfNestaOrdem()
    {
        PageLinks links = new(
            Self: "https://api.uniplus/editais?cursor=cur",
            Next: "https://api.uniplus/editais?cursor=next",
            Prev: "https://api.uniplus/editais?cursor=prev");

        string header = LinkHeaderBuilder.Build(links);

        header.Should().Be(
            "<https://api.uniplus/editais?cursor=prev>; rel=\"prev\", " +
            "<https://api.uniplus/editais?cursor=next>; rel=\"next\", " +
            "<https://api.uniplus/editais?cursor=cur>; rel=\"self\"");
    }
}
