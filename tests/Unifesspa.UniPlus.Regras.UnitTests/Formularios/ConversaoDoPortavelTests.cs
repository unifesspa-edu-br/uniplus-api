namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Leituras;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;

using static Unifesspa.UniPlus.Regras.UnitTests.Formularios.PortavelDeTeste;

/// <summary>
/// A conversão do formulário portável em definição: cada recusa do conteúdo malformado, com a causa e a mensagem.
/// </summary>
public sealed class ConversaoDoPortavelTests
{
    [Fact]
    public void ParaDefinicao_CondicoesInvalidas_PreservaCodigosNaOrdem()
    {
        JsonElement valor = JsonSerializer.SerializeToElement(true);
        FormularioPortavel formulario = Formulario(ItemComRegras(new RegrasDoCampoDto(
            null, Quando([[new("", "IGUAL", valor), new("DADO", "INVALIDO", valor)]]), [], null)));

        Result<DefinicaoFormulario> resultado = formulario.ParaDefinicao();

        resultado.Errors.Select(static erro => erro.Error.Code).Should().Equal(
            "CondicaoDnf.FatoObrigatorio", "CondicaoDnf.OperadorInvalido");
    }

    [Theory]
    [MemberData(nameof(Malformados))]
    public void ConteudoMalformado_ERecusadoComACausa(FormularioPortavel formulario, string codigo)
    {
        ArgumentNullException.ThrowIfNull(formulario);
        Result<DefinicaoFormulario> definicao = formulario.ParaDefinicao();

        definicao.IsFailure.Should().BeTrue();
        definicao.Error!.Code.Should().Be(codigo);
    }

    public static readonly TheoryData<FormularioPortavel, string> Malformados = new()
    {
        { Formulario(Item("RENDA", obrigatoriedade: "QUANDO")), FormularioPortavelErrorCodes.ObrigatoriedadeInvalida },
        { Formulario(Item("RENDA"), Item("RENDA")), FormularioPortavelErrorCodes.EstruturaInvalida },
        { Formulario(Item("CONTATO") with { Formato = "TELEGRAMA" }), FormularioPortavelErrorCodes.EstruturaInvalida },
        {
            Formulario(Item("ELEGIVEL")) with { Derivacoes = [new DerivacaoPortavel("COTISTA", true, [new RegraDerivacaoPortavel(null!, null)])] },
            FormularioPortavelErrorCodes.DerivacaoInvalida
        },
        {
            Formulario(Item("ELEGIVEL")) with { Agregados = [new AgregadoPortavel("ALGUEM", "FAMILIA", "SEM_RENDA", "SOMA")] },
            FormularioPortavelErrorCodes.OperacaoDeAgregadoInvalida
        },
    };

    [Theory(DisplayName = "A recusa do conteúdo malformado chega como texto em português, sem texto da exceção do framework")]
    [MemberData(nameof(MalformadosComDetalhe))]
    public void ConteudoMalformado_TemODetalheExato(FormularioPortavel formulario, string codigo, string detalhe)
    {
        ArgumentNullException.ThrowIfNull(formulario);
        Result<DefinicaoFormulario> definicao = formulario.ParaDefinicao();

        definicao.Error!.Code.Should().Be(codigo);
        definicao.Error.Message.Should().Be(detalhe);
    }

    [Fact(DisplayName = "Derivação sem regra é recusada como derivação inválida, com a mensagem da derivação")]
    public void ParaDefinicao_DerivacaoSemRegras_RecusaComoDerivacaoInvalida()
    {
        FormularioPortavel formulario = Formulario(Item("ELEGIVEL")) with { Derivacoes = [new DerivacaoPortavel("COTISTA", true, [])] };

        Result<DefinicaoFormulario> definicao = formulario.ParaDefinicao();

        definicao.Error!.Code.Should().Be(FormularioPortavelErrorCodes.DerivacaoInvalida);
        definicao.Error.Message.Should().Be("A derivação de 'COTISTA' precisa de ao menos uma regra.");
    }

    public static readonly TheoryData<FormularioPortavel, string, string> MalformadosComDetalhe = new()
    {
        {
            new FormularioPortavel([new PaginaPortavel("DADOS", null, [], []), new PaginaPortavel("DADOS", null, [], [])], [], [], []),
            FormularioPortavelErrorCodes.EstruturaInvalida,
            "O código 'DADOS' aparece mais de uma vez como página."
        },
        {
            Formulario(Item("RENDA"), Item("RENDA")),
            FormularioPortavelErrorCodes.EstruturaInvalida,
            "O código 'RENDA' aparece mais de uma vez como fato produzido ou grupo do formulário."
        },
        {
            Formulario(Item("CONTATO") with { Formato = "TELEGRAMA" }),
            FormularioPortavelErrorCodes.EstruturaInvalida,
            "O formato de texto 'TELEGRAMA' não é do vocabulário do catálogo."
        },
        {
            Formulario(Item("ELEGIVEL")) with { Agregados = [new AgregadoPortavel("ALGUEM", "FAMILIA", "SEM_RENDA", "EXISTE")] },
            FormularioPortavelErrorCodes.EstruturaInvalida,
            "O agregado 'ALGUEM' é sobre o grupo 'FAMILIA', que o formulário não tem."
        },
        {
            Formulario(Item(" ")),
            FormularioPortavelErrorCodes.EstruturaInvalida,
            "Um campo do formulário não tem o código do fato."
        },
        {
            Formulario(Item("RENDA")) with { Termos = [new TermoPortavel("", Nunca())] },
            FormularioPortavelErrorCodes.EstruturaInvalida,
            "Um termo do formulário não tem código."
        },
        {
            ComGrupo(minimo: -1, maximo: null),
            FormularioPortavelErrorCodes.EstruturaInvalida,
            "Os limites de ocorrências do grupo 'FAMILIA' (-1, sem máximo) são incoerentes."
        },
        {
            ComGrupo(minimo: 2, maximo: 1),
            FormularioPortavelErrorCodes.EstruturaInvalida,
            "Os limites de ocorrências do grupo 'FAMILIA' (2, 1) são incoerentes."
        },
        {
            ComGrupos(Grupo("FAMILIA", "PARENTE_A"), Grupo("FAMILIA", "PARENTE_B")),
            FormularioPortavelErrorCodes.EstruturaInvalida,
            "O código 'FAMILIA' aparece mais de uma vez como fato produzido ou grupo do formulário."
        },
        {
            new FormularioPortavel([new PaginaPortavel(" ", null, [], [])], [], [], []),
            FormularioPortavelErrorCodes.EstruturaInvalida,
            "Uma página do formulário não tem código."
        },
        {
            ComGrupos(Grupo("", "PARENTE_A")),
            FormularioPortavelErrorCodes.EstruturaInvalida,
            "Um grupo do formulário não tem código."
        },
        {
            Formulario(Item("RENDA")) with { Derivacoes = [new DerivacaoPortavel(" ", true, [new RegraDerivacaoPortavel([], null)])] },
            FormularioPortavelErrorCodes.EstruturaInvalida,
            "Uma derivação do formulário não tem o código do fato."
        },
        {
            Formulario(Item("RENDA")) with { Agregados = [new AgregadoPortavel("", "FAMILIA", "SEM_RENDA", "EXISTE")] },
            FormularioPortavelErrorCodes.EstruturaInvalida,
            "Um agregado do formulário não tem código."
        },
        {
            Formulario(ItemComRestricao(new RestricaoValorDto(TipoRestricaoValor.FaixaNumerica.Codigo(), 10, 1, null, null, null, null))),
            RestricaoValorErrorCodes.LimitesIncoerentes,
            "O mínimo (10) é maior que o máximo (1)."
        },
        {
            Formulario(ItemComRestricao(new RestricaoValorDto(TipoRestricaoValor.OpcoesDasRespostas.Codigo(), null, null, null, [], null, null))),
            RestricaoValorErrorCodes.FatosVazios,
            "As opções formadas pelas respostas precisam citar ao menos um fato."
        },
        {
            Formulario(ItemComRestricao(new RestricaoValorDto(
                TipoRestricaoValor.OpcoesPermitidas.Codigo(), null, null, [new OpcoesCondicionadasDto(null, [])], null, null, null))),
            RestricaoValorErrorCodes.OpcoesVazias,
            "Um grupo de opções precisa de valores não vazios."
        },
    };

    [Theory(DisplayName = "Com dois defeitos, todas as recusas voltam, cada uma no campo do seu elemento e na ordem de leitura")]
    [MemberData(nameof(ComDoisDefeitos))]
    public void ConteudoComDoisDefeitos_RecusaTodosNaOrdemDeLeitura(FormularioPortavel formulario, string[] esperadas)
    {
        ArgumentNullException.ThrowIfNull(formulario);
        Result<DefinicaoFormulario> definicao = formulario.ParaDefinicao();

        definicao.Errors.Select(static e => $"{e.Field} {e.Error.Code}").Should().Equal(esperadas);
    }

    public static readonly TheoryData<FormularioPortavel, string[]> ComDoisDefeitos = new()
    {
        {
            Formulario(Item("RENDA", obrigatoriedade: "QUANDO"), Item(" ")),
            [
                $"paginas[0].itens[0].regras.obrigatoriedade {FormularioPortavelErrorCodes.ObrigatoriedadeInvalida}",
                $"paginas[0].itens[1] {FormularioPortavelErrorCodes.EstruturaInvalida}",
            ]
        },
        {
            Formulario(Item("CONTATO", obrigatoriedade: "QUANDO") with { Formato = "TELEGRAMA" }),
            [
                $"paginas[0].itens[0].regras.obrigatoriedade {FormularioPortavelErrorCodes.ObrigatoriedadeInvalida}",
                $"paginas[0].itens[0] {FormularioPortavelErrorCodes.EstruturaInvalida}",
            ]
        },
        {
            new FormularioPortavel([new PaginaPortavel(" ", null, [Item(" ")], [])], [], [], []),
            [
                $"paginas[0].itens[0] {FormularioPortavelErrorCodes.EstruturaInvalida}",
                $"paginas[0].codigo {FormularioPortavelErrorCodes.EstruturaInvalida}",
            ]
        },
        {
            Formulario(Item("RENDA"), Item("RENDA")) with { Termos = [new TermoPortavel("", Nunca())] },
            [$"termos[0].codigo {FormularioPortavelErrorCodes.EstruturaInvalida}"]
        },
        {
            Formulario(Item("RENDA")) with { Agregados = [new AgregadoPortavel("", "FAMILIA", "SEM_RENDA", "SOMA")] },
            [
                $"agregados[0].operacao {FormularioPortavelErrorCodes.OperacaoDeAgregadoInvalida}",
                $"agregados[0] {FormularioPortavelErrorCodes.EstruturaInvalida}",
            ]
        },
    };

    [Fact(DisplayName = "Itens recusados em páginas diferentes voltam juntos, cada um no campo da sua posição")]
    public void ParaDefinicao_ItensRecusadosEmPaginasDiferentes_RecusaCadaUmNoSeuCampo()
    {
        FormularioPortavel formulario = new(
            [
                new PaginaPortavel("PAGINA_A", null, [Item("CAMPO_A"), Item(" ")], []),
                new PaginaPortavel("PAGINA_B", null, [Item("CAMPO_B")], []),
                new PaginaPortavel("PAGINA_C", null, [Item(" ")], []),
            ],
            [], [], []);

        formulario.ParaDefinicao().Errors.Select(static e => e.Field).Should().Equal("paginas[0].itens[1]", "paginas[2].itens[0]");
    }

    [Fact(DisplayName = "Duas regras recusadas no mesmo item voltam juntas, a obrigatoriedade antes da restrição")]
    public void ParaDefinicao_DuasRegrasRecusadasNoMesmoItem_RecusaAsDuas()
    {
        RestricaoValorDto restricao = new("TIPO_FICTICIO", null, null, null, null, null, null);
        ItemPortavel item = ItemComRegras(new RegrasDoCampoDto(null, new ObrigatoriedadeDto("QUANDO", null), [restricao], null));

        Result<DefinicaoFormulario> definicao = Formulario(Item("CAMPO_A"), Item("CAMPO_B"), item).ParaDefinicao();

        definicao.Errors.Select(static e => e.Field).Should().Equal(
            "paginas[0].itens[2].regras.obrigatoriedade", "paginas[0].itens[2].regras.restricoes[0]");
        definicao.Errors.Select(static e => e.Error.Code).Should().Equal(
            FormularioPortavelErrorCodes.ObrigatoriedadeInvalida, RestricaoValorErrorCodes.TipoDesconhecido);
    }

    [Fact(DisplayName = "Operador desconhecido na segunda condição da exibição da página aponta a condição")]
    public void ParaDefinicao_OperadorInvalidoNaExibicaoDaPagina_ApontaACondicao()
    {
        JsonElement valor = JsonSerializer.SerializeToElement(true);
        FormularioPortavel formulario = new(
            [new PaginaPortavel("PAGINA_A", [[new("DADO", "IGUAL", valor), new("DADO", "INVALIDO", valor)]], [], [])], [], [], []);

        formulario.ParaDefinicao().Errors.Select(static e => e.Field).Should().Equal("paginas[0].exibicao[0][1].operador");
    }

    [Fact(DisplayName = "Cláusula vazia na exibição da página aponta a cláusula, com a posição uma só vez")]
    public void ParaDefinicao_ClausulaVaziaNaExibicaoDaPagina_ApontaAClausula()
    {
        JsonElement valor = JsonSerializer.SerializeToElement(true);
        FormularioPortavel formulario = new(
            [new PaginaPortavel("PAGINA_A", [[new("DADO", "IGUAL", valor)], []], [], [])], [], [], []);

        formulario.ParaDefinicao().Errors.Select(static e => e.Field).Should().Equal("paginas[0].exibicao[1]");
    }

    [Fact(DisplayName = "Condição recusada da obrigatoriedade não confere a forma dela")]
    public void ParaDefinicao_CondicaoRecusadaDaObrigatoriedade_NaoConfereAForma()
    {
        JsonElement valor = JsonSerializer.SerializeToElement(true);
        FormularioPortavel formulario = Formulario(ItemComRegras(new RegrasDoCampoDto(
            null, new ObrigatoriedadeDto("NUNCA", [[new("", "IGUAL", valor)]]), [], null)));

        formulario.ParaDefinicao().Errors.Select(static e => e.Field).Should().Equal("paginas[0].itens[0].regras.obrigatoriedade.predicado[0][0].fato");
    }

    [Fact(DisplayName = "Página recusada vem antes do termo recusado")]
    public void ParaDefinicao_PaginaETermoRecusados_RecusaAPaginaAntes()
    {
        FormularioPortavel formulario = new([new PaginaPortavel(" ", null, [], [])], [new TermoPortavel("", Nunca())], [], []);

        formulario.ParaDefinicao().Errors.Select(static e => e.Field).Should().Equal("paginas[0].codigo", "termos[0].codigo");
    }

    [Fact(DisplayName = "A conferência do conjunto só aparece depois de corrigido o item recusado, e sem campo")]
    public void ParaDefinicao_CodigoRepetido_SoApareceSemRecusaAnterior()
    {
        Result<DefinicaoFormulario> recusado = Formulario(Item(" "), Item("RENDA"), Item("RENDA")).ParaDefinicao();
        Result<DefinicaoFormulario> corrigido = Formulario(Item("CONTATO"), Item("RENDA"), Item("RENDA")).ParaDefinicao();

        recusado.Errors.Select(static e => e.Field).Should().Equal("paginas[0].itens[0]");
        corrigido.Errors.Should().ContainSingle().Which.Field.Should().BeNull();
        corrigido.Error!.Message.Should().Contain("mais de uma vez");
    }

    [Fact(DisplayName = "Dois termos recusados pelo mesmo motivo geram duas recusas")]
    public void ParaDefinicao_DoisTermosRecusadosPeloMesmoMotivo_RecusaCadaUm()
    {
        FormularioPortavel formulario = Formulario(Item("RENDA")) with
        {
            Termos = [new TermoPortavel("", Nunca()), new TermoPortavel(" ", Nunca())],
        };

        formulario.ParaDefinicao().Errors.Select(static e => e.Field).Should().Equal("termos[0].codigo", "termos[1].codigo");
    }

    [Fact(DisplayName = "A recusa do agregado traz a operação e a conferência do próprio agregado")]
    public void ParaDefinicao_AgregadoComOperacaoECodigoRecusados_RecusaOsDois()
    {
        FormularioPortavel formulario = Formulario(Item("RENDA")) with { Agregados = [new AgregadoPortavel("", "FAMILIA", "SEM_RENDA", "SOMA")] };

        Result<DefinicaoFormulario> definicao = formulario.ParaDefinicao();

        definicao.Errors.Select(static e => e.Field).Should().Equal("agregados[0].operacao", "agregados[0]");
        definicao.Errors[0].Error.Message.Should().Be("A operação do agregado '' é EXISTE ou VALORES_PRESENTES.");
    }

    [Fact(DisplayName = "As mensagens e os campos reescritos não repetem o código enviado")]
    public void ParaDefinicao_CodigoFicticio_NaoApareceNasRecusasReescritas()
    {
        ExibicaoEObrigatoriedadeDto invalida = new(null, new ObrigatoriedadeDto("QUANDO", null));
        FormularioPortavel formulario = new(
            [new PaginaPortavel("PAGINA_A", null, [ItemComRegras(new RegrasDoCampoDto(null, new ObrigatoriedadeDto("QUANDO", null), [], null)) with { FatoCodigo = Codigo }],
                [new GrupoPortavel(Codigo, new OcorrenciasDoGrupoDto(0, null, false), invalida, [])])],
            [new TermoPortavel(Codigo, invalida)],
            [new DerivacaoPortavel(Codigo, true, [new RegraDerivacaoPortavel(null!, null)])],
            []);

        IReadOnlyList<FieldError> recusas = formulario.ParaDefinicao().Errors;

        recusas.Should().HaveCount(4);
        recusas.Select(static e => e.Error.Message).Should().NotContain(mensagem => mensagem.Contains(Codigo, StringComparison.Ordinal));
        recusas.Select(static e => e.Field).Should().NotContain(campo => campo!.Contains(Codigo, StringComparison.Ordinal));
    }

    private const string Codigo = "CODIGO_FICTICIO_DE_TESTE";

    [Fact(DisplayName = "O formato fora do catálogo no item NOME não é recusado, porque o nome não tem formato")]
    public void FormatoForaDoCatalogo_NoNome_EIgnorado()
    {
        FormularioPortavel formulario = Formulario(Item(ConjuntoBasicoDaInscricao.Nome) with { Formato = "TELEGRAMA" });

        formulario.ParaDefinicao().IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Item sem o bloco de regras é recusado como obrigatoriedade inválida")]
    public void ParaDefinicao_ItemSemRegras_RecusaComoObrigatoriedadeInvalida()
    {
        FormularioPortavel formulario = Formulario(Item("RENDA") with { Regras = null! });

        Result<DefinicaoFormulario> definicao = formulario.ParaDefinicao();

        definicao.Error!.Code.Should().Be(FormularioPortavelErrorCodes.ObrigatoriedadeInvalida);
        definicao.Error.Message.Should().Be("A obrigatoriedade é SEMPRE ou NUNCA sem predicado, ou QUANDO com ele.");
        definicao.Errors.Should().ContainSingle().Which.Field.Should().Be("paginas[0].itens[0].regras.obrigatoriedade");
    }
}
