namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.CenarioDeInscricaoPublicada;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.ProcessosDeInscricaoDeTeste;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.PublicacaoDoProcessoDeTeste;

/// <summary>As opções que a versão fixada oferta a um campo (recursos da oferta do processo, lista congelada de órgãos emissores) e a recusa da submissão que sai delas.</summary>
[Trait("Category", "Integration")]
[Collection(InscricaoSobreProcessoPublicadoColecao.Nome)]
public sealed class OpcoesDaVersaoFixadaTests
{
    private readonly CenarioDeInscricaoPublicada _cenario;

    public OpcoesDaVersaoFixadaTests(InscricaoDbFixture fixture) => _cenario = new CenarioDeInscricaoPublicada(fixture);

    [Fact(DisplayName = "A versão fixada recusa o recurso de acessibilidade fora da oferta do processo")]
    public async Task RecursoDeAcessibilidade_ForaDaOfertaDoProcesso_ERecusado()
    {
        ProcessoSeletivo processo = ProcessoComRecurso();
        await using UniPlusDbContext escrita = _cenario.CriarDbContext();
        (Guid processoId, InscricaoDto iniciar) = await _cenario.PublicarEIniciarAsync(escrita, processo, PersonasDeTeste.FernandaDrumond);
        await using UniPlusDbContext leitura = _cenario.CriarDbContext();
        ContextoDaVersaoFixada contexto = await ContextoAsync(leitura, iniciar.Formulario.VersaoFormularioId, processoId);
        contexto.Ofertas[AtendimentoEspecializado.FatoRecursoAcessibilidade].Should().BeEquivalentTo(["SALA_AMAMENTACAO"]);
        RecusasDosRecursos(contexto).Should().ContainSingle(e => e.Field == AtendimentoEspecializado.FatoRecursoAcessibilidade
            && e.Error.Code == "Inscricao.RespostaInvalida");
    }

    private static ProcessoSeletivo ProcessoComRecurso()
    {
        FatoColetado campoRecurso = FatoColetadoDeTeste.Coletando(AtendimentoEspecializado.FatoRecursoAcessibilidade, 1)
            .ComRotulo("De qual recurso de acessibilidade você vai precisar?")
            .DoTipo(TipoRenderizacao.SelecaoMultipla)
            .ComClassificacao("SENSIVEL")
            .Construir();
        AtendimentoEspecializado atendimento = AtendimentoEspecializado.Criar(
            [], [RecursoOfertado.Criar(Guid.CreateVersion7(), "SALA_AMAMENTACAO", "Sala para amamentação")], []).Value!;
        return NovoProcessoDeInscricao(nameof(RecursoDeAcessibilidade_ForaDaOfertaDoProcesso_ERecusado), atendimento, [campoRecurso]);
    }

    private static IReadOnlyList<FieldError> RecusasDosRecursos(ContextoDaVersaoFixada contexto) =>
        VeredictoDeSubmissao.RecusasDeOpcaoForaDaOferta(
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                [AtendimentoEspecializado.FatoRecursoAcessibilidade] = JsonSerializer.SerializeToElement(new[] { "SALA_AMAMENTACAO", "INTERPRETE_LIBRAS" }),
            }, new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), contexto.Ofertas);

    [Fact(DisplayName = "A versão fixada recusa o órgão emissor fora da lista congelada")]
    public async Task OrgaoEmissor_ForaDaListaCongelada_ERecusado()
    {
        ProcessoSeletivo processo = NovoProcessoDeInscricao(nameof(OrgaoEmissor_ForaDaListaCongelada_ERecusado));
        await using UniPlusDbContext escrita = _cenario.CriarDbContext();
        (Guid processoId, InscricaoDto iniciar) = await _cenario.PublicarEIniciarAsync(escrita, processo, PersonasDeTeste.FernandaDrumond);
        await using UniPlusDbContext leitura = _cenario.CriarDbContext();
        ContextoDaVersaoFixada contexto = await ContextoAsync(leitura, iniciar.Formulario.VersaoFormularioId, processoId);
        RecusasDoOrgao(contexto, "SSP").Should().BeEmpty();
        RecusasDoOrgao(contexto, "PA").Should().ContainSingle(static e => e.Field == "RG_ORGAO_EMISSOR" && e.Error.Code == InscricaoErrorCodes.RespostaInvalida);
    }

    private static IReadOnlyList<FieldError> RecusasDoOrgao(ContextoDaVersaoFixada contexto, string orgao) =>
        VeredictoDeSubmissao.RecusasDeOpcaoForaDaOferta(
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["RG_ORGAO_EMISSOR"] = JsonSerializer.SerializeToElement(orgao) },
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), contexto.Ofertas);
}
