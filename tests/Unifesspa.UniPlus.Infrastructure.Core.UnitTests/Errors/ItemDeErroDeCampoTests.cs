namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Errors;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using AwesomeAssertions;

using Kernel.Results;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

public sealed class ItemDeErroDeCampoTests
{
    private static readonly DomainErrorMapping Mapeamento = new(
        StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento.tipo_invalido", "Tipo inválido");

    private static readonly FieldError Erro = new("/etapas/0/nome", new DomainError("Documento.TipoInvalido", "O valor deve ser um texto."));

    [Fact(DisplayName = "O item carrega o campo, o código publicado pelo catálogo e a mensagem do erro")]
    public void De_ErroMapeado_CarregaCampoCodigoPublicadoEMensagem()
    {
        ItemDeErroDeCampo item = ItemDeErroDeCampo.De(Erro, new MapeadorDeTeste(("Documento.TipoInvalido", Mapeamento)));

        item.Should().Be(new ItemDeErroDeCampo("/etapas/0/nome", "uniplus.selecao.documento.tipo_invalido", "O valor deve ser um texto."));
    }

    [Fact(DisplayName = "O código não mapeado cai no código de fallback do catálogo")]
    public void De_ErroNaoMapeado_UsaOCodigoDeFallback()
    {
        ItemDeErroDeCampo item = ItemDeErroDeCampo.De(Erro, new MapeadorDeTeste());

        item.Code.Should().Be("uniplus.erro_nao_mapeado");
    }

    [Fact(DisplayName = "O erro que não é de campo fica sem campo")]
    public void De_ErroSemCampo_DeixaOCampoNulo()
    {
        FieldError semCampo = new(null, new DomainError("Documento.TipoInvalido", "Mensagem."));

        ItemDeErroDeCampo.De(semCampo, new MapeadorDeTeste()).Field.Should().BeNull();
    }

    public static TheoryData<JsonSerializerOptions> PoliticasDeNomes => new()
    {
        new JsonSerializerOptions(),
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase },
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower },
    };

    [Theory(DisplayName = "No fio o item usa field, code e message em qualquer política de nomes")]
    [MemberData(nameof(PoliticasDeNomes))]
    public void Serializar_QualquerPoliticaDeNomes_UsaOsNomesFixosDoFio(JsonSerializerOptions opcoes) =>
        JsonSerializer.Serialize(new ItemDeErroDeCampo("/a", "c", "m"), opcoes).Should().Be("""{"field":"/a","code":"c","message":"m"}""");

    [Fact(DisplayName = "No fio o item sem campo sai sem a chave field")]
    public void Serializar_ItemSemCampo_OmiteAChaveField() =>
        JsonSerializer.Serialize(new ItemDeErroDeCampo(null, "c", "m")).Should().Be("""{"code":"c","message":"m"}""");

    [Fact(DisplayName = "Os identificadores dos itens de lista vão para o item, na ordem do caminho")]
    public void De_ErroComIdentificadores_CarregaOsIdentificadoresNaOrdem()
    {
        FieldError comIdentificadores = Erro with
        {
            Identificadores = [new IdentificadorDoItem("etapas", "e-1"), new IdentificadorDoItem("recursos", "r-2")],
        };

        ItemDeErroDeCampo item = ItemDeErroDeCampo.De(comIdentificadores, new MapeadorDeTeste(("Documento.TipoInvalido", Mapeamento)));

        item.Identificadores.Should().Equal(new IdentificadorDoItemNoFio("etapas", "e-1"), new IdentificadorDoItemNoFio("recursos", "r-2"));
    }

    [Fact(DisplayName = "O erro sem identificadores e o com lista vazia ficam sem identificadores")]
    public void De_ErroSemIdentificadores_DeixaOMembroNulo()
    {
        ItemDeErroDeCampo.De(Erro, new MapeadorDeTeste()).Identificadores.Should().BeNull();
        ItemDeErroDeCampo.De(Erro with { Identificadores = [] }, new MapeadorDeTeste()).Identificadores.Should().BeNull();
    }

    [Theory(DisplayName = "No fio os identificadores usam identificadores, lista e identificador em qualquer política de nomes")]
    [MemberData(nameof(PoliticasDeNomes))]
    public void Serializar_ItemComIdentificadores_UsaOsNomesFixosDoFio(JsonSerializerOptions opcoes) =>
        JsonSerializer.Serialize(
            new ItemDeErroDeCampo("/a", "c", "m", [new IdentificadorDoItemNoFio("fases", "INSCRICAO")]), opcoes)
            .Should().Be("""{"field":"/a","code":"c","message":"m","identificadores":[{"lista":"fases","identificador":"INSCRICAO"}]}""");

    [Fact(DisplayName = "O erro e o mapeador são obrigatórios")]
    public void De_ArgumentoNulo_Lanca()
    {
        Action semErro = () => ItemDeErroDeCampo.De(null!, new MapeadorDeTeste());
        Action semMapeador = () => ItemDeErroDeCampo.De(Erro, null!);

        semErro.Should().Throw<ArgumentNullException>();
        semMapeador.Should().Throw<ArgumentNullException>();
    }

    private sealed class MapeadorDeTeste(params (string Codigo, DomainErrorMapping Mapeamento)[] entradas) : IDomainErrorMapper
    {
        public bool TryGetMapping(string code, [MaybeNullWhen(false)] out DomainErrorMapping mapping)
        {
            mapping = entradas.Where(entrada => entrada.Codigo == code).Select(entrada => entrada.Mapeamento).FirstOrDefault();
            return mapping is not null;
        }

        public string GetProblemTypeUri(string code) => $"https://catalogo.exemplo/{code}";
    }
}
