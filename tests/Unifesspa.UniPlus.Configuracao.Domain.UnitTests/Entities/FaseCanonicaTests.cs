namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.ApoioDosTestesDeFaseCanonica;

public sealed class FaseCanonicaTests
{
    private const int NomeMaximo = 200;
    private const int DescricaoMaximo = 300;
    private const int BaseLegalMaximo = 500;

    // ── Factory válida ─────────────────────────────────────────────────────────

    [Fact(DisplayName = "Fase válida preenche os campos e fica ativa com Guid v7")]
    public void Criar_Valida_Aceita()
    {
        FaseCanonica f = Criar(
            codigo: "AVALIACAO", nome: "Avaliação", sinalizadores: Sinalizadores(agrupaEtapas: true)).Value!;

        f.Id.Should().NotBe(Guid.Empty);
        f.Codigo.Valor.Should().Be("AVALIACAO");
        f.Nome.Should().Be("Avaliação");
        f.Sinalizadores.AgrupaEtapas.Should().BeTrue();
        f.Sinalizadores.PermiteComplementacao.Should().BeFalse();
        f.IsDeleted.Should().BeFalse();
    }

    // ── Formato do código ──────────────────────────────────────────────────────

    [Theory(DisplayName = "Código com minúscula, hífen, dígito ou espaço é rejeitado (formato)")]
    [InlineData("inscricao")]
    [InlineData("RESULTADO-FINAL")]
    [InlineData("FASE2")]
    [InlineData("LISTA ESPERA")]
    public void Criar_CodigoForaDoFormato_Falha(string codigo)
    {
        Result<FaseCanonica> r = Criar(codigo: codigo);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(FaseCanonicaErrorCodes.CodigoFormatoInvalido);
    }

    [Theory(DisplayName = "Código ausente ou em branco é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemCodigo_Falha(string codigo)
    {
        Result<FaseCanonica> r = Criar(codigo: codigo);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(FaseCanonicaErrorCodes.CodigoObrigatorio);
    }

    // ── Domínio canônico ───────────────────────────────────────────────────────

    [Fact(DisplayName = "Código bem-formado fora do conjunto canônico é rejeitado")]
    public void Criar_CodigoForaDoConjuntoCanonico_Falha()
    {
        Result<FaseCanonica> r = Criar(codigo: "ENTREVISTA_FINAL");

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(FaseCanonicaErrorCodes.CodigoForaDoConjuntoCanonico);
    }

    [Theory(DisplayName = "Códigos dentro do conjunto canônico são aceitos")]
    [InlineData("HETEROIDENTIFICACAO")]
    [InlineData("AVALIACAO_BIOPSICOSSOCIAL")]
    [InlineData("HOMOLOGACAO_RESULTADO_FINAL")]
    [InlineData("CHAMADA")]
    [InlineData("SOLICITACAO_ISENCAO")]
    public void Criar_CodigoCanonico_Aceita(string codigo)
    {
        Result<FaseCanonica> r = Criar(codigo: codigo);

        r.IsSuccess.Should().BeTrue();
    }

    // ── Nome ───────────────────────────────────────────────────────────────────

    [Theory(DisplayName = "Nome ausente é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemNome_Falha(string nome)
    {
        Result<FaseCanonica> r = Criar(nome: nome);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(FaseCanonicaErrorCodes.NomeObrigatorio);
    }

    // ── Origem da data ─────────────────────────────────────────────────────────

    [Theory(DisplayName = "Origem da data em domínio é aceita")]
    [InlineData("NO_EDITAL", OrigemDataFase.NoEdital)]
    [InlineData("NA_EXECUCAO", OrigemDataFase.NaExecucao)]
    public void Criar_OrigemDataValida_Aceita(string token, OrigemDataFase esperado)
    {
        FaseCanonica f = Criar(codigo: "MATRICULA", nome: "Matrícula", origemData: token).Value!;

        f.OrigemData.Should().Be(esperado);
    }

    [Theory(DisplayName = "Origem da data ausente é rejeitada (obrigatória)")]
    [InlineData(null)]
    public void Criar_SemOrigemData_Falha(string? origemData)
    {
        Result<FaseCanonica> r = Criar(origemData: origemData);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(FaseCanonicaErrorCodes.OrigemDataObrigatoria);
    }

    [Theory(DisplayName = "Origem da data fora do domínio (inclusive numérico, minúsculo e com espaço) é rejeitada")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("EXTERNA")]
    [InlineData("1")]
    [InlineData("no_edital")]
    [InlineData(" NO_EDITAL")]
    [InlineData("NA_EXECUCAO ")]
    [InlineData("PROPRIA")]
    [InlineData("DELEGADA")]
    public void Criar_OrigemDataInvalida_Falha(string origemData)
    {
        Result<FaseCanonica> r = Criar(origemData: origemData);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(FaseCanonicaErrorCodes.OrigemDataInvalida);
    }

    [Fact(DisplayName = "O código antigo HOMOLOGACAO, sem a qualificação, deixou de ser canônico e é recusado")]
    public void Criar_CodigoAntigoDaHomologacao_ForaDoConjuntoCanonico()
    {
        Result<FaseCanonica> resultado = Criar(codigo: "HOMOLOGACAO", nome: "Homologação das inscrições");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FaseCanonicaErrorCodes.CodigoForaDoConjuntoCanonico);
    }

    // ── Imutabilidade / atualização ────────────────────────────────────────────

    [Fact(DisplayName = "Atualizar troca atributos editáveis mantendo Codigo e Id imutáveis")]
    public void Atualizar_MantemCodigoEId()
    {
        FaseCanonica f = Criar(codigo: "ENSALAMENTO", nome: "Ensalamento").Value!;
        Guid idOriginal = f.Id;

        Result r = f.Atualizar(
            nome: "Ensalamento (novo)", descricao: "Nova descrição", baseLegal: null,
            origemData: "NA_EXECUCAO", sinalizadores: Sinalizadores());

        r.IsSuccess.Should().BeTrue();
        f.Codigo.Valor.Should().Be("ENSALAMENTO", "o código é imutável");
        f.Id.Should().Be(idOriginal, "o Id é imutável");
        f.Nome.Should().Be("Ensalamento (novo)");
        f.OrigemData.Should().Be(OrigemDataFase.NaExecucao);
    }

    // ── Nulo não lança (ADR-0125) e acumulação ──────────────────────────────────

    [Fact(DisplayName = "Código nulo não lança e acumula com nome ausente")]
    public void Criar_CodigoNuloENomeAusente_NaoLancaEAcumulaAsDuasViolacoes()
    {
        Result<FaseCanonica> resultado = FaseCanonica.Criar(
            null, "", null, null, "NO_EDITAL", Sinalizadores());

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors[0].Field.Should().Be("codigo");
        resultado.Errors[0].Error.Code.Should().Be(FaseCanonicaErrorCodes.CodigoObrigatorio);
        resultado.Errors[1].Field.Should().Be("nome");
        resultado.Errors[1].Error.Code.Should().Be(FaseCanonicaErrorCodes.NomeObrigatorio);
    }

    [Fact(DisplayName = "O que não depende do código acumula as violações independentes dele, sem I/O")]
    public void ValidarOQueNaoDependeDoCodigo_DoisCamposInvalidos_AcumulaAsDuasViolacoes()
    {
        Result resultado = FaseCanonica.ValidarOQueNaoDependeDoCodigo(null, null, null, null, Sinalizadores());

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors[0].Field.Should().Be("nome");
        resultado.Errors[1].Field.Should().Be("origemData");
    }

    // ── Acumulação das recusas de campo ────────────────────────────────────────

    [Fact(DisplayName = "As quatro recusas de campo acumulam na mesma resposta, na ordem nome, descrição, base legal e origem da data")]
    public void Criar_QuatroCamposInvalidos_AcumulaAsQuatroRecusasNaOrdem()
    {
        Result<FaseCanonica> resultado = Criar(
            nome: Texto(NomeMaximo + 1),
            descricao: Texto(DescricaoMaximo + 1),
            baseLegal: Texto(BaseLegalMaximo + 1),
            origemData: "EXTERNA");

        resultado.IsFailure.Should().BeTrue();
        Recusas(resultado.Errors).Should().Equal(
            ("nome", FaseCanonicaErrorCodes.NomeTamanho),
            ("descricao", FaseCanonicaErrorCodes.DescricaoTamanho),
            ("baseLegal", FaseCanonicaErrorCodes.BaseLegalTamanho),
            ("origemData", FaseCanonicaErrorCodes.OrigemDataInvalida));
    }

    [Fact(DisplayName = "Atualizar acumula as recusas de campo com a dos sinalizadores na mesma resposta")]
    public void Atualizar_CamposESinalizadorInvalidos_AcumulaTudoNoMesmoLote()
    {
        FaseCanonica fase = Criar(codigo: "INSCRICAO").Value!;

        Result resultado = fase.Atualizar(
            Texto(NomeMaximo + 1), Texto(DescricaoMaximo + 1), Texto(BaseLegalMaximo + 1),
            "EXTERNA", Sinalizadores(agrupaEtapas: true));

        Recusas(resultado.Errors).Should().Equal(
            ("nome", FaseCanonicaErrorCodes.NomeTamanho),
            ("descricao", FaseCanonicaErrorCodes.DescricaoTamanho),
            ("baseLegal", FaseCanonicaErrorCodes.BaseLegalTamanho),
            ("origemData", FaseCanonicaErrorCodes.OrigemDataInvalida),
            ("agrupaEtapas", FaseCanonicaErrorCodes.AgrupaEtapasApenasAvaliacao));
    }

    // ── Limite acima do máximo: campo, código e mensagem ───────────────────────

    [Fact(DisplayName = "Nome em branco recusa com a mensagem de nome obrigatório")]
    public void Criar_NomeEmBranco_RecusaComMensagemDeNomeObrigatorio()
    {
        Result<FaseCanonica> resultado = Criar(nome: "  ");

        UnicaRecusa(resultado).Should().Be(
            ("nome", FaseCanonicaErrorCodes.NomeObrigatorio, "Nome da fase canônica é obrigatório."));
    }

    [Fact(DisplayName = "Nome com 201 caracteres recusa com a mensagem de tamanho do nome")]
    public void Criar_NomeUmAcimaDoLimite_RecusaComMensagemDeTamanho()
    {
        Result<FaseCanonica> resultado = Criar(nome: Texto(NomeMaximo + 1));

        UnicaRecusa(resultado).Should().Be(
            ("nome", FaseCanonicaErrorCodes.NomeTamanho, "Nome da fase deve ter no máximo 200 caracteres."));
    }

    [Fact(DisplayName = "Descrição com 301 caracteres recusa com a mensagem de tamanho da descrição")]
    public void Criar_DescricaoUmAcimaDoLimite_RecusaComMensagemDeTamanho()
    {
        Result<FaseCanonica> resultado = Criar(descricao: Texto(DescricaoMaximo + 1));

        UnicaRecusa(resultado).Should().Be(
            ("descricao", FaseCanonicaErrorCodes.DescricaoTamanho, "Descrição da fase deve ter no máximo 300 caracteres."));
    }

    [Fact(DisplayName = "Base legal com 501 caracteres recusa com a mensagem de tamanho da base legal")]
    public void Criar_BaseLegalUmAcimaDoLimite_RecusaComMensagemDeTamanho()
    {
        Result<FaseCanonica> resultado = Criar(baseLegal: Texto(BaseLegalMaximo + 1));

        UnicaRecusa(resultado).Should().Be(
            ("baseLegal", FaseCanonicaErrorCodes.BaseLegalTamanho, "Base legal da fase deve ter no máximo 500 caracteres."));
    }

    // ── Limite exato ───────────────────────────────────────────────────────────

    [Fact(DisplayName = "Nome, descrição e base legal com exatamente o tamanho máximo são aceitos")]
    public void Criar_CamposNoLimiteExato_Aceita()
    {
        Result<FaseCanonica> resultado = Criar(
            nome: Texto(NomeMaximo),
            descricao: Texto(DescricaoMaximo),
            baseLegal: Texto(BaseLegalMaximo));

        resultado.IsSuccess.Should().BeTrue();
    }

    // ── Espaços nas pontas ─────────────────────────────────────────────────────

    [Fact(DisplayName = "O tamanho é medido depois de remover os espaços das pontas, que não são gravados")]
    public void Criar_CamposNoLimiteComEspacosNasPontas_AceitaEGravaSemOsEspacos()
    {
        FaseCanonica fase = Criar(
            nome: ComEspacos(Texto(NomeMaximo)),
            descricao: ComEspacos(Texto(DescricaoMaximo)),
            baseLegal: ComEspacos(Texto(BaseLegalMaximo))).Value!;

        fase.Nome.Should().Be(Texto(NomeMaximo));
        fase.Descricao.Should().Be(Texto(DescricaoMaximo));
        fase.BaseLegal.Should().Be(Texto(BaseLegalMaximo));
    }

    [Theory(DisplayName = "Descrição e base legal em branco são gravadas como nulas")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Criar_DescricaoEBaseLegalEmBranco_GravaNulo(string? valor)
    {
        FaseCanonica fase = Criar(descricao: valor, baseLegal: valor).Value!;

        fase.Descricao.Should().BeNull();
        fase.BaseLegal.Should().BeNull();
    }

    private static string Texto(int tamanho) => new('a', tamanho);

    private static string ComEspacos(string texto) => $"  {texto}  ";

    private static (string? Campo, string Codigo, string Mensagem) UnicaRecusa(Result<FaseCanonica> resultado)
    {
        FieldError recusa = resultado.Errors.Should().ContainSingle().Subject;

        return (recusa.Field, recusa.Error.Code, recusa.Error.Message);
    }

    private static IEnumerable<(string? Campo, string Codigo)> Recusas(IReadOnlyList<FieldError> erros) =>
        erros.Select(e => (e.Field, e.Error.Code));
}
