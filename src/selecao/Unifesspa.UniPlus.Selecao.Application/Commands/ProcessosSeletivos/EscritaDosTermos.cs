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

/// <summary>Os termos lidos pela forma, sem leitura externa: as condições de cada termo cuja forma foi aceita.</summary>
internal sealed record TermosLidos(
    IReadOnlyList<TermoExigidoInput?> Entradas,
    IReadOnlyList<(PredicadoDnf? Exibicao, Obrigatoriedade Obrigatoriedade)?> Condicoes,
    IReadOnlyList<FieldError> Erros);

/// <summary>
/// A escrita dos termos do formulário do processo, sem ler nem mudar o processo: a forma de cada
/// termo, sem I/O; depois a versão escolhida no catálogo de termos e as condições contra os fatos
/// que o formulário conhece, os mesmos com que ele as avalia; e a unicidade de código e ordem.
/// </summary>
internal static class EscritaDosTermos
{
    public static TermosLidos Ler(IReadOnlyList<TermoExigidoInput?> entradas)
    {
        ArgumentNullException.ThrowIfNull(entradas);
        List<FieldError> erros = [];
        List<(PredicadoDnf? Exibicao, Obrigatoriedade Obrigatoriedade)?> condicoes = [];
        for (int i = 0; i < entradas.Count; i++)
        {
            int recusasAntes = erros.Count;
            (PredicadoDnf? Exibicao, Obrigatoriedade? Obrigatoriedade) forma = ConferirForma(entradas[i], $"termos[{i}]", erros);
            condicoes.Add(erros.Count == recusasAntes ? (forma.Exibicao, forma.Obrigatoriedade!) : null);
        }

        return new TermosLidos(entradas, condicoes, erros);
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
        List<FieldError> erros = [.. lidos.Erros];
        List<TermoExigidoFormulario> termos = [];
        for (int i = 0; i < lidos.Entradas.Count; i++)
        {
            if (lidos.Condicoes[i] is not { } condicao)
            {
                continue;
            }

            TermoExigidoInput entrada = lidos.Entradas[i]!;
            string campo = $"termos[{i}]";

            // As condições não dependem da versão: conferidas antes dela, as recusas do termo saem
            // juntas.
            (PredicadoDnf? exibicao, Obrigatoriedade obrigatoriedade) = condicao;
            if ((VocabularioDeFatos.CitacaoDeAtributoDoCandidato(exibicao?.FatosCitados ?? [], contexto.Fatos)
                    ?? ValidarPredicado(exibicao, contexto.Vocabulario, universo, contexto.DominiosDinamicos)) is { } erroExibicao)
            {
                erros.Add(new($"{campo}.exibicao", erroExibicao));
            }

            if ((VocabularioDeFatos.CitacaoDeAtributoDoCandidato(obrigatoriedade.FatosCitados, contexto.Fatos)
                    ?? ValidarPredicado(obrigatoriedade.Predicado, contexto.Vocabulario, universo, contexto.DominiosDinamicos)) is { } erroObrigatoriedade)
            {
                erros.Add(new($"{campo}.predicadoObrigatoriedade", erroObrigatoriedade));
            }

            if (!versoes.TryGetValue(entrada.VersaoId, out VersaoTermoConsentimentoView? versao) || versao.TermoId != entrada.TermoId)
            {
                erros.Add(new($"{campo}.versaoId", new DomainError(
                    TermoExigidoFormularioErrorCodes.VersaoNaoEncontrada,
                    "A versão informada não existe no catálogo de termos, ou pertence a outro termo.")));
                continue;
            }

            Result<TermoExigidoFormulario> termo = TermoExigidoFormulario.Criar(
                entrada.Codigo,
                entrada.Ordem,
                new VersaoTermoEscolhida(versao.TermoId, versao.VersaoId, versao.Nome, versao.Texto, versao.BaseLegal, versao.FormaAceite, versao.Hash),
                exibicao,
                obrigatoriedade);
            if (termo.IsSuccess)
            {
                termos.Add(termo.Value!);
            }
            else
            {
                erros.AddRange(termo.Errors.Select(e => new FieldError($"{campo}.{e.Field}", e.Error)));
            }
        }

        erros.AddRange(FormaDoTermo.ConferirUnicidade(
            [.. lidos.Entradas.Select(static t => t is null ? null : ((string?, int)?)(t.Codigo, t.Ordem))]));
        return (termos, erros);
    }

    /// <summary>
    /// A forma do termo sem leitura externa: código, ordem, exibição e obrigatoriedade coerente — só
    /// <c>QUANDO</c> tem predicado, e ele o tem sempre.
    /// </summary>
    private static (PredicadoDnf? Exibicao, Obrigatoriedade? Obrigatoriedade) ConferirForma(
        TermoExigidoInput? entrada, string campo, List<FieldError> erros)
    {
        if (entrada is null)
        {
            erros.Add(new(campo, new DomainError(TermoExigidoFormularioErrorCodes.EntradaMalformada, "O termo veio nulo.")));
            return (null, null);
        }

        erros.AddRange(FormaDoTermo.ValidarFormaBasica(entrada.Codigo, entrada.Ordem)
            .Select(e => new FieldError($"{campo}.{e.Field}", e.Error)));

        Result<PredicadoDnf?> exibicao = EntradaDeRegras.Predicado(entrada.Exibicao);
        if (exibicao.IsFailure)
        {
            erros.Add(new($"{campo}.exibicao", exibicao.Error!));
        }

        Result<PredicadoDnf?> predicado = EntradaDeRegras.Predicado(entrada.PredicadoObrigatoriedade);
        if (predicado.IsFailure)
        {
            erros.Add(new($"{campo}.predicadoObrigatoriedade", predicado.Error!));
            return (exibicao.Value, null);
        }

        Obrigatoriedade? obrigatoriedade = EntradaDeRegras.Obrigatoriedade(entrada.Obrigatoriedade, predicado.Value);
        if (obrigatoriedade is null)
        {
            erros.Add(new($"{campo}.obrigatoriedade", new DomainError(
                TermoExigidoFormularioErrorCodes.ObrigatoriedadeInvalida,
                "A obrigatoriedade é SEMPRE ou NUNCA, sem predicado, ou QUANDO, com predicado.")));
        }

        return (exibicao.Value, obrigatoriedade);
    }

    private static DomainError? ValidarPredicado(
        PredicadoDnf? predicado,
        IReadOnlyDictionary<string, DescritorFatoCandidato> vocabulario,
        IReadOnlySet<string> universo,
        IReadOnlyDictionary<string, DominioDeValores> dominiosDinamicos) =>
        predicado is null ? null : PredicadoDnfValidador.Validar(predicado, vocabulario, universo, dominiosDinamicos).Error;
}
