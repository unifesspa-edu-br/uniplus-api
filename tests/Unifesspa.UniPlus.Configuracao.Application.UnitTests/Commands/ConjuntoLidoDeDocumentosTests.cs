namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Grupo = Unifesspa.UniPlus.Configuracao.Contracts.GrupoDeDocumentosDoModeloInput;

/// <summary>
/// A leitura dos grupos de documentos do conjunto do modelo: a quantidade mínima fica como veio, o
/// indeferimento do grupo é lido antes dos documentos, cada documento é lido como o avulso, e cada recusa e
/// cada posição apontam o caminho do grupo.
/// </summary>
public sealed class ConjuntoLidoDeDocumentosTests
{
    private static readonly Dictionary<string, string> SemFormatos = new(StringComparer.Ordinal);

    private static ConteudoLido Ler(ConjuntoDeDocumentosDoModeloInput? conjunto, out LeituraDoConteudo leitura)
    {
        leitura = new LeituraDoConteudo(SemFormatos);
        return leitura.Ler(new ConteudoDoModeloInput(null, null, null, null, null, null, conjunto));
    }

    private static DocumentoExigidoDoModeloInput Documento(string tipo = "RG") =>
        new("INSCRICAO", tipo, [[new CondicaoPrecondicaoInput("CERTIFICADO", "IGUAL", JsonSerializer.SerializeToElement(true))]],
            new IndeferimentoDoDocumentoDoModeloInput(true, "RETIRA_O_ACRESCIMO", [], false));

    private static Grupo GrupoQueIndefere(string consequenciaDoDescumprimento, params DocumentoExigidoDoModeloInput[] documentos) =>
        new(2, new IndeferimentoDoGrupoDeDocumentosDoModeloInput(consequenciaDoDescumprimento, [new BaseLegalDoModeloInput("Lei 12.711/2012", "FEDERAL", "RESOLVIDO", null)], false), documentos);

    [Fact(DisplayName = "O grupo lido guarda a quantidade mínima como veio, o indeferimento e os documentos, sem recusa")]
    public void Ler_GrupoValido_GuardaQuantidadeIndeferimentoEDocumentos()
    {
        ConteudoLido lido = Ler(ConjuntoDeDocumentosDoModeloBuilder.Vazio().ComGrupos(GrupoQueIndefere("RETIRA_O_ACRESCIMO", Documento("RG"), Documento("CPF"))).Construir(), out LeituraDoConteudo leitura);

        leitura.Recusas.Should().BeEmpty();
        GrupoDeDocumentosDoModelo grupo = lido.ParaConteudo().Documentos.GruposDeDocumentos.Should().ContainSingle().Subject;
        grupo.QuantidadeMinima.Should().Be(2);
        grupo.Indeferimento!.ConsequenciaDoDescumprimento.Should().Be("RETIRA_O_ACRESCIMO");
        grupo.Indeferimento.BasesLegais.Should().ContainSingle().Which.Referencia.Should().Be("Lei 12.711/2012");
        grupo.Documentos.Select(static d => d.TipoDocumentoCodigo).Should().Equal("RG", "CPF");
    }

    [Fact(DisplayName = "A quantidade mínima ausente fica ausente, e o grupo sem indeferimento não determina resultado")]
    public void Ler_GrupoSemQuantidadeNemIndeferimento_FicaComoVeio()
    {
        ConteudoLido lido = Ler(ConjuntoDeDocumentosDoModeloBuilder.Vazio().ComGrupos(ConjuntoDeDocumentosDoModeloBuilder.Grupo(null, Documento())).Construir(), out LeituraDoConteudo leitura);

        leitura.Recusas.Should().BeEmpty();
        GrupoDeDocumentosDoModelo grupo = lido.ParaConteudo().Documentos.GruposDeDocumentos.Single();
        grupo.QuantidadeMinima.Should().BeNull();
        grupo.Indeferimento.Should().BeNull();
    }

    [Fact(DisplayName = "A entrada sem conjunto, ou sem grupos, é lida como nenhum grupo, sem recusa")]
    public void Ler_SemConjuntoOuSemGrupos_NenhumGrupo()
    {
        ConteudoLido semConjunto = Ler(null, out LeituraDoConteudo leituraSemConjunto);
        ConteudoLido semGrupos = Ler(new ConjuntoDeDocumentosDoModeloInput(null, null), out LeituraDoConteudo leituraSemGrupos);

        leituraSemConjunto.Recusas.Should().BeEmpty();
        leituraSemGrupos.Recusas.Should().BeEmpty();
        semConjunto.Documentos.GruposDeDocumentos.Should().BeEmpty();
        semGrupos.Documentos.GruposDeDocumentos.Should().BeEmpty();
    }

    [Fact(DisplayName = "O grupo nulo é recusado como entrada malformada no caminho dele, e o documento nulo do grupo no caminho do documento")]
    public void Ler_GrupoEDocumentoNulos_RecusaNoCaminhoDeCadaUm()
    {
        Grupo semDocumento = ConjuntoDeDocumentosDoModeloBuilder.Grupo(1, Documento(), null!);

        Ler(ConjuntoDeDocumentosDoModeloBuilder.Vazio().ComGrupos(null!, semDocumento).Construir(), out LeituraDoConteudo leitura);

        leitura.Recusas.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("conjuntoDeDocumentos.gruposDeDocumentos[0]", ModeloFormularioErrorCodes.EntradaMalformada),
            ("conjuntoDeDocumentos.gruposDeDocumentos[1].documentos[1]", ModeloFormularioErrorCodes.EntradaMalformada));
    }

    [Fact(DisplayName = "O indeferimento do grupo sem consequência é recusado antes dos documentos do grupo, e o grupo recusado não é lido")]
    public void Ler_IndeferimentoSemConsequencia_RecusaAntesDosDocumentos()
    {
        Grupo grupo = GrupoQueIndefere(" ", Documento() with { FaseCodigo = string.Empty });

        ConteudoLido lido = Ler(ConjuntoDeDocumentosDoModeloBuilder.Vazio().ComGrupos(grupo).Construir(), out LeituraDoConteudo leitura);

        leitura.Recusas.Select(static e => e.Field).Should().Equal(
            "conjuntoDeDocumentos.gruposDeDocumentos[0].indeferimento.consequenciaDoDescumprimento",
            "conjuntoDeDocumentos.gruposDeDocumentos[0].documentos[0].faseCodigo");
        lido.Documentos.GruposDeDocumentos.Should().BeEmpty();
    }

    [Fact(DisplayName = "As recusas dos documentos avulsos vêm antes das dos grupos, e as do grupo trazem o caminho do grupo")]
    public void Ler_AvulsosAntesDosGrupos_RecusaNoCaminhoDeCadaUm()
    {
        ConjuntoDeDocumentosDoModeloInput conjunto = ConjuntoDeDocumentosDoModeloBuilder.Vazio()
            .ComGrupos(ConjuntoDeDocumentosDoModeloBuilder.Grupo(1, Documento() with { TipoDocumentoCodigo = string.Empty }))
            .ComDocumentos(Documento() with { FaseCodigo = string.Empty })
            .Construir();

        Ler(conjunto, out LeituraDoConteudo leitura);

        leitura.Recusas.Select(static e => e.Field).Should().Equal(
            "conjuntoDeDocumentos.documentosExigidos[0].faseCodigo",
            "conjuntoDeDocumentos.gruposDeDocumentos[0].documentos[0].tipoDocumentoCodigo");
    }

    private static ConjuntoDeDocumentosDoModeloInput ConjuntoComRecusas() => ConjuntoDeDocumentosDoModeloBuilder.Vazio()
        .ComDocumentos(Documento("RG"), Documento("CPF") with { FaseCodigo = string.Empty }, Documento("LAUDO"))
        .ComGrupos(
            ConjuntoDeDocumentosDoModeloBuilder.Grupo(1, Documento("CERTIDAO") with { FaseCodigo = string.Empty }),
            ConjuntoDeDocumentosDoModeloBuilder.Grupo(1, Documento("HISTORICO"), Documento("ATESTADO")))
        .Construir();

    [Fact(DisplayName = "Cada documento lido guarda o caminho que veio na entrada; o documento e o grupo recusados ficam de fora sem mudar a posição dos demais")]
    public void Ler_DocumentosEGruposLidos_GuardamOCaminhoDaEntrada()
    {
        ConteudoLido lido = Ler(ConjuntoComRecusas(), out _);

        lido.Documentos.ComCaminhoNaOrdemDoPedido().Select(static c => (c.Caminho, c.Documento.TipoDocumentoCodigo)).Should().Equal(
            ("conjuntoDeDocumentos.documentosExigidos[0]", "RG"),
            ("conjuntoDeDocumentos.documentosExigidos[2]", "LAUDO"),
            ("conjuntoDeDocumentos.gruposDeDocumentos[1].documentos[0]", "HISTORICO"),
            ("conjuntoDeDocumentos.gruposDeDocumentos[1].documentos[1]", "ATESTADO"));
        lido.Documentos.GruposDeDocumentos.Select(static g => g.Indice).Should().Equal(1);
    }
}
