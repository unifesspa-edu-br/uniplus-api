namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.CenarioDeInscricaoPublicada;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.ProcessosDeInscricaoDeTeste;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.PublicacaoDoProcessoDeTeste;

/// <summary>As dependências da avaliação e do Fator que o formulário publicado da inscrição e o da isenção trazem, só pelo código do fato.</summary>
[Trait("Category", "Integration")]
[Collection(InscricaoSobreProcessoPublicadoColecao.Nome)]
public sealed class FormularioDaInscricaoPublicadoTests
{
    private readonly CenarioDeInscricaoPublicada _cenario;

    public FormularioDaInscricaoPublicadoTests(InscricaoDbFixture fixture) => _cenario = new CenarioDeInscricaoPublicada(fixture);

    [Fact(DisplayName = "O formulário da inscrição publicado diz, só pelo código, de que campos a avaliação depende, sem regra nem valor")]
    public async Task FormularioDaInscricao_TrazAsDependenciasDaAvaliacaoSoPeloCodigo()
    {
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        (Guid processoId, _) = await PublicarEDivulgarAsync(db, ProcessoQueDerivaAModalidade(nameof(FormularioDaInscricao_TrazAsDependenciasDaAvaliacaoSoPeloCodigo)));
        IUserContext fernanda = UsuarioComCpf(PersonasDeTeste.FernandaDrumond);
        (await _cenario.IniciarRascunhoAsync(db, processoId, fernanda)).IsSuccess.Should().BeTrue();

        Result<FormularioRenderizavelDto> formulario = await _cenario.ObterFormularioAsync(db, processoId, fernanda);

        formulario.IsSuccess.Should().BeTrue(formulario.Error?.Message);
        formulario.Value!.DependenciasDaAvaliacao.Should().BeEquivalentTo(new DependenciasDaAvaliacaoDto(["CONCORRER_PCD"], []));
        JsonSerializer.Serialize(formulario.Value!.DependenciasDaAvaliacao).Should().NotContain("AC", "a matriz e o que ela contribui não saem");
        formulario.Value!.Regras.Derivacoes.Should().NotContain(d => d.FatoCodigo == AvaliacaoDaInscricao.FatoDaModalidade,
            "a derivação da modalidade não vai ao navegador");
    }

    private async Task<FormularioRenderizavelDto> FormularioDaInscricaoDeFernandaAsync(UniPlusDbContext db, ProcessoSeletivo processo)
    {
        (Guid processoId, _) = await PublicarEDivulgarAsync(db, processo);
        IUserContext fernanda = UsuarioComCpf(PersonasDeTeste.FernandaDrumond);
        (await _cenario.IniciarRascunhoAsync(db, processoId, fernanda)).IsSuccess.Should().BeTrue();

        Result<FormularioRenderizavelDto> formulario = await _cenario.ObterFormularioAsync(db, processoId, fernanda);
        formulario.IsSuccess.Should().BeTrue(formulario.Error?.Message);
        return formulario.Value!;
    }

    [Fact(DisplayName = "O formulário da inscrição publicado traz, só pelo código, o fato que o bônus regional cita como dependência do Fator")]
    public async Task FormularioDaInscricao_TrazAsDependenciasDoFatorPeloFatoQueOBonusCita()
    {
        await using UniPlusDbContext db = _cenario.CriarDbContext();

        FormularioRenderizavelDto formulario = await FormularioDaInscricaoDeFernandaAsync(
            db, ProcessoComFator(nameof(FormularioDaInscricao_TrazAsDependenciasDoFatorPeloFatoQueOBonusCita)));

        formulario.DependenciasDoFator.Should().BeEquivalentTo(new DependenciasDaAvaliacaoDto(["PEDE_O_FATOR"], []));
    }

    [Fact(DisplayName = "O formulário da inscrição publicado de processo sem Fator traz as dependências do Fator vazias, não nulas")]
    public async Task FormularioDaInscricao_SemFator_TrazAsDependenciasDoFatorVazias()
    {
        await using UniPlusDbContext db = _cenario.CriarDbContext();

        FormularioRenderizavelDto formulario = await FormularioDaInscricaoDeFernandaAsync(
            db, ProcessoQueDerivaAModalidade(nameof(FormularioDaInscricao_SemFator_TrazAsDependenciasDoFatorVazias)));

        formulario.DependenciasDoFator.Should().BeEquivalentTo(new DependenciasDaAvaliacaoDto([], []));
    }

    [Fact(DisplayName = "O formulário da isenção publicado não traz as dependências do Fator: elas são só da inscrição")]
    public async Task FormularioDaIsencao_NaoTrazAsDependenciasDoFator()
    {
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        (Guid processoId, _) = await PublicarEDivulgarAsync(db, ProcessoComFator(nameof(FormularioDaIsencao_NaoTrazAsDependenciasDoFator), comIsencao: true));

        Result<FormularioRenderizavelDto> formulario = await ObterFormularioRenderizavelQueryHandler.Handle(
            new ObterFormularioRenderizavelQuery(processoId, FinalidadeFormulario.IsencaoTaxa), ProcessoSeletivoRepositoryDeTeste.Criar(db),
            new CertameDivulgadoRepository(db), RegistroCodecs, EnderecosSemCopia(), CancellationToken.None);

        formulario.IsSuccess.Should().BeTrue(formulario.Error?.Message);
        formulario.Value!.DependenciasDoFator.Should().BeNull();
    }
}
