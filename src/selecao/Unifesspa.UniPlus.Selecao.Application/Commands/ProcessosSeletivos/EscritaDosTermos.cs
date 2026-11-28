namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>Os termos lidos pela forma, sem leitura externa: a exibição e a obrigatoriedade de cada termo cuja forma foi aceita.</summary>
internal sealed record TermosLidos(
    IReadOnlyList<TermoExigidoInput?> Entradas,
    IReadOnlyList<ExibicaoEObrigatoriedade?> Regras,
    IReadOnlyList<FieldError> Erros);

/// <summary>
/// A escrita dos termos do formulário do processo, sem ler nem mudar o processo: a forma de cada
/// termo, sem I/O; depois a versão escolhida no catálogo de termos e as condições contra os fatos
/// que o formulário conhece, os mesmos com que ele as avalia; e a unicidade de código e ordem.
/// </summary>
internal static class EscritaDosTermos
{
    private static readonly DomainError ObrigatoriedadeInvalida = new(
        TermoExigidoFormularioErrorCodes.ObrigatoriedadeInvalida, "A obrigatoriedade é SEMPRE ou NUNCA, sem predicado, ou QUANDO, com predicado.");

    public static TermosLidos Ler(IReadOnlyList<TermoExigidoInput?> entradas)
    {
        ArgumentNullException.ThrowIfNull(entradas);
        List<FieldError> erros = [];
        List<ExibicaoEObrigatoriedade?> regras = [];
        for (int i = 0; i < entradas.Count; i++)
        {
            int recusasAntes = erros.Count;
            ExibicaoEObrigatoriedade? lidas = LerForma(entradas[i], $"termos[{i}]", erros);
            regras.Add(erros.Count == recusasAntes ? lidas : null);
        }

        return new TermosLidos(entradas, regras, erros);
    }

    /// <param name="universo">Os fatos que as condições podem citar: os coletados e os derivados por regra.</param>
    public static (List<TermoExigidoFormulario> Termos, List<FieldError> Erros) Resolver(
        TermosLidos lidos,
        ContextoDoCatalogo contexto,
        IReadOnlyDictionary<Guid, VersaoTermoConsentimentoView> versoes,
        IReadOnlySet<string> universo)
    {
        ArgumentNullException.ThrowIfNull(lidos);
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(versoes);
        ArgumentNullException.ThrowIfNull(universo);
        List<(TermoExigidoFormulario? Termo, List<FieldError> Erros)> resolvidos =
        [
            .. lidos.Regras.Select((regras, i) => regras is null
                ? (null, [])
                : ResolverTermo(lidos.Entradas[i]!, regras, $"termos[{i}]", contexto, versoes, universo)),
        ];
        return (
            [.. resolvidos.Select(static r => r.Termo).OfType<TermoExigidoFormulario>()],
            [.. lidos.Erros, .. resolvidos.SelectMany(static r => r.Erros), .. ConferirUnicidade(lidos.Entradas)]);
    }

    private static List<FieldError> ConferirUnicidade(IReadOnlyList<TermoExigidoInput?> entradas) =>
        FormaDoTermo.ConferirUnicidade([.. entradas.Select(static t => t is null ? null : ((string?, int?)?)(t.Codigo, t.Ordem))]);

    /// <summary>
    /// As condições do termo não dependem da versão: conferidas antes dela, as recusas do termo saem juntas. O termo só
    /// é criado com a versão que o catálogo tem.
    /// </summary>
    private static (TermoExigidoFormulario? Termo, List<FieldError> Erros) ResolverTermo(
        TermoExigidoInput entrada,
        ExibicaoEObrigatoriedade regras,
        string campo,
        ContextoDoCatalogo contexto,
        IReadOnlyDictionary<Guid, VersaoTermoConsentimentoView> versoes,
        IReadOnlySet<string> universo)
    {
        List<FieldError> erros = [.. ConferirCondicoes(regras, $"{campo}.{TermoExigidoInput.CaminhoDasRegras}", contexto, universo)];
        if (VersaoEscolhida(entrada.Termo, versoes) is not { } versao)
        {
            erros.Add(new($"{campo}.{TermoExigidoInput.CaminhoDoTermo}.versaoId", VersaoNaoEncontrada));
            return (null, erros);
        }

        Result<TermoExigidoFormulario> termo = TermoExigidoFormulario.Criar(entrada.Codigo, entrada.Ordem, versao, regras.Exibicao, regras.Obrigatoriedade);
        erros.AddRange(termo.Errors.Select(erro => erro.NoCaminhoDe(campo)));
        return (termo.IsSuccess ? termo.Value : null, erros);
    }

    private static readonly DomainError VersaoNaoEncontrada = new(
        TermoExigidoFormularioErrorCodes.VersaoNaoEncontrada, "A versão informada não existe no catálogo de termos, ou pertence a outro termo.");

    private static VersaoTermoEscolhida? VersaoEscolhida(VersaoDoTermoInput? termo, IReadOnlyDictionary<Guid, VersaoTermoConsentimentoView> versoes) =>
        termo is not null && versoes.TryGetValue(termo.VersaoId, out VersaoTermoConsentimentoView? versao) && versao.TermoId == termo.TermoId
            ? new VersaoTermoEscolhida(
                versao.TermoId, versao.VersaoId, versao.Codigo, versao.Numero, versao.Nome, versao.Texto, versao.BaseLegal, versao.FormaAceite, versao.Hash)
            : null;

    private static IEnumerable<FieldError> ConferirCondicoes(
        ExibicaoEObrigatoriedade regras, string campo, ContextoDoCatalogo contexto, IReadOnlySet<string> universo) =>
    [
        .. RecusaDoPredicado($"{campo}.exibicao", regras.Exibicao, contexto, universo),
        .. RecusaDoPredicado($"{campo}.obrigatoriedade.predicado", regras.Obrigatoriedade.Predicado, contexto, universo),
    ];

    /// <summary>O predicado, quando há, não cita atributo do candidato e confere com o vocabulário e os domínios do catálogo.</summary>
    private static IEnumerable<FieldError> RecusaDoPredicado(string caminho, PredicadoDnf? predicado, ContextoDoCatalogo contexto, IReadOnlySet<string> universo)
    {
        DomainError? recusa = predicado is null
            ? null
            : VocabularioDeFatos.CitacaoDeAtributoDoCandidato(predicado.FatosCitados, contexto.Fatos)
                ?? PredicadoDnfValidador.Validar(predicado, contexto.Vocabulario, universo, contexto.DominiosDinamicos).Error;
        return recusa is null ? [] : [new FieldError(caminho, recusa)];
    }

    /// <summary>
    /// A forma do termo sem leitura externa: código, ordem, exibição e obrigatoriedade coerente — só
    /// <c>QUANDO</c> tem predicado, e ele o tem sempre. Nula quando o termo ou a obrigatoriedade foi recusada.
    /// </summary>
    private static ExibicaoEObrigatoriedade? LerForma(TermoExigidoInput? entrada, string campo, List<FieldError> erros)
    {
        if (entrada is null)
        {
            erros.Add(new(campo, new DomainError(TermoExigidoFormularioErrorCodes.EntradaMalformada, "O termo veio nulo.")));
            return null;
        }

        erros.AddRange(FormaDoTermo.ValidarFormaBasica(entrada.Codigo, entrada.Ordem).Select(erro => erro.NoCaminhoDe(campo)));
        return LerRegras(entrada.Regras, $"{campo}.{TermoExigidoInput.CaminhoDasRegras}", erros);
    }

    private static ExibicaoEObrigatoriedade? LerRegras(ExibicaoEObrigatoriedadeInput? entrada, string campo, List<FieldError> erros)
    {
        Result<PredicadoDnf?> exibicao = EntradaDeRegras.Predicado(entrada?.Exibicao);
        if (exibicao.IsFailure)
        {
            erros.Add(new($"{campo}.exibicao", exibicao.Error!));
        }

        Obrigatoriedade? obrigatoriedade = LerObrigatoriedade(entrada?.Obrigatoriedade, $"{campo}.obrigatoriedade", erros);
        return obrigatoriedade is null ? null : new ExibicaoEObrigatoriedade(exibicao.Value, obrigatoriedade);
    }

    private static Obrigatoriedade? LerObrigatoriedade(ObrigatoriedadeInput? entrada, string campo, List<FieldError> erros)
    {
        Result<PredicadoDnf?> predicado = EntradaDeRegras.Predicado(entrada?.Predicado);
        if (predicado.IsFailure)
        {
            erros.Add(new($"{campo}.predicado", predicado.Error!));
            return null;
        }

        Obrigatoriedade? obrigatoriedade = EntradaDeRegras.Obrigatoriedade(entrada?.Tipo, predicado.Value);
        if (obrigatoriedade is null)
        {
            erros.Add(new(campo, ObrigatoriedadeInvalida));
        }

        return obrigatoriedade;
    }
}
