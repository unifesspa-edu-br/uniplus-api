namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class TipoDeficienciaTests
{
    private const string Codigo = "DEFICIENCIA_VISUAL";
    private const string Nome = "Visual";
    private const string Descricao = "Deficiência relacionada à visão";

    private static Result<TipoDeficiencia> Criar(
        string codigo = Codigo,
        string nome = Nome,
        string descricao = Descricao,
        bool? permanente = null) =>
        TipoDeficiencia.Criar(codigo, nome, descricao, permanente);

    [Fact(DisplayName = "Criar com dados válidos preenche os campos e fica ativo com Guid v7")]
    public void Criar_DadosValidos_Preenche()
    {
        TipoDeficiencia tipo = Criar(descricao: "Deficiência relacionada à visão").Value!;

        tipo.Id.Should().NotBe(Guid.Empty);
        tipo.Codigo.Valor.Should().Be(Codigo);
        tipo.Nome.Should().Be(Nome);
        tipo.Descricao.Should().Be("Deficiência relacionada à visão");
        tipo.Permanente.Should().BeNull("sem classificação explícita, o padrão é 'ainda não classificado'");
        tipo.IsDeleted.Should().BeFalse();
    }

    [Theory(DisplayName = "Código ausente ou em branco é rejeitado (UNI-REQ-0061)")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemCodigo_Falha(string codigo)
    {
        Result<TipoDeficiencia> resultado = Criar(codigo: codigo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDeficienciaErrorCodes.CodigoObrigatorio);
    }

    [Theory(DisplayName = "Código fora do formato canônico é rejeitado")]
    [InlineData("deficiencia_visual")]
    [InlineData("1_DEFICIENCIA")]
    [InlineData("DEFICIENCIA-VISUAL")]
    [InlineData("D")]
    public void Criar_CodigoForaDoFormato_Falha(string codigo)
    {
        Result<TipoDeficiencia> resultado = Criar(codigo: codigo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDeficienciaErrorCodes.CodigoFormatoInvalido);
    }

    [Theory(DisplayName = "Descrição ausente ou em branco é rejeitada (ADR-0116)")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemDescricao_Falha(string descricao)
    {
        Result<TipoDeficiencia> resultado = Criar(descricao: descricao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDeficienciaErrorCodes.DescricaoObrigatoria);
    }

    [Theory(DisplayName = "Nome ausente ou em branco é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemNome_Falha(string nome)
    {
        Result<TipoDeficiencia> resultado = Criar(nome: nome);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDeficienciaErrorCodes.NomeObrigatorio);
    }

    [Fact(DisplayName = "Nome abaixo do tamanho mínimo é rejeitado")]
    public void Criar_NomeCurto_Falha()
    {
        Result<TipoDeficiencia> resultado = Criar(nome: "A");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDeficienciaErrorCodes.NomeTamanho);
    }

    [Fact(DisplayName = "Nome acima do tamanho máximo é rejeitado")]
    public void Criar_NomeLongo_Falha()
    {
        Result<TipoDeficiencia> resultado = Criar(nome: new string('A', 201));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDeficienciaErrorCodes.NomeTamanho);
    }

    [Fact(DisplayName = "Descrição acima do tamanho máximo é rejeitada")]
    public void Criar_DescricaoLonga_Falha()
    {
        Result<TipoDeficiencia> resultado = Criar(descricao: new string('A', 1001));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDeficienciaErrorCodes.DescricaoTamanho);
    }

    [Theory(DisplayName = "Permanente aceita null, true e false (ADR-0116: null = ainda não classificado)")]
    [InlineData(null)]
    [InlineData(true)]
    [InlineData(false)]
    public void Criar_Permanente_Aceita(bool? permanente)
    {
        TipoDeficiencia tipo = Criar(permanente: permanente).Value!;

        tipo.Permanente.Should().Be(permanente);
    }

    [Fact(DisplayName = "Atualizar troca os atributos editáveis e preserva o Id imutável")]
    public void Atualizar_AlteraAtributos_PreservaId()
    {
        TipoDeficiencia tipo = Criar().Value!;
        Guid idOriginal = tipo.Id;

        Result resultado = tipo.Atualizar(
            "DEFICIENCIA_AUDITIVA", "Auditiva", "Deficiência relacionada à audição", permanente: true);

        resultado.IsSuccess.Should().BeTrue();
        tipo.Codigo.Valor.Should().Be("DEFICIENCIA_AUDITIVA");
        tipo.Nome.Should().Be("Auditiva");
        tipo.Descricao.Should().Be("Deficiência relacionada à audição");
        tipo.Permanente.Should().BeTrue();
        tipo.Id.Should().Be(idOriginal, "o Id é imutável mesmo com código e nome editáveis");
    }

    [Fact(DisplayName = "Alterar apenas o nome preserva o código")]
    public void Atualizar_SomenteNome_PreservaCodigo()
    {
        TipoDeficiencia tipo = Criar().Value!;

        Result resultado = tipo.Atualizar(Codigo, "Baixa visão", Descricao);

        resultado.IsSuccess.Should().BeTrue();
        tipo.Nome.Should().Be("Baixa visão");
        tipo.Codigo.Valor.Should().Be(Codigo, "o código é identidade semântica própria, não derivada do nome");
    }

    [Fact(DisplayName = "Atualizar com código fora do formato falha e não altera o estado")]
    public void Atualizar_CodigoInvalido_Falha()
    {
        TipoDeficiencia tipo = Criar().Value!;

        Result resultado = tipo.Atualizar("deficiencia_visual", Nome, Descricao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDeficienciaErrorCodes.CodigoFormatoInvalido);
        tipo.Codigo.Valor.Should().Be(Codigo);
    }

    [Fact(DisplayName = "Atualizar com nome inválido falha e não altera o estado")]
    public void Atualizar_NomeInvalido_Falha()
    {
        TipoDeficiencia tipo = Criar().Value!;

        Result resultado = tipo.Atualizar(Codigo, "A", Descricao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDeficienciaErrorCodes.NomeTamanho);
        tipo.Nome.Should().Be(Nome);
    }

    [Fact(DisplayName = "Atualizar sem descrição falha e não altera o estado")]
    public void Atualizar_SemDescricao_Falha()
    {
        TipoDeficiencia tipo = Criar().Value!;

        Result resultado = tipo.Atualizar(Codigo, Nome, "   ");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDeficienciaErrorCodes.DescricaoObrigatoria);
        tipo.Descricao.Should().Be(Descricao);
    }

    // ── Nulo não lança (ADR-0125) e acumulação ──────────────────────────────────

    [Fact(DisplayName = "Código, nome e descrição nulos não lançam — devolvem as três violações de domínio")]
    public void Criar_TodosOsCamposNulos_NaoLancaEAcumulaAsTresViolacoes()
    {
        Result<TipoDeficiencia> resultado = TipoDeficiencia.Criar(null, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(3);
        resultado.Errors[0].Field.Should().Be("codigo");
        resultado.Errors[0].Error.Code.Should().Be(TipoDeficienciaErrorCodes.CodigoObrigatorio);
        resultado.Errors[1].Field.Should().Be("nome");
        resultado.Errors[1].Error.Code.Should().Be(TipoDeficienciaErrorCodes.NomeObrigatorio);
        resultado.Errors[2].Field.Should().Be("descricao");
        resultado.Errors[2].Error.Code.Should().Be(TipoDeficienciaErrorCodes.DescricaoObrigatoria);
    }

    [Fact(DisplayName = "Código fora do formato e descrição ausente acumulam as duas violações rotuladas")]
    public void Criar_CodigoInvalidoEDescricaoAusente_AcumulaAsDuasViolacoesRotuladas()
    {
        Result<TipoDeficiencia> resultado = TipoDeficiencia.Criar("deficiencia_visual", Nome, "");

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors[0].Field.Should().Be("codigo");
        resultado.Errors[0].Error.Code.Should().Be(TipoDeficienciaErrorCodes.CodigoFormatoInvalido);
        resultado.Errors[1].Field.Should().Be("descricao");
        resultado.Errors[1].Error.Code.Should().Be(TipoDeficienciaErrorCodes.DescricaoObrigatoria);
    }

    [Fact(DisplayName = "Nome curto e descrição longa ao mesmo tempo acumulam as duas violações rotuladas")]
    public void Criar_NomeCurtoEDescricaoLonga_AcumulaAsDuasViolacoesRotuladas()
    {
        Result<TipoDeficiencia> resultado = TipoDeficiencia.Criar(Codigo, "A", new string('b', 1001));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors[0].Field.Should().Be("nome");
        resultado.Errors[0].Error.Code.Should().Be(TipoDeficienciaErrorCodes.NomeTamanho);
        resultado.Errors[1].Field.Should().Be("descricao");
        resultado.Errors[1].Error.Code.Should().Be(TipoDeficienciaErrorCodes.DescricaoTamanho);
    }

    [Fact(DisplayName = "Atualizar com código, nome e descrição inválidos acumula as três violações sem mutar o agregado")]
    public void Atualizar_TodosOsCamposInvalidos_AcumulaAsTresViolacoesSemMutar()
    {
        TipoDeficiencia tipo = Criar().Value!;

        Result resultado = tipo.Atualizar("", "", "   ");

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(3);
        tipo.Codigo.Valor.Should().Be(Codigo, "falha de validação não pode mutar o agregado");
        tipo.Nome.Should().Be(Nome);
        tipo.Descricao.Should().Be(Descricao);
    }
}
