namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Authorization;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// A serialização defensiva omite o campo classificado sem decisão permitida na requisição, sem pedir a decisão de novo, e
/// falha fechada: sem decisão, nada classificado sai (ADR-0153).
/// </summary>
public sealed class OmissaoDeCampoClassificadoTests
{
    private readonly IHttpContextAccessor _acessor = Substitute.For<IHttpContextAccessor>();
    private readonly DefaultHttpContext _http = new();
    private readonly JsonSerializerOptions _json;

    public OmissaoDeCampoClassificadoTests()
    {
        _acessor.HttpContext.Returns(_http);
        _json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver().WithAddedModifier(OmissaoDeCampoClassificado.Modificador(_acessor)),
        };
    }

    [Fact(DisplayName = "A propriedade classificada sem decisão permitida é omitida; a permitida e a não classificada saem")]
    public void Propriedade_SoSaiComADecisaoDoPar()
    {
        Permitir((NaturezaDoItem.Dado, ClassificacaoDeProtecao.Publico));
        Linha linha = new(Guid.Parse("01a19629-35d0-78f1-8035-0a66bb26c746"), "Ariel Drumond", "esta é a nota solta");

        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(linha, _json));

        json.RootElement.TryGetProperty("id", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("nome", out _).Should().BeFalse("o nome é dado pessoal e não houve decisão permitida");
        json.RootElement.GetProperty("livre").GetString().Should().Be("esta é a nota solta");
    }

    [Fact(DisplayName = "Sem decisão alguma na requisição nada classificado sai")]
    public void SemDecisao_NadaClassificadoSai()
    {
        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(new Linha(Guid.CreateVersion7(), "Ariel Drumond", "livre"), _json));

        json.RootElement.EnumerateObject().Select(static p => p.Name).Should().Equal("livre");
    }

    [Fact(DisplayName = "Fora de uma requisição HTTP nada classificado sai")]
    public void ForaDeRequisicao_NadaClassificadoSai()
    {
        _acessor.HttpContext.Returns((HttpContext?)null);

        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(new Linha(Guid.CreateVersion7(), "Ariel Drumond", "livre"), _json));

        json.RootElement.EnumerateObject().Select(static p => p.Name).Should().Equal("livre");
    }

    [Fact(DisplayName = "A lista de itens sai só com os permitidos, e o sensível sem base legal fica de fora, sem indício dos omitidos")]
    public void ListaDeItens_SaiSoComOsPermitidos()
    {
        Permitir((NaturezaDoItem.Dado, ClassificacaoDeProtecao.Pessoal), (NaturezaDoItem.Dado, ClassificacaoDeProtecao.Sensivel));
        Pagina pagina = new(
        [
            new Item("NOME", ClassificacaoDeProtecao.Pessoal, "LGPD art. 7º"),
            new Item("QUILOMBOLA", ClassificacaoDeProtecao.Sensivel, "LGPD art. 11"),
            new Item("SEM_BASE", ClassificacaoDeProtecao.Sensivel, null),
            new Item("COR_RACA", ClassificacaoDeProtecao.Interno, "LGPD art. 7º"),
            new Item("NAO_RESOLVIDO", ClassificacaoDeProtecao.Nenhuma, "LGPD art. 7º"),
        ]);

        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(pagina, _json));

        json.RootElement.GetProperty("itens").EnumerateArray().Select(static i => i.GetProperty("codigo").GetString())
            .Should().Equal("NOME", "QUILOMBOLA", "NAO_RESOLVIDO");
    }

    [Fact(DisplayName = "A permissão de dado não abre o item de arquivo")]
    public void Arquivo_NaoSaiPorPermissaoDeDado()
    {
        Permitir((NaturezaDoItem.Dado, ClassificacaoDeProtecao.Pessoal));
        Pagina pagina = new([new Item("LAUDO", ClassificacaoDeProtecao.Pessoal, "LGPD", NaturezaDoItem.Arquivo)]);

        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(pagina, _json));

        json.RootElement.GetProperty("itens").GetArrayLength().Should().Be(0);
    }

    private void Permitir(params (NaturezaDoItem, ClassificacaoDeProtecao)[] pares) =>
        new DecisoesDaRequisicaoHttp(_acessor).Guardar(new DecisoesDaProjecao(pares.ToDictionary(static p => p, static _ => DecisaoDeAcesso.IdentidadeIncompleta)));

    private sealed record Linha(
        [property: ProtegidoComo(ClassificacaoDeProtecao.Publico)] Guid Id,
        [property: ProtegidoComo(ClassificacaoDeProtecao.Pessoal)] string Nome,
        string Livre);

    private sealed record Pagina(IReadOnlyList<Item> Itens);

    private sealed record Item : ItemClassificado
    {
        public Item(string codigo, ClassificacaoDeProtecao classificacao, string? baseLegal, NaturezaDoItem natureza = NaturezaDoItem.Dado)
            : base(natureza, classificacao, baseLegal) => Codigo = codigo;

        [JsonInclude]
        public string Codigo { get; }
    }
}
