namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;
using AwesomeAssertions.Equivalency;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands.ApoioDoConteudoLido;

using Condicoes = System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<Unifesspa.UniPlus.Regras.Entradas.CondicaoPrecondicaoInput>>;
using Recusa = (string Campo, string Codigo, string Mensagem);

/// <summary>
/// A leitura do conteúdo do modelo nos documentos exigidos: os requisitos lidos com a fase de cada referência, o código desconhecido,
/// a restrição de opções e a faixa de data inválidas.
/// </summary>
public sealed class ConteudoLidoDosDocumentosTests
{
    [Fact(DisplayName = "O documento sem bases legais e com a exigência por opção sem exceções é lido sem recusa, com as duas listas vazias")]
    public void Ler_DocumentoSemBasesLegaisNemExcecoes_LeSemRecusa()
    {
        DocumentoExigidoDoModeloInput documento = DocumentoValido() with { Indeferimento = new IndeferimentoDoDocumentoDoModeloInput(true, "RETIRA_O_ACRESCIMO", null, false), PorOpcao = new ExigenciaPorOpcaoInput("CONDICAO", null) };

        LeituraDoConteudo leitura = new(Formatos);
        ConteudoLido lido = leitura.Ler(new ConteudoDoModeloInput(null, null, null, null, null, null, ConjuntoDeDocumentosDoModeloBuilder.SoDocumentos(documento)));

        leitura.Recusas.Should().BeEmpty();
        lido.Documentos.DocumentosExigidos.Single().Documento.Indeferimento.BasesLegais.Should().BeEmpty();
        lido.Documentos.DocumentosExigidos.Single().Documento.PorOpcao!.Exceto.Should().BeEmpty();
    }

    [Fact]
    public void Ler_DocumentoEGrupoSemConsequencia_PreservaReenvio()
    {
        DocumentoExigidoDoModeloInput documento = DocumentoValido() with { Indeferimento = new(true, null, null, true) };
        GrupoDeDocumentosDoModeloInput grupo = new(1, new(null, null, true), [documento]);
        LeituraDoConteudo leitura = new(Formatos);
        ConteudoLido lido = leitura.Ler(new ConteudoDoModeloInput(null, null, null, null, null, null, new([documento], [grupo])));
        leitura.Recusas.Should().BeEmpty();
        lido.Documentos.DocumentosExigidos.Single().Documento.Indeferimento.ConsequenciaDoDescumprimento.Should().BeNull();
        lido.Documentos.GruposDeDocumentos.Single().Grupo.Grupo.Indeferimento!.AbrePrazoParaReenvio.Should().BeTrue();
    }

    [Fact(DisplayName = "Os requisitos do documento são lidos com a fase de cada referência pelo código")]
    public void Ler_DocumentoComRequisitos_GuardaOsRequisitosDeclarados()
    {
        RequisitosDoDocumentoDoModeloInput requisitos = RequisitosDoDocumentoDoModeloBuilder.AindaNaoEscolhidos()
            .ComPrazoDe(90, "DIAS", RequisitosDoDocumentoDoModeloBuilder.NoInicioDaFase("HABILITACAO")).Construir();
        DocumentoExigidoDoModeloInput documento = DocumentoValido() with { Requisitos = requisitos };

        LeituraDoConteudo leitura = new(Formatos);
        ConteudoLido lido = leitura.Ler(new ConteudoDoModeloInput(null, null, null, null, null, null, ConjuntoDeDocumentosDoModeloBuilder.SoDocumentos(documento)));

        leitura.Recusas.Should().BeEmpty();
        lido.Documentos.DocumentosExigidos.Single().Documento.Requisitos.PrazoDeEmissao!.Referencia!.FaseCodigo.Should().Be("HABILITACAO");
        lido.Documentos.DocumentosExigidos.Single().Documento.Requisitos.Validade.Should().BeNull();
    }

    [Fact(DisplayName = "O código desconhecido dos requisitos é recusado no caminho do documento, depois do indeferimento e antes da exigência por opção")]
    public void Ler_DocumentoComRequisitoDesconhecido_RecusaNoCaminhoDoDocumento()
    {
        DocumentoExigidoDoModeloInput documento = DocumentoValido() with
        {
            Indeferimento = new IndeferimentoDoDocumentoDoModeloInput(true, string.Empty, [], false),
            Requisitos = RequisitosDoDocumentoDoModeloBuilder.AindaNaoEscolhidos().ComEscolhaDePrazo("naoExiste").Construir(),
            PorOpcao = new ExigenciaPorOpcaoInput(string.Empty, []),
        };

        LeituraDoConteudo leitura = new(Formatos);
        ConteudoLido lido = leitura.Ler(new ConteudoDoModeloInput(null, null, null, null, null, null, ConjuntoDeDocumentosDoModeloBuilder.SoDocumentos(documento)));

        leitura.Recusas.Select(static e => e.Field).Should().Equal(
            "conjuntoDeDocumentos.documentosExigidos[0].indeferimento.consequenciaDoDescumprimento", "conjuntoDeDocumentos.documentosExigidos[0].requisitos.prazoDeEmissao.escolha", "conjuntoDeDocumentos.documentosExigidos[0].porOpcao.fatoCodigo");
        lido.Documentos.DocumentosExigidos.Should().BeEmpty();
    }

    [Fact(DisplayName = "A restrição de opções com um grupo nulo e outro com condição inválida recusa os dois, cada um no caminho do seu grupo")]
    public void Ler_OpcoesComGrupoNuloEGrupoRecusado_RecusaOsDoisGrupos()
    {
        CondicaoPrecondicaoInput condicaoInvalida = new("MAIOR_IDADE", "OPERADOR_QUE_NAO_EXISTE", JsonSerializer.SerializeToElement(true));
        RestricaoValorInput opcoes = new("OPCOES_PERMITIDAS", Entradas: [null!, new OpcoesCondicionadasInput([[condicaoInvalida]], ["A"])]);

        IReadOnlyList<string> campos = CamposRecusadosDoItemCom(opcoes);

        campos.Should().Equal("itens[0].regras.restricoes[0].entradas[0]", "itens[0].regras.restricoes[0].entradas[1].quando[0][0].operador");
    }

    [Fact(DisplayName = "A faixa de data com os dois limites inválidos recusa os dois, o mínimo antes do máximo")]
    public void Ler_FaixaDeDataComOsDoisLimitesInvalidos_RecusaOsDois()
    {
        RestricaoValorInput faixa = new("FAIXA_DE_DATA", DataMinima: new LimiteDeDataInput("HOJE"), DataMaxima: new LimiteDeDataInput("HOJE"));

        IReadOnlyList<string> campos = CamposRecusadosDoItemCom(faixa);

        campos.Should().Equal("itens[0].regras.restricoes[0].dataMinima", "itens[0].regras.restricoes[0].dataMaxima");
    }
}
