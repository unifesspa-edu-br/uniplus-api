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

    internal DependenciasDaInscricao.CitacoesDosGatilhos Coletar(IEnumerable<NoExigencia> raizes)
    {
        foreach (NoExigencia raiz in raizes)
        {
            Citar(raiz, null);
        }

        return new(_doCandidato, _deMembro, _repetidos);
    }

    private bool Citar(NoExigencia no, string? grupoRepetido)
    {
        string? repetido = no.RepetePorEntidade ?? grupoRepetido;
        bool temFolha = CitarFilhos(no, repetido);
        if (no.DocumentoExigido is { } documento && _incluir?.Invoke(documento.Id) != false)
        {
            temFolha = true;
            CitarGatilho(documento, repetido);
        }

        if (temFolha && no.RepetePorEntidade is { } grupo)
        {
            _repetidos.Add(grupo);
        }

        return temFolha;
    }

    private bool CitarFilhos(NoExigencia no, string? repetido)
    {
        bool temFolha = false;
        foreach (NoExigencia filho in no.Filhos)
        {
            temFolha |= Citar(filho, repetido);
        }

        return temFolha;
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
