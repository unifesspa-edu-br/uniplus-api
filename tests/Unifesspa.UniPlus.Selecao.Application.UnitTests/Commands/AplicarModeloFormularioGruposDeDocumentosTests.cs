namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDaAplicacaoDoModeloDeFormulario;

/// <summary>
/// Os grupos de documentos do modelo na aplicação ao processo: o grupo vira uma regra de quantidade mínima
/// de itens com os documentos que valem, o grupo sem documento que valha sai da cópia, e a recusa do processo
/// volta com o caminho do grupo no modelo.
/// </summary>
public sealed class AplicarModeloFormularioGruposDeDocumentosTests
{
    private const string CaminhoDoGrupo = "modelo.conjuntoDeDocumentos.gruposDeDocumentos[0]";

    private readonly CenarioDaAplicacaoDoModeloDeFormulario _cenario = new();

    private static string Recusas(Result<AplicacaoDeModeloDto> resultado) =>
        string.Join("; ", resultado.Errors.Select(static e => $"{e.Field}: {e.Error.Code}"));

    private static ModeloFormularioView ModeloComGrupos(params GrupoDeDocumentosDoModeloInput[] grupos) =>
        ModeloComConjunto(ConjuntoDeDocumentosDoModeloBuilder.Vazio().ComGrupos(grupos).Construir());

    private RegraDeComprovacao RegraDoGrupoCopiado()
    {
        RegraDeComprovacao regra = _cenario.Processo.RegrasDeComprovacaoSemGrupo.Single();
        regra.Forma.Should().Be(FormaDeComprovacao.QuantidadeMinimaDeItens);
        return regra;
    }

    [Fact(DisplayName = "O grupo do modelo vira uma regra de quantidade mínima com a consequência própria e um item por documento")]
    public async Task Handle_GrupoDeDocumentos_ViraRegraDeQuantidadeMinima()
    {
        _cenario.FaseDoCronograma("INSCRICAO");
        _cenario.DeclararAtendimentoEspecializado();
        GrupoDeDocumentosDoModeloInput grupo = ConjuntoDeDocumentosDoModeloBuilder.GrupoComIndeferimento(
            2, "RETIRA_O_ACRESCIMO", DocumentoDoModelo("LAUDO_MEDICO", "PCD"), DocumentoDoModelo("CERTIDAO_NASCIMENTO", "LACTANTE"));

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(ModeloComGrupos(grupo));

        resultado.IsSuccess.Should().BeTrue(Recusas(resultado));
        RegraDeComprovacao regra = RegraDoGrupoCopiado();
        regra.QuantidadeMinima.Should().Be(2);
        regra.Consequencia.Should().Be(Unifesspa.UniPlus.Regras.Enums.ConsequenciaDoDescumprimento.RetiraOAcrescimo);
        regra.Itens.Select(static i => i.DocumentoExigido!.TipoDocumentoCodigo).Should().BeEquivalentTo("LAUDO_MEDICO", "CERTIDAO_NASCIMENTO");
    }

    [Fact(DisplayName = "O grupo sem quantidade mínima exige um documento, o padrão do processo")]
    public async Task Handle_GrupoSemQuantidade_ExigeUmDocumento()
    {
        _cenario.FaseDoCronograma("INSCRICAO");
        _cenario.DeclararAtendimentoEspecializado();
        GrupoDeDocumentosDoModeloInput grupo = ConjuntoDeDocumentosDoModeloBuilder.Grupo(
            null, DocumentoDoModelo("LAUDO_MEDICO", "PCD"), DocumentoDoModelo("CERTIDAO_NASCIMENTO", "LACTANTE"));

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(ModeloComGrupos(grupo));

        resultado.IsSuccess.Should().BeTrue(Recusas(resultado));
        RegraDoGrupoCopiado().QuantidadeMinima.Should().Be(RegraDeComprovacao.QuantidadeMinimaPadrao);
    }

    [Fact(DisplayName = "O grupo cujo único documento se multiplica por opção que o processo não oferece sai da cópia, e o grupo que ainda tem documento fica")]
    public async Task Handle_GrupoSemDocumentoQueValha_SaiDaCopia()
    {
        _cenario.FaseDoCronograma("INSCRICAO");
        _cenario.DeclararAtendimentoEspecializado();
        GrupoDeDocumentosDoModeloInput semDocumentoQueValha = ConjuntoDeDocumentosDoModeloBuilder.Grupo(
            1, DocumentoDoModelo("LAUDO_MEDICO", "PCD") with { PorOpcao = new ExigenciaPorOpcaoInput("CONDICAO_ATENDIMENTO", []) });
        GrupoDeDocumentosDoModeloInput comDocumento = ConjuntoDeDocumentosDoModeloBuilder.Grupo(1, DocumentoDoModelo("LAUDO_MEDICO", "PCD"));

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(ModeloComGrupos(semDocumentoQueValha, comDocumento));

        resultado.IsSuccess.Should().BeTrue(Recusas(resultado));
        RegraDoGrupoCopiado().Itens.Should().ContainSingle();
    }

    [Fact(DisplayName = "O grupo com menos documentos que a quantidade mínima é recusado no caminho da quantidade do grupo, e nada é gravado")]
    public async Task Handle_GrupoComPoucosDocumentos_RecusaNoCaminhoDoGrupo()
    {
        _cenario.FaseDoCronograma("INSCRICAO");
        _cenario.DeclararAtendimentoEspecializado();
        GrupoDeDocumentosDoModeloInput grupo = ConjuntoDeDocumentosDoModeloBuilder.Grupo(3, DocumentoDoModelo("LAUDO_MEDICO", "PCD"), DocumentoDoModelo("LAUDO_MEDICO", "LACTANTE"));

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(ModeloComGrupos(grupo));

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ($"{CaminhoDoGrupo}.quantidadeMinima", "RegraDeComprovacao.QuantidadeMinimaDeItensForaDosLimites"));
        await _cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        _cenario.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "Os documentos do grupo em fases diferentes caem na recusa de fase comum do processo")]
    public async Task Handle_GrupoComDocumentosEmFasesDiferentes_RecusaPelaFaseComum()
    {
        _cenario.FasesQueColetamInscricao("INSCRICAO", "RESULTADO_FINAL");
        _cenario.DeclararAtendimentoEspecializado();
        GrupoDeDocumentosDoModeloInput grupo = ConjuntoDeDocumentosDoModeloBuilder.Grupo(
            1, DocumentoDoModelo("LAUDO_MEDICO", "PCD"), DocumentoDoModelo("LAUDO_MEDICO", "LACTANTE") with { FaseCodigo = "RESULTADO_FINAL" });

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(ModeloComGrupos(grupo));

        resultado.Errors.Should().ContainSingle(static e => e.Error.Code == "RegraDeComprovacao.GrupoComFasesDiferentes");
        await _cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O documento do grupo numa fase que o cronograma não tem é recusado no caminho do documento do grupo")]
    public async Task Handle_DocumentoDoGrupoEmFaseInexistente_RecusaNoCaminhoDoDocumento()
    {
        _cenario.FaseDoCronograma("RESULTADO_FINAL");
        _cenario.DeclararAtendimentoEspecializado();
        GrupoDeDocumentosDoModeloInput grupo = ConjuntoDeDocumentosDoModeloBuilder.Grupo(1, DocumentoDoModelo("LAUDO_MEDICO", "PCD"));

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(ModeloComGrupos(grupo));

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = $"{CaminhoDoGrupo}.documentos[0].faseCodigo",
            Error = new { Code = AplicacaoDeModeloErrorCodes.FaseDoDocumentoInexistente },
        });
        await _cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O derivado que só a condição de um documento de grupo cita recebe a regra padrão do catálogo")]
    public async Task Handle_DerivadoCitadoSoPeloDocumentoDoGrupo_CopiaARegraPadrao()
    {
        _cenario.FaseDoCronograma("INSCRICAO");
        _cenario.DeclararAtendimentoEspecializado();
        DocumentoExigidoDoModeloInput laudo = new(
            "INSCRICAO", "LAUDO_MEDICO",
            [[new CondicaoPrecondicaoInput("FLAG", "IGUAL", JsonSerializer.SerializeToElement(true))]],
            new IndeferimentoDoDocumentoDoModeloInput(false, "RETIRA_O_ACRESCIMO", []));

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(Modelo(
            FinalidadeFormulario.Inscricao,
            [.. ConjuntoBasicoDaInscricao.Itens, Item("SOLICITA_ATENDIMENTO_ESPECIALIZADO", 100), Item("QUILOMBOLA", 101)],
            documentos: ConjuntoDeDocumentosDoModeloBuilder.Vazio().ComGrupos(ConjuntoDeDocumentosDoModeloBuilder.Grupo(1, laudo)).Construir()));

        resultado.IsSuccess.Should().BeTrue(Recusas(resultado));
        resultado.Value!.DerivacoesCopiadas.Should().Contain("FLAG");
    }
}
