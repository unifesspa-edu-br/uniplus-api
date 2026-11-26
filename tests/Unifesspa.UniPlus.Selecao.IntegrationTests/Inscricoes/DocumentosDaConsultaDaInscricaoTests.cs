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
        ProcessoSeletivo processo = ProcessoComDocumentos();
        Guid fase = processo.CronogramaFases.Single().Id;
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        (Guid processoId, _) = await PublicarEDivulgarAsync(db, processo);
        IUserContext fernanda = UsuarioComCpf(PersonasDeTeste.FernandaDrumond);
        (await _cenario.IniciarRascunhoAsync(db, processoId, fernanda)).IsSuccess.Should().BeTrue();

        await ConferirConsultasAsync(db, processoId, fernanda, fase);

        Result<FormularioApresentadoDoProcessoDto> formulario = await _cenario.ObterFormularioAsync(db, processoId, fernanda);
        formulario.Value!.DependenciasDaAvaliacao!.Fatos.Should().Contain("DECLARA_RENDA", "a resposta que decide o gatilho muda a avaliação");
    }

    private static FatoColetado PerguntaSobreRenda(string codigo) => FatoColetadoDeTeste.Coletando(codigo, 1)
        .ComRotulo("Você declara renda?")
        .ComObrigatoriedade(Obrigatoriedade.Sempre)
        .NaSecao(FormularioDeTeste.Secao)
        .Construir();

    private static ProcessoSeletivo ProcessoComDocumentos()
    {
        const string declaraRenda = "DECLARA_RENDA";
        const string familia = "COMPOSICAO_FAMILIAR";
        ProcessoSeletivo processo = NovoProcessoDeInscricao(
            nameof(DocumentosDaConsultaDaInscricaoTests), camposAdicionais: [PerguntaSobreRenda(declaraRenda)]);
        Guid fase = processo.CronogramaFases.Single().Id;
        DefinirDocumentos(processo, fase, familia, declaraRenda);
        return processo;
    }

    private static void DefinirDocumentos(ProcessoSeletivo processo, Guid fase, string familia, string declaraRenda)
    {
        DocumentoExigido identidade = Exigencia(fase, "RG", []);
        DocumentoExigido renda = Exigencia(fase, "COMPROVANTE_RENDA", [(declaraRenda, JsonSerializer.SerializeToElement(true))]);
        DocumentoExigido doMembro = Exigencia(fase, "RG_DO_MEMBRO", []);
        DocumentoExigido daConvocacao = Exigencia(fase, "CERTIDAO", [("MODALIDADE_CONVOCACAO", JsonSerializer.SerializeToElement("AC"))]);
        processo.DefinirDocumentosExigidos(
            [
                RegraDeComprovacao.CriarRegraDeDocumento(identidade, 0).Value!,
                RegraDeComprovacao.CriarRegraDeDocumento(renda, 1).Value!,
                RegraDeComprovacao.CriarRegraDeDocumento(doMembro, 2, grupoDoFormularioParaRepeticao: familia).Value!,
                RegraDeComprovacao.CriarRegraDeDocumento(daConvocacao, 3).Value!,
            ]).IsSuccess.Should().BeTrue();
    }

    private async Task ConferirConsultasAsync(UniPlusDbContext db, Guid processoId, IUserContext fernanda, Guid fase)
    {
        DocumentosDaInscricaoDto pendente = (await AvaliarDocumentosAsync(db, processoId, fernanda, null, [], fase)).Value!.Documentos;
        DocumentosDaInscricaoDto enquadra = (await AvaliarDocumentosAsync(db, processoId, fernanda, true, ["m1", "m2"], fase)).Value!.Documentos;
        DocumentosDaInscricaoDto naoEnquadra = (await AvaliarDocumentosAsync(db, processoId, fernanda, false, [], fase)).Value!.Documentos;
        ConferirDocumentosPendentes(pendente);
        ConferirDocumentosDaFamilia(enquadra);
        Situacoes(naoEnquadra).Select(static d => d.Tipo).Should().NotContain("COMPROVANTE_RENDA");
        Result<AvaliacaoDaInscricaoDto> faseDesconhecida = await AvaliarDocumentosAsync(db, processoId, fernanda, null, [], Guid.CreateVersion7());
        faseDesconhecida.Error!.Code.Should().Be(InscricaoErrorCodes.FaseNaoEncontrada);
    }

    private static void ConferirDocumentosPendentes(DocumentosDaInscricaoDto pendente)
    {
        Situacoes(pendente).Should().Equal(
            [("RG", null, "PENDENTE"), ("COMPROVANTE_RENDA", null, "PODE_SER_EXIGIDO"), ("CERTIDAO", null, "DEPENDE_DE_FASE_POSTERIOR")]);
        pendente.Fases.Single().Pendencias.Select(static p => p.FatoCodigo).Should().Equal("DECLARA_RENDA");
    }

    private static void ConferirDocumentosDaFamilia(DocumentosDaInscricaoDto enquadra)
    {
        Situacoes(enquadra).Should().Equal(
            [("RG", null, "PENDENTE"), ("COMPROVANTE_RENDA", null, "PENDENTE"), ("RG_DO_MEMBRO", "m1", "PENDENTE"), ("RG_DO_MEMBRO", "m2", "PENDENTE"),
             ("CERTIDAO", null, "DEPENDE_DE_FASE_POSTERIOR")]);
        enquadra.Fases.Single().Documentos.Where(static d => d.TipoDocumentoCodigo == "RG_DO_MEMBRO")
            .Should().OnlyContain(d => d.GrupoCodigo == "COMPOSICAO_FAMILIAR");
    }

    private static IEnumerable<(string Tipo, string? Ocorrencia, string Situacao)> Situacoes(DocumentosDaInscricaoDto documentos) =>
        documentos.Fases.Single().Documentos.Select(static d => (d.TipoDocumentoCodigo, d.OcorrenciaId, d.Situacao.Codigo));

    private async Task<Result<AvaliacaoDaInscricaoDto>> AvaliarDocumentosAsync(
        UniPlusDbContext db, Guid processoId, IUserContext usuario, bool? declaraRenda, string[] membros, Guid faseId)
    {
        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal);
        if (declaraRenda is { } valor)
        {
            respostas["DECLARA_RENDA"] = JsonSerializer.SerializeToElement(valor);
        }

        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>> grupos = new(StringComparer.Ordinal)
        {
            ["COMPOSICAO_FAMILIAR"] = [.. membros.Select(static m => new OcorrenciaDeGrupoInscricaoInput(m, new Dictionary<string, JsonElement>(StringComparer.Ordinal)))],
        };
        return await _cenario.AvaliarAsync(db, processoId, usuario, respostas, grupos, faseId);
    }

    private static DocumentoExigido Exigencia(Guid fase, string tipo, (string Fato, JsonElement Valor)[] gatilho) =>
        DocumentoExigido.Criar(
            Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("exigencia_" + tipo.ToLowerInvariant()).Value!,
            new DocumentoExigidoDeclarado(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), tipo, $"Documento {tipo}", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal),
                new MomentoDaExigencia(fase, null, FormulariosDoProcessoDeTeste.Codigo(FormulariosDoProcessoDeTeste.Inscricao)),
                new AplicabilidadeDaExigencia(gatilho.Length == 0 ? Aplicabilidade.Geral : Aplicabilidade.Condicional, [.. gatilho.Select(static g => CondicaoGatilho.Criar(0, g.Fato, Operador.Igual, g.Valor).Value!)]),
                new IndeferimentoDoDocumento(true, null, [DocumentoExigidoBaseLegal.Criar("Res. Unifesspa 532/2021, art. 12", TipoAbrangencia.InternaNorma, StatusBaseLegal.Resolvido, null).Value!]),
                RequisitosDoDocumentoDeTeste.Com(FormatosPermitidos.Criar(true, null).Value!),
                null)).Value!;
}
