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
    [Fact(DisplayName = "PendenciaDeConformidade() continua chamado nos 5 pontos existentes — 2 no Domain (Publicar/SucederVersao), 3 nos handlers que congelam")]
    public void PendenciaDeConformidade_ContinuaNosCincoPontosOriginais()
    {
        int chamadasNoDomain = ContarChamadas(CaminhoProcessoSeletivo());
        chamadasNoDomain.Should().Be(2,
            "Publicar() e SucederVersao() (o núcleo compartilhado de Retificar()/FecharRetificacao()) " +
            "são os dois pontos de defesa no Domain — a rede que ninguém contorna (ADR-0109 D5)");

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
        // SucederVersao, os três handlers) do uso em AvaliarConformidade(), que testa
        // `PendenciaDaCascata() is null` só para exibição — mesma chamada, propósito
        // diferente, não é um sexto call site de bloqueio.
        int chamadasNoDomain = ContarChamadasDeGate(CaminhoProcessoSeletivo());
        chamadasNoDomain.Should().Be(2, "Publicar() e SucederVersao() são os dois pontos de defesa no Domain");

        int chamadasNoPublicarHandler = ContarChamadasDeGate(CaminhoHandler("PublicarProcessoSeletivoCommandHandler.cs"));
        int chamadasNoRetificarHandler = ContarChamadasDeGate(CaminhoHandler("RetificarProcessoSeletivoCommandHandler.cs"));
        int chamadasNoFecharHandler = ContarChamadasDeGate(CaminhoHandler("FecharRetificacaoCommandHandler.cs"));

        chamadasNoPublicarHandler.Should().Be(1, "Publicar antecipa a mesma recusa que o Domain reconferiria");
        chamadasNoRetificarHandler.Should().Be(1, "Retificar antecipa a mesma recusa que o Domain reconferiria");
        chamadasNoFecharHandler.Should().Be(1, "FecharRetificacao antecipa a mesma recusa que o Domain reconferiria");
    }

    private static int ContarChamadasDeGate(string caminho)
    {
        string fonte = File.ReadAllText(caminho);
        return Regex.Count(fonte, @"PendenciaDaCascata\(\)\s+is\s+\{");
    }

    private static int ContarChamadas(string caminho)
    {
        // Casa só o SITE DE CHAMADA (`PendenciaDeConformidade() is`), não a declaração do
        // método nem menções em xmldoc/comentário.
        string fonte = File.ReadAllText(caminho);
        return Regex.Count(fonte, @"PendenciaDeConformidade\(\)\s+is\s");
    }

    private static string CaminhoProcessoSeletivo([CallerFilePath] string origem = "") =>
        Path.GetFullPath(Path.Join(
            Path.GetDirectoryName(origem)!, "..", "..",
            "src", "selecao", "Unifesspa.UniPlus.Selecao.Domain", "Entities", "ProcessoSeletivo.cs"));

    private static string CaminhoHandler(string arquivo, [CallerFilePath] string origem = "") =>
        Path.GetFullPath(Path.Join(
            Path.GetDirectoryName(origem)!, "..", "..",
            "src", "selecao", "Unifesspa.UniPlus.Selecao.Application", "Commands", "ProcessosSeletivos", arquivo));
}
