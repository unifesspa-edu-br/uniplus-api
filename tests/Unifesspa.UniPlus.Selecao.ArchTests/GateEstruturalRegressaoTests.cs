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
