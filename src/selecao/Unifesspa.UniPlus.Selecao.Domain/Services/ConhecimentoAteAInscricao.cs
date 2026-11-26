namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Enums;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

using ValueObjects;

/// <summary>
/// O que se conhece na fase da inscrição: os fatos que os formulários das fases até ela perguntam, o que se deriva só
/// disso e o que chega de fora como fato conhecido.
/// </summary>
internal sealed class ConhecimentoAteAInscricao
{
    private readonly ProducaoDasPaginas _ateAInscricao;
    private readonly IReadOnlyDictionary<string, FatoResolvido> _fatosConhecidos;

    private ConhecimentoAteAInscricao(ProducaoDasPaginas ateAInscricao, IReadOnlyDictionary<string, FatoResolvido> fatosConhecidos)
    {
        _ateAInscricao = ateAInscricao;
        _fatosConhecidos = fatosConhecidos;
    }

    internal static ConhecimentoAteAInscricao De(
        ConfiguracaoAvaliavel configuracao,
        IReadOnlyList<CodigoDoFormulario> formulariosAteAInscricao,
        IReadOnlyDictionary<string, FatoResolvido> fatosConhecidos) =>
        new(
            ProducaoDasPaginas.De(
                configuracao.Definicao,
                configuracao.Definicao.Paginas.Where(e => formulariosAteAInscricao.Any(f => DefinicaoDoProcesso.EhDoFormulario(e.Codigo, f)))),
            fatosConhecidos);

    internal bool ConheceOGrupo(string? grupoRepetido) =>
        grupoRepetido is null || _ateAInscricao.Grupos.Contains(grupoRepetido);

    internal bool ConheceOFato(string fato) =>
        _ateAInscricao.Fechamento([fato]).Pressupostos.All(ConhecePressuposto);

    private bool ConhecePressuposto(string pressuposto) =>
        _fatosConhecidos.ContainsKey(pressuposto)
        || (DerivadosDoSistema.Dependencias.TryGetValue(pressuposto, out IReadOnlyList<string>? dependencias)
            && dependencias.All(_ateAInscricao.Produzidos.Contains));
}
