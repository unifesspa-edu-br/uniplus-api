namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.ApoioDosTestesDeFaseCanonica;

public sealed class FaseCanonicaCoerenciaDosSinalizadoresTests
{
    // ── Solicitação de isenção (UNI-REQ-0106) ──────────────────────────────────

    [Fact(DisplayName = "Solicitação de isenção não coleta inscrição — são janelas distintas")]
    public void Criar_SolicitacaoIsencao_NaoColetaInscricao_Aceita()
    {
        FaseCanonica f = Criar(
            codigo: FaseCanonicaCatalogo.CodigoSolicitacaoIsencao,
            nome: "Solicitação de isenção").Value!;

        f.Sinalizadores.ColetaSolicitacaoIsencao.Should().BeTrue();
        f.Sinalizadores.ColetaInscricao.Should().BeFalse();
    }

    [Fact(DisplayName = "Fase de isenção sem a marca é recusada — sem ela a janela publica sem validação")]
    public void Criar_SolicitacaoIsencaoSemAMarca_Falha()
    {
        Result<FaseCanonica> r = Criar(
            codigo: FaseCanonicaCatalogo.CodigoSolicitacaoIsencao, sinalizadores: Sinalizadores(coletaSolicitacaoIsencao: false));

        r.IsFailure.Should().BeTrue();
        r.Errors[0].Error.Code.Should().Be(FaseCanonicaErrorCodes.SolicitacaoIsencaoApenasNaFaseDeIsencao);
    }

    [Fact(DisplayName = "A marca de isenção em fase que não é a de isenção é recusada")]
    public void Criar_MarcaDeIsencaoForaDaFaseDeIsencao_Falha()
    {
        Result<FaseCanonica> r = Criar(
            codigo: "HOMOLOGACAO_INSCRICOES", sinalizadores: Sinalizadores(coletaSolicitacaoIsencao: true));

        r.IsFailure.Should().BeTrue();
        r.Errors.Should().ContainSingle();
        r.Errors[0].Field.Should().Be("coletaSolicitacaoIsencao");
        r.Errors[0].Error.Code.Should().Be(FaseCanonicaErrorCodes.SolicitacaoIsencaoApenasNaFaseDeIsencao);
    }

    [Fact(DisplayName = "A fase de isenção que também coleta inscrição é recusada — as janelas são distintas")]
    public void Criar_FaseDeIsencaoQueColetaInscricao_Falha()
    {
        Result<FaseCanonica> r = Criar(
            codigo: FaseCanonicaCatalogo.CodigoSolicitacaoIsencao,
            sinalizadores: Sinalizadores(coletaInscricao: true, coletaSolicitacaoIsencao: true));

        r.IsFailure.Should().BeTrue();
        r.Errors.Should().ContainSingle();
        r.Errors[0].Field.Should().Be("coletaSolicitacaoIsencao");
        r.Errors[0].Error.Code.Should().Be(FaseCanonicaErrorCodes.ColetaInscricaoEIsencaoMutuamenteExclusivas);
    }

    [Fact(DisplayName = "A fase de inscrição coleta inscrição sem a marca de isenção")]
    public void Criar_InscricaoColetandoInscricao_Aceita()
    {
        FaseCanonica f = Criar(codigo: "INSCRICAO", sinalizadores: Sinalizadores(coletaInscricao: true)).Value!;

        f.Sinalizadores.ColetaInscricao.Should().BeTrue();
        f.Sinalizadores.ColetaSolicitacaoIsencao.Should().BeFalse();
    }

    [Fact(DisplayName = "Solicitação de isenção não agrupa etapas — só a avaliação agrupa")]
    public void Criar_SolicitacaoIsencaoAgrupandoEtapas_Falha()
    {
        Result<FaseCanonica> r = Criar(
            codigo: FaseCanonicaCatalogo.CodigoSolicitacaoIsencao,
            sinalizadores: Sinalizadores(agrupaEtapas: true, coletaSolicitacaoIsencao: true));

        r.IsFailure.Should().BeTrue();
        r.Errors[0].Error.Code.Should().Be(FaseCanonicaErrorCodes.AgrupaEtapasApenasAvaliacao);
    }

    [Fact(DisplayName = "Solicitação de isenção não permite complementação documental")]
    public void Criar_SolicitacaoIsencaoComComplementacao_Falha()
    {
        Result<FaseCanonica> r = Criar(
            codigo: FaseCanonicaCatalogo.CodigoSolicitacaoIsencao,
            sinalizadores: Sinalizadores(permiteComplementacao: true, coletaSolicitacaoIsencao: true));

        r.IsFailure.Should().BeTrue();
        r.Errors[0].Error.Code.Should().Be(FaseCanonicaErrorCodes.ComplementacaoApenasFasesPermitidas);
    }

    // ── Coerência agrupa_etapas ⇒ avaliação ────────────────────────────────────

    [Fact(DisplayName = "Agrupar etapas verdadeiro para a fase de avaliação é aceito")]
    public void Criar_AgrupaEtapasAvaliacao_Aceita()
    {
        Result<FaseCanonica> r = Criar(
            codigo: "AVALIACAO", nome: "Avaliação", sinalizadores: Sinalizadores(agrupaEtapas: true));

        r.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Agrupar etapas verdadeiro para fase que não é avaliação é rejeitado")]
    public void Criar_AgrupaEtapasForaDaAvaliacao_Falha()
    {
        Result<FaseCanonica> r = Criar(
            codigo: "HOMOLOGACAO_INSCRICOES", nome: "Homologação", sinalizadores: Sinalizadores(agrupaEtapas: true));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(FaseCanonicaErrorCodes.AgrupaEtapasApenasAvaliacao);
    }

    // ── Coerência permite_complementacao ⇒ fases permitidas ────────────────────

    [Fact(DisplayName = "Permitir complementação verdadeiro em fase permitida é aceito")]
    public void Criar_ComplementacaoFasePermitida_Aceita()
    {
        Result<FaseCanonica> r = Criar(
            codigo: "HOMOLOGACAO_INSCRICOES", nome: "Fase", sinalizadores: Sinalizadores(permiteComplementacao: true));

        r.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Permitir complementação verdadeiro em fase vedada (habilitação) é rejeitado")]
    public void Criar_ComplementacaoFaseVedada_Falha()
    {
        Result<FaseCanonica> r = Criar(
            codigo: "HABILITACAO", nome: "Habilitação", sinalizadores: Sinalizadores(permiteComplementacao: true));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(FaseCanonicaErrorCodes.ComplementacaoApenasFasesPermitidas);
    }

    // ── Atualização e acumulação ───────────────────────────────────────────────

    [Fact(DisplayName = "Atualizar revalida coerência de agrupar etapas contra o código congelado")]
    public void Atualizar_AgrupaEtapasIncoerente_Falha()
    {
        FaseCanonica f = Criar(codigo: "HOMOLOGACAO_INSCRICOES", nome: "Homologação").Value!;

        Result r = f.Atualizar(
            nome: "Homologação", descricao: null, baseLegal: null,
            origemData: "NO_EDITAL", sinalizadores: Sinalizadores(agrupaEtapas: true));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(FaseCanonicaErrorCodes.AgrupaEtapasApenasAvaliacao);
    }

    [Fact(DisplayName = "Código fora do conjunto canônico com AgrupaEtapas=true não soma a coerência dependente do código")]
    public void Criar_CodigoInvalidoComAgrupaEtapas_NaoSomaCoerenciaDependente()
    {
        Result<FaseCanonica> resultado = Criar(
            codigo: "ENTREVISTA_FINAL", sinalizadores: Sinalizadores(agrupaEtapas: true));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle(
            "a coerência AgrupaEtapasApenasAvaliacao depende de um código já confiável — "
            + "com o código inválido, relatá-la seria um segundo erro derivado do primeiro");
        resultado.Errors[0].Error.Code.Should().Be(FaseCanonicaErrorCodes.CodigoForaDoConjuntoCanonico);
    }

    [Fact(DisplayName = "Todas as marcas incoerentes acumulam as quatro recusas na ordem: agrupamento, complementação, marca e exclusividade")]
    public void Criar_QuatroMarcasIncoerentes_AcumulaAsQuatroRecusasNaOrdem()
    {
        Result<FaseCanonica> resultado = CriarHabilitacaoComAsQuatroMarcasLigadas();

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ("agrupaEtapas", FaseCanonicaErrorCodes.AgrupaEtapasApenasAvaliacao),
            ("permiteComplementacao", FaseCanonicaErrorCodes.ComplementacaoApenasFasesPermitidas),
            ("coletaSolicitacaoIsencao", FaseCanonicaErrorCodes.SolicitacaoIsencaoApenasNaFaseDeIsencao),
            ("coletaSolicitacaoIsencao", FaseCanonicaErrorCodes.ColetaInscricaoEIsencaoMutuamenteExclusivas));
    }

    [Fact(DisplayName = "Cada uma das quatro recusas traz a mensagem da sua regra")]
    public void Criar_QuatroMarcasIncoerentes_CadaRecusaTrazASuaMensagem()
    {
        Result<FaseCanonica> resultado = CriarHabilitacaoComAsQuatroMarcasLigadas();

        resultado.Errors.Select(e => e.Error.Message).Should().Equal(
            "Apenas a fase de avaliação agrupa etapas pontuadas.",
            "Complementação documental só é permitida na fase de homologação das inscrições.",
            "A marca de solicitação de isenção pertence à fase de isenção, e só a ela.",
            "Uma fase não coleta inscrição e solicitação de isenção ao mesmo tempo — são períodos distintos, cada um com o seu prazo.");
    }

    [Theory(DisplayName = "Código recusado com inscrição e isenção ligadas acumula a recusa do código e a exclusividade, nesta ordem")]
    [InlineData("ENTREVISTA_FINAL", FaseCanonicaErrorCodes.CodigoForaDoConjuntoCanonico)]
    [InlineData("inscricao", FaseCanonicaErrorCodes.CodigoFormatoInvalido)]
    public void Criar_CodigoRecusadoComInscricaoEIsencaoLigadas_AcumulaCodigoEExclusividade(string codigo, string recusaDoCodigo)
    {
        Result<FaseCanonica> resultado = Criar(
            codigo: codigo, sinalizadores: Sinalizadores(coletaInscricao: true, coletaSolicitacaoIsencao: true));

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ("codigo", recusaDoCodigo),
            ("coletaSolicitacaoIsencao", FaseCanonicaErrorCodes.ColetaInscricaoEIsencaoMutuamenteExclusivas));
    }

    [Fact(DisplayName = "Código recusado só com a marca de isenção recusa apenas o código — a marca não se confere contra código inválido")]
    public void Criar_CodigoRecusadoSoComAMarcaDeIsencao_RecusaApenasOCodigo()
    {
        Result<FaseCanonica> resultado = Criar(
            codigo: "ENTREVISTA_FINAL", sinalizadores: Sinalizadores(coletaSolicitacaoIsencao: true));

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ("codigo", FaseCanonicaErrorCodes.CodigoForaDoConjuntoCanonico));
    }

    [Fact(DisplayName = "Atualizar a fase de inscrição com as duas marcas recusa a marca de isenção e a exclusividade, nesta ordem")]
    public void Atualizar_InscricaoComInscricaoEIsencaoLigadas_AcumulaMarcaEExclusividade()
    {
        FaseCanonica f = Criar(codigo: "INSCRICAO", sinalizadores: Sinalizadores(coletaInscricao: true)).Value!;

        Result r = f.Atualizar(
            nome: "Inscrição", descricao: null, baseLegal: null, origemData: "NO_EDITAL",
            sinalizadores: Sinalizadores(coletaInscricao: true, coletaSolicitacaoIsencao: true));

        r.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ("coletaSolicitacaoIsencao", FaseCanonicaErrorCodes.SolicitacaoIsencaoApenasNaFaseDeIsencao),
            ("coletaSolicitacaoIsencao", FaseCanonicaErrorCodes.ColetaInscricaoEIsencaoMutuamenteExclusivas));
    }

    [Fact(DisplayName = "Atualizar recusado não troca nenhum sinalizador da fase")]
    public void Atualizar_Recusado_MantemOsSinalizadoresGravados()
    {
        FaseCanonica f = Criar(codigo: "INSCRICAO", sinalizadores: Sinalizadores(coletaInscricao: true)).Value!;
        SinalizadoresDaFase gravados = f.Sinalizadores;

        f.Atualizar("Inscrição", null, null, "NO_EDITAL", Sinalizadores(agrupaEtapas: true));

        f.Sinalizadores.Should().BeSameAs(gravados);
    }

    [Fact(DisplayName = "Atualizar com os mesmos sinalizadores mantém o valor gravado, para a edição não sujar a linha")]
    public void Atualizar_MesmosSinalizadores_MantemAInstanciaGravada()
    {
        FaseCanonica f = Criar(codigo: "INSCRICAO", sinalizadores: Sinalizadores(coletaInscricao: true)).Value!;
        SinalizadoresDaFase gravados = f.Sinalizadores;

        f.Atualizar("Inscrição", null, null, "NO_EDITAL", Sinalizadores(coletaInscricao: true));

        f.Sinalizadores.Should().BeSameAs(gravados);
    }

    // ── O que não depende do código ────────────────────────────────────────────

    [Fact(DisplayName = "Nome ausente com inscrição e isenção ligadas recusa o nome e a exclusividade, nesta ordem")]
    public void ValidarOQueNaoDependeDoCodigo_NomeAusenteEAsDuasMarcas_AcumulaNomeEExclusividade()
    {
        Result r = FaseCanonica.ValidarOQueNaoDependeDoCodigo(
            nome: null, descricao: null, baseLegal: null, origemData: "NO_EDITAL",
            sinalizadores: Sinalizadores(coletaInscricao: true, coletaSolicitacaoIsencao: true));

        r.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ("nome", FaseCanonicaErrorCodes.NomeObrigatorio),
            ("coletaSolicitacaoIsencao", FaseCanonicaErrorCodes.ColetaInscricaoEIsencaoMutuamenteExclusivas));
    }

    [Fact(DisplayName = "A marca de isenção sozinha passa: conferi-la exige o código gravado")]
    public void ValidarOQueNaoDependeDoCodigo_SoAMarcaDeIsencao_Aceita()
    {
        Result r = FaseCanonica.ValidarOQueNaoDependeDoCodigo(
            "Fase", null, null, "NO_EDITAL", Sinalizadores(coletaSolicitacaoIsencao: true));

        r.IsSuccess.Should().BeTrue();
    }

    private static Result<FaseCanonica> CriarHabilitacaoComAsQuatroMarcasLigadas() =>
        Criar(
            codigo: "HABILITACAO",
            sinalizadores: Sinalizadores(
                agrupaEtapas: true, permiteComplementacao: true, coletaInscricao: true, coletaSolicitacaoIsencao: true));
}
