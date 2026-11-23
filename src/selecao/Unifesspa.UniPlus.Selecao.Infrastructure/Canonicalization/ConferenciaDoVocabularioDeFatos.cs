namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Confere os códigos, ordens e referências dos fatos congelados.</summary>
internal static class ConferenciaDoVocabularioDeFatos
{
    private static DomainError Malformado(string mensagem) => new(ErrosCodecEnvelope.EnvelopeMalformado, mensagem);

    // Produtor único no processo, contando os campos dos grupos; ordem única dentro de cada
    // formulário, entre itens e grupos, e dentro de cada grupo; código de grupo único no processo e
    // fora do espaço de códigos dos fatos.
    internal static DomainError? ConferirCodigosEOrdens(
        BlocoDeFatosEDerivacao bloco,
        HashSet<string> coletados,
        HashSet<string> codigosDeGrupo)
    {
        HashSet<(FinalidadeFormulario, int)> ordens = [];
        DomainError? erro = ConferirFatosColetados(bloco.Fatos, coletados, ordens);
        if (erro is not null)
        {
            return erro;
        }

        return ConferirGrupos(bloco.Grupos, coletados, codigosDeGrupo, ordens);
    }

    private static DomainError? ConferirGrupos(
        IReadOnlyList<GrupoColetado> grupos, HashSet<string> coletados, HashSet<string> codigosDeGrupo,
        HashSet<(FinalidadeFormulario, int)> ordens)
    {
        foreach (GrupoColetado grupo in grupos)
        {
            if (ConferirGrupo(grupo, coletados, codigosDeGrupo, ordens) is { } incoerencia)
            {
                return incoerencia;
            }
        }

        // Depois de todos os campos: o código de grupo não coincide com fato coletado, item ou campo.
        return codigosDeGrupo.Overlaps(coletados)
            ? Malformado("'gruposDosFormularios': um código de grupo coincide com o de um fato coletado.")
            : null;
    }

    private static DomainError? ConferirFatosColetados(
        IReadOnlyList<FatoColetado> fatos, HashSet<string> coletados, HashSet<(FinalidadeFormulario, int)> ordens)
    {
        foreach (FatoColetado fato in fatos)
        {
            if (!coletados.Add(fato.FatoCodigo))
            {
                return Malformado($"'dadosDosFormularios': o fato '{fato.FatoCodigo}' aparece mais de uma vez.");
            }

            if (!ordens.Add((fato.Finalidade, fato.Ordem)))
            {
                return Malformado($"'dadosDosFormularios': a ordem {fato.Ordem} é usada por mais de um fato do mesmo formulário.");
            }
        }

        return null;
    }

    private static DomainError? ConferirGrupo(
        GrupoColetado grupo,
        HashSet<string> coletados,
        HashSet<string> codigosDeGrupo,
        HashSet<(FinalidadeFormulario, int)> ordens)
    {
        if (!codigosDeGrupo.Add(grupo.Codigo))
        {
            return Malformado($"'gruposDosFormularios': o código '{grupo.Codigo}' aparece em mais de um grupo.");
        }

        if (!ordens.Add((grupo.Finalidade, grupo.Ordem)))
        {
            return Malformado($"'gruposDosFormularios': a ordem {grupo.Ordem} é usada por mais de um item ou grupo do mesmo formulário.");
        }

        return ConferirCamposDoGrupo(grupo, coletados);
    }

    private static DomainError? ConferirCamposDoGrupo(GrupoColetado grupo, HashSet<string> coletados)
    {
        HashSet<int> ordensNoGrupo = [];
        foreach (FatoColetado campo in grupo.Subitens)
        {
            if (ConferirCampo(grupo, campo, coletados, ordensNoGrupo) is { } erro)
            {
                return erro;
            }
        }

        return null;
    }

    private static DomainError? ConferirCampo(GrupoColetado grupo, FatoColetado campo, HashSet<string> coletados, HashSet<int> ordens)
    {
        if (!coletados.Add(campo.FatoCodigo))
        {
            return Malformado($"'gruposDosFormularios': o fato '{campo.FatoCodigo}' aparece mais de uma vez.");
        }

        return !ordens.Add(campo.Ordem)
            ? Malformado($"'gruposDosFormularios': a ordem {campo.Ordem} é usada por mais de um campo do grupo '{grupo.Codigo}'.")
            : null;
    }

    internal static DomainError? ConferirDerivados(
        IReadOnlyList<ConfiguracaoDerivacaoFato> regrasDerivacao,
        HashSet<string> coletados,
        HashSet<string> codigosDeGrupo,
        HashSet<string> universo)
    {
        HashSet<string> derivados = new(StringComparer.Ordinal);
        foreach (ConfiguracaoDerivacaoFato config in regrasDerivacao)
        {
            DomainError? erro = ConferirCodigoDerivado(config, derivados, coletados, codigosDeGrupo);
            if (erro is not null)
            {
                return erro;
            }

            universo.Add(config.CodigoFato);
        }

        return null;
    }

    private static DomainError? ConferirCodigoDerivado(
        ConfiguracaoDerivacaoFato config, HashSet<string> derivados, HashSet<string> coletados, HashSet<string> codigosDeGrupo)
    {
        if (!derivados.Add(config.CodigoFato))
        {
            return Malformado($"'calculosDosDados': o fato '{config.CodigoFato}' tem mais de uma configuração de derivação.");
        }

        // A modalidade do candidato e a opção pelas cotas saem da oferta, e o encoder não as grava.
        if (DerivacoesDaOferta.Deriva(config.CodigoFato))
        {
            return Malformado($"'calculosDosDados': o fato '{config.CodigoFato}' é derivado da oferta de vagas, e não se grava.");
        }

        return ConferirColisaoDoDerivado(config.CodigoFato, coletados, codigosDeGrupo);
    }

    private static DomainError? ConferirColisaoDoDerivado(string codigo, HashSet<string> coletados, HashSet<string> codigosDeGrupo) =>
        coletados.Contains(codigo) || codigosDeGrupo.Contains(codigo)
            ? Malformado($"'calculosDosDados': o código '{codigo}' já é de fato coletado ou de grupo.")
            : null;

    // Toda citação de regra de derivação gravada existe em coletados e derivados, inclusive os que a
    // oferta define. A completude das citações de gatilho é recusa da fatia de dependência declarada, não
    // desta: o grafo conjunto ignora de propósito a citação a fato ausente.
    internal static DomainError? ConferirCitacoes(
        IReadOnlyList<ConfiguracaoDerivacaoFato> regrasDerivacao,
        IReadOnlyList<ConfiguracaoDerivacaoFato> efetivas,
        HashSet<string> universo)
    {
        universo.UnionWith(efetivas.Select(static d => d.CodigoFato));
        foreach (ConfiguracaoDerivacaoFato config in regrasDerivacao)
        {
            CondicaoRegraDerivacao? ausente = config.Regras
                .SelectMany(static regra => regra.Condicoes)
                .FirstOrDefault(condicao => !universo.Contains(condicao.Fato));
            if (ausente is not null)
            {
                return Malformado(
                    $"'calculosDosDados': a derivação de '{config.CodigoFato}' cita '{ausente.Fato}', que o processo não coleta nem deriva.");
            }
        }

        return null;
    }

}
