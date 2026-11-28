namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DefinirDistribuicaoVagasCommandValidatorTests
{
    private readonly DefinirDistribuicaoVagasCommandValidator _validator = new();

    private static ConfiguracaoDistribuicaoVagasInput ItemValido() => new(
        Guid.CreateVersion7(), 50, 0.5m, "DISTRIBUICAO_LEI_12711", "v1", null, null, null, [Guid.CreateVersion7()], []);

    [Fact(DisplayName = "Lista de distribuição vazia atravessa a validação de forma — quem recusa é o domínio")]
    public void Validar_ListaVazia_AtravessaOValidator()
    {
        DefinirDistribuicaoVagasCommand command = new(Guid.CreateVersion7(), []);

        ValidationResult resultado = _validator.Validate(command);

        resultado.Errors.Should().NotContain(
            e => e.PropertyName == nameof(DefinirDistribuicaoVagasCommand.DistribuicaoVagas),
            "ProcessoSeletivo.DistribuicaoVagasVazia é a causa nomeada, e recusar aqui a tornava inalcançável");
    }

    [Fact(DisplayName = "Command válido não gera erros")]
    public void Validar_Valido_SemErros()
    {
        DefinirDistribuicaoVagasCommand command = new(Guid.CreateVersion7(), [ItemValido()]);

        ValidationResult resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "Validator passa com vagas ofertadas base não positivo — a rejeição é do agregado (ConfiguracaoDistribuicaoVagas.Criar, ADR-0125)")]
    [InlineData(0)]
    [InlineData(-1)]
    public void Aceita_VagasOfertadasBaseInvalidoNoValidator(int vagasOfertadasBase)
    {
        ConfiguracaoDistribuicaoVagasInput item = ItemValido() with { VagasOfertadasBase = vagasOfertadasBase };
        DefinirDistribuicaoVagasCommand command = new(Guid.CreateVersion7(), [item]);

        ValidationResult resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "Validator passa com percentual da reserva legal fora de [0,5; 1] — a rejeição é do agregado (ConfiguracaoDistribuicaoVagas.Criar, ADR-0125)")]
    [InlineData(0.49)]
    [InlineData(1.01)]
    public void Aceita_PercentualDaReservaLegalForaDoLimiteNoValidator(double pr)
    {
        ConfiguracaoDistribuicaoVagasInput item = ItemValido() with { PercentualDaReservaLegal = (decimal)pr };
        DefinirDistribuicaoVagasCommand command = new(Guid.CreateVersion7(), [item]);

        ValidationResult resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Aceita_PercentualDaReservaLegalComEscalaExcessiva()
    {
        ConfiguracaoDistribuicaoVagasInput item = ItemValido() with { PercentualDaReservaLegal = 0.75001m };
        DefinirDistribuicaoVagasCommand command = new(Guid.CreateVersion7(), [item]);

        ValidationResult resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Validator passa com lista de modalidades vazia — a rejeição é do agregado (ConfiguracaoDistribuicaoVagas.Criar, ADR-0125)")]
    public void Aceita_ModalidadeIdsVazioNoValidator()
    {
        ConfiguracaoDistribuicaoVagasInput item = ItemValido() with { ModalidadeIds = [] };
        DefinirDistribuicaoVagasCommand command = new(Guid.CreateVersion7(), [item]);

        ValidationResult resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Validator deixa ModalidadeIds nulo, sem estourar, para o domínio recusar como obrigatório")]
    public void Aceita_ModalidadeIdsNulo_ARecusaEDoDominio()
    {
        ConfiguracaoDistribuicaoVagasInput item = ItemValido() with { ModalidadeIds = null };
        DefinirDistribuicaoVagasCommand command = new(Guid.CreateVersion7(), [item]);

        ValidationResult resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Item nulo na lista gera erro, sem lançar exceção")]
    public void Validar_ItemNulo_GeraErro()
    {
        DefinirDistribuicaoVagasCommand command = new(Guid.CreateVersion7(), [null!]);

        ValidationResult resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeFalse();
    }

    [Theory(DisplayName = "Código e versão da regra de distribuição ausentes, nulos, vazios ou em branco são recusados no campo da configuração")]
    [InlineData(null, "v1", "RegraDistribuicaoCodigo")]
    [InlineData("", "v1", "RegraDistribuicaoCodigo")]
    [InlineData("DISTRIBUICAO_LEI_12711", null, "RegraDistribuicaoVersao")]
    [InlineData("DISTRIBUICAO_LEI_12711", "  ", "RegraDistribuicaoVersao")]
    public void Validar_RegraDeDistribuicaoAusente_RecusaOCampo(string? codigo, string? versao, string campo)
    {
        ConfiguracaoDistribuicaoVagasInput item = ItemValido() with { RegraDistribuicaoCodigo = codigo, RegraDistribuicaoVersao = versao };
        DefinirDistribuicaoVagasCommand command = new(Guid.CreateVersion7(), [item]);

        ValidationResult resultado = _validator.Validate(command);

        resultado.Errors.Should().ContainSingle().Which.PropertyName.Should().EndWith(campo);
    }

    [Fact(DisplayName = "Código e versão da regra de distribuição ausentes juntos são recusados numa resposta só")]
    public void Validar_CodigoEVersaoAusentes_Acumula()
    {
        ConfiguracaoDistribuicaoVagasInput item = ItemValido() with { RegraDistribuicaoCodigo = null, RegraDistribuicaoVersao = null };

        ValidationResult resultado = _validator.Validate(new DefinirDistribuicaoVagasCommand(Guid.CreateVersion7(), [item]));

        resultado.Errors.Select(static e => e.PropertyName).Should().HaveCount(2);
    }

    [Fact(DisplayName = "Quadro nulo passa pelo validator, sem lançar exceção: a recusa como obrigatório é do domínio")]
    public void Validar_QuadroNulo_SegueParaODominioSemLancarExcecao()
    {
        ConfiguracaoDistribuicaoVagasInput item = ItemValido() with { Quadro = null };
        DefinirDistribuicaoVagasCommand command = new(Guid.CreateVersion7(), [item]);

        ValidationResult resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Item nulo dentro do quadro gera erro de validação, sem lançar exceção")]
    public void Validar_ItemNuloDentroDoQuadro_GeraErroSemLancarExcecao()
    {
        ConfiguracaoDistribuicaoVagasInput item = ItemValido() with { Quadro = [null!] };
        DefinirDistribuicaoVagasCommand command = new(Guid.CreateVersion7(), [item]);

        ValidationResult resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Quantidade negativa no quadro não é recusada pelo validator: a regra é do domínio")]
    public void Validar_QuadroComQuantidadeNegativa_NaoGeraErro()
    {
        ConfiguracaoDistribuicaoVagasInput item = ItemValido() with
        {
            Quadro = [new QuantidadeVagaInput(Guid.CreateVersion7(), -1)],
        };
        DefinirDistribuicaoVagasCommand command = new(Guid.CreateVersion7(), [item]);

        ValidationResult resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "ModalidadeId vazio no quadro gera erro")]
    public void Validar_QuadroComModalidadeIdVazio_GeraErro()
    {
        ConfiguracaoDistribuicaoVagasInput item = ItemValido() with
        {
            Quadro = [new QuantidadeVagaInput(Guid.Empty, 10)],
        };
        DefinirDistribuicaoVagasCommand command = new(Guid.CreateVersion7(), [item]);

        ValidationResult resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "ModalidadeId repetido no quadro gera erro")]
    public void Validar_QuadroComModalidadeIdRepetido_GeraErro()
    {
        Guid modalidadeId = Guid.CreateVersion7();
        ConfiguracaoDistribuicaoVagasInput item = ItemValido() with
        {
            Quadro = [new QuantidadeVagaInput(modalidadeId, 10), new QuantidadeVagaInput(modalidadeId, 20)],
        };
        DefinirDistribuicaoVagasCommand command = new(Guid.CreateVersion7(), [item]);

        ValidationResult resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Quadro com quantidade válida não gera erro")]
    public void Validar_QuadroValido_SemErros()
    {
        ConfiguracaoDistribuicaoVagasInput item = ItemValido() with
        {
            Quadro = [new QuantidadeVagaInput(Guid.CreateVersion7(), 10)],
        };
        DefinirDistribuicaoVagasCommand command = new(Guid.CreateVersion7(), [item]);

        ValidationResult resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeTrue();
    }
}
