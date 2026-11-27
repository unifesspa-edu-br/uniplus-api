namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands.CenarioDoHandlerDoModelo;
using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands.RecusasParaTeste;

/// <summary>
/// O campo e o documento que a leitura aceita em grupo recusado passam pelas regras e pelo catálogo, na posição que veio, sem
/// entrar no conteúdo.
/// </summary>
public sealed class CamposEDocumentosDeGrupoNaoLidoTests
{
    private static readonly string AjudaLonga = new('a', 1001);

    private readonly CenarioDoHandlerDoModelo _cenario = new();

    [Fact(DisplayName = "O campo não lido de um grupo com a ajuda acima de 1000 caracteres é recusado na posição do grupo e do campo")]
    public void Ler_CampoNaoLidoDeGrupoComAjudaLonga_RecusaNaPosicaoDoCampo()
    {
        FatoColetadoInput naoLido = Montar("MAIOR_IDADE", 1).NaSecao(null).ComObrigatoriedade("INEXISTENTE").ComAjuda(AjudaLonga).Construir();

        DaLeitura(Conteudo(Item("CERTIFICADO", 0)) with { Grupos = [Grupo(Campo("SEM_RENDA", 0), naoLido)] }).Should().Equal(
            ("grupos[0].subitens[1].regras.obrigatoriedade", "ModeloFormulario.ObrigatoriedadeInvalida"),
            ("grupos[0].subitens[1].apresentacao.ajuda", "ItemFormulario.AjudaTamanho"));
    }

    [Fact(DisplayName = "O campo lido de um grupo recusado com fato desconhecido é recusado pelo catálogo na posição do campo")]
    public async Task Criar_CampoLidoDeGrupoRecusadoComFatoDesconhecido_RecusaNoCatalogo()
    {
        FatoColetadoInput naoLido = Montar("MAIOR_IDADE", 0).ComObrigatoriedade("INEXISTENTE").Construir();
        ConteudoDoModeloInput conteudo = Conteudo(Item("CERTIFICADO", 0)) with { Grupos = [Grupo(naoLido, Campo("FATO_FORA_DO_CATALOGO", 1))] };

        Result<Guid> resultado = await _cenario.CriarAsync(conteudo);

        Campos(resultado).Should().Contain(("conteudo.grupos[0].subitens[1].fatoCodigo", ItemFormularioErrorCodes.FatoDesconhecido));
    }

    [Fact(DisplayName = "O campo lido do grupo recusado fica fora do conteúdo e dentro da conferência no catálogo")]
    public void Ler_GrupoRecusado_GuardaOsCamposLidosForaDoConteudo()
    {
        FatoColetadoInput naoLido = Montar("MAIOR_IDADE", 0).ComObrigatoriedade("INEXISTENTE").Construir();

        DaLeituraComOConteudo(Conteudo(Item("CERTIFICADO", 0)) with { Grupos = [Grupo(naoLido, Campo("SEM_RENDA", 1))] }, out ConteudoLido lido);

        lido.Grupos.Lidos.Should().BeEmpty();
        lido.Grupos.CamposDeGruposNaoLidos.Select(static c => (c.Caminho, c.Campo.FatoCodigo)).Should().Equal(("grupos[0].subitens[1]", "SEM_RENDA"));
        lido.ParaConteudo().Grupos.Should().BeEmpty();
    }

    [Fact(DisplayName = "O documento lido de um grupo de documentos recusado é recusado pelo catálogo com a condição que cita fato desconhecido")]
    public async Task Criar_DocumentoLidoDeGrupoRecusado_ConfereNoCatalogo()
    {
        DocumentoExigidoDoModeloInput documento = new(
            "INSCRICAO", "LAUDO_MEDICO", Quando("FATO_FORA_DO_CATALOGO", true), new IndeferimentoDoDocumentoDoModeloInput(true, "RETIRA_O_ACRESCIMO", [], false));
        GrupoDeDocumentosDoModeloInput grupo = new(1, new("DESCONHECIDA", [], false), [documento]);

        Result<Guid> resultado = await _cenario.CriarAsync(
            Conteudo(Item("CERTIFICADO", 0)) with { ConjuntoDeDocumentos = new ConjuntoDeDocumentosDoModeloInput([], [grupo]) });

        resultado.Errors.Select(static e => e.Field).Should().Contain(static campo =>
            campo!.StartsWith("conteudo.conjuntoDeDocumentos.gruposDeDocumentos[0].documentos[0].quando", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "O catálogo recusa na ordem do pedido: o fato desconhecido do campo do grupo zero, recusado, sai antes do grupo um, lido")]
    public async Task Criar_GrupoZeroRecusadoEGrupoUmLido_CatalogoNaOrdemDoPedido()
    {
        GrupoColetadoInput recusado = GrupoNomeado("PRIMEIRO", 1, "INEXISTENTE", Campo("FATO_FORA_DO_CATALOGO", 0));
        GrupoColetadoInput lido = GrupoNomeado("SEGUNDO", 2, "SEMPRE", Campo("OUTRO_FATO_FORA_DO_CATALOGO", 0));

        Result<Guid> resultado = await _cenario.CriarAsync(Conteudo(Item("CERTIFICADO", 0)) with { Grupos = [recusado, lido] });

        Campos(resultado).Where(static c => c.Codigo == ItemFormularioErrorCodes.FatoDesconhecido).Select(static c => c.Campo).Should().Equal(
            "conteudo.grupos[0].subitens[0].fatoCodigo", "conteudo.grupos[1].subitens[0].fatoCodigo");
    }

    [Fact(DisplayName = "O catálogo recusa na ordem do pedido: o fato desconhecido do documento do grupo zero, recusado, sai antes do grupo um, lido")]
    public async Task Criar_GrupoDeDocumentosZeroRecusadoEUmLido_CatalogoNaOrdemDoPedido()
    {
        GrupoDeDocumentosDoModeloInput recusado = new(1, new("DESCONHECIDA", [], false), [DocumentoComFatoForaDoCatalogo("FATO_FORA_DO_CATALOGO")]);
        GrupoDeDocumentosDoModeloInput lido = new(1, null, [DocumentoComFatoForaDoCatalogo("OUTRO_FATO_FORA_DO_CATALOGO")]);

        Result<Guid> resultado = await _cenario.CriarAsync(
            Conteudo(Item("CERTIFICADO", 0)) with { ConjuntoDeDocumentos = new ConjuntoDeDocumentosDoModeloInput([], [recusado, lido]) });

        resultado.Errors.Select(static e => e.Field!).Where(static f => f.Contains(".documentos[0].quando", StringComparison.Ordinal))
            .Select(static f => f[..f.IndexOf(".quando", StringComparison.Ordinal)]).Distinct().Should().Equal(
                "conteudo.conjuntoDeDocumentos.gruposDeDocumentos[0].documentos[0]", "conteudo.conjuntoDeDocumentos.gruposDeDocumentos[1].documentos[0]");
    }

    private static DocumentoExigidoDoModeloInput DocumentoComFatoForaDoCatalogo(string fato) =>
        new("INSCRICAO", "LAUDO_MEDICO", Quando(fato, true), new IndeferimentoDoDocumentoDoModeloInput(true, "RETIRA_O_ACRESCIMO", [], false));

    private static GrupoColetadoInput GrupoNomeado(string codigo, int ordem, string obrigatoriedade, FatoColetadoInput campo) =>
        EntradaDoGrupoColetadoDeTeste.Compondo(codigo, ordem).ComRotulo("Composição").NaSecao("DADOS").ComOcorrencias(1, 10)
            .ComObrigatoriedade(obrigatoriedade).ComCampos(campo).Construir();

    private static GrupoColetadoInput Grupo(params FatoColetadoInput[] campos) =>
        EntradaDoGrupoColetadoDeTeste.Compondo("COMPOSICAO", 1).ComRotulo("Composição").NaSecao("DADOS").ComOcorrencias(1, 10)
            .ComObrigatoriedade("SEMPRE").ComCampos(campos).Construir();
}
