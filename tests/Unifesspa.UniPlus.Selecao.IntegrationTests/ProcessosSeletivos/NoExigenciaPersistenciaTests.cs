namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

/// <summary>
/// Cobertura de integração (Postgres real via Testcontainers) da árvore de satisfação
/// (Story #920) — persiste via <see cref="ProcessoSeletivoRepository"/> e recarrega em uma
/// SESSÃO NOVA do DbContext (simulando uma requisição HTTP diferente), pelo MESMO caminho de
/// produção (<see cref="ProcessoSeletivoRepository.ObterParaMutacaoAsync"/>) — não uma query
/// de teste hand-rolled. Achado de revisão (Codex, rodada de review desta PR): sem o
/// <c>Include(p =&gt; p.NosExigencia)</c> no repositório, a coleção tracked nascia vazia em
/// todo carregamento novo do agregado — furo real de "perda silenciosa" já no caminho
/// ordinário (toda requisição recarrega o agregado do zero). Story #923: o antigo gate
/// fail-closed de publicação (<c>PendenciaDaArvoreDeSatisfacaoAindaNaoPublicavel</c>, que
/// este teste originalmente usava como prova indireta de que a árvore recarregada era a
/// mesma) foi removido — o envelope canônico agora congela a topologia inteira
/// (<c>arvoreSatisfacao</c>) — e a prova passa a ser direta: os nós/filhos/consequência/base
/// legal recarregados batem byte a byte com o que foi definido.
/// </summary>
public sealed class NoExigenciaPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private readonly ProcessoSeletivoDbFixture _fixture;

    public NoExigenciaPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
    }

    private static DocumentoExigido Documento(Guid faseId) => DocumentoExigido.Criar(
        faseId, Guid.CreateVersion7(), "IDENTIDADE", "Documento de identidade", "PESSOAL",
        Aplicabilidade.Geral, obrigatorio: true, consequenciaIndeferimento: null,
        [], [], null, FormatosPermitidos.Criar(true, null).Value!, null).Value!;

    [Fact(DisplayName = "Persiste e recarrega uma árvore OU[E[RG,CPF],CIN] em sessão NOVA do DbContext, pelo caminho de produção (ObterParaMutacaoAsync)")]
    public async Task PersisteERecarrega_ArvoreComGrupos_EmSessaoNova()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Árvore", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);
        FaseCronograma fase = FaseCronograma.Criar(
            1, Guid.CreateVersion7(), "INSCRICAO", "CEPS", OrigemDataFase.Delegada,
            agrupaEtapas: false, permiteComplementacao: false, produzResultado: false,
            resultadoDefinitivo: false, coletaInscricao: false, inicio: null, fim: null,
            atoProduzidoCodigo: null, atoProduzidoEfeitoIrreversivel: false,
            bancasRequeridas: [], regraRecurso: null).Value!;
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        DocumentoExigido rg = Documento(fase.Id);
        DocumentoExigido cpf = Documento(fase.Id);
        DocumentoExigido cin = Documento(fase.Id);
        NoExigencia grupoE = NoExigencia.CriarGrupo(
            TipoNo.GrupoE, 0, null, null, [],
            [NoExigencia.CriarFolha(rg, 0).Value!, NoExigencia.CriarFolha(cpf, 1).Value!]).Value!;
        NoExigenciaBaseLegal baseLegalDoGrupo = NoExigenciaBaseLegal.Criar(
            "Res. Unifesspa 532/2021", TipoAbrangencia.InternaEdital, StatusBaseLegal.Resolvido, null).Value!;
        NoExigencia raiz = NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, 0, 1, "ELIMINA", [baseLegalDoGrupo], [grupoE, NoExigencia.CriarFolha(cin, 1).Value!]).Value!;

        Result definirResult = processo.DefinirDocumentosExigidos([raiz], PrecondicaoIfMatch.Ausente);
        definirResult.IsSuccess.Should().BeTrue(definirResult.Error?.Message);

        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        // Sessão NOVA — simula uma requisição HTTP diferente da que definiu a árvore. Pelo
        // caminho REAL de produção, não uma query de teste (é justamente o Include deste
        // método que o achado de revisão apontou faltando).
        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        ProcessoSeletivoRepository readRepository = new(readContext, TimeProvider.System);
        ProcessoSeletivo? recarregado = await readRepository.ObterParaMutacaoAsync(processo.Id, CancellationToken.None);

        recarregado.Should().NotBeNull();
        recarregado!.NosExigencia.Should().HaveCount(5, "raiz OU + grupo E + 3 folhas (RG, CPF, CIN)");

        NoExigencia raizRecarregada = recarregado.RaizesDeExigencia.Should().ContainSingle().Which;
        raizRecarregada.Tipo.Should().Be(TipoNo.GrupoOu);
        raizRecarregada.Consequencia.Should().Be("ELIMINA");
        raizRecarregada.BasesLegais.Should().ContainSingle().Which.Status.Should().Be(StatusBaseLegal.Resolvido);
        raizRecarregada.Filhos.Should().HaveCount(2, "grupo E + folha CIN — fix-up do EF a partir da self-FK NoPaiId, sem ThenInclude recursivo");

        NoExigencia grupoERecarregado = raizRecarregada.Filhos.Should().ContainSingle(n => n.Tipo == TipoNo.GrupoE).Which;
        grupoERecarregado.Filhos.Should().HaveCount(2);
        grupoERecarregado.Filhos.Should().OnlyContain(f => f.Tipo == TipoNo.Folha && f.DocumentoExigido != null,
            "NoExigencia.DocumentoExigido é fixed-up automaticamente a partir da MESMA instância trazida por DocumentosExigidos");

        // Story #923: a árvore com grupos E/OU já é publicável — sem pendência estrutural
        // remanescente para esta configuração mínima (sem REMOVE_VANTAGEM/modalidade
        // incoerente, sem exigência CONDICIONAL vazia, sem gatilho por FAIXA_ETARIA).
        DomainError? pendencia = recarregado.PendenciaPreCanonicalizacao();
        pendencia.Should().BeNull(pendencia?.Message);
    }
}
