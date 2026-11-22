namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Entities;

using Unifesspa.UniPlus.Regras.Formularios;

internal sealed class ColetorDeCitacoes
{
    private readonly Dictionary<string, HashSet<string>> _subitensPorGrupo;
    private readonly Func<Guid, bool>? _incluir;
    private readonly HashSet<string> _doCandidato = new(StringComparer.Ordinal);
    private readonly HashSet<(string Grupo, string Fato)> _deMembro = [];
    private readonly HashSet<string> _repetidos = new(StringComparer.Ordinal);

    internal ColetorDeCitacoes(DefinicaoFormulario definicao, Func<Guid, bool>? incluir)
    {
        _incluir = incluir;
        _subitensPorGrupo = definicao.Etapas.SelectMany(static e => e.Grupos).ToDictionary(
            static g => g.Codigo,
            static g => g.Subitens.Select(static s => s.FatoCodigo).ToHashSet(StringComparer.Ordinal),
            StringComparer.Ordinal);
    }

    internal DependenciasDaInscricao.CitacoesDosGatilhos Coletar(IEnumerable<RegraDeComprovacao> regrasSemGrupo)
    {
        foreach (RegraDeComprovacao regra in regrasSemGrupo)
        {
            Citar(regra, null);
        }

        return new(_doCandidato, _deMembro, _repetidos);
    }

    private bool Citar(RegraDeComprovacao regra, string? grupoRepetido)
    {
        string? repetido = regra.GrupoDoFormularioParaRepeticao ?? grupoRepetido;
        bool temDocumento = CitarItens(regra, repetido);
        if (regra.DocumentoExigido is { } documento && _incluir?.Invoke(documento.Id) != false)
        {
            temDocumento = true;
            CitarGatilho(documento, repetido);
        }

        if (temDocumento && regra.GrupoDoFormularioParaRepeticao is { } grupo)
        {
            _repetidos.Add(grupo);
        }

        return temDocumento;
    }

    private bool CitarItens(RegraDeComprovacao regra, string? repetido)
    {
        bool temDocumento = false;
        foreach (RegraDeComprovacao item in regra.Itens)
        {
            temDocumento |= Citar(item, repetido);
        }

        return temDocumento;
    }

    private void CitarGatilho(DocumentoExigido documento, string? repetido)
    {
        foreach (string fato in documento.Condicoes.Select(static c => c.Fato))
        {
            if (repetido is not null && _subitensPorGrupo.TryGetValue(repetido, out HashSet<string>? subitens) && subitens.Contains(fato))
            {
                _deMembro.Add((repetido, fato));
            }
            else
            {
                _doCandidato.Add(fato);
            }
        }
    }
}
