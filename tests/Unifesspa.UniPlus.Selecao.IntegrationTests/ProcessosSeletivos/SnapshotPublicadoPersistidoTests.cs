namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Cobertura de integração (Postgres real via Testcontainers) do congelamento do snapshot de
/// publicação (congelamento dos parâmetros na publicação, UNI-REQ-0019, ADR-0100): re-hashear os
/// bytes lidos de volta do banco bate com o hash persistido pela app, os 33 blocos canônicos —
/// todos reais — estão presentes, e editar a configuração viva durante uma retificação aberta não
/// altera a versão já persistida.
/// </summary>
[Collection(PublicacaoSnapshotPersistenciaColecao.Nome)]
public sealed class SnapshotPublicadoPersistidoTests
{
    private readonly CenarioDeRetificacaoPersistida _cenario;

    public SnapshotPublicadoPersistidoTests(ProcessoSeletivoDbFixture fixture)
    {
        _cenario = new CenarioDeRetificacaoPersistida(fixture);
    }

    [Fact(DisplayName = "Snapshot_HashConfereAppEBanco — re-hashear os bytes lidos do banco bate com o hash persistido pela app")]
    public async Task Snapshot_HashConfereAppEBanco()
    {
        VersaoConfiguracao publicada = await PublicarAsync(nameof(Snapshot_HashConfereAppEBanco));

        await using UniPlusDbContext readContext = _cenario.CriarDbContext();
        VersaoConfiguracao versao = await CenarioDeRetificacaoPersistida.LerVersaoAsync(readContext, publicada.Id);

        string hashRecalculado = HashCanonicalComputer.ComputeSha256Hex(versao.ConfiguracaoCongeladaCanonica);

        hashRecalculado.Should().Be(versao.HashConfiguracao,
            "ADR-0100 §Confirmação: re-hashear os bytes persistidos deve bater com o hash calculado pela aplicação na publicação");
    }

    private async Task<VersaoConfiguracao> PublicarAsync(string nome) =>
        await _cenario.PublicarEPersistirAsync(CenarioDeRetificacaoPersistida.NovoProcessoConforme(nome));

    [Fact(DisplayName = "Snapshot_Contem33BlocosCanonicos — os 33 blocos, todos reais, estão presentes")]
    public async Task Snapshot_Contem33BlocosCanonicos()
    {
        VersaoConfiguracao publicada = await PublicarAsync(nameof(Snapshot_Contem33BlocosCanonicos));

        await using UniPlusDbContext readContext = _cenario.CriarDbContext();
        VersaoConfiguracao versao = await CenarioDeRetificacaoPersistida.LerVersaoAsync(readContext, publicada.Id);

        JsonNode payload = JsonNode.Parse(versao.ConfiguracaoCongelada)!;

        string[] blocosEsperados =
        [
            "tipoProcesso", "periodo", "etapas", "vagas", "distribuicao", "modalidades", "ofertas",
            "atendimento", "bonusRegional", "criteriosDesempate", "classificacao", "hashesEdital",
            "documentosExigidos", "arvoreSatisfacao", "formularios", "cascataRemanejamento", "divulgacao",
            "cronogramaFases", "identidadesUnidade",
            // Story #928, §7.4: coleta de fatos, derivação, grafo conjunto congelado, versão do
            // interpretador e conjunto de modalidades ofertadas.
            "fatosColetados", "regrasDerivacao", "grafoDependencia", "versaoInterpretador", "modalidadesOfertadas",
            // Issue #1112: taxa de inscrição e fundamentos de isenção — 25º bloco.
            "taxaInscricao",
            // UNI-REQ-0111: município que rege a contagem dos prazos e fuso aplicado — 26º bloco.
            "localidade",
            // UNI-REQ-0112: convenção de contagem declarada pelo certame — 27º bloco.
            "algoritmoContagemPrazo",
            // UNI-REQ-0080: calendário de dias úteis copiado por valor — 28º bloco.
            "calendarioDiasUteis",
            // Identificador legível: o endereço público do certame, congelado com a versão — 29º bloco.
            "identificadorLegivel",
            // Grupos repetíveis dos formulários (UNI-REQ-0146) — 30º bloco.
            "gruposColetados",
            // Rótulo e tipo do catálogo dos fatos que as regras citam e nenhum formulário pergunta — 31º bloco.
            "apresentacaoDosPressupostos",
            // Quantos cursos o candidato escolhe na inscrição, dimensão do processo — 32º bloco.
            "opcoesDeCurso",
            // A classificação, congelada, de cada tipo de ato que o cronograma declara — 33º bloco.
            "tiposDeAto",
            "fatosDerivados",
        ];
        JsonObject objeto = payload.AsObject();
        foreach (string bloco in blocosEsperados)
        {
            objeto.Should().ContainKey(bloco, $"o bloco canônico '{bloco}' deve estar presente no envelope");
        }

        // Equivalência EXATA de conjunto entre as chaves do envelope e a lista literal acima —
        // não só "os blocos esperados estão presentes" (o foreach acima, que por si só não
        // reprovaria uma chave espúria), mas também "não sobra nenhuma outra". A lista literal
        // FICA: este é o teste de contrato da abertura, e derivá-la do próprio objeto o
        // tornaria tautológico — passaria com qualquer conjunto de chaves.
        objeto.Select(static kvp => kvp.Key).Should().BeEquivalentTo(blocosEsperados,
            "o envelope de abertura tem exatamente os blocos canônicos listados acima — nem mais, nem menos");

        string[] stubs = [.. objeto
            .Where(static kvp => kvp.Value is JsonObject bloco
                && bloco.TryGetPropertyValue("status", out JsonNode? status)
                && status?.GetValue<string>() == "nao_construido")
            .Select(static kvp => kvp.Key)
            .Order(StringComparer.Ordinal)];

        stubs.Should().BeEmpty(
            "issue #563 promoveu 'divulgacao' — a última dimensão sem dono da Feature #40 — a bloco real, e " +
            "issue #1112 acrescentou 'taxaInscricao' já como bloco real. Os 25 blocos são todos reais " +
            "(Story #851 promoveu cronogramaFases; Story #853 promoveu documentosExigidos; issue #848 promoveu " +
            "vagas; Story #575 promoveu cascataRemanejamento; issue #849 promoveu identidadesUnidade; Story #559 " +
            "promoveu formulario; os demais sempre foram reais)");

        // Nenhum bloco REAL emite `nao_construido` na RAIZ. Atendimento, classificação e
        // documentosExigidos são dimensões obrigatórias/já entregues: a ausência da primeira é
        // pendência de conformidade, não stub silencioso; a segunda nunca foi stub.
        // documentosExigidos é totalmente real desde a Story #554 (PR #903, bump 1.2): a sub-chave
        // `exigencias` deixou de ser stub e materializa o item rico de cada DocumentoExigido vivo
        // do processo — vazia aqui porque este fixture não configura nenhuma exigência documental.
        objeto["atendimento"]!.AsObject().Should().NotContainKey("status");
        objeto["classificacao"]!.AsObject().Should().NotContainKey("status");
        objeto["cronogramaFases"]!.AsObject().Should().NotContainKey("status");
        objeto["vagas"]!.AsArray().Should().NotBeEmpty("issue #848: o quadro de vagas é sempre materializado junto da configuração");
        objeto["documentosExigidos"]!.AsObject().Should().NotContainKey("status");
        // issue #849: identidadesUnidade é bloco real desde a criação — nunca stub na raiz.
        objeto["identidadesUnidade"]!.AsObject().Should().NotContainKey("status");
        objeto["identidadesUnidade"]!.AsObject()["administradora"]!.AsObject().Should().ContainKey("sigla");
        // issue #563: divulgacao é bloco real — o default minimizado (só o número de inscrição),
        // porque este teste não configura divulgação nenhuma.
        objeto["divulgacao"]!.AsObject().Should().NotContainKey("status");
        objeto["divulgacao"]!.AsObject()["camposPublicos"]!.AsArray().Should().ContainSingle()
            .Which!.GetValue<string>().Should().Be("numero_inscricao");
        objeto["documentosExigidos"]!["exigencias"]!.AsArray().Should().BeEmpty(
            "nenhum DocumentoExigido foi configurado para este processo neste teste");
        objeto["documentosExigidos"]!["obrigatoriedades"]!.AsArray().Should().BeEmpty(
            "nenhuma ObrigatoriedadeLegal vigente foi cadastrada para este processo neste teste");

        // Blocos reais carregam dado de negócio, não o marcador de stub.
        objeto["etapas"]!.AsArray().Should().NotBeEmpty();
        objeto["bonusRegional"]!["presente"]!.GetValue<bool>().Should().BeFalse();
        objeto["cronogramaFases"]!["fases"]!.AsArray().Should().NotBeEmpty();

        // cascataRemanejamento (Story #575): bloco real, ausência congela `{"presente":false}`,
        // NUNCA o stub `nao_construido` — este fixture não tem nenhuma modalidade SegueCascata
        // (é a mesma AC institucional de bonusRegional acima).
        objeto["cascataRemanejamento"]!.AsObject().Should().NotContainKey("status");
        objeto["cascataRemanejamento"]!["presente"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact(DisplayName = "Editar a configuração viva numa retificação aberta não altera o hash já persistido da versão anterior")]
    public async Task Publicar_RetificacaoAbertaEditaConfiguracaoViva_NaoAlteraHashDaVersaoAnterior()
    {
        ProcessoSeletivo processo = CenarioDeRetificacaoPersistida.NovoProcessoConforme(nameof(Publicar_RetificacaoAbertaEditaConfiguracaoViva_NaoAlteraHashDaVersaoAnterior));
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos([ExigenciaDeIdentidade(faseId, "IDENTIDADE", "Documento de identidade")], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        VersaoConfiguracao versaoAbertura = await _cenario.PublicarEPersistirAsync(processo);
        string hashOriginal = versaoAbertura.HashConfiguracao;
        byte[] bytesOriginais = versaoAbertura.ConfiguracaoCongeladaCanonica;

        // "Editar o cadastro de TipoDocumento" (CA-12) — simulado como uma sessão de
        // retificação que redefine a exigência com um TipoDocumento diferente e persiste a
        // mudança no MESMO DbContext que abriu a retificação.
        await using (UniPlusDbContext sessao = _cenario.CriarDbContext())
        {
            ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(sessao);
            ProcessoSeletivo carregado = (await repository.ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;
            CenarioDeRetificacaoPersistida.AbrirRetificacao(carregado, "Corrigir código do tipo de documento exigido", versaoAbertura);

            carregado.DefinirDocumentosExigidos(
                [ExigenciaDeIdentidade(faseId, "IDENTIDADE_EDITADA", "Documento de identidade (cadastro editado)")], PrecondicaoIfMatch.Curinga)
                .IsSuccess.Should().BeTrue("mutar a configuração viva durante a sessão é permitido");

            await sessao.SaveChangesAsync(CancellationToken.None);
        }

        await using UniPlusDbContext readContext = _cenario.CriarDbContext();
        VersaoConfiguracao versaoRelida = await CenarioDeRetificacaoPersistida.LerVersaoAsync(readContext, versaoAbertura.Id);

        versaoRelida.HashConfiguracao.Should().Be(hashOriginal,
            "editar a configuração viva do processo (aqui, o TipoDocumento de uma exigência, dentro de uma " +
            "sessão de retificação aberta e persistida) não pode alterar o hash de uma versão JÁ persistida");
        versaoRelida.ConfiguracaoCongeladaCanonica.Should().Equal(bytesOriginais,
            "os bytes congelados da versão anterior também permanecem imutáveis — não só o hash resumido");
    }

    private static NoExigencia ExigenciaDeIdentidade(Guid faseId, string codigo, string nome)
    {
        DocumentoExigidoBaseLegal baseLegal = DocumentoExigidoBaseLegal.Criar(
            "Lei 12.711/2012, art. 3º", TipoAbrangencia.InternaEdital, StatusBaseLegal.Resolvido, null).Value!;
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            faseId,
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: codigo,
            tipoDocumentoNome: nome,
            tipoDocumentoCategoria: "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            aplicabilidade: Aplicabilidade.Geral,
            obrigatorio: true,
            consequenciaIndeferimento: null,
            condicoes: [], basesLegais: [baseLegal], idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null, finalidade: FinalidadeFormulario.Inscricao).Value!;
        return NoExigencia.CriarFolha(exigencia, 0).Value!;
    }
}
