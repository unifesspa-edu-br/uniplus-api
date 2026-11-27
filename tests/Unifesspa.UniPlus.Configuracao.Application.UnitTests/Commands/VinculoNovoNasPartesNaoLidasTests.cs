namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands.CenarioDoHandlerDoModelo;
using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands.RecusasParaTeste;

/// <summary>
/// O vínculo novo a dado desativado, ou que aguarda a aprovação da proteção de dados, é recusado também quando a leitura
/// recusou outra parte do campo ou do documento que o cita, na mesma resposta e uma só vez para cada dado.
/// </summary>
public sealed class VinculoNovoNasPartesNaoLidasTests
{
    private readonly CenarioDoHandlerDoModelo _cenario = new();

    [Fact(DisplayName = "O item com a obrigatoriedade inválida e o dado desativado é recusado pela obrigatoriedade e pelo vínculo novo")]
    public async Task Criar_ItemRecusadoComDadoDesativado_RecusaOVinculoJuntoComADaLeitura()
    {
        _cenario.Certificado.Desativar().IsSuccess.Should().BeTrue();

        Result<Guid> resultado = await _cenario.CriarAsync(Conteudo(ItemNaoLido("CERTIFICADO", 0)));

        resultado.Errors.Select(static e => e.Error.Code).Should().Contain([ModeloFormularioCodigoDaLeitura, VinculoCatalogoErrorCodes.FatoDesativado]);
        Vinculos(resultado.Errors, VinculoCatalogoErrorCodes.FatoDesativado).Should().Equal("CERTIFICADO");
    }

    [Fact(DisplayName = "O item recusado com o dado que aguarda a aprovação da proteção de dados recebe a recusa do vínculo novo")]
    public async Task Criar_ItemRecusadoComDadoSemProtecaoAprovada_RecusaOVinculo()
    {
        FatoCandidato pendente = Declarado("DADO_PENDENTE");
        _cenario.Fatos.ListarTodosAsync(Arg.Any<CancellationToken>()).Returns(_ => [_cenario.Certificado, pendente]);

        Result<Guid> resultado = await _cenario.CriarAsync(Conteudo(ItemNaoLido("DADO_PENDENTE", 0)));

        Vinculos(resultado.Errors, VinculoCatalogoErrorCodes.FatoComProtecaoPendente).Should().Equal("DADO_PENDENTE");
    }

    [Fact(DisplayName = "O campo de grupo recusado pelo tipo de campo inexistente e com dado desativado recebe a recusa do vínculo novo")]
    public async Task Criar_CampoDeGrupoRecusadoComDadoDesativado_RecusaOVinculo()
    {
        _cenario.MaiorIdade.Desativar().IsSuccess.Should().BeTrue();

        Result<Guid> resultado = await _cenario.CriarAsync(ComComposicao(Montar("MAIOR_IDADE", 0, "TIPO_INEXISTENTE").NaSecao(null).Construir()));

        Vinculos(resultado.Errors, VinculoCatalogoErrorCodes.FatoDesativado).Should().Equal("MAIOR_IDADE");
    }

    [Fact(DisplayName = "O campo que a leitura aceitou em grupo recusado e tem dado desativado recebe a recusa do vínculo novo")]
    public async Task Criar_CampoLidoDeGrupoRecusadoComDadoDesativado_RecusaOVinculo()
    {
        _cenario.SemRenda.Desativar().IsSuccess.Should().BeTrue();
        ConteudoDoModeloInput conteudo = ComComposicao(Campo("SEM_RENDA", 0));
        conteudo = conteudo with { Grupos = [.. conteudo.Grupos!.Select(static g => g with { Regras = g.Regras! with { Obrigatoriedade = new("INEXISTENTE", null) } })] };

        Result<Guid> resultado = await _cenario.CriarAsync(conteudo);

        Vinculos(resultado.Errors, VinculoCatalogoErrorCodes.FatoDesativado).Should().Equal("SEM_RENDA");
    }

    [Fact(DisplayName = "O dado desativado citado por dois itens recusados é recusado uma só vez")]
    public async Task Criar_DoisItensRecusadosComODadoDesativado_RecusaUmaVez()
    {
        _cenario.Certificado.Desativar().IsSuccess.Should().BeTrue();

        Result<Guid> resultado = await _cenario.CriarAsync(Conteudo(ItemNaoLido("CERTIFICADO", 0), ItemNaoLido("CERTIFICADO", 1)));

        Vinculos(resultado.Errors, VinculoCatalogoErrorCodes.FatoDesativado).Should().Equal("CERTIFICADO");
    }

    [Fact(DisplayName = "O dado que o modelo gravado já vinculava, desativado depois, não recebe a recusa do vínculo novo no item recusado")]
    public async Task Atualizar_ItemRecusadoComVinculoJaGravado_NaoRecusaOVinculo()
    {
        ModeloFormulario existente = Existente(tipo: null);
        _cenario.Certificado.Desativar().IsSuccess.Should().BeTrue();

        Result resultado = await _cenario.AtualizarAsync(existente, Conteudo(ItemNaoLido("CERTIFICADO", 0)), tipo: null);

        resultado.Errors.Select(static e => e.Error.Code).Should().Contain(ModeloFormularioCodigoDaLeitura)
            .And.NotContain(VinculoCatalogoErrorCodes.FatoDesativado);
    }

    [Fact(DisplayName = "O item recusado que acrescenta, na edição, um dado desativado que o modelo não tinha recebe a recusa do vínculo novo")]
    public async Task Atualizar_ItemRecusadoComDadoDesativadoNovo_RecusaOVinculo()
    {
        ModeloFormulario existente = Existente(tipo: null);
        _cenario.DataNascimento.Desativar().IsSuccess.Should().BeTrue();

        Result resultado = await _cenario.AtualizarAsync(existente, Conteudo(Item("CERTIFICADO", 0), ItemNaoLido("DATA_NASCIMENTO", 1)), tipo: null);

        Vinculos(resultado.Errors, VinculoCatalogoErrorCodes.FatoDesativado).Should().Equal("DATA_NASCIMENTO");
    }

    [Fact(DisplayName = "O item recusado pela obrigatoriedade com dado que o catálogo não tem recebe a recusa de dado desconhecido")]
    public async Task Criar_ItemRecusadoComDadoDesconhecido_RecusaODado()
    {
        Result<Guid> resultado = await _cenario.CriarAsync(Conteudo(ItemNaoLido("DADO_FORA_DO_CATALOGO", 0)));

        Campos(resultado).Should().Contain(("conteudo.itens[0].fatoCodigo", ItemFormularioErrorCodes.FatoDesconhecido));
    }

    [Fact(DisplayName = "O item recusado pela obrigatoriedade com dado de membro de grupo recebe a recusa de dado não coletável")]
    public async Task Criar_ItemRecusadoComDadoDeMembroDeGrupo_RecusaODadoNaoColetavel()
    {
        Result<Guid> resultado = await _cenario.CriarAsync(Conteudo(ItemNaoLido("MAIOR_IDADE", 0)));

        Campos(resultado).Should().Contain(("conteudo.itens[0].fatoCodigo", ItemFormularioErrorCodes.FatoNaoColetavel));
    }

    [Fact(DisplayName = "O campo de grupo recusado com dado desconhecido ou calculado recebe a recusa do dado na posição do campo")]
    public async Task Criar_CampoDeGrupoRecusadoComDadoQueNaoSeColeta_RecusaODadoNaPosicaoDoCampo()
    {
        Result<Guid> resultado = await _cenario.CriarAsync(ComComposicao(
            Montar("DADO_FORA_DO_CATALOGO", 0).NaSecao(null).ComObrigatoriedade("INEXISTENTE").Construir(),
            Montar("IDADE", 1).NaSecao(null).ComObrigatoriedade("INEXISTENTE").Construir()));

        Campos(resultado).Where(static c => c.Campo.EndsWith(".fatoCodigo", StringComparison.Ordinal)).Should().Equal(
            ("conteudo.grupos[0].subitens[0].fatoCodigo", ItemFormularioErrorCodes.FatoDesconhecido),
            ("conteudo.grupos[0].subitens[1].fatoCodigo", ItemFormularioErrorCodes.FatoNaoColetavel));
    }

    [Fact(DisplayName = "A recusa do dado do item recusado sai na posição do pedido, entre as dos itens lidos")]
    public async Task Criar_ItemRecusadoEntreItensLidos_RecusaODadoNaPosicaoDoPedido()
    {
        Result<Guid> resultado = await _cenario.CriarAsync(Conteudo(
            Item("DADO_FORA_DO_CATALOGO", 0), ItemNaoLido("OUTRO_FORA_DO_CATALOGO", 1), Item("TERCEIRO_FORA_DO_CATALOGO", 2)));

        Campos(resultado).Where(static c => c.Codigo == ItemFormularioErrorCodes.FatoDesconhecido).Select(static c => c.Campo).Should().Equal(
            "conteudo.itens[0].fatoCodigo", "conteudo.itens[1].fatoCodigo", "conteudo.itens[2].fatoCodigo");
    }

    [Fact(DisplayName = "O dado que o item recusado coleta, sem desativação, grava o modelo sem recusa de vínculo")]
    public async Task Criar_ConteudoSemRecusaComDadosAtivos_Grava()
    {
        Result<Guid> resultado = await _cenario.CriarAsync(Conteudo(Item("CERTIFICADO", 0)));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "O documento avulso recusado que se multiplica pelas opções de dado desativado recebe a recusa do vínculo novo")]
    public async Task Criar_DocumentoRecusadoPorOpcaoDeDadoDesativado_RecusaOVinculo()
    {
        _cenario.DataNascimento.Desativar().IsSuccess.Should().BeTrue();
        DocumentoExigidoDoModeloInput recusado = new("INSCRICAO", "LAUDO_MEDICO", null, null, PorOpcao: new ExigenciaPorOpcaoInput("DATA_NASCIMENTO", []));

        Result<Guid> resultado = await _cenario.CriarAsync(
            Conteudo(Item("CERTIFICADO", 0)) with { ConjuntoDeDocumentos = new ConjuntoDeDocumentosDoModeloInput([recusado], []) });

        Vinculos(resultado.Errors, VinculoCatalogoErrorCodes.FatoDesativado).Should().Equal("DATA_NASCIMENTO");
    }

    [Fact(DisplayName = "O documento aceito em grupo de documentos recusado cuja condição cita dado desativado recebe a recusa do vínculo novo")]
    public async Task Criar_DocumentoLidoDeGrupoRecusadoComCondicaoDeDadoDesativado_RecusaOVinculo()
    {
        _cenario.DataNascimento.Desativar().IsSuccess.Should().BeTrue();
        DocumentoExigidoDoModeloInput documento = new(
            "INSCRICAO", "LAUDO_MEDICO", Quando("DATA_NASCIMENTO", true), new IndeferimentoDoDocumentoDoModeloInput(true, "RETIRA_O_ACRESCIMO", [], false));
        GrupoDeDocumentosDoModeloInput grupo = new(1, new("DESCONHECIDA", [], false), [documento]);

        Result<Guid> resultado = await _cenario.CriarAsync(
            Conteudo(Item("CERTIFICADO", 0)) with { ConjuntoDeDocumentos = new ConjuntoDeDocumentosDoModeloInput([], [grupo]) });

        Vinculos(resultado.Errors, VinculoCatalogoErrorCodes.FatoDesativado).Should().Equal("DATA_NASCIMENTO");
    }

    private static string ModeloFormularioCodigoDaLeitura => "ModeloFormulario.ObrigatoriedadeInvalida";

    private static string[] Vinculos(IEnumerable<FieldError> erros, string codigo) =>
        [.. erros.Where(e => e.Error.Code == codigo).Select(static e => e.Origem!)];
}
