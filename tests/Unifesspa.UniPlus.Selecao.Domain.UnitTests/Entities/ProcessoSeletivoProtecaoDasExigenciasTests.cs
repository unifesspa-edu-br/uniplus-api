namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A exigência que comprova um fato não pode usar tipo de documento menos protegido que o fato,
/// na classificação que o fato tem congelada no processo: o arquivo carrega o dado do fato.
/// </summary>
public sealed class ProcessoSeletivoProtecaoDasExigenciasTests
{
    private const string FatoSensivel = "TIPO_DEFICIENCIA";
    private const string FatoPessoal = "RENDA_PER_CAPITA";

    private static readonly FormatosPermitidos Qualquer = FormatosPermitidos.Criar(true, null).Value!;

    private static FaseCronograma Inscricao() => FaseCronograma.Criar(
        1, Guid.CreateVersion7(), "INSCRICAO", OrigemDataFase.NoEdital,
        agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: true, coletaSolicitacaoIsencao: false,
        inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero),
        produtos: [ProdutoDaFase.Criar("INSCRICAO", PapelProdutoFase.Definitivo)],
        faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;

    private static FatoColetado Fato(string codigo, int ordem, string classificacao) =>
        FatoColetado.Criar(codigo, ordem, codigo, TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, classificacaoProtecao: classificacao).Value!;

    private static DocumentoExigido Exigencia(
        Guid faseId, string tipo, ClassificacaoDeProtecao protecao, string[]? comprova = null, CondicaoGatilho[]? gatilho = null) =>
        DocumentoExigido.Criar(
            faseId, Guid.CreateVersion7(), tipo, tipo, "SAUDE", ProtecaoDoDocumentoDeTeste.De(protecao),
            gatilho is { Length: > 0 } ? Aplicabilidade.Condicional : Aplicabilidade.Geral,
            obrigatorio: true, consequenciaIndeferimento: null, gatilho ?? [], [], null, Qualquer, null,
            finalidade: FinalidadeFormulario.Inscricao, fatosComprovados: comprova).Value!;

    private static ProcessoSeletivo Processo(params Func<Guid, DocumentoExigido>[] exigencias)
    {
        FaseCronograma inscricao = Inscricao();
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(fase: inscricao);
        processo.DefinirItens([Fato(FatoSensivel, 0, "SENSIVEL")]).IsSuccess.Should().BeTrue();
        processo.DefinirDocumentosExigidos(
            [.. exigencias.Select((e, i) => NoExigencia.CriarFolha(e(inscricao.Id), i).Value!)], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        return processo;
    }

    /// <summary>O catálogo como a publicação o lê: a renda per capita é derivada, e nenhum formulário a coleta.</summary>
    private static readonly Dictionary<string, ClassificacaoDeProtecao> Catalogo = new(StringComparer.Ordinal)
    {
        [FatoSensivel] = ClassificacaoDeProtecao.Sensivel,
        [FatoPessoal] = ClassificacaoDeProtecao.Pessoal,
    };

    private static readonly ProtecaoVigenteDasExigencias ComCatalogo = new(Catalogo, new Dictionary<Guid, ProtecaoDoDocumento>());

    private static DomainError? Pendencia(ProcessoSeletivo processo, ProtecaoVigenteDasExigencias? protecao = null) =>
        processo.PendenciaPreCanonicalizacao(FatosDeModalidadeDeTeste.DoCatalogo, protecao);

    private static IEnumerable<string> VermelhosDoPainel(ProcessoSeletivo processo, ProtecaoVigenteDasExigencias? protecao) =>
        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum, protecao)
            .Where(i => !i.Ok).Select(i => i.Codigo);

    [Fact(DisplayName = "Exigência pessoal que comprova fato sensível é recusada, citando o tipo e o fato")]
    public void TipoPessoalComprovandoFatoSensivel_Recusa()
    {
        ProcessoSeletivo processo = Processo(f => Exigencia(f, "ATESTADO_SIMPLES", ClassificacaoDeProtecao.Pessoal, [FatoSensivel]));

        DomainError? pendencia = Pendencia(processo);

        pendencia.Should().NotBeNull();
        pendencia!.Code.Should().Be(ProcessoSeletivoErrorCodes.ExigenciaMenosProtegidaQueFatoComprovado);
        pendencia.Message.Should().Contain("ATESTADO_SIMPLES").And.Contain(FatoSensivel);
        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum)
            .Should().Contain(i => !i.Ok && i.Codigo == "exigencia_menos_protegida_que_fato_comprovado");
    }

    [Fact(DisplayName = "Exigência tão protegida quanto o fato comprovado é aceita")]
    public void TipoSensivelComprovandoFatoSensivel_Aceita()
    {
        ProcessoSeletivo processo = Processo(f => Exigencia(f, "LAUDO_MEDICO", ClassificacaoDeProtecao.Sensivel, [FatoSensivel]));

        Pendencia(processo).Should().BeNull();
    }

    [Fact(DisplayName = "Exigência que comprova vários fatos é conferida contra o mais protegido")]
    public void VariosFatos_OMaisProtegidoDecide()
    {
        ProcessoSeletivo processo = Processo(f => Exigencia(f, "DECLARACAO_RENDA_E_SAUDE", ClassificacaoDeProtecao.Pessoal, [FatoPessoal, FatoSensivel]));

        Pendencia(processo, ComCatalogo)!.Message.Should().Contain(FatoSensivel).And.NotContain(FatoPessoal);
        VermelhosDoPainel(processo, ComCatalogo).Should().Contain("exigencia_menos_protegida_que_fato_comprovado");
    }

    [Fact(DisplayName = "O fato que só aciona a exigência pelo gatilho não entra na conferência")]
    public void FatoSoDoGatilho_NaoEntra()
    {
        CondicaoGatilho gatilho = CondicaoGatilho.Criar(0, FatoSensivel, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!;
        ProcessoSeletivo processo = Processo(f => Exigencia(f, "COMPROVANTE_RESIDENCIA", ClassificacaoDeProtecao.Pessoal, gatilho: [gatilho]));

        Pendencia(processo).Should().BeNull();
    }

    [Fact(DisplayName = "Exigência sem fato comprovado vale pela classificação do tipo, que fica congelada")]
    public void SemFatoComprovado_AceitaECongelaOTipo()
    {
        ProcessoSeletivo processo = Processo(f => Exigencia(f, "DOCUMENTO_IDENTIDADE", ClassificacaoDeProtecao.Identificador));

        Pendencia(processo).Should().BeNull();
        processo.DocumentosExigidos.Single().ClassificacaoProtecao.Should().Be(ClassificacaoDeProtecao.Identificador);
    }

    [Fact(DisplayName = "Fato comprovado que o processo não coleta e o catálogo não conhece é recusado")]
    public void FatoForaDoProcesso_Recusa()
    {
        ProcessoSeletivo processo = Processo(f => Exigencia(f, "LAUDO_MEDICO", ClassificacaoDeProtecao.Sensivel, ["FATO_QUE_NAO_EXISTE"]));

        Pendencia(processo, ComCatalogo)!.Code.Should().Be(DocumentoExigido.FatoComprovadoForaDoProcesso);
        VermelhosDoPainel(processo, ComCatalogo).Should()
            .Contain("exigencia_fato_comprovado_fora_do_processo").And.NotContain("exigencia_menos_protegida_que_fato_comprovado");
    }

    [Fact(DisplayName = "Fato derivado do catálogo, que nenhum formulário coleta, pode ser comprovado")]
    public void FatoDerivadoDoCatalogo_Aceita()
    {
        ProcessoSeletivo processo = Processo(f => Exigencia(f, "COMPROVANTE_RENDA", ClassificacaoDeProtecao.Pessoal, [FatoPessoal]));

        Pendencia(processo, ComCatalogo).Should().BeNull();
    }

    [Fact(DisplayName = "Fatos comprovados repetidos ou em branco são recusados na forma da exigência")]
    public void FatosComprovadosInvalidos_RecusaNaForma()
    {
        Result<DocumentoExigido> repetido = DocumentoExigido.Criar(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "LAUDO_MEDICO", "Laudo", "SAUDE", ProtecaoDoDocumentoDeTeste.Pessoal, Aplicabilidade.Geral,
            obrigatorio: true, consequenciaIndeferimento: null, [], [], null, Qualquer, null, fatosComprovados: [FatoSensivel, FatoSensivel]);

        repetido.IsFailure.Should().BeTrue();
        repetido.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(DocumentoExigido.FatosComprovadosInvalidos);
    }

    [Fact(DisplayName = "A proteção efetiva da exigência, vinda do contexto, é a que a conferência usa, e AplicarProtecaoVigente a congela")]
    public void ProtecaoEfetiva_ConferenciaEAplicacao()
    {
        ProcessoSeletivo processo = Processo(f => Exigencia(f, "LAUDO_MEDICO", ClassificacaoDeProtecao.Sensivel, [FatoSensivel]));
        Guid exigenciaId = processo.DocumentosExigidos.Single().Id;
        Pendencia(processo).Should().BeNull();
        ProtecaoVigenteDasExigencias rebaixada = new(Catalogo, new Dictionary<Guid, ProtecaoDoDocumento> { [exigenciaId] = ProtecaoDoDocumentoDeTeste.De(ClassificacaoDeProtecao.Pessoal) });

        Pendencia(processo, rebaixada)!.Code.Should().Be(ProcessoSeletivoErrorCodes.ExigenciaMenosProtegidaQueFatoComprovado);
        VermelhosDoPainel(processo, rebaixada).Should().Contain("exigencia_menos_protegida_que_fato_comprovado");

        processo.AplicarProtecaoVigente(rebaixada);
        processo.DocumentosExigidos.Single().ClassificacaoProtecao.Should().Be(ClassificacaoDeProtecao.Pessoal);
    }
}
