namespace Unifesspa.UniPlus.Selecao.ArchTests;

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using AwesomeAssertions;

/// <summary>
/// Story #853, CA-15: o gate <b>estrutural</b> (<c>ProcessoSeletivo.PendenciaDeConformidade</c>)
/// não é tocado por esta story — ela acrescenta uma SEGUNDA dimensão (legal), ao lado da
/// primeira, sem remover, substituir ou duplicar nenhuma das chamadas existentes.
/// </summary>
public sealed class GateEstruturalRegressaoTests
{
    [Fact(DisplayName = "PendenciaDeConformidade() continua nos 5 pontos — a prontidão do Domain (uma ordem, duas transições) e 3 handlers que congelam")]
    public void PendenciaDeConformidade_ContinuaNosCincoPontosOriginais()
    {
        string transicoes = File.ReadAllText(CaminhoTransicoes());
        Regex.Count(transicoes, @"PrimeiraPendencia\(").Should().Be(2,
            "TransicoesDaVersao.Publicar e a sucessão compartilhada por Retificar e Fechar " +
            "são os dois pontos de defesa no Domain — a rede que ninguém contorna (ADR-0109 D5)");
        Regex.Count(File.ReadAllText(CaminhoProntidao()), @"Conformidade:\s+\(\)\s+=>\s+ChecklistDeConformidade\.PendenciaDeConformidade\(estado\),").Should().Be(1,
            "os dois pontos de defesa usam a mesma composição dos portões, montada num só lugar");
        Regex.Count(File.ReadAllText(CaminhoProntidao()), @"portoes\.Conformidade\b").Should().Be(1,
            "a prontidão avalia o portão de conformidade uma vez, na posição que a ordem manda");

        int chamadasNoPublicarHandler = ContarChamadas(CaminhoHandler("PublicarProcessoSeletivoCommandHandler.cs"));
        int chamadasNoRetificarHandler = ContarChamadas(CaminhoHandler("RetificarProcessoSeletivoCommandHandler.cs"));
        int chamadasNoFecharHandler = ContarChamadas(CaminhoHandler("FecharRetificacaoCommandHandler.cs"));

        chamadasNoPublicarHandler.Should().Be(1, "Publicar antecipa a mesma recusa que o Domain reconferiria");
        chamadasNoRetificarHandler.Should().Be(1, "Retificar antecipa a mesma recusa que o Domain reconferiria");
        chamadasNoFecharHandler.Should().Be(1, "FecharRetificacao antecipa a mesma recusa que o Domain reconferiria");
    }

    /// <summary>
    /// Story #575 (achado R4-2): a topologia de <c>PendenciaDaCascata()</c> tem os
    /// mesmos cinco call sites de <c>PendenciaDeConformidade()</c>, mas em posições
    /// diferentes por camada — <c>PendenciaDoCronograma()</c> (privada) só é chamada
    /// pela raiz; os handlers da Application nunca a antecipam e não devem passar a
    /// fazê-lo só por esta story. A cascata sempre precede
    /// <c>PendenciaPreCanonicalizacao()</c>/canonicalização, nunca depois.
    /// </summary>
    [Fact(DisplayName = "PendenciaDaCascata() é chamada nos 5 call sites — 2 no Domain, 3 nos handlers que canonicalizam — sempre antes da canonicalização")]
    public void PendenciaDaCascata_AtravessaOsCincoCallSites()
    {
        // O padrão `is { }` (destructuring) isola os call sites de GATE (Publicar,
        // a sucessão, os três handlers) do uso em AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario), que testa
        // `PendenciaDaCascata() is null` só para exibição — mesma chamada, propósito
        // diferente, não é um sexto call site de bloqueio.
        string raiz = File.ReadAllText(CaminhoProntidao());
        Regex.Count(raiz, @"Cascata:\s+\(\)\s+=>\s+CoberturaDaCascata\.PendenciaDaCascata\(estado\),").Should().Be(1, "os dois pontos de defesa compartilham a composição dos portões");
        Regex.Count(File.ReadAllText(CaminhoProntidao()), @"portoes\.Cascata\b").Should().Be(1,
            "a prontidão avalia a cascata uma vez, antes da pré-canonicalização");

        int chamadasNoPublicarHandler = ContarChamadasDeGate(CaminhoHandler("PublicarProcessoSeletivoCommandHandler.cs"));
        int chamadasNoRetificarHandler = ContarChamadasDeGate(CaminhoHandler("RetificarProcessoSeletivoCommandHandler.cs"));
        int chamadasNoFecharHandler = ContarChamadasDeGate(CaminhoHandler("FecharRetificacaoCommandHandler.cs"));

        chamadasNoPublicarHandler.Should().Be(1, "Publicar antecipa a mesma recusa que o Domain reconferiria");
        chamadasNoRetificarHandler.Should().Be(1, "Retificar antecipa a mesma recusa que o Domain reconferiria");
        chamadasNoFecharHandler.Should().Be(1, "FecharRetificacao antecipa a mesma recusa que o Domain reconferiria");
    }

    /// <summary>
    /// Localidade e convenção de contagem são gates da raiz, e só dela: os handlers da
    /// Application não os antecipam. Diferente de <c>PendenciaDeConformidade</c> e
    /// <c>PendenciaDaCascata</c>, cada um tem <b>três</b> call sites no Domain — os dois de
    /// <c>TransicoesDaVersao.Publicar</c> e a sucessão mais a projeção em <c>AvaliarConformidade</c>, que
    /// é o que mantém checklist e recusa bicondicionais. Perder a terceira chamada devolve o
    /// defeito em que a publicação recusa por uma causa que o checklist declarava verde.
    /// </summary>
    [Fact(DisplayName = "Localidade e convenção de contagem: um portão na prontidão do Domain e a projeção no checklist, sem antecipação nos handlers")]
    public void ContagemDePrazos_TemGatesNaRaizEProjecaoNoChecklist()
    {
        string prontidao = File.ReadAllText(CaminhoProntidao());
        string checklist = File.ReadAllText(CaminhoChecklist());

        Regex.Count(checklist, @"PendenciaDaLocalidade\(estado\)\s+is\s").Should().Be(1,
            "AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario) projeta o mesmo predicado que a prontidão aplica");
        Regex.Count(prontidao, @"Localidade:\s+\(\)\s+=>\s+ContagemDePrazos\.PendenciaDaLocalidade\(estado\),").Should().Be(1, "Publicar e a sucessão de Retificar e Fechar aplicam o gate pela prontidão");
        Regex.Count(prontidao, @"portoes\.Localidade\b").Should().Be(1);

        Regex.Count(checklist, @"PendenciaDoAlgoritmoDeContagem\(estado\)\s+is\s").Should().Be(1,
            "mesma topologia da localidade — o gate pela prontidão e a projeção no checklist");
        Regex.Count(prontidao, @"AlgoritmoDeContagem:\s+\(\)\s+=>\s+ContagemDePrazos\.PendenciaDoAlgoritmoDeContagem\(estado\),").Should().Be(1);
        Regex.Count(prontidao, @"portoes\.AlgoritmoDeContagem\b").Should().Be(1);

        foreach (string handler in new[]
                 {
                     "PublicarProcessoSeletivoCommandHandler.cs",
                     "RetificarProcessoSeletivoCommandHandler.cs",
                     "FecharRetificacaoCommandHandler.cs",
                 })
        {
            string fonte = File.ReadAllText(CaminhoHandler(handler));
            fonte.Should().NotContain("PendenciaDaLocalidade(",
                "o gate de localidade não é antecipado na Application — a raiz é o único ponto");
            fonte.Should().NotContain("PendenciaDoAlgoritmoDeContagem(",
                "o gate da convenção de contagem não é antecipado na Application");
        }
    }

    private static int ContarChamadasDeGate(string caminho)
    {
        string fonte = File.ReadAllText(caminho);
        return Regex.Count(fonte, @"\bPendenciaDaCascata\((?:[^()\n]|\([^()\n]*\))*\)\s+is\s+\{");
    }

    private static int ContarChamadas(string caminho)
    {
        // Casa só o SITE DE CHAMADA (`PendenciaDeConformidade() is`), não a declaração do
        // método nem menções em xmldoc/comentário.
        string fonte = File.ReadAllText(caminho);
        return Regex.Count(fonte, @"\bPendenciaDeConformidade\((?:[^()\n]|\([^()\n]*\))*\)\s+is\s");
    }

    private static string CaminhoTransicoes([CallerFilePath] string origem = "") =>
        Path.GetFullPath(Path.Join(
            Path.GetDirectoryName(origem)!, "..", "..",
            "src", "selecao", "Unifesspa.UniPlus.Selecao.Domain", "ProcessosSeletivos", "Publicacao", "TransicoesDaVersao.cs"));

    private static string CaminhoProntidao([CallerFilePath] string origem = "") =>
        Path.GetFullPath(Path.Join(
            Path.GetDirectoryName(origem)!, "..", "..",
            "src", "selecao", "Unifesspa.UniPlus.Selecao.Domain", "ProcessosSeletivos", "Publicacao", "ProntidaoParaCongelar.cs"));

    private static string CaminhoChecklist([CallerFilePath] string origem = "") =>
        Path.GetFullPath(Path.Join(
            Path.GetDirectoryName(origem)!, "..", "..",
            "src", "selecao", "Unifesspa.UniPlus.Selecao.Domain", "ProcessosSeletivos", "Conformidade", "ChecklistDeConformidade.cs"));

    private static string CaminhoHandler(string arquivo, [CallerFilePath] string origem = "") =>
        Path.GetFullPath(Path.Join(
            Path.GetDirectoryName(origem)!, "..", "..",
            "src", "selecao", "Unifesspa.UniPlus.Selecao.Application", "Commands", "ProcessosSeletivos", arquivo));
}
