namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.CenarioDeInscricaoPublicada;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.ProcessosDeInscricaoDeTeste;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.PublicacaoDoProcessoDeTeste;

/// <summary>Os documentos do candidato que a consulta da inscrição diz por fase, conforme o gatilho, os membros do grupo e a convocação.</summary>
[Trait("Category", "Integration")]
[Collection(InscricaoSobreProcessoPublicadoColecao.Nome)]
public sealed class DocumentosDaConsultaDaInscricaoTests
{
    private readonly CenarioDeInscricaoPublicada _cenario;

    public DocumentosDaConsultaDaInscricaoTests(InscricaoDbFixture fixture) => _cenario = new CenarioDeInscricaoPublicada(fixture);

    [Fact(DisplayName = "Sobre o processo publicado, a consulta diz por fase os documentos do candidato: o geral, o do gatilho, um por membro e o que depende da convocação")]
    public async Task Consulta_DocumentosDaInscricao_PorFase()
    {
        const string declaraRenda = "DECLARA_RENDA";
        const string familia = "COMPOSICAO_FAMILIAR";
        ProcessoSeletivo processo = NovoProcessoDeInscricao(
            nameof(Consulta_DocumentosDaInscricao_PorFase),
            camposAdicionais:
            [
                FatoColetado.Criar(
                    declaraRenda, 1, "Você declara renda?", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null,
                    classificacaoProtecao: "PESSOAL", etapaCodigo: FormularioDeTeste.Secao, finalidade: FinalidadeFormulario.Inscricao, hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!,
            ]);
        Guid fase = processo.CronogramaFases.Single().Id;
        DocumentoExigido identidade = Exigencia(fase, "RG", []);
        DocumentoExigido renda = Exigencia(fase, "COMPROVANTE_RENDA", [(declaraRenda, JsonSerializer.SerializeToElement(true))]);
        DocumentoExigido doMembro = Exigencia(fase, "RG_DO_MEMBRO", []);
        DocumentoExigido daConvocacao = Exigencia(fase, "CERTIDAO", [("MODALIDADE_CONVOCACAO", JsonSerializer.SerializeToElement("AC"))]);
        processo.DefinirDocumentosExigidos(
            [
                NoExigencia.CriarFolha(identidade, 0).Value!,
                NoExigencia.CriarFolha(renda, 1).Value!,
                NoExigencia.CriarFolha(doMembro, 2, repetePorEntidade: familia).Value!,
                NoExigencia.CriarFolha(daConvocacao, 3).Value!,
            ]).IsSuccess.Should().BeTrue();
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        (Guid processoId, _) = await PublicarEDivulgarAsync(db, processo);
        IUserContext fernanda = UsuarioComCpf(PersonasDeTeste.FernandaDrumond);
        (await _cenario.IniciarRascunhoAsync(db, processoId, fernanda)).IsSuccess.Should().BeTrue();

        DocumentosDaInscricaoDto pendente = (await AvaliarDocumentosAsync(db, processoId, fernanda, null, [], fase)).Value!.Documentos;
        DocumentosDaInscricaoDto enquadra = (await AvaliarDocumentosAsync(db, processoId, fernanda, true, ["m1", "m2"], fase)).Value!.Documentos;
        DocumentosDaInscricaoDto naoEnquadra = (await AvaliarDocumentosAsync(db, processoId, fernanda, false, [], fase)).Value!.Documentos;
        Result<AvaliacaoDaInscricaoDto> faseDesconhecida = await AvaliarDocumentosAsync(db, processoId, fernanda, null, [], Guid.CreateVersion7());

        Situacoes(pendente).Should().Equal(
            [("RG", null, "PENDENTE"), ("COMPROVANTE_RENDA", null, "PODE_SER_EXIGIDO"), ("CERTIDAO", null, "DEPENDE_DE_FASE_POSTERIOR")],
            "sem a resposta o comprovante pode ser exigido; sem membro, nada por membro; a convocação só existe depois da inscrição");
        pendente.Fases.Single().Pendencias.Select(static p => p.FatoCodigo).Should().Equal(declaraRenda);
        Situacoes(enquadra).Should().Equal(
            [("RG", null, "PENDENTE"), ("COMPROVANTE_RENDA", null, "PENDENTE"), ("RG_DO_MEMBRO", "m1", "PENDENTE"), ("RG_DO_MEMBRO", "m2", "PENDENTE"),
             ("CERTIDAO", null, "DEPENDE_DE_FASE_POSTERIOR")]);
        enquadra.Fases.Single().Documentos.Where(static d => d.TipoDocumentoCodigo == "RG_DO_MEMBRO")
            .Should().OnlyContain(d => d.GrupoCodigo == familia);
        Situacoes(naoEnquadra).Select(static d => d.Tipo).Should().NotContain("COMPROVANTE_RENDA", "quem não declara renda não apresenta o comprovante");
        faseDesconhecida.Error!.Code.Should().Be(InscricaoErrorCodes.FaseNaoEncontrada);

        Result<FormularioRenderizavelDto> formulario = await _cenario.ObterFormularioAsync(db, processoId, fernanda);
        formulario.Value!.DependenciasDaAvaliacao!.Fatos.Should().Contain(declaraRenda, "a resposta que decide o gatilho muda a avaliação");
    }

    private static IEnumerable<(string Tipo, string? Ocorrencia, string Situacao)> Situacoes(DocumentosDaInscricaoDto documentos) =>
        documentos.Fases.Single().Documentos.Select(static d => (d.TipoDocumentoCodigo, d.OcorrenciaId, d.Situacao));

    private async Task<Result<AvaliacaoDaInscricaoDto>> AvaliarDocumentosAsync(
        UniPlusDbContext db, Guid processoId, IUserContext usuario, bool? declaraRenda, string[] membros, Guid faseId) =>
        await AvaliarInscricaoQueryHandler.Handle(
            new AvaliarInscricaoQuery(processoId, new AvaliacaoDaInscricaoRequest(RespostasDaAvaliacao(declaraRenda), GrupoDaComposicaoFamiliar(membros)), faseId),
            new InscricaoAtivaDoCandidato(_cenario.NovoRepositorioDeInscricao(db)), PeriodoDe(db), AvaliacaoDe(db), usuario, DentroDoPeriodo, CancellationToken.None);

    private static Dictionary<string, JsonElement> RespostasDaAvaliacao(bool? declaraRenda)
    {
        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal);
        if (declaraRenda is { } valor)
        {
            respostas["DECLARA_RENDA"] = JsonSerializer.SerializeToElement(valor);
        }

        return respostas;
    }

    private static Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>> GrupoDaComposicaoFamiliar(string[] membros) =>
        new(StringComparer.Ordinal)
        {
            ["COMPOSICAO_FAMILIAR"] = [.. membros.Select(static m => new OcorrenciaDeGrupoInscricaoInput(m, new Dictionary<string, JsonElement>(StringComparer.Ordinal)))],
        };

    private static DocumentoExigido Exigencia(Guid fase, string tipo, (string Fato, JsonElement Valor)[] gatilho) =>
        DocumentoExigido.Criar(
            fase, Guid.CreateVersion7(), tipo, $"Documento {tipo}", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            gatilho.Length == 0 ? Aplicabilidade.Geral : Aplicabilidade.Condicional, obrigatorio: true, consequenciaIndeferimento: null,
            condicoes: [.. gatilho.Select(static g => CondicaoGatilho.Criar(0, g.Fato, Operador.Igual, g.Valor).Value!)],
            basesLegais: [DocumentoExigidoBaseLegal.Criar("Res. Unifesspa 532/2021, art. 12", TipoAbrangencia.InternaNorma, StatusBaseLegal.Resolvido, null).Value!],
            idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null,
            finalidade: FinalidadeFormulario.Inscricao).Value!;
}
