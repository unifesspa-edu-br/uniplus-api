namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Entities;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

using ValueObjects;

internal sealed class PendenciasDaInscricao
{
    private readonly ConfiguracaoAvaliavel _configuracao;
    private readonly AvaliacaoFormulario _formulario;
    private readonly HashSet<string> _campos;
    private readonly HashSet<string> _grupos;
    private readonly Dictionary<string, AvaliacaoItem> _itemPorFato;
    private readonly Dictionary<string, AvaliacaoGrupo> _grupoPorCodigo;
    private readonly Dictionary<string, FatoColetado> _campoPorFato;
    private readonly Dictionary<string, GrupoColetado> _grupoColetado;

    internal PendenciasDaInscricao(ConfiguracaoAvaliavel configuracao, AvaliacaoFormulario formulario, DependenciasDaAvaliacao dependencias)
    {
        _configuracao = configuracao;
        _formulario = formulario;
        _campos = new(dependencias.Fatos, StringComparer.Ordinal);
        _grupos = new(dependencias.Grupos, StringComparer.Ordinal);
        _itemPorFato = formulario.Itens.ToDictionary(static i => i.FatoCodigo, StringComparer.Ordinal);
        _grupoPorCodigo = formulario.Grupos.ToDictionary(static g => g.Codigo, StringComparer.Ordinal);
        _campoPorFato = configuracao.Campos.Where(static c => c.Finalidade == FinalidadeFormulario.Inscricao)
            .ToDictionary(static c => c.FatoCodigo, StringComparer.Ordinal);
        _grupoColetado = configuracao.Grupos.Where(static g => g.Finalidade == FinalidadeFormulario.Inscricao)
            .ToDictionary(static g => g.Codigo, StringComparer.Ordinal);
    }

    internal List<PendenciaDaAvaliacao> Apurar()
    {
        List<PendenciaDaAvaliacao> pendencias = [];
        foreach (DefinicaoEtapa etapa in AvaliacaoDaInscricao.EtapasDaInscricao(_configuracao.Definicao))
        {
            AdicionarCampos(etapa, pendencias);
            AdicionarGrupos(etapa, pendencias);
        }

        return pendencias;
    }

    private void AdicionarCampos(DefinicaoEtapa etapa, List<PendenciaDaAvaliacao> pendencias) =>
        pendencias.AddRange(etapa.Itens
            .Where(item => _campos.Contains(item.FatoCodigo) && CampoPendente(item.FatoCodigo))
            .Select(item => _campoPorFato[item.FatoCodigo])
            .Select(static campo => new PendenciaDaAvaliacao(campo.FatoCodigo, campo.Rotulo, campo.EtapaCodigo)));

    private bool CampoPendente(string fatoCodigo) =>
        _itemPorFato.GetValueOrDefault(fatoCodigo) is { Visivel: Ternario.Verdadeiro }
        && _formulario.Fatos.GetValueOrDefault(fatoCodigo)?.Estado == EstadoFato.Indeterminado
        && _campoPorFato.ContainsKey(fatoCodigo);

    private void AdicionarGrupos(DefinicaoEtapa etapa, List<PendenciaDaAvaliacao> pendencias) =>
        pendencias.AddRange(etapa.Grupos
            .Where(grupo => _grupos.Contains(grupo.Codigo) && GrupoPendente(grupo.Codigo))
            .Select(grupo => _grupoColetado[grupo.Codigo])
            .Select(static coletado => new PendenciaDaAvaliacao(coletado.Codigo, coletado.Rotulo, coletado.EtapaCodigo)));

    private bool GrupoPendente(string codigo) =>
        _grupoPorCodigo.GetValueOrDefault(codigo) is { Visivel: Ternario.Verdadeiro, Estado: EstadoFato.Indeterminado }
        && _grupoColetado.ContainsKey(codigo);
}
