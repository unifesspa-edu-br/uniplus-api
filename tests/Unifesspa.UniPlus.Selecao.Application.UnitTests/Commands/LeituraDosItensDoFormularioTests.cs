namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.ApoioDaColetaDeFatos;

/// <summary>
/// A leitura dos itens pela forma: o item recusado passa por toda regra de forma que não depende do que foi recusado, cada
/// recusa sai uma vez, no caminho do item, e o item aceito não emite nada.
/// </summary>
public sealed class LeituraDosItensDoFormularioTests
{
    private static ContextoDoCatalogo Contexto() => ContextoDoCatalogo.De(ProcessoEmRascunho(), VocabularioSeed());

    private static ItensLidos Ler(params FatoColetadoInput[] itens) => LeituraDosItensDoFormulario.Ler(itens, Contexto());

    private static IEnumerable<string?> Campos(ItensLidos lidos) => lidos.Erros.Select(static e => e.Field);

    [Fact(DisplayName = "Tipo de campo recusado e ajuda acima do limite saem juntos, cada um uma vez, no item")]
    public void Ler_TipoRecusadoEAjudaLonga_AcusaAsDuas()
    {
        ItensLidos lidos = Ler(EntradaDoFatoColetadoDeTeste.Coletando("BAIXA_RENDA").DoTipo("TIPO_QUE_NAO_EXISTE").ComAjuda(new string('a', 1001)).Construir());

        Campos(lidos).Should().BeEquivalentTo(["itens[0].apresentacao.tipoDeCampo", "itens[0].apresentacao.ajuda"]);
    }

    private static string AjudaLonga(string marca) => string.Concat(Enumerable.Repeat(marca, 1001 / marca.Length + 1));

    [Fact(DisplayName = "Ajuda acima do limite e obrigatoriedade inválida saem juntas, cada uma uma vez")]
    public void Ler_AjudaLongaEObrigatoriedadeInvalida_AcusaAsDuas()
    {
        ItensLidos lidos = Ler(EntradaDoFatoColetadoDeTeste.Coletando("BAIXA_RENDA").ComAjuda(AjudaLonga("a")).ComObrigatoriedade("TALVEZ").Construir());

        Campos(lidos).Should().BeEquivalentTo(["itens[0].regras.obrigatoriedade", "itens[0].apresentacao.ajuda"]);
    }

    [Fact(DisplayName = "Obrigatoriedade inválida e faixa numérica em campo de texto saem juntas")]
    public void Ler_ObrigatoriedadeInvalidaEFaixaEmCampoDeTexto_AcusaAsDuas()
    {
        ItensLidos lidos = Ler(EntradaDoFatoColetadoDeTeste.Coletando("NOME_SOCIAL").DoTipo("TEXTO").ComObrigatoriedade("TALVEZ")
            .ComRestricoes(new RestricaoValorInput("FAIXA_NUMERICA", 1, 5)).Construir());

        lidos.Erros.Should().HaveCount(2);
        lidos.Erros.Should().Contain(static e => e.Error.Code == ItemFormularioErrorCodes.RestricaoIncoerente);
        Campos(lidos).Should().Contain("itens[0].regras.obrigatoriedade");
    }

    [Fact(DisplayName = "Tipo inexistente, ajuda longa e faixa numérica: saem o tipo e a ajuda, sem a restrição fora do tipo")]
    public void Ler_TipoInexistenteAjudaLongaEFaixa_NaoConfereARestricaoPeloTipo()
    {
        ItensLidos lidos = Ler(EntradaDoFatoColetadoDeTeste.Coletando("NOME_SOCIAL").DoTipo("TIPO_QUE_NAO_EXISTE").ComAjuda(AjudaLonga("a"))
            .ComRestricoes(new RestricaoValorInput("FAIXA_NUMERICA", 1, 5)).Construir());

        Campos(lidos).Should().BeEquivalentTo(["itens[0].apresentacao.tipoDeCampo", "itens[0].apresentacao.ajuda"]);
        lidos.Erros.Should().NotContain(static e => e.Error.Code == ItemFormularioErrorCodes.RestricaoIncoerente);
    }

    [Fact(DisplayName = "Campo de grupo com obrigatoriedade inválida e ajuda longa aponta o grupo e o campo")]
    public void LerGrupos_CampoComObrigatoriedadeInvalidaEAjudaLonga_ApontaOCampo()
    {
        GrupoColetadoInput grupo = Composicao() with
        {
            Subitens = [EntradaDoFatoColetadoDeTeste.Coletando("PARENTESCO").DoTipo("ESCOLHA_UNICA").ComAjuda(AjudaLonga("a")).ComObrigatoriedade("TALVEZ").Construir()],
        };

        GruposLidos lidos = LeituraDosItensDoFormulario.LerGrupos([grupo], Contexto());

        lidos.Erros.Select(static e => e.Field).Should().Contain(["grupos[0].subitens[0].regras.obrigatoriedade", "grupos[0].subitens[0].apresentacao.ajuda"]);
    }

    [Fact(DisplayName = "A mensagem da ajuda longa não repete o texto enviado")]
    public void Ler_AjudaLonga_MensagemNaoRepeteOTexto()
    {
        const string Marca = "TEXTO-MARCANTE-FICTICIO-";
        ItensLidos lidos = Ler(EntradaDoFatoColetadoDeTeste.Coletando("BAIXA_RENDA").ComRotulo("").ComAjuda(AjudaLonga(Marca)).Construir());

        lidos.Erros.Should().Contain(static e => e.Error.Code == ItemFormularioErrorCodes.AjudaTamanho);
        lidos.Erros.Should().OnlyContain(static e => !e.Error.Message.Contains("TEXTO-MARCANTE", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Obrigatoriedade inválida e exibição que cita o próprio dado saem juntas")]
    public void Ler_ObrigatoriedadeInvalidaEExibicaoAutorreferente_AcusaAsDuas()
    {
        ItensLidos lidos = Ler(EntradaDoFatoColetadoDeTeste.Coletando("BAIXA_RENDA").ComObrigatoriedade("TALVEZ")
            .ExibidoQuando([[Condicao("BAIXA_RENDA", "IGUAL", true)]]).Construir());

        Campos(lidos).Should().BeEquivalentTo(["itens[0].regras.obrigatoriedade", "itens[0].regras.exibicao"]);
    }

    [Fact(DisplayName = "Rótulo vazio e restrição que não cabe no tipo do campo saem juntos")]
    public void Ler_RotuloVazioERestricaoForaDoTipo_AcusaAsDuas()
    {
        ItensLidos lidos = Ler(EntradaDoFatoColetadoDeTeste.Coletando("BAIXA_RENDA").ComRotulo("")
            .ComRestricoes(new RestricaoValorInput("TAMANHO_TEXTO", 1, 5)).Construir());

        lidos.Erros.Should().HaveCount(2);
        lidos.Erros.Should().Contain(static e => e.Error.Code == ItemFormularioErrorCodes.RestricaoIncoerente);
        lidos.Itens.Single().Configuracao.Should().BeNull();
    }

    [Fact(DisplayName = "Tipo recusado vem antes da forma básica do item")]
    public void Ler_FormaBasicaETipoRecusados_TipoPrimeiro()
    {
        ItensLidos lidos = Ler(EntradaDoFatoColetadoDeTeste.Coletando("BAIXA_RENDA").ComRotulo("").DoTipo("TIPO_QUE_NAO_EXISTE").Construir());

        Campos(lidos).First().Should().Be("itens[0].apresentacao.tipoDeCampo");
        lidos.Erros.Should().HaveCountGreaterThan(1);
    }

    [Fact(DisplayName = "Item recusado com exibição malformada recusa a exibição uma só vez")]
    public void Ler_ItemRecusadoComExibicaoMalformada_AcusaUmaVez()
    {
        ItensLidos lidos = Ler(EntradaDoFatoColetadoDeTeste.Coletando("BAIXA_RENDA").ComRotulo("")
            .ExibidoQuando([[Condicao("COR_RACA", "OPERADOR_QUE_NAO_EXISTE", "PRETA")]]).Construir());

        Campos(lidos).Count(static c => c!.StartsWith("itens[0].regras.exibicao", StringComparison.Ordinal)).Should().Be(1);
    }

    [Fact(DisplayName = "Item aceito com exibição malformada não emite nada na leitura; a recusa sai na resolução")]
    public void Ler_ItemAceitoComExibicaoMalformada_NaoEmiteNaLeitura()
    {
        ItensLidos lidos = Ler(EntradaDoFatoColetadoDeTeste.Coletando("BAIXA_RENDA")
            .ExibidoQuando([[Condicao("COR_RACA", "OPERADOR_QUE_NAO_EXISTE", "PRETA")]]).Construir());

        lidos.Erros.Should().BeEmpty();
        lidos.Itens.Single().Configuracao.Should().NotBeNull();
        (_, List<FieldError> daResolucao) = EscritaDosItens.Resolver(lidos, Contexto());
        daResolucao.Select(static e => e.Field).Should().ContainSingle().Which.Should().StartWith("itens[0].regras.exibicao");
    }

    [Fact(DisplayName = "Campo de grupo recusado aponta o grupo e o campo, e o grupo acusa só a falta do parentesco")]
    public void LerGrupos_CampoRecusado_ApontaOCaminhoDoCampo()
    {
        GrupoColetadoInput grupo = Composicao() with
        {
            Subitens = [EntradaDoFatoColetadoDeTeste.Coletando("PARENTESCO").DoTipo("TIPO_QUE_NAO_EXISTE").Construir()],
        };

        GruposLidos lidos = LeituraDosItensDoFormulario.LerGrupos([grupo], Contexto());

        lidos.Erros.Select(static e => e.Field).Should().Contain("grupos[0].subitens[0].apresentacao.tipoDeCampo");
        lidos.Erros.Where(static e => e.Field is not null && e.Field.StartsWith("grupos[0].subitens", StringComparison.Ordinal))
            .Should().ContainSingle();
        lidos.Grupos.Single().Campos.Itens.Single().Configuracao.Should().BeNull();
    }

    [Fact(DisplayName = "Duas restrições de opções permitidas no item recusado não lançam e as duas entram nas condições lidas")]
    public void Ler_DuasOpcoesPermitidasNoItemRecusado_GuardaAsCondicoesDasDuas()
    {
        RestricaoValorInput opcoes = new("OPCOES_PERMITIDAS", Entradas: [new OpcoesCondicionadasInput([[Condicao("BAIXA_RENDA", "IGUAL", true)]], ["PRETA"])]);

        ItensLidos lidos = Ler(EntradaDoFatoColetadoDeTeste.Coletando("COR_RACA").ComRotulo("").DoTipo("ESCOLHA_UNICA").ComRestricoes(opcoes, opcoes).Construir());

        lidos.CondicoesLidas.Should().HaveCount(4);
    }

    [Fact(DisplayName = "Campo de texto recusado com dado de texto conhecido não recusa o formato")]
    public void Ler_TextoRecusadoComDadoDeTexto_NaoRecusaOFormato()
    {
        ItensLidos lidos = Ler(EntradaDoFatoColetadoDeTeste.Coletando("NOME_SOCIAL").ComRotulo("").DoTipo("TEXTO").Construir());

        lidos.Erros.Should().NotBeEmpty();
        lidos.Erros.Should().NotContain(static e => e.Error.Code == ItemFormularioErrorCodes.FormatoIncoerente);
    }

    [Fact(DisplayName = "Nenhuma mensagem de recusa repete o valor enviado")]
    public void Ler_ItemRecusado_MensagensNaoRepetemOValorEnviado()
    {
        const string Enviado = "valor-ficticio-enviado";
        ItensLidos lidos = Ler(EntradaDoFatoColetadoDeTeste.Coletando("BAIXA_RENDA").DoTipo(Enviado).ComObrigatoriedade(Enviado).ComRotulo("").Construir());

        lidos.Erros.Should().NotBeEmpty();
        lidos.Erros.Should().OnlyContain(static e => !e.Error.Message.Contains(Enviado, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Item sem recusa é lido com a configuração e os códigos enviados")]
    public void Ler_ItemSemRecusa_DevolveAConfiguracao()
    {
        ItensLidos lidos = Ler(EntradaDoFatoColetadoDeTeste.Coletando("BAIXA_RENDA").ComRotulo("Baixa renda").Construir());

        lidos.Erros.Should().BeEmpty();
        lidos.Itens.Single().Configuracao!.TipoDeCampo.Should().Be(TipoDeCampo.SimOuNao);
        lidos.CodigosEnviados.Should().Equal("BAIXA_RENDA");
    }

    [Fact(DisplayName = "Código em branco fica fora dos códigos enviados sem lançar")]
    public void Ler_CodigoEmBranco_FicaForaDosCodigosEnviados()
    {
        ItensLidos lidos = Ler(EntradaDoFatoColetadoDeTeste.Coletando(" ").ComRotulo("Rótulo").Construir());

        lidos.CodigosEnviados.Should().BeEmpty();
        lidos.CondicoesLidas.Should().BeEmpty();
    }
}
