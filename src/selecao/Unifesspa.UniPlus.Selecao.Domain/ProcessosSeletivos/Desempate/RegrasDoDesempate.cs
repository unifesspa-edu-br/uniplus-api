namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public static class RegrasDoDesempate
{
    /// <summary>
    /// Teto de critérios de desempate aceitos numa definição: acima dele a lista é recusada
    /// inteira, sem um erro por critério — uma entrada pequena não pode gerar resposta
    /// desproporcional. Uma ordem de desempate de edital tem poucos critérios.
    /// </summary>
    public const int CriteriosDesempateMaximo = 20;

    internal static Result ConferirSubstituicao(EstadoDaConfiguracao estado, IReadOnlyList<CriterioDesempate> criterios)
    {
        List<FieldError> erros = ValidarCriteriosDesempate(estado, [.. criterios.Select(CriterioDesempateInformado.De)]);
        return erros.Count > 0
            ? Result.ValidationFailure(AnexarAreasAceitasDoDesempate(estado, erros))
            : Result.Success();
    }

    /// <summary>
    /// A quantidade de critérios não depende do catálogo de regras: existe separada para quem
    /// recebe a lista recusá-la acima do teto antes de consultar o <c>rol_de_regras</c> e
    /// antes de conferir item a item.
    /// </summary>
    public static List<FieldError> ValidarQuantidadeDeCriteriosDesempate(int quantidade)
    {
        List<FieldError> erros = [];

        if (quantidade > CriteriosDesempateMaximo)
        {
            erros.Add(new("criterios", new DomainError(
                ProcessoSeletivoErrorCodes.CriteriosDesempateEmExcesso,
                $"A ordem de desempate admite no máximo {CriteriosDesempateMaximo} critérios; vieram {quantidade}.")));
        }

        return erros;
    }

    /// <summary>O campo de um critério de desempate no comando que define a ordem de desempate.</summary>
    public static string CampoDoCriterioDesempate(int indice, string? campo = null) =>
        campo is null ? $"criterios[{indice}]" : $"criterios[{indice}].{campo}";

    /// <summary>
    /// As invariantes que um critério de desempate só cumpre diante do processo — ordem única,
    /// <c>etapa_ref</c> existente (INV-B6) e área do ENEM presente no quadro de pesos por área
    /// da classificação —, acumuladas (ADR-0125), cada uma no campo do item:
    /// <c>criterios[i].ordem</c>, <c>criterios[i].etapaRef</c>, <c>criterios[i].areas[j]</c> e
    /// <c>criterios[i].regraCodigo</c> quando a classificação não tem quadro.
    /// </summary>
    /// <param name="criterios">
    /// Na posição que cada critério ocupa no comando, inclusive a do critério que o próprio
    /// <see cref="CriterioDesempate"/> recusou: a ordem vem sempre do comando, e os args, quando
    /// foi possível montá-los. Assim a ordem duplicada e a área fora do quadro saem junto com a
    /// recusa do item, e não só na tentativa seguinte.
    /// </param>
    public static List<FieldError> ValidarCriteriosDesempate(EstadoDaConfiguracao estado, IReadOnlyList<CriterioDesempateInformado> criterios)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(criterios);

        List<FieldError> erros = ValidarQuantidadeDeCriteriosDesempate(criterios.Count);
        if (erros.Count > 0)
        {
            return erros;
        }

        HashSet<int> ordensVistas = [];
        for (int indice = 0; indice < criterios.Count; indice++)
        {
            CriterioDesempateInformado criterio = criterios[indice];
            if (!ordensVistas.Add(criterio.Ordem))
            {
                erros.Add(new(CampoDoCriterioDesempate(indice, "ordem"), new DomainError(
                    "ProcessoSeletivo.OrdemDesempateDuplicada",
                    "Cada critério de desempate deve ter uma ordem única dentro do processo.")));
            }

            if (criterio.Args is ArgsDesempateMaiorNotaEtapa args && !estado.Etapas.Any(e => e.Id == args.EtapaRef))
            {
                erros.Add(new(CampoDoCriterioDesempate(indice, "etapaRef"), new DomainError(
                    "ProcessoSeletivo.EtapaRefDesempateInexistente",
                    $"O critério de desempate na ordem {criterio.Ordem} referencia a etapa {args.EtapaRef}, que não existe neste processo (INV-B6).")));
            }
        }

        // O desempate é gravado antes da classificação no fluxo de configuração: num processo
        // novo ela ainda é nula aqui, e só então a coerência com o quadro de pesos por área
        // fica para DefinirClassificacao conferir.
        List<ViolacaoDoDesempatePorArea> contraOQuadro = ValidarDesempatePorAreaDoEnem(criterios, estado.Classificacao);

        // A área citada por outro critério e também fora do quadro recebe só a recusa do
        // quadro, que traz a lista de aceitas: o mesmo campo não recebe duas.
        erros.AddRange(AreasCitadasPorOutroCriterio(criterios)
            .Where(v => !contraOQuadro.Any(q => q.IndiceCriterio == v.IndiceCriterio && q.IndiceArea == v.IndiceArea))
            .Select(static v => new FieldError(CampoDoCriterioDesempate(v.IndiceCriterio, $"areas[{v.IndiceArea}]"), v.Erro)));

        erros.AddRange(contraOQuadro.Select(static v => new FieldError(
            CampoDoCriterioDesempate(v.IndiceCriterio, v.IndiceArea is { } indiceArea ? $"areas[{indiceArea}]" : "regraCodigo"),
            v.Erro)));

        return erros;
    }

    /// <summary>
    /// Uma violação da coerência entre o desempate por área do ENEM e o quadro de pesos por
    /// área da classificação. Os índices são posições na lista de critérios e na ordem de
    /// áreas do critério, para que cada caminho de gravação monte o campo no formato do próprio
    /// comando.
    /// </summary>
    /// <param name="IndiceArea"><see langword="null"/> quando a violação é do critério inteiro.</param>
    /// <param name="Codigo">O código da área fora do quadro; <see langword="null"/> quando a violação é do critério inteiro.</param>
    internal readonly record struct ViolacaoDoDesempatePorArea(int IndiceCriterio, int Ordem, int? IndiceArea, string? Codigo, DomainError Erro);

    /// <summary>
    /// O critério de desempate por área do ENEM cita áreas pelo código, e a única fonte de
    /// área no processo é o quadro de pesos por área congelado na classificação. Por isso ele
    /// exige classificação baseada em ENEM que tenha congelado esse quadro, e cada área citada
    /// tem de existir em todos os grupos do quadro, para que nenhum candidato fique sem a nota
    /// que o desempate compara. Acumula todas as violações, de todos os critérios (ADR-0125).
    /// </summary>
    /// <param name="classificacao">
    /// <see langword="null"/> enquanto a classificação não foi gravada — aí a coerência com
    /// ela ainda não é conferível, e fica para quando for.
    /// </param>
    internal static List<ViolacaoDoDesempatePorArea> ValidarDesempatePorAreaDoEnem(
        IReadOnlyList<CriterioDesempateInformado> criterios,
        ConfiguracaoClassificacao? classificacao)
    {
        List<ViolacaoDoDesempatePorArea> violacoes = [];
        if (classificacao is null || !criterios.Any(static c => c.Args is ArgsDesempateMaiorNotaAreaEnem))
        {
            return violacoes;
        }

        bool temQuadro = classificacao.TemQuadroPesoAreaEnem;
        HashSet<string> aceitas = temQuadro ? AreasAceitasNoDesempate(classificacao) : [];

        for (int indice = 0; indice < criterios.Count; indice++)
        {
            if (criterios[indice] is not { Args: ArgsDesempateMaiorNotaAreaEnem args } criterio)
            {
                continue;
            }

            if (!temQuadro)
            {
                violacoes.Add(new(indice, criterio.Ordem, null, null, new DomainError(
                    DesempatePorAreaEnemErrorCodes.SemQuadro,
                    $"O critério de desempate na ordem {criterio.Ordem} compara a nota por área do ENEM e exige classificação baseada em ENEM, calculada pela média ponderada, com o quadro de pesos por área congelado.")));
                continue;
            }

            foreach ((int indiceArea, string codigo) in CriterioDesempate.ConferirAreas(args.Areas).BemFormadas.Where(area => !aceitas.Contains(area.Codigo)))
            {
                violacoes.Add(new(indice, criterio.Ordem, indiceArea, codigo, new DomainError(
                    DesempatePorAreaEnemErrorCodes.ForaDoQuadro,
                    $"A área {codigo}, citada pelo critério de desempate na ordem {criterio.Ordem}, não está em todos os grupos do quadro de pesos por área da classificação.")));
            }
        }

        return violacoes;
    }

    /// <summary>
    /// Cada área é citada por um critério de desempate só: depois do primeiro que a compara,
    /// o empate que chega a outro já tem a mesma nota nela, e a segunda citação nunca desempata.
    /// "Primeiro" é pela ordem do desempate, não pela posição na lista, que o comando não
    /// garante; o índice na lista continua dando o campo da recusa.
    /// </summary>
    internal static List<ViolacaoDoDesempatePorArea> AreasCitadasPorOutroCriterio(IReadOnlyList<CriterioDesempateInformado> criterios)
    {
        List<ViolacaoDoDesempatePorArea> violacoes = [];
        Dictionary<string, int> citadas = new(StringComparer.Ordinal);
        foreach (int indice in Enumerable.Range(0, criterios.Count).OrderBy(i => criterios[i].Ordem))
        {
            if (criterios[indice] is not { Args: ArgsDesempateMaiorNotaAreaEnem args } criterio)
            {
                continue;
            }

            foreach ((int indiceArea, string codigo) in CriterioDesempate.ConferirAreas(args.Areas).BemFormadas)
            {
                if (citadas.TryGetValue(codigo, out int ordemAnterior))
                {
                    violacoes.Add(new(indice, criterio.Ordem, indiceArea, codigo, new DomainError(
                        DesempatePorAreaEnemErrorCodes.AreaCitadaPorOutroCriterio,
                        $"A área {codigo} já é comparada pelo critério de desempate na ordem {ordemAnterior}, e cada área é citada por um critério só.")));
                    continue;
                }

                citadas.Add(codigo, criterio.Ordem);
            }
        }

        return violacoes;
    }

    /// <summary>
    /// A ordem de áreas mal formada — vazia, acima do teto, com código fora da forma ou
    /// repetido —, pela mesma regra de <see cref="CriterioDesempate.Criar"/>. Os critérios
    /// gravados e os restaurados já passaram por ela; conferi-la aqui serve ao gate, para o
    /// critério que chega por outro caminho.
    /// </summary>
    internal static List<ViolacaoDoDesempatePorArea> OrdensDeAreasMalFormadas(List<CriterioDesempateInformado> criterios)
    {
        List<ViolacaoDoDesempatePorArea> violacoes = [];
        for (int indice = 0; indice < criterios.Count; indice++)
        {
            if (criterios[indice] is not { Args: ArgsDesempateMaiorNotaAreaEnem args } criterio)
            {
                continue;
            }

            violacoes.AddRange(CriterioDesempate.ConferirAreas(args.Areas).Erros.Select(erro => new ViolacaoDoDesempatePorArea(
                indice, criterio.Ordem, null, null,
                erro.Error with { Message = $"{erro.Error.Message} Critério de desempate na ordem {criterio.Ordem}." })));
        }

        return violacoes;
    }

    internal static HashSet<string> AreasAceitasNoDesempate(ConfiguracaoClassificacao classificacao) =>
        ConfiguracaoClassificacao.AreasEmTodosOsGrupos(classificacao.QuadroPesoAreaEnem);

    /// <summary>
    /// Toda área recusada — pela forma ou por estar fora do quadro — deixa o operador sem saber
    /// o que pode citar. A lista das aceitas, com o rótulo, vai na primeira dessas recusas, e só
    /// nela: repeti-la em cada uma faria a resposta crescer com o produto de critérios por
    /// áreas. Sem quadro não há lista a dar.
    /// </summary>
    internal static List<FieldError> AnexarAreasAceitas(IEnumerable<FieldError> erros, ConfiguracaoClassificacao? classificacao)
    {
        List<FieldError> lista = [.. erros];
        int primeira = lista.FindIndex(static e =>
            e.Error.Code is DesempatePorAreaEnemErrorCodes.AreaInvalida or DesempatePorAreaEnemErrorCodes.ForaDoQuadro);
        if (primeira >= 0)
        {
            lista[primeira] = lista[primeira] with { Error = ComAreasAceitas(lista[primeira].Error, classificacao) };
        }

        return lista;
    }

    /// <summary>
    /// Anexa a lista de áreas aceitas às recusas de área do desempate — as da criação de cada
    /// critério e as da conferência contra o processo — já reunidas por quem as acumulou.
    /// </summary>
    public static List<FieldError> AnexarAreasAceitasDoDesempate(EstadoDaConfiguracao estado, IEnumerable<FieldError> erros)
    {
        ArgumentNullException.ThrowIfNull(estado);

        return AnexarAreasAceitas(erros, estado.Classificacao);
    }

    internal static DomainError ComAreasAceitas(DomainError erro, ConfiguracaoClassificacao? classificacao) =>
        classificacao is not null && classificacao.TemQuadroPesoAreaEnem
            ? erro with { Message = $"{erro.Message} Áreas aceitas: {ConfiguracaoClassificacao.ListarAreas(classificacao.QuadroPesoAreaEnem, AreasAceitasNoDesempate(classificacao))}." }
            : erro;

    /// <summary>
    /// O teto de critérios e as regras do desempate por área do ENEM, como gate de publicação.
    /// Os caminhos de gravação e a restauração já os conferem; o gate é defesa em profundidade
    /// para o estado que chega por outro caminho — um rascunho gravado antes do teto, uma
    /// gravação feita fora do agregado ou uma rota nova que esqueça a conferência. Sem ele o
    /// processo publicaria, e a restauração daquela versão recusaria depois pela mesma regra.
    /// </summary>
    internal static DomainError? PendenciaDosCriteriosDesempate(EstadoDaConfiguracao estado)
    {
        if (ValidarQuantidadeDeCriteriosDesempate(estado.CriteriosDesempate.Count) is [var excesso, ..])
        {
            return excesso.Error;
        }

        // A lista vem na precedência de sempre: forma e repetição das áreas antes do quadro. A
        // lista de áreas aceitas acompanha só a recusa contra o quadro.
        return ViolacoesDoDesempatePorAreaDoEnem(estado) is [var primeira, ..]
            ? primeira.Erro.Code == DesempatePorAreaEnemErrorCodes.ForaDoQuadro ? ComAreasAceitas(primeira.Erro, estado.Classificacao) : primeira.Erro
            : null;
    }

    /// <summary>
    /// A primeira violação das regras do desempate por área do ENEM, para quem recusa com um
    /// erro só. A lista de áreas aceitas acompanha só a recusa contra o quadro: as demais não
    /// dependem dele.
    /// </summary>
    internal static DomainError? PrimeiraViolacaoDoDesempatePorArea(
        List<CriterioDesempateInformado> criterios,
        ConfiguracaoClassificacao? classificacao)
    {
        if (OrdensDeAreasMalFormadas(criterios) is [var malFormada, ..])
        {
            return malFormada.Erro;
        }

        if (AreasCitadasPorOutroCriterio(criterios) is [var areaRepetida, ..])
        {
            return areaRepetida.Erro;
        }

        return ValidarDesempatePorAreaDoEnem(criterios, classificacao) is [var violacaoDeArea, ..]
            ? ComAreasAceitas(violacaoDeArea.Erro, classificacao)
            : null;
    }

    /// <summary>
    /// As violações do desempate por área que o checklist e o gate de publicação projetam, na
    /// precedência do gate. Sob a média pelos pesos por área, a recusa por falta de quadro só
    /// surge sem quadro, que é pendência da resolução
    /// (<see cref="ItensEstruturaisDeConformidade.ItensDaMediaPelosPesosPorAreaDoEnem"/>): repeti-la aqui daria duas pendências
    /// pela mesma causa, uma delas longe de onde se corrige. A gravação do desempate e a
    /// restauração recusam pela regra inteira.
    /// </summary>
    internal static List<ViolacaoDoDesempatePorArea> ViolacoesDoDesempatePorAreaDoEnem(EstadoDaConfiguracao estado)
    {
        List<CriterioDesempateInformado> criterios = EmOrdem(estado.CriteriosDesempate);
        IEnumerable<ViolacaoDoDesempatePorArea> contraOQuadro = ValidarDesempatePorAreaDoEnem(criterios, estado.Classificacao);
        if (estado.Classificacao is { CalculaPelosPesosPorAreaDoEnem: true })
        {
            contraOQuadro = contraOQuadro.Where(static violacao => violacao.Erro.Code != DesempatePorAreaEnemErrorCodes.SemQuadro);
        }

        return [.. OrdensDeAreasMalFormadas(criterios), .. AreasCitadasPorOutroCriterio(criterios), .. contraOQuadro];
    }

    internal static List<CriterioDesempateInformado> EmOrdem(IEnumerable<CriterioDesempate> criterios) =>
        [.. criterios.OrderBy(static c => c.Ordem).Select(CriterioDesempateInformado.De)];
}
