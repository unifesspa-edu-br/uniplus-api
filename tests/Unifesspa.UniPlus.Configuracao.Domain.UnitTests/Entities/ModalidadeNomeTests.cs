namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.ApoioDaModalidade;

/// <summary>Nome obrigatório da <see cref="Modalidade"/>: ausência, tamanho, normalização, acúmulo com outras recusas e edição.</summary>
public sealed class ModalidadeNomeTests
{
    [Theory(DisplayName = "Nome ausente, vazio ou só com espaços é recusado como obrigatório")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_NomeSemConteudo_RecusaComoObrigatorio(string? nome)
    {
        Result<Modalidade> resultado = Criar(ajuste: d => d with { Nome = nome });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("nome");
        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.NomeObrigatorio);
    }

    [Fact(DisplayName = "Nome com 200 caracteres é aceito")]
    public void Criar_NomeComDuzentosCaracteres_Aceita()
    {
        Modalidade modalidade = Criar(ajuste: d => d with { Nome = new string('a', 200) }).ValorEsperado();

        modalidade.Nome.Should().HaveLength(200);
    }

    [Fact(DisplayName = "Nome com 201 caracteres é recusado pelo tamanho")]
    public void Criar_NomeComDuzentosEUmCaracteres_RecusaPeloTamanho()
    {
        Result<Modalidade> resultado = Criar(ajuste: d => d with { Nome = new string('a', 201) });

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("nome");
        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.NomeTamanho);
    }

    [Fact(DisplayName = "Os espaços do início e do fim do nome são aparados antes de contar o tamanho")]
    public void Criar_NomeComEspacosNasPontas_ApareiaAntesDeContar()
    {
        Modalidade modalidade = Criar(ajuste: d => d with { Nome = "  " + new string('a', 200) + "  " }).ValorEsperado();

        modalidade.Nome.Should().Be(new string('a', 200));
    }

    [Fact(DisplayName = "Nome ausente e código em formato inválido acumulam as duas recusas")]
    public void Criar_NomeAusenteECodigoInvalido_AcumulaAsDuasRecusas()
    {
        Result<Modalidade> resultado = Criar("lb-ppi", d => d with { Nome = null });

        resultado.Errors.Select(erro => erro.Field).Should().Equal("codigo", "nome");
        resultado.Errors.Select(erro => erro.Error.Code).Should().Equal(
            ModalidadeErrorCodes.CodigoFormatoInvalido, ModalidadeErrorCodes.NomeObrigatorio);
    }

    [Fact(DisplayName = "A recusa do nome vem antes da recusa da descrição")]
    public void Criar_NomeAusenteEDescricaoLonga_RecusaONomePrimeiro()
    {
        Result<Modalidade> resultado = Criar(ajuste: d => d with { Nome = null, Descricao = new string('a', 301) });

        resultado.Errors.Select(erro => erro.Field).Should().Equal("nome", "descricao");
    }

    [Fact(DisplayName = "Atualizar sem nome é recusado e não altera a modalidade")]
    public void Atualizar_SemNome_RecusaENaoMuta()
    {
        Modalidade modalidade = Criar("PSIQ_REGIONAL").ValorEsperado();

        Result resultado = modalidade.Atualizar(Declarar(d => d with { Nome = " ", Descricao = "Outra descrição" }));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.NomeObrigatorio);
        modalidade.Nome.Should().Be("Modalidade de teste");
        modalidade.Descricao.Should().BeNull();
    }

    [Fact(DisplayName = "Atualizar troca o nome já aparado")]
    public void Atualizar_NomeNovo_TrocaOApareado()
    {
        Modalidade modalidade = Criar("PSIQ_REGIONAL").ValorEsperado();

        Result resultado = modalidade.Atualizar(Declarar(d => d with { Nome = "  Nome novo  " }));

        resultado.IsSuccess.Should().BeTrue();
        modalidade.Nome.Should().Be("Nome novo");
    }

    [Fact(DisplayName = "O nome é editável numa modalidade do catálogo legal fixo")]
    public void Atualizar_LegalFixaComNomeNovoEEstruturaIgual_Aceita()
    {
        Modalidade modalidade = CotaFederal();

        Result resultado = modalidade.Atualizar(Declarar(d => d.ComoCotaReservada() with
        {
            Nome = "Nome novo",
            Descricao = "Cota",
            BaseLegal = "Lei 12.711/2012",
        }));

        resultado.IsSuccess.Should().BeTrue();
        modalidade.Nome.Should().Be("Nome novo");
    }

    [Fact(DisplayName = "Mudar o nome não libera a alteração da estrutura de uma modalidade do catálogo legal fixo")]
    public void Atualizar_LegalFixaComNomeNovoEEstruturaDiferente_RecusaPelaEstrutura()
    {
        Modalidade modalidade = CotaFederal();

        Result resultado = modalidade.Atualizar(Declarar(d => d with { Nome = "Nome novo" }));

        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.EstruturaProtegidaNaoEditavel);
        modalidade.Nome.Should().Be("Modalidade de teste");
    }
}
