namespace Unifesspa.UniPlus.Contratos.UnitTests.SolutionRules;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Contratos.UnitTests.TestSupport.CodigosDeErro;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>As regras do gerador do catálogo de códigos de erro, sobre registros sintéticos.</summary>
public sealed class CatalogoDeCodigosDeErroTests
{
    private static readonly DomainErrorMapping NaoEncontrado =
        new(StatusCodes.Status404NotFound, "uniplus.modulo.coisa.nao_encontrada", "Coisa não encontrada");

    private static readonly DomainErrorMapping Conflito =
        new(StatusCodes.Status409Conflict, "uniplus.modulo.coisa.em_conflito", "Coisa em conflito");

    private const string JsonEsperadoDeUmCodigo =
        "{\n"
        + "  \"versaoDoFormato\": 1,\n"
        + "  \"codigos\": [\n"
        + "    {\n"
        + "      \"codigo\": \"uniplus.modulo.coisa.nao_encontrada\",\n"
        + "      \"status\": 404,\n"
        + "      \"titulo\": \"Coisa não encontrada\"\n"
        + "    }\n"
        + "  ]\n"
        + "}\n";

    [Fact(DisplayName = "A agregação ordena por código, qualquer que seja a ordem dos registros")]
    public void Agregar_OrdenaPorCodigoDeFormaEstavel()
    {
        RegistroDeTeste primeiro = new(NaoEncontrado, Conflito);
        RegistroDeTeste segundo = new(Conflito, NaoEncontrado);

        IReadOnlyList<EntradaDoCatalogoDeErros> aPartirDoPrimeiro = AgregadorDoCatalogoDeErros.Agregar([primeiro]);
        IReadOnlyList<EntradaDoCatalogoDeErros> aPartirDoSegundo = AgregadorDoCatalogoDeErros.Agregar([segundo]);

        aPartirDoPrimeiro.Select(static e => e.Codigo).Should().Equal(
            "uniplus.modulo.coisa.em_conflito", "uniplus.modulo.coisa.nao_encontrada");
        aPartirDoSegundo.Should().Equal(aPartirDoPrimeiro);
    }

    [Fact(DisplayName = "O mesmo código declarado idêntico em dois registros entra uma vez")]
    public void Agregar_CodigoRepetidoIdenticoEntraUmaVez()
    {
        IReadOnlyList<EntradaDoCatalogoDeErros> entradas =
            AgregadorDoCatalogoDeErros.Agregar([new RegistroDeTeste(NaoEncontrado), new RegistroDeTeste(NaoEncontrado)]);

        entradas.Should().ContainSingle().Which.Should().Be(
            new EntradaDoCatalogoDeErros("uniplus.modulo.coisa.nao_encontrada", 404, "Coisa não encontrada"));
    }

    [Fact(DisplayName = "Duas chaves de domínio com o mesmo código de fio idêntico entram uma vez")]
    public void Agregar_ChavesDeDominioDiferentesParaOMesmoCodigoDeFioEntramUmaVez()
    {
        RegistroDeTeste registro = new(NaoEncontrado, NaoEncontrado);

        AgregadorDoCatalogoDeErros.Agregar([registro]).Should().ContainSingle();
    }

    [Fact(DisplayName = "O mesmo código com status diferente falha e nomeia o código e os registros")]
    public void Agregar_CodigoComStatusDiferenteFalha()
    {
        DomainErrorMapping comOutroStatus = NaoEncontrado with { Status = StatusCodes.Status410Gone };

        Action agregar = () => AgregadorDoCatalogoDeErros.Agregar([new RegistroDeTeste(NaoEncontrado), new OutroRegistroDeTeste(comOutroStatus)]);

        agregar.Should().Throw<InvalidOperationException>()
            .WithMessage("*'uniplus.modulo.coisa.nao_encontrada'*404*410*")
            .WithMessage($"*{nameof(RegistroDeTeste)}*{nameof(OutroRegistroDeTeste)}*");
    }

    [Fact(DisplayName = "O mesmo código com título diferente falha")]
    public void Agregar_CodigoComTituloDiferenteFalha()
    {
        DomainErrorMapping comOutroTitulo = NaoEncontrado with { Title = "Outro título" };

        Action agregar = () => AgregadorDoCatalogoDeErros.Agregar([new RegistroDeTeste(NaoEncontrado, comOutroTitulo)]);

        agregar.Should().Throw<InvalidOperationException>().WithMessage("*'uniplus.modulo.coisa.nao_encontrada'*Outro título*");
    }

    [Fact(DisplayName = "A agregação acumula todos os conflitos numa só falha")]
    public void Agregar_AcumulaTodosOsConflitos()
    {
        RegistroDeTeste registro = new(
            NaoEncontrado, NaoEncontrado with { Title = "Outro" }, Conflito, Conflito with { Status = StatusCodes.Status400BadRequest });

        Action agregar = () => AgregadorDoCatalogoDeErros.Agregar([registro]);

        agregar.Should().Throw<InvalidOperationException>()
            .WithMessage("*'uniplus.modulo.coisa.nao_encontrada'*")
            .WithMessage("*'uniplus.modulo.coisa.em_conflito'*");
    }

    [Fact(DisplayName = "O documento é JSON indentado, com a versão do formato e os acentos legíveis")]
    public void Serializar_ProduzJsonIndentadoComVersaoEAcentos()
    {
        DocumentoDoCatalogoDeErros documento = DocumentoDoCatalogoDeErros.Gerar(AgregadorDoCatalogoDeErros.Agregar([new RegistroDeTeste(NaoEncontrado)]));

        string json = documento.Serializar();

        json.Should().Be(JsonEsperadoDeUmCodigo);
    }

    [Fact(DisplayName = "O documento lido do JSON é o que foi serializado")]
    public void Ler_DevolveOQueFoiSerializado()
    {
        DocumentoDoCatalogoDeErros documento = DocumentoDoCatalogoDeErros.Gerar(AgregadorDoCatalogoDeErros.Agregar([new RegistroDeTeste(NaoEncontrado, Conflito)]));

        DocumentoDoCatalogoDeErros lido = DocumentoDoCatalogoDeErros.Ler(documento.Serializar());

        lido.VersaoDoFormato.Should().Be(DocumentoDoCatalogoDeErros.VersaoAtualDoFormato);
        lido.Codigos.Should().Equal(documento.Codigos);
    }

    [Fact(DisplayName = "JSON que não é um documento do catálogo falha na leitura")]
    public void Ler_JsonInvalidoFalha()
    {
        Action ler = () => DocumentoDoCatalogoDeErros.Ler("{ isto não é json");

        ler.Should().Throw<JsonException>();
    }

    [Fact(DisplayName = "Arquivo igual ao gerado não tem divergência")]
    public void Comparar_ListasIguaisNaoDivergem()
    {
        IReadOnlyList<EntradaDoCatalogoDeErros> emitidas = AgregadorDoCatalogoDeErros.Agregar([new RegistroDeTeste(NaoEncontrado, Conflito)]);

        DivergenciasDoCatalogoDeErros.Comparar(emitidas, emitidas).Should().BeEmpty();
    }

    [Fact(DisplayName = "Código novo sem entrada no arquivo é apontado")]
    public void Comparar_CodigoNovoSemEntradaEApontado()
    {
        IReadOnlyList<EntradaDoCatalogoDeErros> publicadas = AgregadorDoCatalogoDeErros.Agregar([new RegistroDeTeste(NaoEncontrado)]);
        IReadOnlyList<EntradaDoCatalogoDeErros> emitidas = AgregadorDoCatalogoDeErros.Agregar([new RegistroDeTeste(NaoEncontrado, Conflito)]);

        DivergenciasDoCatalogoDeErros.Comparar(publicadas, emitidas).Should().ContainSingle()
            .Which.Should().Contain("'uniplus.modulo.coisa.em_conflito'").And.Contain("não tem entrada no arquivo");
    }

    [Fact(DisplayName = "Entrada sem código correspondente na API é apontada")]
    public void Comparar_EntradaSobrandoEApontada()
    {
        IReadOnlyList<EntradaDoCatalogoDeErros> publicadas = AgregadorDoCatalogoDeErros.Agregar([new RegistroDeTeste(NaoEncontrado, Conflito)]);
        IReadOnlyList<EntradaDoCatalogoDeErros> emitidas = AgregadorDoCatalogoDeErros.Agregar([new RegistroDeTeste(NaoEncontrado)]);

        DivergenciasDoCatalogoDeErros.Comparar(publicadas, emitidas).Should().ContainSingle()
            .Which.Should().Contain("'uniplus.modulo.coisa.em_conflito'").And.Contain("não corresponde a nenhum código emitido");
    }

    [Fact(DisplayName = "Status diferente entre o arquivo e a API é apontado")]
    public void Comparar_StatusDiferenteEApontado()
    {
        IReadOnlyList<EntradaDoCatalogoDeErros> publicadas = AgregadorDoCatalogoDeErros.Agregar([new RegistroDeTeste(NaoEncontrado)]);
        IReadOnlyList<EntradaDoCatalogoDeErros> emitidas =
            AgregadorDoCatalogoDeErros.Agregar([new RegistroDeTeste(NaoEncontrado with { Status = StatusCodes.Status410Gone })]);

        DivergenciasDoCatalogoDeErros.Comparar(publicadas, emitidas).Should().ContainSingle()
            .Which.Should().Contain("'uniplus.modulo.coisa.nao_encontrada'").And.Contain("404").And.Contain("410");
    }

    [Fact(DisplayName = "Título diferente entre o arquivo e a API é apontado")]
    public void Comparar_TituloDiferenteEApontado()
    {
        IReadOnlyList<EntradaDoCatalogoDeErros> publicadas = AgregadorDoCatalogoDeErros.Agregar([new RegistroDeTeste(NaoEncontrado)]);
        IReadOnlyList<EntradaDoCatalogoDeErros> emitidas =
            AgregadorDoCatalogoDeErros.Agregar([new RegistroDeTeste(NaoEncontrado with { Title = "Título novo" })]);

        DivergenciasDoCatalogoDeErros.Comparar(publicadas, emitidas).Should().ContainSingle()
            .Which.Should().Contain("Coisa não encontrada").And.Contain("Título novo");
    }

    [Fact(DisplayName = "Entrada repetida no arquivo é apontada")]
    public void Comparar_EntradaRepetidaNoArquivoEApontada()
    {
        EntradaDoCatalogoDeErros entrada = new("uniplus.modulo.coisa.nao_encontrada", 404, "Coisa não encontrada");

        IReadOnlyList<string> divergencias = DivergenciasDoCatalogoDeErros.Comparar([entrada, entrada], [entrada]);

        divergencias.Should().ContainSingle().Which.Should().Contain("aparece 2 vezes");
    }

    private class RegistroDeTeste(params DomainErrorMapping[] definicoes) : IDomainErrorRegistration
    {
        public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() =>
            definicoes.Select((definicao, indice) => new KeyValuePair<string, DomainErrorMapping>($"Chave.{indice}", definicao));
    }

    private sealed class OutroRegistroDeTeste(DomainErrorMapping definicao) : RegistroDeTeste(definicao);
}
