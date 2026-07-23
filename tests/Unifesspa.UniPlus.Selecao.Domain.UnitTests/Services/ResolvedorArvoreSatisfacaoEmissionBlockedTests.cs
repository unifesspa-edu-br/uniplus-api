namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Story #928, §6 — a fronteira de disponibilidade (<c>BLOQUEADO</c>) e o predicado recursivo
/// <c>emissionBlocked</c> sobre a árvore de satisfação. <c>BLOQUEADO</c> não é um 6º estado: a folha
/// projeta <see cref="EstadoSatisfacao.Indeterminado"/> na agregação e tem a emissão suprimida.
/// Cobre os cenários do delta "Consequência por nó e fronteira ativa de emissão" e da requirement
/// "Grafo de dependência executável e ordem de coleta".
/// </summary>
public sealed class ResolvedorArvoreSatisfacaoEmissionBlockedTests
{
    private static readonly Guid FaseId = Guid.CreateVersion7();
    private static readonly FormatosPermitidos Qualquer = FormatosPermitidos.Criar(true, null).Value!;
    private static readonly IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> SemApresentacoes =
        new Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>>();

    private static DocumentoExigido DocumentoGeral(string? consequencia) =>
        DocumentoExigido.Criar(
            FaseId, Guid.CreateVersion7(), "COD", "Nome", "CAT",
            Aplicabilidade.Geral, obrigatorio: true, consequencia, [], [], null, Qualquer, null).Value!;

    private static DocumentoExigido DocumentoCondicional(string fato) =>
        DocumentoExigido.Criar(
            FaseId, Guid.CreateVersion7(), "COD", "Nome", "CAT",
            Aplicabilidade.Condicional, obrigatorio: false, consequenciaIndeferimento: null,
            [CondicaoGatilho.Criar(0, fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!],
            [], null, Qualquer, null).Value!;

    private static Dictionary<string, FatoResolvido> Fatos(params (string Fato, bool Valor)[] fatos) =>
        fatos.ToDictionary(
            static f => f.Fato,
            static f => FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(f.Valor)),
            StringComparer.Ordinal);

    private static ArvoreExigenciasCongelada Arvore(params NoExigencia[] raizes) =>
        ArvoreExigenciasCongelada.DeGrafoReidratado(raizes);

    private static Result<ResultadoResolucaoArvore> Resolver(
        ArvoreExigenciasCongelada arvore,
        IReadOnlyDictionary<string, FatoResolvido>? fatos = null,
        IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>>? apresentacoes = null,
        IReadOnlySet<Guid>? bloqueadas = null) =>
        ResolvedorArvoreSatisfacao.Resolver(
            arvore, fatos ?? new Dictionary<string, FatoResolvido>(StringComparer.Ordinal),
            apresentacoes ?? SemApresentacoes, null, bloqueadas);

    [Fact(DisplayName = "Folha BLOQUEADA sob E projeta INDETERMINADO, não emite; o irmão desbloqueado emite")]
    public void FolhaBloqueadaSobE_NaoEmite_IrmaoEmite()
    {
        DocumentoExigido bloqueado = DocumentoGeral("ELIMINA");
        DocumentoExigido aberto = DocumentoGeral("REMOVE_VANTAGEM");
        NoExigencia folhaBloqueada = NoExigencia.CriarFolha(bloqueado, 0).Value!;
        NoExigencia folhaAberta = NoExigencia.CriarFolha(aberto, 1).Value!;
        NoExigencia raiz = NoExigencia.CriarGrupo(TipoNo.GrupoE, 0, null, null, [], [folhaBloqueada, folhaAberta]).Value!;

        ResultadoResolucaoArvore r = Resolver(Arvore(raiz), bloqueadas: new HashSet<Guid> { bloqueado.Id }).Value!;

        r.EstadosPorNo[folhaBloqueada.Id].Should().Be(EstadoSatisfacao.Indeterminado, "a folha bloqueada projeta INDETERMINADO");
        r.NosEmissaoSuprimida.Should().Contain(folhaBloqueada.Id);
        r.ConsequenciasVigentes.Should().ContainSingle(c => c.NoExigenciaId == folhaAberta.Id && c.Consequencia == "REMOVE_VANTAGEM");
        r.ConsequenciasVigentes.Should().NotContain(c => c.NoExigenciaId == folhaBloqueada.Id, "o ramo bloqueado não emite");
    }

    [Fact(DisplayName = "OU opaco cuja fronteira decisiva é toda bloqueada suprime a própria consequência")]
    public void OuComFronteiraDecisivaBloqueada_Suprime()
    {
        // OU[N=1, E[BLOQUEADO, NAO_APLICAVEL], NAO_APLICAVEL]
        DocumentoExigido bloqueado = DocumentoGeral("ELIMINA");
        DocumentoExigido naoAplicavelDentro = DocumentoCondicional("GATE");
        DocumentoExigido naoAplicavelFora = DocumentoCondicional("GATE");
        NoExigencia folhaBloqueada = NoExigencia.CriarFolha(bloqueado, 0).Value!;
        NoExigencia folhaNaDentro = NoExigencia.CriarFolha(naoAplicavelDentro, 1).Value!;
        NoExigencia grupoE = NoExigencia.CriarGrupo(TipoNo.GrupoE, 0, null, null, [], [folhaBloqueada, folhaNaDentro]).Value!;
        NoExigencia folhaNaFora = NoExigencia.CriarFolha(naoAplicavelFora, 1).Value!;
        NoExigencia raiz = NoExigencia.CriarGrupo(TipoNo.GrupoOu, 0, 1, "RECLASSIFICA_AC", [], [grupoE, folhaNaFora]).Value!;

        // GATE=false → as duas folhas condicionais são NAO_APLICAVEL; a bloqueada é a única decisiva.
        ResultadoResolucaoArvore r = Resolver(
            Arvore(raiz), fatos: Fatos(("GATE", false)), bloqueadas: new HashSet<Guid> { bloqueado.Id }).Value!;

        r.NosEmissaoSuprimida.Should().Contain(raiz.Id, "toda a fronteira decisiva do OU está bloqueada");
        r.NosEmissaoSuprimida.Should().Contain(grupoE.Id);
        r.ConsequenciasVigentes.Should().BeEmpty("o OU opaco não emite a própria consequência sob fronteira bloqueada");
    }

    [Fact(DisplayName = "A mesma árvore com a razão decisiva alcançada (não bloqueada) emite a consequência do OU")]
    public void OuComRazaoAlcancada_Emite()
    {
        DocumentoExigido alcancavel = DocumentoGeral("ELIMINA");
        DocumentoExigido naoAplicavelDentro = DocumentoCondicional("GATE");
        DocumentoExigido naoAplicavelFora = DocumentoCondicional("GATE");
        NoExigencia folha = NoExigencia.CriarFolha(alcancavel, 0).Value!;
        NoExigencia folhaNaDentro = NoExigencia.CriarFolha(naoAplicavelDentro, 1).Value!;
        NoExigencia grupoE = NoExigencia.CriarGrupo(TipoNo.GrupoE, 0, null, null, [], [folha, folhaNaDentro]).Value!;
        NoExigencia folhaNaFora = NoExigencia.CriarFolha(naoAplicavelFora, 1).Value!;
        NoExigencia raiz = NoExigencia.CriarGrupo(TipoNo.GrupoOu, 0, 1, "RECLASSIFICA_AC", [], [grupoE, folhaNaFora]).Value!;

        // Nada bloqueado: a folha alcançável fica pendente, a fronteira decisiva do OU não é bloqueada.
        ResultadoResolucaoArvore r = Resolver(Arvore(raiz), fatos: Fatos(("GATE", false))).Value!;

        r.NosEmissaoSuprimida.Should().BeEmpty();
        r.ConsequenciasVigentes.Should().ContainSingle(c => c.NoExigenciaId == raiz.Id && c.Consequencia == "RECLASSIFICA_AC");
    }

    [Fact(DisplayName = "N-de(N=2)[SATISFEITO, NAO_APLICAVEL] é IMPOSSIVEL e emite — fronteira vazia não bloqueia")]
    public void NDeImpossivel_SemBloqueio_Emite()
    {
        DocumentoExigido satisfeito = DocumentoGeral(null);
        DocumentoExigido naoAplicavel = DocumentoCondicional("GATE");
        NoExigencia folhaSat = NoExigencia.CriarFolha(satisfeito, 0).Value!;
        NoExigencia folhaNa = NoExigencia.CriarFolha(naoAplicavel, 1).Value!;
        NoExigencia raiz = NoExigencia.CriarGrupo(TipoNo.GrupoOu, 0, 2, "ELIMINA", [], [folhaSat, folhaNa]).Value!;

        Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresenta = new()
        {
            [satisfeito.Id] = [new ApresentacaoDocumento(Guid.CreateVersion7())],
        };
        ResultadoResolucaoArvore r = Resolver(Arvore(raiz), fatos: Fatos(("GATE", false)), apresentacoes: apresenta).Value!;

        r.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.Impossivel, "1 satisfeito < 2 exigidos e o resto é NAO_APLICAVEL");
        r.NosEmissaoSuprimida.Should().BeEmpty("IMPOSSIVEL decidido emite o inevitável, nunca é emissionBlocked");
        r.ConsequenciasVigentes.Should().ContainSingle(c => c.NoExigenciaId == raiz.Id && c.Consequencia == "ELIMINA");
    }

    [Fact(DisplayName = "Desbloqueio reavalia sem latch: a mesma árvore emite quando a folha deixa o conjunto bloqueado")]
    public void Desbloqueio_ReavaliaSemLatch()
    {
        DocumentoExigido documento = DocumentoGeral("ELIMINA");
        NoExigencia folha = NoExigencia.CriarFolha(documento, 0).Value!;
        NoExigencia raiz = NoExigencia.CriarGrupo(TipoNo.GrupoE, 0, null, null, [], [folha]).Value!;
        ArvoreExigenciasCongelada arvore = Arvore(raiz);

        ResultadoResolucaoArvore bloqueado = Resolver(arvore, bloqueadas: new HashSet<Guid> { documento.Id }).Value!;
        bloqueado.NosEmissaoSuprimida.Should().Contain(folha.Id);
        bloqueado.ConsequenciasVigentes.Should().BeEmpty("bloqueada, não emite");

        ResultadoResolucaoArvore desbloqueado = Resolver(arvore).Value!;
        desbloqueado.NosEmissaoSuprimida.Should().BeEmpty("sem latch — o desbloqueio reavalia do zero");
        desbloqueado.ConsequenciasVigentes.Should().ContainSingle(c => c.NoExigenciaId == folha.Id && c.Consequencia == "ELIMINA");
    }

    [Fact(DisplayName = "Folha BLOQUEADA sob grupo IMPOSSIVEL continua suprimida — o grupo emite o inevitável, a folha não")]
    public void FolhaBloqueadaSobGrupoImpossivel_ContinuaSuprimida()
    {
        // E[BLOQUEADA(ELIMINA), OU N=2[SATISFEITO, NAO_APLICAVEL]] — o OU interno é IMPOSSIVEL, então
        // o E é IMPOSSIVEL. A folha bloqueada não pode emitir só porque o ancestral virou terminal.
        DocumentoExigido bloqueado = DocumentoGeral("ELIMINA");
        DocumentoExigido satisfeito = DocumentoGeral(null);
        DocumentoExigido naoAplicavel = DocumentoCondicional("GATE");
        NoExigencia folhaBloqueada = NoExigencia.CriarFolha(bloqueado, 0).Value!;
        NoExigencia folhaSat = NoExigencia.CriarFolha(satisfeito, 0).Value!;
        NoExigencia folhaNa = NoExigencia.CriarFolha(naoAplicavel, 1).Value!;
        NoExigencia ouInterno = NoExigencia.CriarGrupo(TipoNo.GrupoOu, 1, 2, "RECLASSIFICA_AC", [], [folhaSat, folhaNa]).Value!;
        NoExigencia raiz = NoExigencia.CriarGrupo(TipoNo.GrupoE, 0, null, null, [], [folhaBloqueada, ouInterno]).Value!;

        Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresenta = new()
        {
            [satisfeito.Id] = [new ApresentacaoDocumento(Guid.CreateVersion7())],
        };
        ResultadoResolucaoArvore r = Resolver(
            Arvore(raiz), fatos: Fatos(("GATE", false)), apresentacoes: apresenta,
            bloqueadas: new HashSet<Guid> { bloqueado.Id }).Value!;

        r.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.Impossivel);
        r.NosEmissaoSuprimida.Should().Contain(folhaBloqueada.Id, "a folha bloqueada sob grupo terminal continua suprimida");
        r.ConsequenciasVigentes.Should().NotContain(c => c.NoExigenciaId == folhaBloqueada.Id, "a folha bloqueada não emite o ELIMINA");
        r.ConsequenciasVigentes.Should().Contain(c => c.NoExigenciaId == ouInterno.Id && c.Consequencia == "RECLASSIFICA_AC", "o OU IMPOSSIVEL emite o inevitável");
    }

    [Fact(DisplayName = "OU N-de IMPOSSIVEL não gera orientação para o filho BLOQUEADO")]
    public void OuNDeImpossivel_NaoOrientaFilhoBloqueado()
    {
        // OU N=3[SATISFEITO, NAO_APLICAVEL, BLOQUEADA]: maximo atingível 2 < 3 → IMPOSSIVEL; a folha
        // bloqueada não deve virar orientação (não se pede o que não está disponível).
        DocumentoExigido satisfeito = DocumentoGeral(null);
        DocumentoExigido naoAplicavel = DocumentoCondicional("GATE");
        DocumentoExigido bloqueado = DocumentoGeral(null);
        NoExigencia folhaSat = NoExigencia.CriarFolha(satisfeito, 0).Value!;
        NoExigencia folhaNa = NoExigencia.CriarFolha(naoAplicavel, 1).Value!;
        NoExigencia folhaBloqueada = NoExigencia.CriarFolha(bloqueado, 2).Value!;
        NoExigencia raiz = NoExigencia.CriarGrupo(TipoNo.GrupoOu, 0, 3, "ELIMINA", [], [folhaSat, folhaNa, folhaBloqueada]).Value!;

        Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresenta = new()
        {
            [satisfeito.Id] = [new ApresentacaoDocumento(Guid.CreateVersion7())],
        };
        ResultadoResolucaoArvore r = Resolver(
            Arvore(raiz), fatos: Fatos(("GATE", false)), apresentacoes: apresenta,
            bloqueadas: new HashSet<Guid> { bloqueado.Id }).Value!;

        r.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.Impossivel);
        r.NosEmissaoSuprimida.Should().Contain(folhaBloqueada.Id);
        r.PendenciasDeOrientacao.Should().NotContain(p => p.NoExigenciaId == folhaBloqueada.Id, "a folha bloqueada não vira orientação");
        r.ConsequenciasVigentes.Should().Contain(c => c.NoExigenciaId == raiz.Id && c.Consequencia == "ELIMINA");
    }

    [Fact(DisplayName = "Sem conjunto bloqueado, a resolução é idêntica à anterior — a fronteira não muda o comportamento existente")]
    public void SemBloqueio_ComportamentoInalterado()
    {
        DocumentoExigido a = DocumentoGeral("ELIMINA");
        DocumentoExigido b = DocumentoGeral("REMOVE_VANTAGEM");
        NoExigencia folhaA = NoExigencia.CriarFolha(a, 0).Value!;
        NoExigencia folhaB = NoExigencia.CriarFolha(b, 1).Value!;
        NoExigencia raiz = NoExigencia.CriarGrupo(TipoNo.GrupoE, 0, null, null, [], [folhaA, folhaB]).Value!;

        ResultadoResolucaoArvore r = Resolver(Arvore(raiz)).Value!;

        r.NosEmissaoSuprimida.Should().BeEmpty();
        r.ConsequenciasVigentes.Should().HaveCount(2, "os dois ramos pendentes emitem, como antes da fronteira");
    }
}
