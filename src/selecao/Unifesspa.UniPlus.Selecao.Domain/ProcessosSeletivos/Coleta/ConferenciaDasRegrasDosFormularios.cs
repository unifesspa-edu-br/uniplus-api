namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

/// <summary>
/// Exibição, obrigatoriedade, opções e condição de termo de um campo, de uma etapa ou de um termo só citam
/// fatos conhecidos no formulário dono, e o conjunto das dependências é acíclico (UNI-REQ-0145,
/// UNI-REQ-0077, UNI-REQ-0086). O campo de grupo repetível só é citado dentro do próprio grupo. Como o
/// formulário facultativo não é fonte de ninguém e dois obrigatórios da mesma fase não se alcançam, dois
/// formulários não dependem um do outro em círculo (UNI-REQ-0144): a citação cruzada é fato desconhecido
/// nos dois lados. A recusa aponta a regra e o fato pelo caminho, sem repeti-los no texto.
/// </summary>
public static class ConferenciaDasRegrasDosFormularios
{
    public static IReadOnlyList<FieldError> Conferir(ConhecimentoDosFatos conhecimento)
    {
        ArgumentNullException.ThrowIfNull(conhecimento);

        return [.. conhecimento.Coletas.SelectMany(coleta => RecusasDoFormulario(conhecimento, coleta))];
    }

    private static IEnumerable<FieldError> RecusasDoFormulario(ConhecimentoDosFatos conhecimento, ColetaDoFormulario coleta)
    {
        string formulario = $"formularios[{coleta.Formulario.Codigo.Valor}]";
        IEnumerable<FieldError> citacoes = conhecimento.Cronograma.FirstOrDefault(fase => fase.Id == coleta.Formulario.FaseId) is { } fase
            ? coleta.Regras.SelectMany(regra => RecusasDaRegra(conhecimento, new PontoDeUso(fase, coleta.Formulario), formulario, regra))
            : [];
        return [.. citacoes, .. RecusaDoCiclo(conhecimento, coleta, formulario)];
    }

    private static IEnumerable<FieldError> RecusasDaRegra(ConhecimentoDosFatos conhecimento, PontoDeUso ponto, string formulario, RegraDoFormulario regra) =>
        regra.FatosCitados
            .Distinct(StringComparer.Ordinal)
            .Select(fato => (fato, recusa: RecusaDaCitacao(conhecimento, ponto, regra, fato)))
            .Where(static citacao => citacao.recusa is not null)
            .Select(citacao => new FieldError($"{formulario}.{regra.Caminho}.fatosCitados.{citacao.fato}", citacao.recusa!));

    private static DomainError? RecusaDaCitacao(ConhecimentoDosFatos conhecimento, PontoDeUso ponto, RegraDoFormulario regra, string fato)
    {
        ConhecimentoDoFatoNoPonto noPonto = conhecimento.NoPonto(fato, ponto);
        if (noPonto == ConhecimentoDoFatoNoPonto.ConhecidoEmFasePosterior)
        {
            return new DomainError(GrafoFormularioErrorCodes.CitaFatoPosterior, "A regra cita fato que só é conhecido numa fase posterior à do formulário.");
        }

        if (noPonto != ConhecimentoDoFatoNoPonto.Conhecido)
        {
            return new DomainError(GrafoFormularioErrorCodes.CitaFatoNaoConhecido, "A regra cita fato que não é conhecido no formulário.");
        }

        return conhecimento.GrupoDoCampo(fato) is { } grupo && !string.Equals(grupo, regra.GrupoProprio, StringComparison.Ordinal)
            ? new DomainError(GrafoFormularioErrorCodes.CitaFatoDeMembroForaDoGrupo, "A regra cita campo de grupo repetível fora do próprio grupo.")
            : null;
    }

    private static IEnumerable<FieldError> RecusaDoCiclo(ConhecimentoDosFatos conhecimento, ColetaDoFormulario coleta, string formulario) =>
        GrafoDeFatos.DetectarCiclo(DependenciasDoFormulario(conhecimento, coleta)) is null
            ? []
            : [new FieldError(formulario, new DomainError(GrafoFormularioErrorCodes.GrafoComCiclo, "As regras dos campos do formulário formam um ciclo."))];

    /// <summary>
    /// As dependências entre os fatos que as regras do formulário alcançam: o que cada campo cita e, a partir
    /// do que é citado, as dependências dos derivados, que também entram no ciclo.
    /// </summary>
    private static Dictionary<string, IReadOnlyCollection<string>> DependenciasDoFormulario(ConhecimentoDosFatos conhecimento, ColetaDoFormulario coleta)
    {
        Dictionary<string, IReadOnlyCollection<string>> dependencias = new(StringComparer.Ordinal);
        foreach ((ItemDoGrafo campo, IEnumerable<string> daExibicao) in CamposComAExibicaoQueOsLibera(coleta))
        {
            dependencias.TryAdd(campo.FatoCodigo, [.. campo.FatosCitados, .. daExibicao]);
        }

        Queue<string> citados = new(coleta.Regras.SelectMany(static regra => regra.FatosCitados));
        while (citados.TryDequeue(out string? fato))
        {
            AcrescentarDerivado(conhecimento.Catalogo, fato, dependencias, citados);
        }

        return dependencias;
    }

    /// <summary>
    /// Cada campo com os fatos de que a exibição dele depende: a da seção dele e, no campo de grupo repetível,
    /// também a do grupo e a da seção do grupo. O campo só é respondido depois de aparecer.
    /// </summary>
    private static IEnumerable<(ItemDoGrafo Campo, IEnumerable<string> DaExibicao)> CamposComAExibicaoQueOsLibera(ColetaDoFormulario coleta) =>
    [
        .. coleta.Itens.Select(item => (item, FatosDaSecao(coleta, item.EtapaCodigo))),
        .. coleta.Grupos.SelectMany(grupo => grupo.Subitens.Select(campo =>
            (campo, grupo.FatosCitados.Concat(FatosDaSecao(coleta, grupo.EtapaCodigo)).Concat(FatosDaSecao(coleta, campo.EtapaCodigo))))),
    ];

    private static IEnumerable<string> FatosDaSecao(ColetaDoFormulario coleta, string? codigoDaSecao) =>
        coleta.Secoes.Where(secao => string.Equals(secao.Codigo, codigoDaSecao, StringComparison.Ordinal)).SelectMany(static secao => secao.FatosCitados);

    private static void AcrescentarDerivado(
        CatalogoDosFatosCitados catalogo, string fato, Dictionary<string, IReadOnlyCollection<string>> dependencias, Queue<string> citados)
    {
        if (dependencias.ContainsKey(fato) || !catalogo.DependenciasDosDerivados.TryGetValue(fato, out IReadOnlyCollection<string>? doDerivado))
        {
            return;
        }

        dependencias[fato] = doDerivado;
        foreach (string dependencia in doDerivado)
        {
            citados.Enqueue(dependencia);
        }
    }
}
