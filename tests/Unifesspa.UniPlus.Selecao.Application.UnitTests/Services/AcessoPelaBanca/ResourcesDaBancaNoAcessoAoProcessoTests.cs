namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.AcessoPelaBanca;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>Cada tipo de resource da banca nasce com os campos do tipo, na unidade da fase, ou não nasce.</summary>
public sealed class ResourcesDaBancaNoAcessoAoProcessoTests
{
    private static readonly UnidadeDaFase Fase = new(Guid.CreateVersion7(), Guid.CreateVersion7());
    private static readonly Guid Processo = Guid.CreateVersion7();
    private static readonly Guid Etapa = Guid.CreateVersion7();
    private static readonly Guid Banca = Guid.CreateVersion7();
    private static readonly Guid Inscricao = Guid.CreateVersion7();

    [Fact(DisplayName = "A avaliação da inscrição é dado pessoal, na unidade da fase, com processo, etapa, banca e inscrição")]
    public void Avaliacao_LevaTodosOsCampos()
    {
        ResourceContext resource = AcessoAoProcesso.AvaliacaoDaInscricao(Fase, Processo, Etapa, Banca, Inscricao).Value!;

        resource.ResourceTipo.Should().Be(ResourceTiposSelecao.AvaliacaoDaInscricao);
        resource.Sensibilidade.Should().Be(ClassificacaoDeProtecao.Pessoal);
        resource.UnidadeProprietariaId.Should().Be(Fase.UnidadeId);
        resource.ProcessoId.Should().Be(Processo);
        resource.InscricaoId.Should().Be(Inscricao);
        resource.Banca!.FaseId.Should().Be(Fase.FaseId);
        resource.Banca.EtapaId.Should().Be(Etapa);
        resource.Banca.BancaId.Should().Be(Banca);
    }

    [Fact(DisplayName = "A banca da fase por processo não leva inscrição nem etapa")]
    public void BancaDaFase_SemInscricaoNemEtapa()
    {
        ResourceContext resource = AcessoAoProcesso.BancaDaFase(Fase, Processo, Banca).Value!;

        resource.ResourceTipo.Should().Be(ResourceTiposSelecao.BancaDaFase);
        resource.InscricaoId.Should().BeNull();
        resource.Banca!.EtapaId.Should().BeNull();
        resource.ProcessoId.Should().Be(Processo);
    }

    [Fact(DisplayName = "A banca da fase por inscrição leva a inscrição e é dado pessoal")]
    public void BancaDaFaseNaInscricao_LevaAInscricao()
    {
        ResourceContext resource = AcessoAoProcesso.BancaDaFaseNaInscricao(Fase, Processo, Banca, Inscricao).Value!;

        resource.InscricaoId.Should().Be(Inscricao);
        resource.Sensibilidade.Should().Be(ClassificacaoDeProtecao.Pessoal);
    }

    [Fact(DisplayName = "A unidade do resource é a da fase, mesmo quando a dona do processo é outra")]
    public void Unidade_EADaFase()
    {
        Guid donaDoProcesso = Guid.CreateVersion7();

        AcessoAoProcesso.BancaDaFase(Fase, Processo, Banca).Value!.UnidadeProprietariaId
            .Should().Be(Fase.UnidadeId).And.NotBe(donaDoProcesso);
    }

    [Fact(DisplayName = "Rejeita identificador vazio de processo, etapa, banca ou inscrição")]
    public void GuidVazio_Rejeita()
    {
        AcessoAoProcesso.AvaliacaoDaInscricao(Fase, Guid.Empty, Etapa, Banca, Inscricao).IsFailure.Should().BeTrue();
        AcessoAoProcesso.AvaliacaoDaInscricao(Fase, Processo, Guid.Empty, Banca, Inscricao).IsFailure.Should().BeTrue();
        AcessoAoProcesso.AvaliacaoDaInscricao(Fase, Processo, Etapa, Guid.Empty, Inscricao).Error!.Code
            .Should().Be(AuthorizationErrorCodes.VinculosDaBancaInvalidos);
        AcessoAoProcesso.AvaliacaoDaInscricao(Fase, Processo, Etapa, Banca, Guid.Empty).IsFailure.Should().BeTrue();
        AcessoAoProcesso.BancaDaFase(Fase, Processo, Guid.Empty).IsFailure.Should().BeTrue();
    }
}
