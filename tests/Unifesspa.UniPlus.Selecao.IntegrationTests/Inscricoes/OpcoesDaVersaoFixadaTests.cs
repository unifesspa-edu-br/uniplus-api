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

    [Fact(DisplayName = "A versão fixada oferece ao campo de recurso de acessibilidade só os recursos da oferta do processo, e a submissão recusa o que ela não oferta")]
    public async Task RecursoDeAcessibilidade_ForaDaOfertaDoProcesso_ERecusado()
    {
        FatoColetado campoRecurso = FatoColetado.Criar(
            AtendimentoEspecializado.FatoRecursoAcessibilidade, 1, "De qual recurso de acessibilidade você vai precisar?",
            TipoRenderizacao.SelecaoMultipla, Obrigatoriedade.Nunca, null, classificacaoProtecao: "SENSIVEL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
        AtendimentoEspecializado atendimento = AtendimentoEspecializado.Criar(
            [], [RecursoOfertado.Criar(Guid.CreateVersion7(), "SALA_AMAMENTACAO", "Sala para amamentação")], []).Value!;
        ProcessoSeletivo processo = NovoProcessoDeInscricao(
            nameof(RecursoDeAcessibilidade_ForaDaOfertaDoProcesso_ERecusado), atendimento, [campoRecurso]);

        await using UniPlusDbContext writeContext = _cenario.CriarDbContext();
        (Guid processoId, _) = await PublicarEDivulgarAsync(writeContext, processo);

        Result<InscricaoDto> iniciar = await _cenario.IniciarRascunhoAsync(writeContext, processoId, UsuarioComCpf(PersonasDeTeste.FernandaDrumond));
        iniciar.IsSuccess.Should().BeTrue(iniciar.Error?.Message);

        await using UniPlusDbContext readContext = _cenario.CriarDbContext();
        Result<ContextoDaVersaoFixada> contexto = await ContextoDaVersaoFixadaResolver.ResolverAsync(
            iniciar.Value!.VersaoFormularioId, processoId, ProcessoSeletivoRepositoryDeTeste.Criar(readContext), RegistroCodecs, ofertaCursoId: null, null, CancellationToken.None);
        contexto.IsSuccess.Should().BeTrue(contexto.Error?.Message);
        contexto.Value!.Ofertas[AtendimentoEspecializado.FatoRecursoAcessibilidade].Should().BeEquivalentTo(["SALA_AMAMENTACAO"]);

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.RecusasDeOpcaoForaDaOferta(
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                [AtendimentoEspecializado.FatoRecursoAcessibilidade] = JsonSerializer.SerializeToElement(new[] { "SALA_AMAMENTACAO", "INTERPRETE_LIBRAS" }),
            },
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal),
            contexto.Value.Ofertas);

        recusas.Should().ContainSingle(e => e.Field == AtendimentoEspecializado.FatoRecursoAcessibilidade
            && e.Error.Code == "Inscricao.RespostaInvalida", "o intérprete de Libras não está na oferta deste processo");
    }

    [Fact(DisplayName = "A versão fixada oferece ao órgão emissor do RG a lista congelada, e a submissão recusa o órgão fora dela")]
    public async Task OrgaoEmissor_ForaDaListaCongelada_ERecusado()
    {
        ProcessoSeletivo processo = NovoProcessoDeInscricao(nameof(OrgaoEmissor_ForaDaListaCongelada_ERecusado));

        await using UniPlusDbContext writeContext = _cenario.CriarDbContext();
        (Guid processoId, _) = await PublicarEDivulgarAsync(writeContext, processo);

        Result<InscricaoDto> iniciar = await _cenario.IniciarRascunhoAsync(writeContext, processoId, UsuarioComCpf(PersonasDeTeste.FernandaDrumond));
        iniciar.IsSuccess.Should().BeTrue(iniciar.Error?.Message);

        await using UniPlusDbContext readContext = _cenario.CriarDbContext();
        Result<ContextoDaVersaoFixada> contexto = await ContextoDaVersaoFixadaResolver.ResolverAsync(
            iniciar.Value!.VersaoFormularioId, processoId, ProcessoSeletivoRepositoryDeTeste.Criar(readContext), RegistroCodecs, ofertaCursoId: null, null, CancellationToken.None);
        contexto.IsSuccess.Should().BeTrue(contexto.Error?.Message);

        IReadOnlyList<FieldError> RecusasDe(string orgao) => VeredictoDeSubmissao.RecusasDeOpcaoForaDaOferta(
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["RG_ORGAO_EMISSOR"] = JsonSerializer.SerializeToElement(orgao) },
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal),
            contexto.Value!.Ofertas);

        RecusasDe("SSP").Should().BeEmpty();
        RecusasDe("PA").Should().ContainSingle(static e => e.Field == "RG_ORGAO_EMISSOR" && e.Error.Code == InscricaoErrorCodes.RespostaInvalida,
            "a UF não é órgão emissor");
    }
}
