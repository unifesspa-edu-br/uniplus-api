namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura de <see cref="ResolvedorExigenciasDocumentais"/> (Story #554, PR-e, issue
/// #548, ADR-0076) — resolvedor puro que classifica cada exigência congelada como
/// Satisfeita/Pendente/NaoAplicavel para um candidato.
/// </summary>
public sealed class ResolvedorExigenciasDocumentaisTests
{
    private static readonly Guid FaseA = Guid.CreateVersion7();
    private static readonly Guid FaseB = Guid.CreateVersion7();
    private static readonly IReadOnlyDictionary<Guid, ApresentacaoDocumento> SemApresentacoes =
        new Dictionary<Guid, ApresentacaoDocumento>();
    private static readonly IReadOnlyDictionary<string, JsonElement> SemFatos =
        new Dictionary<string, JsonElement>();

    private static DocumentoExigido Geral(Guid? exigidoNaFaseId = null, Guid? grupoSatisfacaoId = null) =>
        DocumentoExigido.Criar(
            exigidoNaFaseId ?? FaseA,
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "IDENTIDADE",
            tipoDocumentoNome: "Documento de identidade",
            tipoDocumentoCategoria: "PESSOAL",
            Aplicabilidade.Geral,
            obrigatorio: true,
            consequenciaIndeferimento: null,
            grupoSatisfacaoId,
            condicoes: [],
            basesLegais: [],
            idadeMaximaEmissao: null,
            formatoPermitido: null,
            tamanhoMaximoBytes: null).Value!;

    private static DocumentoExigido Condicional(string fato, string valor, Guid? exigidoNaFaseId = null)
    {
        CondicaoGatilho condicao = CondicaoGatilho.Criar(
            0, fato, Operador.Igual, JsonSerializer.SerializeToElement(valor)).Value!;
        return DocumentoExigido.Criar(
            exigidoNaFaseId ?? FaseA,
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "LAUDO",
            tipoDocumentoNome: "Laudo médico",
            tipoDocumentoCategoria: "SAUDE",
            Aplicabilidade.Condicional,
            obrigatorio: true,
            consequenciaIndeferimento: null,
            grupoSatisfacaoId: null,
            condicoes: [condicao],
            basesLegais: [],
            idadeMaximaEmissao: null,
            formatoPermitido: null,
            tamanhoMaximoBytes: null).Value!;
    }

    private static Dictionary<string, JsonElement> Fatos(string chave, string valor) =>
        new() { [chave] = JsonSerializer.SerializeToElement(valor) };

    private static Dictionary<Guid, ApresentacaoDocumento> ApresentacaoPara(Guid exigenciaId) =>
        new() { [exigenciaId] = new(Guid.CreateVersion7()) };

    // ── ADR-0076 — erros nomeados, nunca resultado vazio ──

    [Fact(DisplayName = "Snapshot ausente (bloco null) retorna erro nomeado")]
    public void Resolver_SnapshotAusente_RetornaErroNomeado()
    {
        Result<ResultadoResolucaoExigencias> resultado = ResolvedorExigenciasDocumentais.Resolver(
            null, SemFatos, SemApresentacoes);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ResolvedorExigencias.SnapshotAusente");
    }

    [Fact(DisplayName = "Bloco estruturalmente inválido (exigenciaId repetido) retorna erro nomeado")]
    public void Resolver_BlocoComIdentidadeRepetida_RetornaErroNomeado()
    {
        DocumentoExigido exigencia = Geral();
        BlocoExigenciasCongelado bloco = BlocoExigenciasCongelado.DeGrafoReidratado([exigencia, exigencia]);

        Result<ResultadoResolucaoExigencias> resultado = ResolvedorExigenciasDocumentais.Resolver(
            bloco, SemFatos, SemApresentacoes);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ResolvedorExigencias.BlocoEstruturalmenteInvalido");
    }

    [Fact(DisplayName = "Bloco semanticamente inválido (grupo de satisfação fora do escopo processo+fase) retorna erro nomeado")]
    public void Resolver_GrupoSatisfacaoEntreFasesDiferentes_RetornaErroNomeado()
    {
        Guid grupo = Guid.CreateVersion7();
        DocumentoExigido naFaseA = Geral(FaseA, grupo);
        DocumentoExigido naFaseB = Geral(FaseB, grupo);
        BlocoExigenciasCongelado bloco = BlocoExigenciasCongelado.DeGrafoReidratado([naFaseA, naFaseB]);

        Result<ResultadoResolucaoExigencias> resultado = ResolvedorExigenciasDocumentais.Resolver(
            bloco, SemFatos, SemApresentacoes);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ResolvedorExigencias.BlocoSemanticamenteInvalido");
    }

    // ── Aplicabilidade ──

    [Fact(DisplayName = "GERAL sem apresentação resolve Pendente")]
    public void Resolver_GeralSemApresentacao_Pendente()
    {
        DocumentoExigido exigencia = Geral();
        BlocoExigenciasCongelado bloco = BlocoExigenciasCongelado.DeGrafoReidratado([exigencia]);

        ResultadoResolucaoExigencias resultado = ResolvedorExigenciasDocumentais.Resolver(
            bloco, SemFatos, SemApresentacoes).Value!;

        resultado.Exigencias.Should().ContainSingle(
            e => e.ExigenciaId == exigencia.Id && e.Status == StatusResolucaoExigencia.Pendente && e.ApresentacaoId == null);
    }

    [Fact(DisplayName = "GERAL com apresentação resolve Satisfeita, referenciando a apresentação")]
    public void Resolver_GeralComApresentacao_Satisfeita()
    {
        DocumentoExigido exigencia = Geral();
        BlocoExigenciasCongelado bloco = BlocoExigenciasCongelado.DeGrafoReidratado([exigencia]);
        Dictionary<Guid, ApresentacaoDocumento> apresentacoes = ApresentacaoPara(exigencia.Id);

        ResultadoResolucaoExigencias resultado = ResolvedorExigenciasDocumentais.Resolver(
            bloco, SemFatos, apresentacoes).Value!;

        ExigenciaResolvida veredicto = resultado.Exigencias.Single();
        veredicto.Status.Should().Be(StatusResolucaoExigencia.Satisfeita);
        veredicto.ApresentacaoId.Should().Be(apresentacoes[exigencia.Id].Id);
    }

    [Fact(DisplayName = "CONDICIONAL cujo gatilho DNF não casa resolve NaoAplicavel, mesmo com apresentação")]
    public void Resolver_CondicionalSemCasar_NaoAplicavel()
    {
        DocumentoExigido exigencia = Condicional("CONDICAO_ATENDIMENTO", "PCD");
        BlocoExigenciasCongelado bloco = BlocoExigenciasCongelado.DeGrafoReidratado([exigencia]);
        Dictionary<string, JsonElement> fatos = Fatos("CONDICAO_ATENDIMENTO", "NENHUMA");
        Dictionary<Guid, ApresentacaoDocumento> apresentacoes = ApresentacaoPara(exigencia.Id);

        ResultadoResolucaoExigencias resultado = ResolvedorExigenciasDocumentais.Resolver(
            bloco, fatos, apresentacoes).Value!;

        ExigenciaResolvida veredicto = resultado.Exigencias.Single();
        veredicto.Status.Should().Be(StatusResolucaoExigencia.NaoAplicavel);
        veredicto.ApresentacaoId.Should().BeNull("uma exigência não-aplicável não tem apresentação a reportar, mesmo que uma exista");
    }

    [Fact(DisplayName = "CONDICIONAL cujo gatilho DNF casa e sem apresentação resolve Pendente")]
    public void Resolver_CondicionalCasaSemApresentacao_Pendente()
    {
        DocumentoExigido exigencia = Condicional("CONDICAO_ATENDIMENTO", "PCD");
        BlocoExigenciasCongelado bloco = BlocoExigenciasCongelado.DeGrafoReidratado([exigencia]);
        Dictionary<string, JsonElement> fatos = Fatos("CONDICAO_ATENDIMENTO", "PCD");

        ResultadoResolucaoExigencias resultado = ResolvedorExigenciasDocumentais.Resolver(
            bloco, fatos, SemApresentacoes).Value!;

        resultado.Exigencias.Single().Status.Should().Be(StatusResolucaoExigencia.Pendente);
    }

    [Fact(DisplayName = "CONDICIONAL cujo gatilho DNF casa e com apresentação resolve Satisfeita")]
    public void Resolver_CondicionalCasaComApresentacao_Satisfeita()
    {
        DocumentoExigido exigencia = Condicional("CONDICAO_ATENDIMENTO", "PCD");
        BlocoExigenciasCongelado bloco = BlocoExigenciasCongelado.DeGrafoReidratado([exigencia]);
        Dictionary<string, JsonElement> fatos = Fatos("CONDICAO_ATENDIMENTO", "PCD");
        Dictionary<Guid, ApresentacaoDocumento> apresentacoes = ApresentacaoPara(exigencia.Id);

        ResultadoResolucaoExigencias resultado = ResolvedorExigenciasDocumentais.Resolver(
            bloco, fatos, apresentacoes).Value!;

        resultado.Exigencias.Single().Status.Should().Be(StatusResolucaoExigencia.Satisfeita);
    }

    // ── CA-09 — correlação por identidade ──

    [Fact(DisplayName = "CA-09: duas exigências do MESMO TipoDocumento resolvem por identidade distinta — uma apresentação satisfaz só a sua")]
    public void Resolver_MesmoTipoDocumento_ResolvePorIdentidade()
    {
        DocumentoExigido primeira = Condicional("CONDICAO_ATENDIMENTO", "PCD");
        DocumentoExigido segunda = Condicional("CONDICAO_ATENDIMENTO", "PCD");
        BlocoExigenciasCongelado bloco = BlocoExigenciasCongelado.DeGrafoReidratado([primeira, segunda]);
        Dictionary<string, JsonElement> fatos = Fatos("CONDICAO_ATENDIMENTO", "PCD");
        Dictionary<Guid, ApresentacaoDocumento> apresentacoes = ApresentacaoPara(primeira.Id);

        ResultadoResolucaoExigencias resultado = ResolvedorExigenciasDocumentais.Resolver(
            bloco, fatos, apresentacoes).Value!;

        resultado.Exigencias.Should().ContainSingle(e => e.ExigenciaId == primeira.Id && e.Status == StatusResolucaoExigencia.Satisfeita);
        resultado.Exigencias.Should().ContainSingle(e => e.ExigenciaId == segunda.Id && e.Status == StatusResolucaoExigencia.Pendente);
    }

    // ── Grupo de satisfação ──

    [Fact(DisplayName = "Grupo de satisfação: uma apresentação satisfaz TODAS as exigências aplicáveis do grupo")]
    public void Resolver_GrupoSatisfacao_UmaApresentacaoSatisfazGrupoInteiro()
    {
        Guid grupo = Guid.CreateVersion7();
        DocumentoExigido membro1 = Geral(FaseA, grupo);
        DocumentoExigido membro2 = Geral(FaseA, grupo);
        BlocoExigenciasCongelado bloco = BlocoExigenciasCongelado.DeGrafoReidratado([membro1, membro2]);
        Dictionary<Guid, ApresentacaoDocumento> apresentacoes = ApresentacaoPara(membro1.Id);

        ResultadoResolucaoExigencias resultado = ResolvedorExigenciasDocumentais.Resolver(
            bloco, SemFatos, apresentacoes).Value!;

        resultado.Exigencias.Should().AllSatisfy(e => e.Status.Should().Be(StatusResolucaoExigencia.Satisfeita));
        resultado.Exigencias.Should().ContainSingle(e => e.ExigenciaId == membro2.Id && e.ApresentacaoId == apresentacoes[membro1.Id].Id,
            "a exigência satisfeita pelo grupo referencia a apresentação que de fato a satisfez, mesmo sendo de uma exigência-irmã");
    }

    [Fact(DisplayName = "Grupo de satisfação: sem apresentação de nenhum membro, todos ficam Pendente")]
    public void Resolver_GrupoSatisfacaoSemApresentacao_TodosPendentes()
    {
        Guid grupo = Guid.CreateVersion7();
        DocumentoExigido membro1 = Geral(FaseA, grupo);
        DocumentoExigido membro2 = Geral(FaseA, grupo);
        BlocoExigenciasCongelado bloco = BlocoExigenciasCongelado.DeGrafoReidratado([membro1, membro2]);

        ResultadoResolucaoExigencias resultado = ResolvedorExigenciasDocumentais.Resolver(
            bloco, SemFatos, SemApresentacoes).Value!;

        resultado.Exigencias.Should().AllSatisfy(e => e.Status.Should().Be(StatusResolucaoExigencia.Pendente));
    }

    [Fact(DisplayName = "Grupo de satisfação: membro NÃO aplicável (CONDICIONAL sem casar) não é satisfeito pelo grupo, e não contamina os demais")]
    public void Resolver_GrupoSatisfacaoComMembroNaoAplicavel_NaoInterfereNosDemais()
    {
        Guid grupo = Guid.CreateVersion7();
        DocumentoExigido geral = Geral(FaseA, grupo);
        CondicaoGatilho condicaoQueNaoCasa = CondicaoGatilho.Criar(
            0, "CONDICAO_ATENDIMENTO", Operador.Igual, JsonSerializer.SerializeToElement("PCD")).Value!;
        DocumentoExigido condicionalSemCasar = DocumentoExigido.Criar(
            FaseA, Guid.CreateVersion7(), "LAUDO", "Laudo médico", "SAUDE", Aplicabilidade.Condicional,
            obrigatorio: true, consequenciaIndeferimento: null, grupo,
            condicoes: [condicaoQueNaoCasa], basesLegais: [], idadeMaximaEmissao: null,
            formatoPermitido: null, tamanhoMaximoBytes: null).Value!;
        BlocoExigenciasCongelado bloco = BlocoExigenciasCongelado.DeGrafoReidratado([geral, condicionalSemCasar]);
        Dictionary<Guid, ApresentacaoDocumento> apresentacoes = ApresentacaoPara(geral.Id);

        ResultadoResolucaoExigencias resultado = ResolvedorExigenciasDocumentais.Resolver(
            bloco, SemFatos, apresentacoes).Value!;

        resultado.Exigencias.Should().ContainSingle(e => e.ExigenciaId == geral.Id && e.Status == StatusResolucaoExigencia.Satisfeita);
        resultado.Exigencias.Should().ContainSingle(e => e.ExigenciaId == condicionalSemCasar.Id && e.Status == StatusResolucaoExigencia.NaoAplicavel);
    }

    // ── Contraprova geral ──

    [Fact(DisplayName = "Bloco vazio resolve com sucesso e lista vazia (nenhuma exigência configurada é estado válido)")]
    public void Resolver_BlocoVazio_Aceita()
    {
        BlocoExigenciasCongelado bloco = BlocoExigenciasCongelado.DeGrafoReidratado([]);

        Result<ResultadoResolucaoExigencias> resultado = ResolvedorExigenciasDocumentais.Resolver(
            bloco, SemFatos, SemApresentacoes);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Exigencias.Should().BeEmpty();
    }
}
