namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Validators;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

public sealed class CriarProcessoSeletivoCommandValidatorTests
{
    private static readonly Guid UnidadeId = Guid.NewGuid();

    [Fact(DisplayName = "Validator passa com nome, tipo, origem e unidade administradora válidos")]
    public void Aceita_ComandoValido()
    {
        ValidationResult result = new CriarProcessoSeletivoCommandValidator()
            .Validate(new CriarProcessoSeletivoCommand("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, UnidadeId, "1504208", "Marabá", "PA"));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Validator falha quando nome é vazio")]
    public void Rejeita_NomeVazio()
    {
        ValidationResult result = new CriarProcessoSeletivoCommandValidator()
            .Validate(new CriarProcessoSeletivoCommand(string.Empty, TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, UnidadeId, "1504208", "Marabá", "PA"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Nome");
    }

    [Fact(DisplayName = "Validator falha quando tipo é Nenhum")]
    public void Rejeita_TipoNenhum()
    {
        ValidationResult result = new CriarProcessoSeletivoCommandValidator()
            .Validate(new CriarProcessoSeletivoCommand("PS 2026", TipoProcesso.Nenhum, OrigemCandidatos.InscricaoPropria, UnidadeId, "1504208", "Marabá", "PA"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "TipoProcessoOrigemId");
    }

    [Fact(DisplayName = "Validator falha quando origem dos candidatos é Nenhuma (Story #851 §3.4)")]
    public void Rejeita_OrigemCandidatosNenhuma()
    {
        ValidationResult result = new CriarProcessoSeletivoCommandValidator()
            .Validate(new CriarProcessoSeletivoCommand("PS 2026", TipoProcesso.SiSU, OrigemCandidatos.Nenhuma, UnidadeId, "1504208", "Marabá", "PA"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "OrigemCandidatos");
    }

    [Fact(DisplayName = "Validator falha quando UnidadeAdministradoraOrigemId é Guid.Empty (issue #849, CA-01)")]
    public void Rejeita_UnidadeAdministradoraOrigemIdVazio()
    {
        ValidationResult result = new CriarProcessoSeletivoCommandValidator()
            .Validate(new CriarProcessoSeletivoCommand("PS 2026", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.Empty, "1504208", "Marabá", "PA"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "UnidadeAdministradoraOrigemId");
    }
}
