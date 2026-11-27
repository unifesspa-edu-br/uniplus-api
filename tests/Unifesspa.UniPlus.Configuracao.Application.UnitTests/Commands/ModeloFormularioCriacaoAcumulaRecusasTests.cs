namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands.CenarioDoHandlerDoModelo;
using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport.DeclaracoesDeModeloDeTeste;

/// <summary>
/// A criação do modelo devolve na mesma resposta as recusas da leitura, as do catálogo e as do domínio, na
/// ordem das regras (ADR-0125), sem repetir nenhuma e sem conferir o que depende de parte que não se leu.
/// </summary>
public sealed class ModeloFormularioCriacaoAcumulaRecusasTests
{
    private readonly CenarioDoHandlerDoModelo _cenario = new();

    [Fact(DisplayName = "Item que a leitura recusa e página com código inválido saem juntos, e o modelo não é gravado")]
    public async Task Criar_RecusaDeLeituraEDeDominio_DevolveAsDuas()
    {
        ConteudoDoModeloInput conteudo = ComSegundaPagina(Conteudo(ItemNaoLido("CERTIFICADO", 0)), "PAGINA RUIM");

        Result<Guid> resultado = await _cenario.CriarAsync(conteudo);

        Campos(resultado).Should().Equal(
            ("conteudo.itens[0].regras.obrigatoriedade", ModeloFormularioErrorCodes.ObrigatoriedadeInvalida),
            ("conteudo.paginas[1].codigo", EstruturaFormularioErrorCodes.PaginaCodigoInvalido));
        await _cenario.Repository.DidNotReceive().AdicionarAsync(Arg.Any<ModeloFormulario>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A recusa de domínio de um item aponta a posição que veio, mesmo com um item antes dele que não se leu")]
    public async Task Criar_ItemNaoLidoAntes_RecusaDeDominioNaPosicaoDeEntrada()
    {
        ConteudoDoModeloInput conteudo = Conteudo(
            ItemNaoLido("CERTIFICADO", 0), Item("DATA_NASCIMENTO", 1, "DATA"), ItemSemRotulo("CPF_RESPONSAVEL", 2, "TEXTO"));

        Result<Guid> resultado = await _cenario.CriarAsync(conteudo);

        Campos(resultado).Select(static c => c.Campo).Should().Equal(
            "conteudo.itens[0].regras.obrigatoriedade", "conteudo.itens[2].apresentacao.rotulo");
    }

    [Fact(DisplayName = "A recusa de domínio de um grupo aponta a posição que veio, mesmo com um grupo antes dele que não se leu")]
    public async Task Criar_GrupoNaoLidoAntes_RecusaDeDominioNaPosicaoDeEntrada()
    {
        ConteudoDoModeloInput conteudo = Conteudo(Item("CERTIFICADO", 0)) with
        {
            Grupos =
            [
                Grupo("COMPOSICAO_FAMILIAR", 1, "MAIOR_IDADE").SemOcorrencias().Construir(),
                Grupo("OUTRA_COMPOSICAO", 2, "SEM_RENDA").ComRotulo(string.Empty).Construir(),
            ],
        };

        Result<Guid> resultado = await _cenario.CriarAsync(conteudo);

        Campos(resultado).Select(static c => c.Campo).Should().Equal(
            "conteudo.grupos[0].ocorrencias.minimo", "conteudo.grupos[0].ocorrencias.incluiCandidato", "conteudo.grupos[1].rotulo");
    }

    [Fact(DisplayName = "Página que a leitura recusa e item que aponta para ela: só a recusa da leitura, sem recusa de estrutura")]
    public async Task Criar_PaginaNaoLidaEItemQueApontaParaEla_RecusaSoALeitura()
    {
        ConteudoDoModeloInput conteudo = Conteudo(Item("CERTIFICADO", 0)) with
        {
            Paginas = [EntradaDaPaginaDoFormularioDeTeste.BlocoDeSistema("DADOS", 0, "BLOCO_INEXISTENTE").ComTitulo("Dados").Construir()],
        };

        Result<Guid> resultado = await _cenario.CriarAsync(conteudo);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("conteudo.paginas[0].bloco");
    }

    [Fact(DisplayName = "Código, descritivo e declaração recusados saem uma vez cada, junto da recusa de leitura")]
    public async Task Criar_CadastroRecusadoEConteudoNaoLido_NaoRepeteRecusa()
    {
        CriarModeloFormularioCommand comando = new(" CODIGO RUIM", null, null, Conteudo(ItemNaoLido("CERTIFICADO", 0)));

        Result<Guid> resultado = await _cenario.CriarAsync(comando);

        Campos(resultado).Select(static c => c.Campo).Should().Equal(
            "conteudo.itens[0].regras.obrigatoriedade", "codigo", "descritivo.nome", "declaracao");
    }

    [Fact(DisplayName = "As recusas saem na ordem das regras: leitura, catálogo, tipo de processo, código, descritivo, declaração e conteúdo")]
    public async Task Criar_TodasAsFamilias_SaemNaOrdemDasRegras()
    {
        ConteudoDoModeloInput conteudo = Conteudo(
            ItemNaoLido("CERTIFICADO", 0), Item("FATO_FORA_DO_CATALOGO", 1), ItemSemRotulo("DATA_NASCIMENTO", 2, "DATA"));
        CriarModeloFormularioCommand comando = new(" CODIGO RUIM", null, new DescritivoDoModeloInput(null, null, "NAO_EXISTE"), conteudo);

        Result<Guid> resultado = await _cenario.CriarAsync(comando);

        Campos(resultado).Select(static c => c.Campo).Should().Equal(
            "conteudo.itens[0].regras.obrigatoriedade",
            "conteudo.itens[1].fatoCodigo",
            "descritivo.tipoProcessoCodigo",
            "codigo",
            "descritivo.nome",
            "declaracao",
            "conteudo.itens[2].apresentacao.rotulo");
    }

    [Fact(DisplayName = "Com o conteúdo lido por inteiro, a recusa do catálogo vem antes da de estrutura")]
    public async Task Criar_ConteudoLidoPorInteiro_CatalogoAntesDaEstrutura()
    {
        ConteudoDoModeloInput conteudo = Conteudo(Item("FATO_FORA_DO_CATALOGO", 0), Montar("CERTIFICADO", 1).NaSecao("OUTRA").Construir());

        Result<Guid> resultado = await _cenario.CriarAsync(conteudo);

        Campos(resultado).Select(static c => c.Codigo).Should().ContainInOrder(
            ItemFormularioErrorCodes.FatoDesconhecido, EstruturaFormularioErrorCodes.ItemForaDeSecao);
    }

    [Fact(DisplayName = "A seção dos dados básicos recusada sai antes da recusa de leitura")]
    public async Task Criar_SecaoDosDadosBasicosRecusada_SaiAntesDaLeitura()
    {
        CriarModeloFormularioCommand comando = new(
            "HABILITACAO_MEDICINA",
            new DeclaracaoDoModeloInput(ServeAFaseDeInscricao: true, Obrigatorio: true),
            new DescritivoDoModeloInput("Inscrição", null, null),
            Conteudo(ItemNaoLido("CERTIFICADO", 0), Montar("CPF_RESPONSAVEL", 1, "TEXTO").NaSecao(ConjuntoBasicoDaInscricao.CodigoDaSecao).Construir()));

        Result<Guid> resultado = await _cenario.CriarAsync(comando);

        Campos(resultado).Select(static c => c.Codigo).Should().ContainInOrder(
            EstruturaFormularioErrorCodes.SecaoReservada, ModeloFormularioErrorCodes.ObrigatoriedadeInvalida);
    }

    [Fact(DisplayName = "Termo que a leitura recusa não esconde o código repetido de outros termos, na posição que veio")]
    public async Task Criar_TermoNaoLidoECodigoRepetido_RecusaARepeticao()
    {
        TermoExigidoInput naoLido = EntradaDoTermoExigidoDeTeste.Exigindo("LGPD", 0).ComObrigatoriedade("INEXISTENTE").Construir();
        TermoExigidoInput primeiro = EntradaDoTermoExigidoDeTeste.Exigindo("ACEITE", 1).DaVersao(Guid.NewGuid(), Guid.NewGuid()).Construir();
        TermoExigidoInput repetido = EntradaDoTermoExigidoDeTeste.Exigindo("ACEITE", 2).DaVersao(Guid.NewGuid(), Guid.NewGuid()).Construir();

        Result<Guid> resultado = await _cenario.CriarAsync(Conteudo(Item("CERTIFICADO", 0)) with { Termos = [naoLido, primeiro, repetido] });

        Campos(resultado).Should().Contain(("conteudo.termos[0].regras.obrigatoriedade", ModeloFormularioErrorCodes.ObrigatoriedadeInvalida));
        Campos(resultado).Should().Contain(("conteudo.termos[2].codigo", TermoFormularioErrorCodes.CodigoDuplicado));
    }

    [Fact(DisplayName = "O conteúdo válido continua gravando o modelo desativado")]
    public async Task Criar_ConteudoValido_GravaDesativado()
    {
        ModeloFormulario? gravado = null;
        await _cenario.Repository.AdicionarAsync(Arg.Do<ModeloFormulario>(m => gravado = m), Arg.Any<CancellationToken>());

        Result<Guid> resultado = await _cenario.CriarAsync(Conteudo(Item("CERTIFICADO", 0)));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        gravado!.Ativo.Should().BeFalse();
    }

    private static EntradaDoGrupoColetadoDeTeste Grupo(string codigo, int ordem, string campo) =>
        EntradaDoGrupoColetadoDeTeste.Compondo(codigo, ordem).ComRotulo("Composição").NaSecao("DADOS")
            .ComOcorrencias(1, 10).ComObrigatoriedade("SEMPRE").ComCampos(Campo(campo, 0));

    private static (string Campo, string Codigo)[] Campos(Result<Guid> resultado) =>
        [.. resultado.Errors.Select(static e => (e.Field!, e.Error.Code))];
}
