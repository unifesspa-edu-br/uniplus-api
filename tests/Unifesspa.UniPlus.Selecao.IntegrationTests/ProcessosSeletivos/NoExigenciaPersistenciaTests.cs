namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

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

    /// <summary>
    /// Issue #943: <c>nos_exigencia</c> é auto-referenciada (<c>no_pai_id → id</c>, Restrict) —
    /// substituir uma árvore que já tem um grupo E/OU por outra (ou pela mesma, de novo) faz o
    /// EF Core apagar bottom-up (netos → raiz) mas inserir a raiz NOVA antes da antiga sair do
    /// banco, dentro do mesmo <c>SaveChangesAsync</c>. Um índice único comum era verificado
    /// por-statement e recusava a raiz nova com a MESMA ordem da antiga — mesmo a transação
    /// terminando num estado final válido. A correção troca os dois índices únicos filtrados
    /// (<c>ux_nos_exigencia_raiz_ordem</c>/<c>ux_nos_exigencia_irmaos_ordem</c>) por exclusion
    /// constraints GiST <c>DEFERRABLE INITIALLY DEFERRED</c> (mesmo padrão de
    /// <c>ex_tipo_ato_publicado_codigo_vigencia</c>), que só checam no <c>COMMIT</c>.
    /// </summary>
    [Fact(DisplayName = "Issue #943: substituir uma árvore com grupo E/OU por outra, várias vezes seguidas na mesma sessão, não falha com 500")]
    public async Task SubstituiArvoreComGrupo_PorOutrasArvores_MultiplasVezesSeguidas()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Substituição de árvore", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);
        FaseCronograma fase = FaseCronograma.Criar(
            1, Guid.CreateVersion7(), "INSCRICAO", "CEPS", OrigemDataFase.Delegada,
            agrupaEtapas: false, permiteComplementacao: false, produzResultado: false,
            resultadoDefinitivo: false, coletaInscricao: false, inicio: null, fim: null,
            atoProduzidoCodigo: null, atoProduzidoEfeitoIrreversivel: false,
            bancasRequeridas: [], regraRecurso: null).Value!;
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        await using SelecaoDbContext context = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = new(context, TimeProvider.System);
        await repository.AdicionarAsync(processo, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        // Três substituições seguidas na MESMA sessão de DbContext — a raiz de cada árvore usa
        // Ordem 0, a mesma da raiz anterior, exatamente o cenário que colide com o índice
        // não-deferível. A 1ª chamada não tem nada para colidir (árvore vazia -> com grupo);
        // são a 2ª e a 3ª que exercitam a substituição de uma árvore-com-grupo por outra.
        for (int tentativa = 1; tentativa <= 3; tentativa++)
        {
            NoExigencia grupoE = NoExigencia.CriarGrupo(
                TipoNo.GrupoE, 0, null, null, [],
                [NoExigencia.CriarFolha(Documento(fase.Id), 0).Value!, NoExigencia.CriarFolha(Documento(fase.Id), 1).Value!]).Value!;

            Result definirResult = processo.DefinirDocumentosExigidos([grupoE], PrecondicaoIfMatch.Curinga);
            definirResult.IsSuccess.Should().BeTrue(definirResult.Error?.Message);

            Func<Task> salvar = async () => await context.SaveChangesAsync(CancellationToken.None);
            await salvar.Should().NotThrowAsync(
                $"tentativa {tentativa}: substituir uma árvore com grupo E/OU por outra não pode falhar com " +
                "violação de índice único transitória (issue #943)");
        }

        processo.RaizesDeExigencia.Should().ContainSingle().Which.Tipo.Should().Be(TipoNo.GrupoE);
    }

    /// <summary>
    /// As duas exclusion constraints vivem em SQL cru, fora do <c>ModelSnapshot</c> (mesmo
    /// padrão de <c>ex_tipo_ato_publicado_codigo_vigencia</c>,
    /// <c>TipoAtoPublicadoPersistenceTests.ExclusionConstraint_ExisteNoCatalogo</c>): um
    /// squash de migrations as descartaria em silêncio, e o teste de substituição acima
    /// continuaria passando contra um Testcontainer criado do zero — só voltaria a falhar em
    /// produção, contra um banco com histórico de migrations real. Esta asserção olha o
    /// catálogo do Postgres diretamente: <c>contype = 'x'</c> (EXCLUDE) e
    /// <c>condeferrable AND condeferred</c> (a checagem só no COMMIT é o que resolve a
    /// issue #943 — sem isso, seria só um EXCLUDE normal, tão imediato quanto o índice
    /// único que substituiu).
    /// </summary>
    [Theory(DisplayName = "As exclusion constraints de ordem existem no catálogo, como EXCLUDE deferível")]
    [InlineData("ex_nos_exigencia_raiz_ordem")]
    [InlineData("ex_nos_exigencia_irmaos_ordem")]
    public async Task ExclusionConstraintsDeOrdem_ExistemNoCatalogo_ComoDeferiveis(string nomeDaConstraint)
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        await context.Database.OpenConnectionAsync();

        await using NpgsqlCommand command = new(
            """
            SELECT contype::text, condeferrable, condeferred
            FROM pg_constraint c
            JOIN pg_class t ON t.oid = c.conrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace
            WHERE n.nspname = 'selecao'
              AND t.relname = 'nos_exigencia'
              AND c.conname = @nome
            """,
            (NpgsqlConnection)context.Database.GetDbConnection());
        command.Parameters.AddWithValue("nome", nomeDaConstraint);

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue($"a constraint '{nomeDaConstraint}' deveria existir no catálogo");

        reader.GetString(0).Should().Be("x", "'x' é o contype de EXCLUDE no pg_constraint");
        reader.GetBoolean(1).Should().BeTrue("a checagem deferível é o que evita a colisão transitória da issue #943");
        reader.GetBoolean(2).Should().BeTrue("INITIALLY DEFERRED — checada só no COMMIT, não a cada statement");
    }
}
