namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using System.Text.Json;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>As conferências do conteúdo do modelo de formulário, já na forma em que é gravado.</summary>
internal static class ConferenciaDoConteudoDoModelo
{
    /// <summary>
    /// O cabeçalho, a forma de cada etapa, item, grupo e termo, o teto de itens e os pressupostos acumulam; a
    /// estrutura por finalidade e o grafo de coleta só são conferidos sobre partes bem formadas, porque
    /// dependem delas.
    /// </summary>
    internal static List<FieldError> ConferirNormalizado(
        FinalidadeFormulario finalidade, ConteudoDoModelo conteudo, IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes)
    {
        List<FieldError> erros = [.. TextosGravaveisDoModelo.NaoGravaveis(conteudo)];
        erros.AddRange(FormaDoCabecalho.ValidarTitulo(conteudo.Titulo));

        for (int i = 0; i < conteudo.Etapas.Count; i++)
        {
            EtapaDoModelo etapa = conteudo.Etapas[i];
            erros.AddRange(FormaDaEtapa.Conferir(etapa.Codigo, etapa.Ordem, etapa.Tipo, etapa.Titulo, etapa.Descricao, etapa.Aviso, etapa.Exibicao is not null)
                .Select(e => e with { Field = $"etapas[{i}].{e.Field}" }));
        }

        List<FieldError> excesso = FormaDoItem.ValidarQuantidade(
            FormaDoItem.QuantidadeNoTeto(conteudo.Itens.Count, conteudo.Grupos.Select(static g => g.Subitens.Count)));
        erros.AddRange(excesso);
        if (excesso.Count == 0)
        {
            for (int i = 0; i < conteudo.Itens.Count; i++)
            {
                erros.AddRange(FormaDoCampo(conteudo.Itens[i], $"itens[{i}]"));
            }

            erros.AddRange(Impedimento.ConferirFinalidade(finalidade, conteudo.Itens.Select(static i => i.Impedimento)));

            for (int i = 0; i < conteudo.Grupos.Count; i++)
            {
                GrupoDoModelo grupo = conteudo.Grupos[i];
                erros.AddRange(FormaDoGrupo.Conferir(
                        grupo.Codigo, grupo.Ordem, grupo.Rotulo, grupo.Minimo, grupo.Maximo,
                        [.. grupo.Subitens.Select(static s => ((string?)s.FatoCodigo, s.EtapaCodigo, s.Impedimento is not null))],
                        grupo.Exibicao?.FatosCitados ?? [], grupo.Obrigatoriedade)
                    .Select(e => e with { Field = $"grupos[{i}].{e.Field}" }));
                if (grupo.IncluiCandidato)
                {
                    erros.AddRange(CandidatoComoMembro.Conferir(
                            grupo.Minimo, [.. grupo.Subitens.Select(static s => (s.FatoCodigo, s.Exibicao is not null, s.Obrigatoriedade, s.Restricoes))])
                        .Select(e => e with { Field = $"grupos[{i}].{e.Field}" }));
                }

                for (int j = 0; j < grupo.Subitens.Count; j++)
                {
                    erros.AddRange(FormaDoCampo(grupo.Subitens[j], $"grupos[{i}].subitens[{j}]"));
                }
            }
        }

        for (int i = 0; i < conteudo.Termos.Count; i++)
        {
            erros.AddRange(FormaDoTermo.ValidarFormaBasica(conteudo.Termos[i].Codigo, conteudo.Termos[i].Ordem)
                .Select(e => e with { Field = $"termos[{i}].{e.Field}" }));
        }

        erros.AddRange(FormaDoTermo.ConferirUnicidade([.. conteudo.Termos.Select(static t => ((string?, int)?)(t.Codigo, t.Ordem))]));
        erros.AddRange(ConferirPressupostos(finalidade, conteudo));

        // A estrutura depende da finalidade, recusada no cadastro.
        if (erros.Count > 0 || FormaDoCabecalho.ValidarFinalidade(finalidade).Count > 0)
        {
            return erros;
        }

        EtapaEstrutura[] estrutura = [.. conteudo.Etapas.Select(static e => new EtapaEstrutura(e.Codigo, e.Ordem, e.Tipo, e.Bloco))];
        erros.AddRange(EstruturaFormulario.ValidarEtapas(finalidade, estrutura));
        if (erros.Count > 0)
        {
            return erros;
        }

        erros.AddRange(EstruturaFormulario.ValidarItens(
            estrutura,
            [
                .. conteudo.Itens.Select(static i => new ItemEstrutura(i.FatoCodigo, i.Ordem, i.EtapaCodigo)),
                .. conteudo.Grupos.Select(static g => new ItemEstrutura(g.Codigo, g.Ordem, g.EtapaCodigo)),
            ],
            secaoObrigatoria: true));
        if (erros.Count > 0)
        {
            return erros;
        }

        if (ConferirGrafo(conteudo, derivacoes) is { } recusa)
        {
            erros.Add(new(string.Empty, recusa));
        }

        return erros;
    }

    /// <summary>A forma de um campo do modelo, item ou campo de grupo, no caminho dele.</summary>
    private static IEnumerable<FieldError> FormaDoCampo(ItemDoModelo campo, string caminho) =>
        FormaDoItem.Conferir(
                campo.FatoCodigo, campo.Ordem, campo.Rotulo, campo.TipoRenderizacao, campo.Formato, campo.Ajuda,
                campo.Exibicao?.FatosCitados ?? [], campo.Obrigatoriedade, campo.Restricoes, campo.Impedimento)
            .Select(e => e with { Field = $"{caminho}.{e.Field}" });

    /// <summary>
    /// O grafo de coleta pelas regras do formulário: os pressupostos são os fatos conhecidos antes dele,
    /// e as derivações, as do catálogo. Depois, o campo opcional que alimenta derivação ou negação.
    /// </summary>
    private static DomainError? ConferirGrafo(ConteudoDoModelo conteudo, IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes)
    {
        ItemDoGrafo[] itens = [.. conteudo.Itens.Select(ParaGrafo)];
        EtapaDoGrafo[] etapas = [.. conteudo.Etapas.Select(static e => new EtapaDoGrafo(e.Codigo, e.Ordem, e.FatosCitados))];
        GrupoDoGrafo[] grupos = [.. conteudo.Grupos.Select(static g => new GrupoDoGrafo(
            g.Codigo, g.Ordem, g.EtapaCodigo, g.FatosCitados, [.. g.Subitens.OrderBy(static s => s.Ordem).Select(ParaGrafo)]))];
        HashSet<string> pressupostos = new(conteudo.Pressupostos, StringComparer.Ordinal);

        if (GrafoDoFormulario.ValidarColeta(itens, etapas, pressupostos, derivacoes, grupos) is { } coleta)
        {
            return coleta;
        }

        DependenciasDoFormulario dependencias = GrafoDoFormulario.Dependencias(itens, pressupostos, derivacoes);
        if (conteudo.Termos.Select(t => GrafoDoFormulario.CitacaoInvalidaDoTermo(new TermoDoGrafo(t.Codigo, t.FatosCitados), dependencias))
                .FirstOrDefault(static e => e is not null) is { } termo)
        {
            return termo;
        }

        // O documento do conjunto padrão cita só o que o modelo coleta, como o termo.
        for (int i = 0; i < conteudo.Documentos.Count; i++)
        {
            if (conteudo.Documentos[i].FatosCitados.FirstOrDefault(f => dependencias.Conferir(f, DependenciasDoFormulario.PosicaoDosTermos) is not null) is { } citado)
            {
                return new DomainError(
                    GrafoFormularioErrorCodes.CitaFatoNaoConhecido,
                    $"A condição do documento exigido '{conteudo.Documentos[i].TipoDocumentoCodigo}' (documentosExigidos[{i}]) cita '{citado}', que o modelo não coleta nem deriva, nem é conhecido antes dele.");
            }
        }

        return CampoQueAlimentaRegra.PrimeiroOpcional(
            conteudo.Itens.OrderBy(static i => i.Ordem)
                .Concat(conteudo.Grupos.OrderBy(static g => g.Ordem).SelectMany(static g => g.Subitens.OrderBy(static s => s.Ordem)))
                .Select(static i => (i.FatoCodigo, i.Obrigatoriedade.Tipo)),
            CampoQueAlimentaRegra.Fatos(
                DependenciasCitadas(conteudo, derivacoes), CondicoesDasRegras(conteudo),
                conteudo.Itens.Where(static i => i.Impedimento is not null).Select(static i => i.FatoCodigo)));
    }

    private static ItemDoGrafo ParaGrafo(ItemDoModelo campo) => new(campo.FatoCodigo, campo.Ordem, campo.EtapaCodigo, campo.FatosCitados);

    /// <summary>Os campos do modelo, itens e campos de grupo.</summary>
    private static IEnumerable<ItemDoModelo> Campos(ConteudoDoModelo conteudo) =>
        conteudo.Itens.Concat(conteudo.Grupos.SelectMany(static g => g.Subitens));

    /// <summary>
    /// As dependências dos derivados que as regras do modelo citam, direta ou transitivamente — só elas
    /// alimentam regra no formulário, e não todas as derivações do catálogo.
    /// </summary>
    private static List<string> DependenciasCitadas(ConteudoDoModelo conteudo, IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes)
    {
        HashSet<string> visitados = new(StringComparer.Ordinal);
        Stack<string> pendentes = new(TodosOsCitados(conteudo));
        List<string> dependencias = [];
        while (pendentes.TryPop(out string? fato))
        {
            if (!visitados.Add(fato) || !derivacoes.TryGetValue(fato, out IReadOnlyCollection<string>? deps))
            {
                continue;
            }

            foreach (string dependencia in deps)
            {
                dependencias.Add(dependencia);
                pendentes.Push(dependencia);
            }
        }

        return dependencias;
    }

    private static IEnumerable<string> TodosOsCitados(ConteudoDoModelo conteudo) =>
        Campos(conteudo).SelectMany(static i => i.FatosCitados)
            .Concat(conteudo.Grupos.SelectMany(static g => g.FatosCitados))
            .Concat(conteudo.Etapas.SelectMany(static e => e.FatosCitados))
            .Concat(conteudo.Termos.SelectMany(static t => t.FatosCitados))
            .Concat(conteudo.Documentos.SelectMany(static d => d.FatosCitados));

    /// <summary>As condições de todas as regras do modelo, com o operador — a negação é o que importa aqui.</summary>
    private static IEnumerable<(string Fato, Operador Operador)> CondicoesDasRegras(ConteudoDoModelo conteudo)
    {
        IEnumerable<PredicadoDnf?> predicados = Campos(conteudo)
            .SelectMany(static i => new[] { i.Exibicao, i.Obrigatoriedade.Predicado, i.Impedimento?.Quando }
                .Concat(i.Restricoes.OfType<OpcoesPermitidas>().SelectMany(static o => o.Entradas.Select(static e => e.Quando))))
            .Concat(conteudo.Grupos.SelectMany(static g => new[] { g.Exibicao, g.Obrigatoriedade.Predicado }))
            .Concat(conteudo.Etapas.Select(static e => e.Exibicao))
            .Concat(conteudo.Termos.SelectMany(static t => new[] { t.Exibicao, t.Obrigatoriedade.Predicado }))
            .Concat(conteudo.Documentos.Select(static d => d.Quando));
        return predicados.OfType<PredicadoDnf>()
            .SelectMany(static p => p.Clausulas)
            .SelectMany(static c => c.Condicoes)
            .Select(static c => (c.Fato, c.Operador));
    }

    /// <summary>
    /// Pressuposto é fato coletado pela inscrição (UNI-REQ-0144): o modelo de inscrição não tem, e o
    /// fato não é pressuposto e item do mesmo modelo.
    /// </summary>
    private static IEnumerable<FieldError> ConferirPressupostos(FinalidadeFormulario finalidade, ConteudoDoModelo conteudo)
    {
        if (finalidade == FinalidadeFormulario.Inscricao && conteudo.Pressupostos.Count > 0)
        {
            yield return new("pressupostos", new DomainError(
                ModeloFormularioErrorCodes.PressupostoNaInscricao,
                "O modelo de inscrição não tem pressupostos: nenhum formulário é respondido antes dele."));
            yield break;
        }

        HashSet<string> vistos = new(StringComparer.Ordinal);
        HashSet<string> itens = new(conteudo.Itens.Select(static i => i.FatoCodigo), StringComparer.Ordinal);
        for (int i = 0; i < conteudo.Pressupostos.Count; i++)
        {
            string fato = conteudo.Pressupostos[i];
            if (fato.Length == 0)
            {
                yield return new($"pressupostos[{i}]", new DomainError(
                    ModeloFormularioErrorCodes.PressupostoEmBranco, "O pressuposto nomeia um fato."));
                continue;
            }

            if (!vistos.Add(fato))
            {
                yield return new($"pressupostos[{i}]", new DomainError(
                    ModeloFormularioErrorCodes.PressupostoRepetido, $"O fato '{fato}' aparece mais de uma vez entre os pressupostos."));
            }

            if (itens.Contains(fato))
            {
                yield return new($"pressupostos[{i}]", new DomainError(
                    ModeloFormularioErrorCodes.PressupostoTambemItem,
                    $"O fato '{fato}' é item do modelo e não pode ser também pressuposto: ou o formulário o coleta, ou o recebe da inscrição."));
            }
        }
    }
}
