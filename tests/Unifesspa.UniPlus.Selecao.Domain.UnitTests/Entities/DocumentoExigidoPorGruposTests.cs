namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A exigência é criada e reidratada a partir dos cinco grupos que o contrato REST já usa: o tipo copiado, o momento
/// da coleta, a aplicabilidade, o indeferimento e os requisitos do arquivo.
/// </summary>
public sealed class DocumentoExigidoPorGruposTests
{
    private static readonly Guid IdCongelado = Guid.Parse("0190a1b2-0000-7000-8000-000000000001");

    private static ChaveLocal Chave => ChaveLocal.Criar("exigencia_teste").Value;

    private static CondicaoGatilho CondicaoDeCorOuRaca() =>
        CondicaoGatilho.Criar(0, "COR_RACA", Operador.Igual, JsonSerializer.SerializeToElement("BRANCA")).Value!;

    private static DocumentoExigidoDeclarado Declarada() => new(
        new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "LAUDO", "Laudo médico", "SAUDE", ProtecaoDoDocumentoDeTeste.Pessoal),
        new MomentoDaExigencia(Guid.CreateVersion7(), null, null),
        new AplicabilidadeDaExigencia(Aplicabilidade.Geral, []),
        new IndeferimentoDoDocumento(true, null, []),
        new RequisitosDoDocumento(FormatosPermitidos.Criar(true, null).Value!, null, null, null, null),
        null);

    private static DocumentoExigido Reidratada(DocumentoExigidoDeclarado declarada) =>
        DocumentoExigido.Reidratar(Chave, IdCongelado, declarada);

    [Fact(DisplayName = "Reidratar grava o Id congelado e o momento da coleta")]
    public void Reidratar_GravaOIdEOMomento()
    {
        Guid faseId = Guid.CreateVersion7();
        Guid etapaId = Guid.CreateVersion7();

        DocumentoExigido exigencia = Reidratada(Declarada() with { Momento = new(faseId, etapaId, FinalidadeFormulario.Inscricao) });

        exigencia.Id.Should().Be(IdCongelado);
        exigencia.ExigidoNaFaseId.Should().Be(faseId);
        exigencia.ExigidoNaEtapaId.Should().Be(etapaId);
        exigencia.Finalidade.Should().Be(FinalidadeFormulario.Inscricao);
    }

    [Fact(DisplayName = "Reidratar grava o tipo de documento copiado e a proteção dele")]
    public void Reidratar_GravaOTipoCopiadoEAProtecao()
    {
        Guid tipoId = Guid.CreateVersion7();
        CopiaDoTipoDeDocumento tipo = new(tipoId, "IDENTIDADE", "Documento de identidade", "PESSOAL", ProtecaoDoDocumentoDeTeste.De(ClassificacaoDeProtecao.Sensivel));

        DocumentoExigido exigencia = Reidratada(Declarada() with { Tipo = tipo });

        exigencia.TipoDocumentoOrigemId.Should().Be(tipoId);
        exigencia.TipoDocumentoCodigo.Should().Be("IDENTIDADE");
        exigencia.ClassificacaoProtecao.Should().Be(ClassificacaoDeProtecao.Sensivel);
    }

    [Fact(DisplayName = "Reidratar grava a aplicabilidade, a obrigatoriedade e o tamanho máximo")]
    public void Reidratar_GravaAAplicabilidadeEOsRequisitos()
    {
        DocumentoExigido exigencia = Reidratada(Declarada() with
        {
            Indeferimento = new(false, "ELIMINA", []),
            Requisitos = new(FormatosPermitidos.Criar(true, null).Value!, 2048, null, null, null),
        });

        exigencia.Aplicabilidade.Should().Be(Aplicabilidade.Geral);
        exigencia.Obrigatorio.Should().BeFalse();
        exigencia.ConsequenciaIndeferimento.Should().Be("ELIMINA");
        exigencia.TamanhoMaximoBytes.Should().Be(2048);
    }

    [Fact(DisplayName = "Reidratar vincula as condições e as bases legais ao Id congelado")]
    public void Reidratar_VinculaOsFilhosAoIdCongelado()
    {
        CondicaoGatilho condicao = CondicaoDeCorOuRaca();
        DocumentoExigidoBaseLegal baseLegal = ApoioDosDocumentosExigidosDoProcessoSeletivo.BaseLegalResolvida();

        DocumentoExigido exigencia = Reidratada(Declarada() with
        {
            Aplicabilidade = new(Aplicabilidade.Condicional, [condicao]),
            Indeferimento = new(true, null, [baseLegal]),
        });

        condicao.DocumentoExigidoId.Should().Be(IdCongelado);
        baseLegal.DocumentoExigidoId.Should().Be(IdCongelado);
        exigencia.Condicoes.Should().ContainSingle().Which.Should().BeSameAs(condicao);
        exigencia.BasesLegais.Should().ContainSingle().Which.Should().BeSameAs(baseLegal);
    }

    [Fact(DisplayName = "Reidratar não normaliza a consequência já congelada")]
    public void Reidratar_NaoNormalizaAConsequencia()
    {
        DocumentoExigido exigencia = Reidratada(Declarada() with { Indeferimento = new(true, "  ", []) });

        exigencia.ConsequenciaIndeferimento.Should().Be("  ");
    }

    [Fact(DisplayName = "Reidratar recusa a aplicabilidade ausente")]
    public void Reidratar_AplicabilidadeAusente_Lanca()
    {
        Action reidratar = () => Reidratada(Declarada() with { Aplicabilidade = new(null, []) });

        reidratar.Should().Throw<ArgumentException>().WithParameterName("declarado");
    }

    [Fact(DisplayName = "Reidratar recusa o Id vazio")]
    public void Reidratar_IdVazio_Lanca()
    {
        Action reidratar = () => DocumentoExigido.Reidratar(Chave, Guid.Empty, Declarada());

        reidratar.Should().Throw<ArgumentException>().WithParameterName("id");
    }

    [Fact(DisplayName = "Criar recusa a fase vazia")]
    public void Criar_FaseVazia_Lanca()
    {
        Action criar = () => DocumentoExigido.Criar(Chave, Declarada() with { Momento = new(Guid.Empty, null, null) });

        criar.Should().Throw<ArgumentException>().WithParameterName("momento");
    }

    [Fact(DisplayName = "Criar recusa o tipo de documento de origem vazio")]
    public void Criar_TipoDeOrigemVazio_Lanca()
    {
        DocumentoExigidoDeclarado declarada = Declarada();

        Action criar = () => DocumentoExigido.Criar(Chave, declarada with { Tipo = declarada.Tipo with { Id = Guid.Empty } });

        criar.Should().Throw<ArgumentException>().WithParameterName("momento");
    }

    [Fact(DisplayName = "Criar recusa o código do tipo de documento em branco")]
    public void Criar_CodigoDoTipoEmBranco_Lanca()
    {
        DocumentoExigidoDeclarado declarada = Declarada();

        Action criar = () => DocumentoExigido.Criar(Chave, declarada with { Tipo = declarada.Tipo with { Codigo = " " } });

        criar.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(" ELIMINA ")]
    [InlineData("elimina")]
    public void Criar_ConsequenciaComEspacosOuMinuscula_Recusa(string consequencia)
    {
        DocumentoExigidoDeclarado declarada = Declarada() with { Indeferimento = new(true, consequencia, []) };

        Result<DocumentoExigido> resultado = DocumentoExigido.Criar(Chave, declarada);

        resultado.Error!.Code.Should().Be("DocumentoExigido.ConsequenciaIndeferimentoInvalida");
        resultado.Error.Message.Should().NotContain(consequencia);
    }

    [Fact]
    public void Reidratar_FatoComEspacos_PreservaReferencia()
    {
        DocumentoExigidoDeclarado declarada = Declarada() with { FatosComprovados = [" COR_RACA "] };

        DocumentoExigido exigencia = DocumentoExigido.Reidratar(Chave, Guid.CreateVersion7(), declarada);

        exigencia.FatosComprovados.Should().Equal(" COR_RACA ");
    }

    [Fact(DisplayName = "Criar vincula as condições e as bases legais ao Id da exigência criada")]
    public void Criar_VinculaOsFilhosAoIdDaExigencia()
    {
        CondicaoGatilho condicao = CondicaoDeCorOuRaca();
        DocumentoExigidoBaseLegal baseLegal = ApoioDosDocumentosExigidosDoProcessoSeletivo.BaseLegalResolvida();

        DocumentoExigido exigencia = DocumentoExigido.Criar(Chave, Declarada() with
        {
            Aplicabilidade = new(Aplicabilidade.Condicional, [condicao]),
            Indeferimento = new(true, null, [baseLegal]),
        }).Value!;

        condicao.DocumentoExigidoId.Should().Be(exigencia.Id);
        baseLegal.DocumentoExigidoId.Should().Be(exigencia.Id);
    }
}
