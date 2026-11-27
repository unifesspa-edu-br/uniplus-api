namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport.ConteudosDoModeloDeTeste;
using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport.DeclaracoesDeModeloDeTeste;

/// <summary>
/// A forma do conteúdo do modelo, parte a parte, e o acúmulo das recusas na ordem das regras. As regras
/// de forma em si são as de <c>Unifesspa.UniPlus.Regras</c>; aqui vale que cada uma é aplicada à parte
/// certa do conteúdo e que a recusa aponta o campo dentro dele.
/// </summary>
public sealed class ConferenciaDoConteudoDoModeloTests
{
    private static readonly Dictionary<string, IReadOnlyCollection<string>> SemDerivacoes = new(StringComparer.Ordinal);

    private static readonly PaginaDoModelo Revisao = new("REVISAO", 1, TipoPaginaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão", null, null, null);

    private static ConteudoDoModelo Conteudo(params ItemDoModelo[] itens) => new("Habilitação", [Pagina(), Revisao], itens, [], [], []);

    private static List<FieldError> Conferir(ConteudoDoModelo conteudo) => Conferir(conteudo, DeOutraFaseObrigatoria);

    private static List<FieldError> Conferir(ConteudoDoModelo conteudo, DeclaracaoDoFormulario? declaracao) =>
        ConferenciaDoConteudoDoModelo.Conferir(declaracao, conteudo, SemDerivacoes, conteudoPorInteiro: true);

    private static string[] Campos(IEnumerable<FieldError> erros) => [.. erros.Select(static e => e.Field ?? "(sem campo)")];

    private static string[] Codigos(IEnumerable<FieldError> erros) => [.. erros.Select(static e => e.Error.Code)];

    [Fact(DisplayName = "As recusas do conteúdo: sem campo segue sem campo, a raiz relativa é o conteúdo e o campo é filho dele")]
    public void NoConteudo_PrefixaSoOQueTemCaminho()
    {
        DomainError erro = new("Modelo.Recusado", "O conteúdo foi recusado.");

        IEnumerable<FieldError> recusas = ConferenciaDoConteudoDoModelo.NoConteudo(
            [new FieldError(null, erro), new FieldError(string.Empty, erro), new FieldError("itens[0]", erro)]);

        recusas.Select(static recusa => recusa.Field).Should().Equal(null, "conteudo", "conteudo.itens[0]");
    }

    [Fact(DisplayName = "O conteúdo bem formado não tem recusa")]
    public void ConteudoBemFormado_NaoRecusa() =>
        Conferir(Conteudo(Item("CERTIFICADO"))).Should().BeEmpty();

    [Fact(DisplayName = "O conteúdo e as derivações são obrigatórios")]
    public void ConteudoOuDerivacoesNulos_Lanca()
    {
        Action semConteudo = () => ConferenciaDoConteudoDoModelo.Conferir(DeOutraFaseObrigatoria, null!, SemDerivacoes, conteudoPorInteiro: true);
        Action semDerivacoes = () => ConferenciaDoConteudoDoModelo.Conferir(DeOutraFaseObrigatoria, Conteudo(), null!, conteudoPorInteiro: true);

        semConteudo.Should().Throw<ArgumentNullException>();
        semDerivacoes.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "O texto que o banco não grava é recusado no campo dele, com o código de texto não gravável")]
    public void TextoNaoGravavel_RecusaNoCampo()
    {
        List<FieldError> erros = Conferir(Conteudo(Item("CERTIFICADO")) with { Titulo = "Habilitação\0" });

        erros.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "conteudo.titulo",
            Error = new { Code = ModeloFormularioErrorCodes.TextoNaoGravavel },
        });
    }

    [Theory(DisplayName = "O título cabe no limite do cabeçalho: no limite passa, acima dele é recusado")]
    [InlineData(FormaDoCabecalho.TituloMaxLength, false)]
    [InlineData(FormaDoCabecalho.TituloMaxLength + 1, true)]
    public void Titulo_ConferidoContraOLimite(int tamanho, bool recusado)
    {
        List<FieldError> erros = Conferir(Conteudo(Item("CERTIFICADO")) with { Titulo = new string('T', tamanho) });

        Codigos(erros).Should().Equal(recusado ? [EstruturaFormularioErrorCodes.TituloTamanho] : []);
        Campos(erros).Should().Equal(recusado ? ["conteudo.titulo"] : []);
    }

    [Fact(DisplayName = "A forma de cada pagina é recusada no índice dela")]
    public void Pagina_RecusaNoIndice()
    {
        ConteudoDoModelo conteudo = Conteudo(Item("CERTIFICADO")) with { Paginas = [Pagina(), Revisao with { Titulo = " " }] };

        List<FieldError> erros = Conferir(conteudo);

        Campos(erros).Should().Equal("conteudo.paginas[1].apresentacao.titulo");
        Codigos(erros).Should().Equal(EstruturaFormularioErrorCodes.PaginaTituloInvalido);
    }

    [Fact(DisplayName = "Código de pagina repetido é recusado no índice do conteúdo")]
    public void Paginas_CodigoRepetido_RecusaNoIndiceDoConteudo()
    {
        ConteudoDoModelo conteudo = Conteudo() with { Paginas = [Pagina(), Pagina() with { Ordem = 1 }, Revisao with { Ordem = 2 }] };

        List<FieldError> erros = Conferir(conteudo);

        erros.Select(static e => (e.Field, e.Error.Code)).Should()
            .Contain(("conteudo.paginas[1].codigo", EstruturaFormularioErrorCodes.PaginaCodigoDuplicado));
    }

    [Fact(DisplayName = "A forma de cada item é recusada no índice dele")]
    public void Item_RecusaNoIndice()
    {
        List<FieldError> erros = Conferir(Conteudo(Item("CERTIFICADO"), Item("LAUDO", 1).ComRotulo(string.Empty)));

        Campos(erros).Should().Equal("conteudo.itens[1].apresentacao.rotulo");
        Codigos(erros).Should().Equal(ItemFormularioErrorCodes.RotuloObrigatorio);
    }

    [Fact(DisplayName = "No teto de itens a forma dos itens é conferida")]
    public void TetoDeItens_NoTeto_ConfereAFormaDosItens()
    {
        List<FieldError> erros = Conferir(Conteudo(ItensComOPrimeiroSemRotulo(FormaDoItem.MaximoDeItens)));

        Codigos(erros).Should().Equal(ItemFormularioErrorCodes.RotuloObrigatorio);
    }

    [Fact(DisplayName = "Acima do teto de itens só o excesso é recusado, e a forma dos itens não é lida")]
    public void TetoDeItens_AcimaDoTeto_RecusaSoOExcesso()
    {
        List<FieldError> erros = Conferir(Conteudo(ItensComOPrimeiroSemRotulo(FormaDoItem.MaximoDeItens + 1)));

        Codigos(erros).Should().Equal(ItemFormularioErrorCodes.ItensEmExcesso);
    }

    [Fact(DisplayName = "Com parte do conteúdo fora, o teto de itens não é conferido pelo domínio; a forma dos itens continua")]
    public void TetoDeItens_ConteudoSemParte_NaoConfereOExcesso()
    {
        ConteudoDoModelo conteudo = Conteudo(ItensComOPrimeiroSemRotulo(FormaDoItem.MaximoDeItens + 1));

        List<FieldError> erros = ConferenciaDoConteudoDoModelo.Conferir(DeOutraFaseObrigatoria, conteudo, SemDerivacoes, conteudoPorInteiro: false);

        Codigos(erros).Should().Equal(ItemFormularioErrorCodes.RotuloObrigatorio);
    }

    [Fact(DisplayName = "O item com impedimento fora do modelo de inscrição é recusado, e no de inscrição passa")]
    public void Impedimento_SoNaInscricao()
    {
        ConteudoDoModelo conteudo = Conteudo(Item("CERTIFICADO").ComImpedimento(new Impedimento(Quando("CERTIFICADO"), "Já possui o certificado.")));

        Campos(Conferir(conteudo)).Should().Equal("conteudo.itens[0].regras.impedimento");
        Conferir(conteudo, DaInscricao).Should().BeEmpty();
    }

    [Fact(DisplayName = "A forma do grupo, o candidato como membro e a forma de cada campo saem nessa ordem, cada um no caminho do grupo")]
    public void Grupo_RecusaNaOrdemDaFormaDoCandidatoEDosCampos()
    {
        GrupoDoModelo grupo = Grupo("COMPOSICAO_FAMILIAR", 1, null, (Item("RENDA") with { PaginaCodigo = null }).ComRotulo(string.Empty))
            .ComOcorrencias(3, 2, incluiCandidato: true);

        List<FieldError> erros = Conferir(Conteudo(Item("CERTIFICADO")) with { Grupos = [grupo] });

        Campos(erros).Should().Equal("conteudo.grupos[0].ocorrencias.maximo", "conteudo.grupos[0].subitens", "conteudo.grupos[0].subitens[0].apresentacao.rotulo");
    }

    [Fact(DisplayName = "O grupo que não inclui o candidato não pede o parentesco")]
    public void Grupo_SemCandidatoComoMembro_NaoPedeParentesco()
    {
        GrupoDoModelo grupo = Grupo("COMPOSICAO_FAMILIAR", 1, null, Item("RENDA") with { PaginaCodigo = null });

        Conferir(Conteudo(Item("CERTIFICADO")) with { Grupos = [grupo] }).Should().BeEmpty();
    }

    [Fact(DisplayName = "O código do termo é obrigatório e único: a recusa aponta o índice do termo")]
    public void Termos_RecusaFormaEUnicidadeNoIndice()
    {
        ConteudoDoModelo conteudo = Conteudo(Item("CERTIFICADO")) with { Termos = [Termo(" ", 0), Termo("LGPD", 1), Termo("LGPD", 2)] };

        List<FieldError> erros = Conferir(conteudo);

        Campos(erros).Should().Equal("conteudo.termos[0].codigo", "conteudo.termos[2].codigo");
        Codigos(erros).Should().Equal(TermoFormularioErrorCodes.CodigoObrigatorio, TermoFormularioErrorCodes.CodigoDuplicado);
    }

    [Fact(DisplayName = "Com parte do conteúdo fora, a unicidade dos termos não é conferida; a forma de cada termo continua")]
    public void Termos_ConteudoSemParte_NaoConfereUnicidade()
    {
        ConteudoDoModelo conteudo = Conteudo(Item("CERTIFICADO")) with { Termos = [Termo(" ", 0), Termo("LGPD", 1), Termo("LGPD", 1)] };

        List<FieldError> erros = ConferenciaDoConteudoDoModelo.Conferir(DeOutraFaseObrigatoria, conteudo, SemDerivacoes, conteudoPorInteiro: false);

        Codigos(erros).Should().Equal(TermoFormularioErrorCodes.CodigoObrigatorio);
    }

    [Fact(DisplayName = "Com o conteúdo por inteiro, o código e a ordem repetidos dos termos são recusados uma vez cada")]
    public void Termos_ConteudoPorInteiro_ConfereUnicidade()
    {
        ConteudoDoModelo conteudo = Conteudo(Item("CERTIFICADO")) with { Termos = [Termo("LGPD", 1), Termo("LGPD", 1)] };

        Codigos(Conferir(conteudo)).Should().Equal(TermoFormularioErrorCodes.CodigoDuplicado, TermoFormularioErrorCodes.OrdemDuplicada);
    }

    [Fact(DisplayName = "O modelo de inscrição não tem pressupostos")]
    public void Pressupostos_NaInscricao_Recusa()
    {
        List<FieldError> erros = Conferir(Conteudo(Item("CERTIFICADO")) with { Pressupostos = ["SEXO", string.Empty] }, DaInscricao);

        Campos(erros).Should().Equal("conteudo.pressupostos");
        Codigos(erros).Should().Equal(ModeloFormularioErrorCodes.PressupostoNaInscricao);
    }

    [Fact(DisplayName = "O pressuposto em branco é recusado no índice dele, sem outra recusa para o mesmo pressuposto")]
    public void Pressuposto_EmBranco_RecusaSo()
    {
        List<FieldError> erros = Conferir(Conteudo(Item("CERTIFICADO")) with { Pressupostos = [string.Empty] });

        Campos(erros).Should().Equal("conteudo.pressupostos[0]");
        Codigos(erros).Should().Equal(ModeloFormularioErrorCodes.PressupostoEmBranco);
    }

    [Fact(DisplayName = "O pressuposto repetido e o que também é item acumulam, por posição e na ordem das duas regras")]
    public void Pressupostos_RepetidoETambemItem_AcumulamPorPosicao()
    {
        List<FieldError> erros = Conferir(Conteudo(Item("CONCLUSAO_REGULAR")) with { Pressupostos = ["CONCLUSAO_REGULAR", "CONCLUSAO_REGULAR"] });

        Campos(erros).Should().Equal("conteudo.pressupostos[0]", "conteudo.pressupostos[1]", "conteudo.pressupostos[1]");
        Codigos(erros).Should().Equal(
            ModeloFormularioErrorCodes.PressupostoTambemItem, ModeloFormularioErrorCodes.PressupostoRepetido, ModeloFormularioErrorCodes.PressupostoTambemItem);
        erros.Select(static e => e.Error.Message).Should().Equal(
            "O fato é item do modelo e não pode ser também pressuposto: ou o formulário o coleta, ou o recebe da inscrição.",
            "O fato aparece mais de uma vez entre os pressupostos.",
            "O fato é item do modelo e não pode ser também pressuposto: ou o formulário o coleta, ou o recebe da inscrição.");
    }

    [Fact(DisplayName = "A estrutura da declaração só é conferida sobre partes bem formadas")]
    public void Estrutura_NaoEConferidaComParteMalFormada()
    {
        ConteudoDoModelo foraDeSecao = Conteudo(Item("CERTIFICADO") with { PaginaCodigo = null });

        Codigos(Conferir(foraDeSecao)).Should().Equal(EstruturaFormularioErrorCodes.ItemForaDeSecao);
        Codigos(Conferir(foraDeSecao with { Titulo = new string('T', FormaDoCabecalho.TituloMaxLength + 1) }))
            .Should().Equal(EstruturaFormularioErrorCodes.TituloTamanho);
    }

    [Fact(DisplayName = "Com parte do conteúdo fora, a estrutura e o grafo não são conferidos, e as demais regras são")]
    public void ConteudoSemParte_NaoConfereEstruturaNemGrafoEConfereAsDemaisRegras()
    {
        ConteudoDoModelo foraDeSecao = Conteudo(Item("CERTIFICADO") with { PaginaCodigo = null });
        ConteudoDoModelo citaFatoQueNaoColeta = Conteudo(Item("CERTIFICADO", 0, Quando("OUTRO_FATO")));
        ConteudoDoModelo pressupostoRepetido = Conteudo(Item("CERTIFICADO")) with { Pressupostos = ["SEXO", "SEXO"] };

        ConferenciaDoConteudoDoModelo.Conferir(DeOutraFaseObrigatoria, foraDeSecao, SemDerivacoes, conteudoPorInteiro: false).Should().BeEmpty();
        ConferenciaDoConteudoDoModelo.Conferir(DeOutraFaseObrigatoria, citaFatoQueNaoColeta, SemDerivacoes, conteudoPorInteiro: false).Should().BeEmpty();
        Codigos(ConferenciaDoConteudoDoModelo.Conferir(DeOutraFaseObrigatoria, pressupostoRepetido, SemDerivacoes, conteudoPorInteiro: false))
            .Should().Equal(ModeloFormularioErrorCodes.PressupostoRepetido);
    }

    [Fact(DisplayName = "O conteúdo inteiro tem a estrutura e o grafo conferidos como antes")]
    public void ConteudoPorInteiro_ConfereEstruturaEGrafo()
    {
        ConteudoDoModelo foraDeSecao = Conteudo(Item("CERTIFICADO") with { PaginaCodigo = null });
        ConteudoDoModelo citaFatoQueNaoColeta = Conteudo(Item("CERTIFICADO", 0, Quando("OUTRO_FATO")));

        Codigos(Conferir(foraDeSecao)).Should().Equal(EstruturaFormularioErrorCodes.ItemForaDeSecao);
        Codigos(Conferir(citaFatoQueNaoColeta)).Should().Equal(GrafoFormularioErrorCodes.CitaFatoNaoConhecido);
    }

    [Fact(DisplayName = "Com a declaração recusada no cadastro, a estrutura que depende dela não é conferida")]
    public void Estrutura_NaoEConferidaSemDeclaracao() =>
        Conferir(Conteudo(Item("CERTIFICADO") with { PaginaCodigo = null }), declaracao: null).Should().BeEmpty();

    [Fact(DisplayName = "A estrutura das paginas recusada impede a conferência da posição dos itens")]
    public void Estrutura_PaginaRecusada_ImpedeAConferenciaDosItens()
    {
        PaginaDoModelo repetida = Pagina() with { Ordem = 2 };
        ConteudoDoModelo conteudo = Conteudo(Item("CERTIFICADO") with { PaginaCodigo = null }) with { Paginas = [Pagina(), repetida, Revisao with { Ordem = 3 }] };

        Codigos(Conferir(conteudo)).Should().Equal(EstruturaFormularioErrorCodes.PaginaCodigoDuplicado);
    }

    [Fact(DisplayName = "As recusas das famílias acumulam juntas, cada uma no seu ponteiro, na ordem das regras do conteúdo")]
    public void Recusas_AcumulamNaOrdemDasRegras()
    {
        ConteudoDoModelo conteudo = Conteudo(Item("CERTIFICADO").ComRotulo(string.Empty), ComImpedimento(Item("LAUDO", 1))) with
        {
            Titulo = new string('T', FormaDoCabecalho.TituloMaxLength + 1),
            Paginas = [Pagina(), Revisao with { Titulo = " " }],
            Grupos = [Grupo("COMPOSICAO_FAMILIAR", 2, null, Item("RENDA") with { PaginaCodigo = null }).ComOcorrencias(3, 2)],
            Termos = [Termo(" ", 0)],
            Pressupostos = [string.Empty],
        };

        Campos(Conferir(conteudo)).Should().Equal(
            "conteudo.titulo", "conteudo.paginas[1].apresentacao.titulo", "conteudo.itens[0].apresentacao.rotulo", "conteudo.itens[1].regras.impedimento",
            "conteudo.grupos[0].ocorrencias.maximo", "conteudo.termos[0].codigo", "conteudo.pressupostos[0]");
    }

    private static ItemDoModelo[] ItensComOPrimeiroSemRotulo(int quantidade) =>
        [.. Enumerable.Range(0, quantidade).Select(static i => Item($"FATO_{i}", i).ComRotulo(i == 0 ? string.Empty : $"Fato {i}"))];

    private static ItemDoModelo ComImpedimento(ItemDoModelo item) =>
        item.ComImpedimento(new Impedimento(Quando(item.FatoCodigo), "Já possui o certificado."));
}
