namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CondicaoAtendimentoEspecializadoTests
{
    private const string Codigo = "DISLEXIA";
    private const string Nome = "Dislexia";

    private static Result<CondicaoAtendimentoEspecializado> Criar(
        string codigo = Codigo,
        string nome = Nome,
        string? descricao = null) =>
        CondicaoAtendimentoEspecializado.Criar(codigo, nome, descricao);

    [Fact(DisplayName = "Criar com dados válidos preenche os campos e fica ativa com Guid v7")]
    public void Criar_DadosValidos_Preenche()
    {
        CondicaoAtendimentoEspecializado condicao = Criar(descricao: "Transtorno específico de aprendizagem").Value!;

        condicao.Id.Should().NotBe(Guid.Empty);
        condicao.Codigo.Valor.Should().Be(Codigo);
        condicao.Nome.Should().Be(Nome);
        condicao.Descricao.Should().Be("Transtorno específico de aprendizagem");
        condicao.IsDeleted.Should().BeFalse();
    }

    [Fact(DisplayName = "Criar sem descrição é aceito")]
    public void Criar_SemDescricao_Aceita()
    {
        CondicaoAtendimentoEspecializado condicao = Criar(descricao: null).Value!;

        condicao.Descricao.Should().BeNull();
    }

    [Theory(DisplayName = "Código ausente ou em branco é rejeitado (CodigoObrigatorio)")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemCodigo_Falha(string codigo)
    {
        Result<CondicaoAtendimentoEspecializado> resultado = Criar(codigo: codigo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CondicaoAtendimentoErrorCodes.CodigoObrigatorio);
    }

    [Theory(DisplayName = "Código fora do formato fechado é rejeitado (CodigoFormatoInvalido)")]
    [InlineData("dislexia")]      // minúsculas
    [InlineData("1PCD")]          // começa com dígito
    [InlineData("_PCD")]          // começa com sublinhado
    [InlineData("P")]             // curto demais (< 2)
    [InlineData("PCD-A")]         // hífen não permitido
    [InlineData("PCD A")]         // espaço não permitido
    public void Criar_CodigoFormatoInvalido_Falha(string codigo)
    {
        Result<CondicaoAtendimentoEspecializado> resultado = Criar(codigo: codigo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CondicaoAtendimentoErrorCodes.CodigoFormatoInvalido);
    }

    [Fact(DisplayName = "Código acima de 50 caracteres é rejeitado (CodigoFormatoInvalido)")]
    public void Criar_CodigoLongo_Falha()
    {
        Result<CondicaoAtendimentoEspecializado> resultado = Criar(codigo: "A" + new string('B', 50));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CondicaoAtendimentoErrorCodes.CodigoFormatoInvalido);
    }

    [Theory(DisplayName = "Nome ausente ou em branco é rejeitado (NomeObrigatorio)")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemNome_Falha(string nome)
    {
        Result<CondicaoAtendimentoEspecializado> resultado = Criar(nome: nome);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CondicaoAtendimentoErrorCodes.NomeObrigatorio);
    }

    [Theory(DisplayName = "Nome com 1 caractere ou acima de 200 é rejeitado (NomeTamanho)")]
    [InlineData("A")]
    [InlineData("longo")]
    public void Criar_NomeTamanhoInvalido_Falha(string variante)
    {
        string nome = variante == "longo" ? new string('N', 201) : variante;

        Result<CondicaoAtendimentoEspecializado> resultado = Criar(nome: nome);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CondicaoAtendimentoErrorCodes.NomeTamanho);
    }

    [Fact(DisplayName = "Descrição acima de 1000 caracteres é rejeitada (DescricaoTamanho)")]
    public void Criar_DescricaoLonga_Falha()
    {
        Result<CondicaoAtendimentoEspecializado> resultado = Criar(descricao: new string('D', 1001));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CondicaoAtendimentoErrorCodes.DescricaoTamanho);
    }

    [Fact(DisplayName = "Atualizar troca os atributos editáveis, inclusive o código (não reservado)")]
    public void Atualizar_AlteraAtributos_InclusiveCodigo()
    {
        CondicaoAtendimentoEspecializado condicao = Criar(codigo: "LACTANTE", nome: "Lactante").Value!;
        Guid idOriginal = condicao.Id;

        Result resultado = condicao.Atualizar("LACTANTE_GESTANTE", "Lactante ou gestante", "Atendimento ampliado");

        resultado.IsSuccess.Should().BeTrue();
        condicao.Codigo.Valor.Should().Be("LACTANTE_GESTANTE");
        condicao.Nome.Should().Be("Lactante ou gestante");
        condicao.Id.Should().Be(idOriginal, "o Id é imutável mesmo com o código editável");
    }

    [Fact(DisplayName = "Atualizar bloqueia renomear o código reservado PCD (CodigoProtegidoNaoEditavel)")]
    public void Atualizar_RenomearPcd_Bloqueia()
    {
        CondicaoAtendimentoEspecializado pcd = Criar(codigo: CodigoCondicao.Pcd, nome: "Pessoa com deficiência").Value!;

        Result resultado = pcd.Atualizar("OUTRA_CONDICAO", "Outra condição", null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CondicaoAtendimentoErrorCodes.CodigoProtegidoNaoEditavel);
        pcd.Codigo.Valor.Should().Be(CodigoCondicao.Pcd, "o código reservado permanece intacto");
    }

    [Fact(DisplayName = "Atualizar o nome/descrição mantendo o código PCD é permitido")]
    public void Atualizar_PcdMantendoCodigo_Aceita()
    {
        CondicaoAtendimentoEspecializado pcd = Criar(codigo: CodigoCondicao.Pcd, nome: "Pessoa com deficiência").Value!;

        Result resultado = pcd.Atualizar(CodigoCondicao.Pcd, "Pessoa com deficiência (PcD)", "Conforme LBI");

        resultado.IsSuccess.Should().BeTrue();
        pcd.Nome.Should().Be("Pessoa com deficiência (PcD)");
        pcd.Codigo.Valor.Should().Be(CodigoCondicao.Pcd);
    }
}
