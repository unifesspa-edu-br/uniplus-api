namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Leitura das ofertas de curso congeladas com a distribuição de vagas, e a conferência de que os blocos
/// derivados da mesma coleção declaram o mesmo conjunto de ofertas.
/// </summary>
internal static class LeituraDasOfertas
{
    /// <summary>
    /// Cada oferta de curso com o que a nomeia para o candidato, congelado na definição da
    /// distribuição.
    /// </summary>
    internal static List<(Guid Id, OfertaCursoSnapshot Oferta)> LerOfertas(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonArray array = leitor.Array(payload, "ofertas", "$");
        List<(Guid, OfertaCursoSnapshot)> ofertas = [];
        for (int i = 0; !leitor.Falhou && i < array.Count; i++)
        {
            if (LerOferta(leitor, leitor.ItemObjeto(array, i, "ofertas"), $"ofertas[{i}]") is { } lida)
            {
                ofertas.Add(lida);
            }
        }

        return leitor.Falhou ? [] : ofertas;
    }

    private static (Guid, OfertaCursoSnapshot)? LerOferta(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, "ofertaCursoOrigemId", "ofertaCursoCodigo", "cursoNome", "cursoGrau", "unidadeOfertanteNome", "turnos");
        Guid id = leitor.Identificador(item, "ofertaCursoOrigemId", path);
        string codigo = leitor.TextoNaoVazio(item, "ofertaCursoCodigo", path, OfertaCursoSnapshot.CodigoMaxLength);
        string cursoNome = leitor.TextoNaoVazio(item, "cursoNome", path, OfertaCursoSnapshot.CursoNomeMaxLength);
        string cursoGrau = leitor.TextoNaoVazio(item, "cursoGrau", path, OfertaCursoSnapshot.CursoGrauMaxLength);
        string unidade = leitor.TextoNaoVazio(item, "unidadeOfertanteNome", path, OfertaCursoSnapshot.UnidadeOfertanteNomeMaxLength);
        IReadOnlyList<string> turnos = leitor.Textos(item, "turnos", path);

        return leitor.Falhou ? null : Congelar(leitor, id, OfertaCursoSnapshot.Criar(codigo, cursoNome, cursoGrau, unidade, turnos));
    }

    private static (Guid, OfertaCursoSnapshot)? Congelar(LeitorEnvelope leitor, Guid id, Result<OfertaCursoSnapshot> oferta)
    {
        if (oferta.IsFailure)
        {
            leitor.Propagar<OfertaCursoSnapshot>(oferta.Error!);
            return null;
        }

        return (id, oferta.Value!);
    }

    internal static DomainError? VerificarBlocosDerivados(
        List<Guid> emDistribuicao, List<Guid> emModalidades, List<Guid> emVagas, List<Guid> ofertas)
    {
        DomainError? repetida =
            RepeticaoDeOferta(emDistribuicao, "O bloco 'distribuicao' repete uma oferta de curso — cada oferta tem no máximo uma distribuição.")
            ?? RepeticaoDeOferta(emVagas, "O bloco 'vagas' repete uma oferta de curso — cada oferta tem no máximo um quadro.")
            ?? RepeticaoDeOferta(ofertas, "O bloco 'ofertas' repete uma oferta de curso.");

        return repetida ?? ConjuntosDiferentes(emDistribuicao, emModalidades, emVagas, ofertas);
    }

    private static DomainError? ConjuntosDiferentes(List<Guid> emDistribuicao, List<Guid> emModalidades, List<Guid> emVagas, List<Guid> ofertas)
    {
        HashSet<Guid> conjuntoDistribuicao = [.. emDistribuicao];
        return conjuntoDistribuicao.SetEquals(ofertas) && conjuntoDistribuicao.SetEquals(emModalidades) && conjuntoDistribuicao.SetEquals(emVagas)
            ? null
            : new DomainError(
                ErrosCodecEnvelope.BlocosDerivadosIncoerentes,
                "Os blocos 'distribuicao', 'modalidades', 'vagas' e 'ofertas' derivam da mesma coleção e não declaram o mesmo conjunto de ofertas de curso.");
    }

    private static DomainError? RepeticaoDeOferta(List<Guid> ofertas, string mensagem) =>
        ofertas.Distinct().Count() != ofertas.Count
            ? new DomainError(ErrosCodecEnvelope.BlocosDerivadosIncoerentes, mensagem)
            : null;
}
